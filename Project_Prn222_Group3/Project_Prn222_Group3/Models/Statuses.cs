namespace Project_Prn222_Group3.Models;

public static class AppointmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string CheckedIn = "CheckedIn";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";
}

public static class VisitStatuses
{
    public const string Waiting = "Waiting";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class QueueStatuses
{
    public const string Waiting = "Waiting";
    public const string Called = "Called";
    public const string Serving = "Serving";
    public const string Done = "Done";
    public const string Skipped = "Skipped";
}

public static class DisplayText
{
    public static string AppointmentStatus(string status) => status switch
    {
        AppointmentStatuses.Pending => "Chờ xác nhận",
        AppointmentStatuses.Confirmed => "Đã xác nhận",
        AppointmentStatuses.CheckedIn => "Đã check-in",
        AppointmentStatuses.Completed => "Hoàn tất",
        AppointmentStatuses.Cancelled => "Đã hủy",
        AppointmentStatuses.NoShow => "Không đến",
        _ => status
    };

    public static string QueueStatus(string status) => status switch
    {
        QueueStatuses.Waiting => "Đang chờ",
        QueueStatuses.Called => "Đã gọi",
        QueueStatuses.Serving => "Đang phục vụ",
        QueueStatuses.Done => "Hoàn tất",
        QueueStatuses.Skipped => "Bỏ qua",
        _ => status
    };

    public static string AppointmentBadge(string status) => status switch
    {
        AppointmentStatuses.Pending => "bg-warning text-dark",
        AppointmentStatuses.Confirmed => "bg-primary",
        AppointmentStatuses.CheckedIn => "bg-info text-dark",
        AppointmentStatuses.Completed => "bg-success",
        AppointmentStatuses.Cancelled => "bg-secondary",
        AppointmentStatuses.NoShow => "bg-danger",
        _ => "bg-secondary"
    };

    public static string QueueBadge(string status) => status switch
    {
        QueueStatuses.Waiting => "bg-warning text-dark",
        QueueStatuses.Called => "bg-primary",
        QueueStatuses.Serving => "bg-info text-dark",
        QueueStatuses.Done => "bg-success",
        QueueStatuses.Skipped => "bg-secondary",
        _ => "bg-secondary"
    };
}
