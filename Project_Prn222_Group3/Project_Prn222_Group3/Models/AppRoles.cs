namespace Project_Prn222_Group3.Models;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Client = "Client";
    public const string Receptionist = "Receptionist";
    public const string Veterinarian = "Veterinarian";
    public const string DispensingStaff = "DispensingStaff";
    public const string Cashier = "Cashier";
    public const string ClinicManager = "ClinicManager";

    public const string Managers = Admin + "," + ClinicManager;
    public const string ReceptionDesk = Admin + "," + Receptionist;

    public static readonly string[] StaffRoleNames =
    [
        Admin, Receptionist, Veterinarian, DispensingStaff, Cashier, ClinicManager
    ];

    public static string HomePath(string? roleName) => roleName switch
    {
        Receptionist => "/Reception/Queue",
        ClinicManager or Admin => "/Staff",
        Client => "/Client",
        _ => "/Index"
    };

    public static string DisplayName(string? roleName) => roleName switch
    {
        Admin => "Quản trị hệ thống",
        Client => "Chủ thú cưng",
        Receptionist => "Nhân viên lễ tân",
        Veterinarian => "Bác sĩ thú y",
        DispensingStaff => "Nhân viên cấp phát thuốc",
        Cashier => "Nhân viên thu ngân",
        ClinicManager => "Quản lý phòng khám",
        _ => roleName ?? "Người dùng"
    };
}
