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
    public const string PetGroomer = "PetGroomer";

    public const string PharmacyStaff = Admin + "," + ClinicManager + "," + DispensingStaff;
    public const string ClinicalReaders = Admin + "," + Veterinarian;
    public const string Managers = Admin + "," + ClinicManager;
    public const string ReceptionDesk = Admin + "," + Receptionist + "," + ClinicManager;
    public const string RecordEditors = Admin + "," + Receptionist + "," + ClinicManager;
    public const string Bookers = Admin + "," + Receptionist + "," + ClinicManager + "," + Client;
    public const string AppointmentViewers = Bookers + "," + Veterinarian;

    public static readonly string[] StaffRoleNames =
    [
        Admin, Receptionist, Veterinarian, DispensingStaff, Cashier, ClinicManager
    ];

    public static readonly string[] SchedulableRoleNames =
    [
        Veterinarian
    ];

    public static string HomePath(string? roleName) => roleName switch
    {
        Receptionist => "/Reception/Queue",
        ClinicManager or Admin => "/Staff",
        Client => "/Client",
        Veterinarian => "/Examination",
        DispensingStaff => "/Pharmacy",
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
        PetGroomer => "Nhân viên grooming",
        _ => roleName ?? "Người dùng"
    };
}
