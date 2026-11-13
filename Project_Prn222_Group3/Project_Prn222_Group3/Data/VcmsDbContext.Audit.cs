using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Data;

// Adapted from Linh's ApplicationDbContext.Audit: same transaction and generated-ID capture,
// using the main context and authenticated actor, with the existing care snapshot writer preserved.
public partial class VcmsDbContext
{
    private int auditSuppression;
    public int? AuditActorUserId { get; set; }
    private static readonly JsonSerializerOptions AuditJson = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), WriteIndented = true };
    private static readonly Dictionary<string, string[]> AllowedFields = new()
    {
        ["Medicines"] = ["MedicineName", "Unit", "CurrentPrice", "IsActive"],
        ["Appointments"] = ["AppointmentTime", "ExpectedDurationMinutes", "Status", "Reason", "CancelledAt"],
        ["MedicineBatches"] = ["BatchNumber", "ExpiryDate", "QuantityOnHand"],
        ["Visits"] = ["Status", "CheckInAt", "CompletedAt"],
        ["Encounters"] = ["Status", "ExaminedAt", "FinalizedAt"],
        ["Payments"] = ["Amount", "PaymentMethod", "Status", "PaidAt"],
        ["Users"] = ["RoleID", "IsActive"],
        ["Clients"] = [], ["Animals"] = [], ["QueueEntries"] = ["QueueNumber", "QueueDate", "Status", "CalledAt"]
    };
    private sealed class AuditScope(Action dispose) : IDisposable { public void Dispose() => dispose(); }
    public IDisposable SuppressAutomaticAudit()
    {
        auditSuppression++;
        return new AuditScope(() => auditSuppression--);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => SaveAuditedAsync(acceptAllChangesOnSuccess, default).GetAwaiter().GetResult();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => SaveAuditedAsync(acceptAllChangesOnSuccess, cancellationToken);

    private async Task<int> SaveAuditedAsync(bool accept, CancellationToken ct)
    {
        GuardAudit();
        if (auditSuppression > 0) return await base.SaveChangesAsync(accept, ct);
        var changes = ChangeTracker.Entries().Where(e => e.Entity is not AuditLog && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        var pending = changes.Select(e =>
        {
            var table = e.Metadata.GetTableName()!;
            var old = new Dictionary<string, object?>(); var updated = new Dictionary<string, object?>();
            if (AllowedFields.TryGetValue(table, out var fields))
                foreach (var p in e.Properties.Where(p => fields.Contains(p.Metadata.Name)))
                {
                    if (e.State == EntityState.Modified && (!p.IsModified || Equals(p.OriginalValue, p.CurrentValue))) continue;
                    if (e.State != EntityState.Added) old[p.Metadata.Name] = p.OriginalValue;
                    if (e.State != EntityState.Deleted) updated[p.Metadata.Name] = p.CurrentValue;
                }
            return new { Entry = e, Table = table, State = e.State, Original = e.OriginalValues.Clone(), Old = old, New = updated };
        }).Where(x => AllowedFields.ContainsKey(x.Table) && (x.State != EntityState.Modified || x.New.Count > 0)).ToList();
        if (pending.Count == 0) return await base.SaveChangesAsync(accept, ct);
        var actor = AuditActorUserId ?? (int.TryParse(httpContext?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null);
        await using var transaction = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(ct) : null;
        // Save business rows first so identity keys in audit records are real IDs.
        // For SaveChanges(false), temporarily accept then restore caller tracking semantics below.
        var originals = changes.Select(e => (Entry: e, State: e.State, Values: e.OriginalValues.Clone())).ToList();
        try
        {
            var count = await base.SaveChangesAsync(true, ct);
            foreach (var change in pending)
                AuditLogs.Add(new AuditLog
                {
                    ActorUserID = actor, EntityName = change.Table,
                    EntityID = string.Join(",", change.Entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString() ?? "")),
                    Action = change.State == EntityState.Added ? "CREATE" : change.State == EntityState.Deleted ? "DELETE" : "UPDATE",
                    OldValues = change.Old.Count == 0 ? null : JsonSerializer.Serialize(change.Old, AuditJson),
                    NewValues = change.New.Count == 0 ? null : JsonSerializer.Serialize(change.New, AuditJson), OccurredAt = ClinicTime.UtcNow
                });
            await base.SaveChangesAsync(true, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            if (!accept)
                foreach (var original in originals)
                {
                    original.Entry.State = original.State;
                    original.Entry.OriginalValues.SetValues(original.Values);
                }
            return count;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
