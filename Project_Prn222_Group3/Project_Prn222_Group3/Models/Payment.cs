
namespace Project_Prn222_Group3.Models
{
    public class Payment
    {
        public int PaymentID { get; set; }
        public int BillID { get; set; }

        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } = "";

        public string Status { get; set; } = "";

        public int CollectedByUserID { get; set; }

        public DateTime PaidAt { get; set; }

        public Guid RequestKey { get; set; }
    }
}
