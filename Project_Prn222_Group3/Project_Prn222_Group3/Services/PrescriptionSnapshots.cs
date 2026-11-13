using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
namespace Project_Prn222_Group3.Services;

public static class PrescriptionSnapshots
{
    public static async Task<PrescriptionSnapshot?> ReadAsync(VcmsDbContext db, int id)
    {
        var json = await db.AuditLogs.AsNoTracking().Where(x => x.EntityName == "Prescription" && x.EntityID == id.ToString() && x.Action == "Finalize")
            .OrderByDescending(x => x.AuditLogID).Select(x => x.NewValues).FirstOrDefaultAsync();
        try
        {
            var value = json is null ? null : JsonSerializer.Deserialize<PrescriptionSnapshot>(json);
            return value?.PrescriptionId == id && value.Lines is not null ? value : null;
        }
        catch (JsonException) { return null; }
    }
}
