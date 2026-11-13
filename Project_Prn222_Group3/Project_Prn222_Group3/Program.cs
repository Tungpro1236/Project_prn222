using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;

var bootstrapAdmin = args.Contains("--bootstrap-admin");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--bootstrap-admin").ToArray());

var connectionString = builder.Configuration.GetConnectionString("Vcms")
    ?? throw new InvalidOperationException("Thiếu chuỗi kết nối 'Vcms' trong appsettings.json.");

builder.Services.AddDbContext<VcmsDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ReceptionService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ExaminationService>();
builder.Services.AddScoped<PharmacyService>();
builder.Services.AddScoped<ActiveUserCookieEvents>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.EventsType = typeof(ActiveUserCookieEvents);
        options.Cookie.Name = "Vcms.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRazorPages().AddMvcOptions(options =>
    options.ModelBinderProviders.Insert(0, new Project_Prn222_Group3.Infrastructure.InvariantDecimalModelBinderProvider()));

var app = builder.Build();

await DbSeeder.SeedAsync(app.Services);
if (bootstrapAdmin)
{
    await DbSeeder.BootstrapAdminAsync(app.Services);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
