using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Models;
namespace Project_Prn222_Group3.Data;
// Team SQL owns schema/catalogue seeds. Normal startup never inserts demo records.
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VcmsDbContext>();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Không kết nối được CSDL VCMS. Kiểm tra cấu hình kết nối và SQL của nhóm.");
    }
    public static async Task BootstrapAdminAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VcmsDbContext>();
        if (await db.Users.AnyAsync()) throw new InvalidOperationException("Chỉ khởi tạo quản trị khi chưa có tài khoản.");
        var password = Environment.GetEnvironmentVariable("VCMS_BOOTSTRAP_PASSWORD");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("Đặt VCMS_BOOTSTRAP_PASSWORD tối thiểu 12 ký tự.");
        var role = await db.Roles.SingleAsync(x => x.RoleName == AppRoles.Admin);
        var user = new User { RoleID = role.RoleID, Username = "admin", FullName = "Quản trị VCMS", IsActive = true };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
