using Microsoft.AspNetCore.Authorization;
using Project_Prn222_Group3.Models;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;

namespace Project_Prn222_Group3.Pages.Dashboard
{
    [Authorize(Roles = AppRoles.Managers)]
    public class IndexModel : PageModel
    {
        private readonly VcmsDbContext _context;

        private const int LowStockThreshold = 10;
        private const int ExpiringSoonDays = 30;

        public IndexModel(VcmsDbContext context)
        {
            _context = context;
        }

        // Dashboard statistics
        public int TotalAppointments { get; set; }
        public int TotalVisits { get; set; }
        public int TotalExaminations { get; set; }
        public decimal TotalRevenue { get; set; }
        public int CompletedVisits { get; set; }
        public int WaitingVisits { get; set; }
        public int TotalBills { get; set; }
        public int TotalPayments { get; set; }
        public decimal TotalBillAmount { get; set; }
        public decimal TotalCollectedAmount { get; set; }
        public decimal OutstandingAmount => TotalBillAmount - TotalCollectedAmount;
        public int LowStockMedicines { get; set; }

        // Medicine alerts
        public int ExpiredBatches { get; set; }
        public int ExpiringSoonBatches { get; set; }
        public int LowStockBatches { get; set; }

        public List<MedicineAlertRow> MedicineAlerts { get; set; }
            = new();

        public class MedicineAlertRow
        {
            public int BatchID { get; set; }
            public string MedicineName { get; set; } = "";
            public string BatchNumber { get; set; } = "";
            public DateTime ExpiryDate { get; set; }
            public int QuantityOnHand { get; set; }
            public string AlertStatus { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            var today = ClinicTime.VnToday;
            var expiryLimit = today.AddDays(ExpiringSoonDays);

            // Total appointments
            TotalAppointments = await _context.Appointments
                .AsNoTracking()
                .CountAsync();

            // Total visits
            TotalVisits = await _context.Visits
                .AsNoTracking()
                .CountAsync();

            // Total examinations
            TotalExaminations = await _context.Encounters
                .AsNoTracking()
                .CountAsync();

            CompletedVisits = await _context.Visits.CountAsync(x => x.Status == "Completed");
            WaitingVisits = await _context.Visits.CountAsync(x => x.Status == "Waiting");

            // Keep every financial metric on the same eligible bill/payment population.
            var validBills = _context.Bills.AsNoTracking().Where(x => x.Status != "Cancelled");
            var completedPayments = _context.Payments.AsNoTracking().Where(p =>
                p.Status == "Completed" && validBills.Any(b => b.BillID == p.BillID));
            TotalBills = await validBills.CountAsync();
            TotalBillAmount = await validBills.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
            TotalPayments = await completedPayments.CountAsync();
            TotalCollectedAmount = await completedPayments
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            TotalRevenue = TotalCollectedAmount;

            // Usable stock is summed across lots, including active medicines with no stock.
            LowStockMedicines = await _context.Medicines.AsNoTracking().CountAsync(m => m.IsActive &&
                (_context.MedicineBatches.Where(b => b.MedicineID == m.MedicineID && b.ExpiryDate >= today)
                    .Sum(b => (long?)b.QuantityOnHand) ?? 0L) <= LowStockThreshold);

            // Expired medicine batches
            ExpiredBatches = await _context.MedicineBatches
                .AsNoTracking()
                .CountAsync(b =>
                    b.QuantityOnHand > 0 &&
                    b.ExpiryDate < today);

            // Expiring soon
            ExpiringSoonBatches = await _context.MedicineBatches
                .AsNoTracking()
                .CountAsync(b =>
                    b.QuantityOnHand > 0 &&
                    b.ExpiryDate >= today &&
                    b.ExpiryDate <= expiryLimit);

            // Low stock
            LowStockBatches = await _context.MedicineBatches
                .AsNoTracking()
                .CountAsync(b =>
                    b.QuantityOnHand > 0 &&
                    b.QuantityOnHand <= LowStockThreshold);

            // Medicine alert details
            MedicineAlerts = await (
                from batch in _context.MedicineBatches.AsNoTracking()
                join medicine in _context.Medicines.AsNoTracking()
                    on batch.MedicineID equals medicine.MedicineID
                where batch.QuantityOnHand > 0 &&
                    (
                        batch.ExpiryDate <= expiryLimit ||
                        batch.QuantityOnHand <= LowStockThreshold
                    )
                orderby batch.ExpiryDate,
                        batch.QuantityOnHand
                select new MedicineAlertRow
                {
                    BatchID = batch.BatchID,
                    MedicineName = medicine.MedicineName,
                    BatchNumber = batch.BatchNumber,
                    ExpiryDate = batch.ExpiryDate,
                    QuantityOnHand = batch.QuantityOnHand,
                    AlertStatus =
                        batch.ExpiryDate < today
                            ? "Expired"
                            : batch.ExpiryDate <= expiryLimit
                                ? "Expiring Soon"
                                : "Low Stock"
                }
            )
            .Take(50)
            .ToListAsync();
        }
    }
}
