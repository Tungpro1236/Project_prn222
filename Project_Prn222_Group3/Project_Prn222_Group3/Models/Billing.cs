using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Prn222_Group3.Models;

[Table("Bills")]
public class Bill
{
    [Key] public int BillID { get; set; }
    public int EncounterID { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Unpaid";
    public int CreatedByUserID { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("BillItems")]
public class BillItem
{
    [Key] public int BillItemID { get; set; }
    public int BillID { get; set; }
    public int? ServiceOrderID { get; set; }
    public int? DispensationItemID { get; set; }
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; private set; }
}
