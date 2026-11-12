namespace Project_Prn222_Group3.Models;

public static class ClinicTime
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateTime UtcNow => DateTime.UtcNow;

    public static DateTime ToVietnam(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).Add(VietnamOffset);

    public static DateTime VnToday => ToVietnam(UtcNow).Date;

    public static DateTime VnLocalToUtc(DateTime local) =>
        DateTime.SpecifyKind(local, DateTimeKind.Unspecified) - VietnamOffset;

    public static string Format(DateTime utc) =>
        ToVietnam(utc).ToString("HH:mm dd/MM/yyyy");

    public static string FormatTime(DateTime utc) =>
        ToVietnam(utc).ToString("HH:mm");

    public static string FormatDate(DateTime utc) =>
        ToVietnam(utc).ToString("dd/MM/yyyy");
}
