namespace Project_Prn222_Group3.Models;
public static class CareText
{
    public static string Status(string status) => status switch
    {
        "Draft" => "Nháp", "Finalized" => "Đã chốt", "Corrected" => "Đã hiệu chỉnh",
        "Completed" => "Hoàn tất", "Cancelled" => "Đã hủy", _ => status
    };
    public static string Action(string action) => action switch
    {
        "SaveDraft" => "Lưu nháp", "Finalize" => "Chốt", "Correct" => "Hiệu chỉnh", "Create" => "Tạo mới",
        "SaveItem" => "Lưu dòng thuốc", "RemoveItem" => "Xóa dòng thuốc", "Cancel" => "Hủy",
        "Receive" => "Nhập kho", "Adjust" => "Điều chỉnh", "Complete" => "Cấp thuốc", "Update" => "Cập nhật", _ => action
    };
    public static string Movement(string value) => value switch
    {
        "Receipt" => "Nhập kho", "Dispense" => "Cấp thuốc", "Adjustment" => "Điều chỉnh", "Return" => "Hoàn trả", _ => value
    };
}
