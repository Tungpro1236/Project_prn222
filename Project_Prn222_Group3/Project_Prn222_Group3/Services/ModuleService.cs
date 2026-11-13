using System.Data;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Services;

public record OperationResult(bool Ok, string Message, int? Id = null)
{
    public static OperationResult Fail(string message) => new(false, message);
    public static OperationResult Success(string message, int? id = null) => new(true, message, id);
}

public abstract class ModuleService(VcmsDbContext db)
{
    protected readonly VcmsDbContext Db = db;
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), WriteIndented = true
    };

    // SQL application locks serialize module writes across processes, avoiding lock upgrade races.
    protected async Task<OperationResult> ExecuteAsync(string resource, int actorId, string[] roles, Func<User, Task<OperationResult>> action)
    {
        using var auditScope = resource == "VCMS:Care" ? Db.SuppressAutomaticAudit() : null;
        Db.AuditActorUserId = actorId;
        await using var tx = await Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await Db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; IF @r < 0 THROW 51000, 'Module busy', 1;");
            var actor = await Db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.UserID == actorId && x.IsActive);
            if (actor is null || !roles.Contains(actor.Role.RoleName))
                return OperationResult.Fail("Bạn không có quyền thực hiện thao tác này.");
            var result = await action(actor);
            if (!result.Ok) return result;
            await Db.SaveChangesAsync();
            await tx.CommitAsync();
            return result;
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException)
        {
            return OperationResult.Fail("Dữ liệu vừa thay đổi hoặc hệ thống đang bận. Tải lại trang và thử lại.");
        }
        finally
        {
            // Disposal rolls back uncommitted transactions, including validation returns.
            Db.ChangeTracker.Clear();
        }
    }

    protected void Audit(int actor, string entity, int id, string action, object? before, object after)
        => Db.AuditLogs.Add(new AuditLog
        {
            ActorUserID = actor, EntityName = entity, EntityID = id.ToString(), Action = action,
            OldValues = before is null ? null : JsonSerializer.Serialize(before, AuditJson),
            NewValues = JsonSerializer.Serialize(after, AuditJson), OccurredAt = ClinicTime.UtcNow
        });

    protected static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    protected static bool Fits(string? value, int max) => value is null || value.Length <= max;
}
