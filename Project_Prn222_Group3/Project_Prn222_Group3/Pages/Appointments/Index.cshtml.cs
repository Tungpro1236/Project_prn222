using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;

namespace Project_Prn222_Group3.Pages.Appointments;

[Authorize(Roles = AppRoles.AppointmentViewers)]
public class IndexModel : PageModel
{
    private readonly VcmsDbContext _db;

    public IndexModel(VcmsDbContext db)
    {
        _db = db;
    }

    public IList<Appointment> Items { get; set; } = new List<Appointment>();
    public bool IsClient => User.IsInRole(AppRoles.Client);
    public bool CanBook => User.IsInRole(AppRoles.Admin)
        || User.IsInRole(AppRoles.Receptionist)
        || User.IsInRole(AppRoles.ClinicManager)
        || User.IsInRole(AppRoles.Client);
    public bool IsProvider => User.IsInRole(AppRoles.Veterinarian) || User.IsInRole(AppRoles.PetGroomer);

    [BindProperty(SupportsGet = true)]
    public DateTime? Date { get; set; }

    public async Task OnGetAsync()
    {
        var query = _db.Appointments
            .Include(a => a.Animal).ThenInclude(a => a.Client)
            .Include(a => a.Service)
            .Include(a => a.Veterinarian).ThenInclude(u => u.Role)
            .AsQueryable();

        if (IsClient)
        {
            var userId = CurrentUserId;
            query = query.Where(a => a.Animal.Client.UserID == userId);
        }
        else if (IsProvider && !CanBook)
        {
            var userId = CurrentUserId;
            var date = (Date ?? ClinicTime.VnToday).Date;
            Date = date;
            var start = ClinicTime.VnLocalToUtc(date);
            var end = ClinicTime.VnLocalToUtc(date.AddDays(1));
            query = query.Where(a => a.VeterinarianID == userId && a.AppointmentTime >= start && a.AppointmentTime < end);
        }
        else
        {
            var date = (Date ?? ClinicTime.VnToday).Date;
            Date = date;
            var start = ClinicTime.VnLocalToUtc(date);
            var end = ClinicTime.VnLocalToUtc(date.AddDays(1));
            query = query.Where(a => a.AppointmentTime >= start && a.AppointmentTime < end);
        }

        Items = await query.OrderBy(a => a.AppointmentTime).ToListAsync();
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}

[Authorize(Roles = AppRoles.Bookers)]
public class BookModel : PageModel
{
    private readonly VcmsDbContext _db;
    private readonly BookingService _booking;

    public BookModel(VcmsDbContext db, BookingService booking)
    {
        _db = db;
        _booking = booking;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> ClientOptions { get; set; } = new();
    public List<SelectListItem> AnimalOptions { get; set; } = new();
    public List<SelectListItem> ServiceOptions { get; set; } = new();
    public List<SelectListItem> ProviderOptions { get; set; } = new();
    public IReadOnlyList<AvailableSlot> Slots { get; set; } = [];
    public bool IsClient { get; set; }
    public int? EditingId { get; set; }

    public class InputModel
    {
        public int? ClientID { get; set; }
        public int AnimalID { get; set; }
        public int ServiceID { get; set; }
        public int ProviderID { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày.")]
        public DateTime? Date { get; set; }

        public DateTime? Slot { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        IsClient = User.IsInRole(AppRoles.Client);
        EditingId = id;
        Input.Date ??= ClinicTime.VnToday.AddDays(1);

        if (id is not null)
        {
            var appointment = await LoadEditableAsync(id.Value);
            if (appointment is null)
            {
                return NotFound();
            }

            Input.ClientID = appointment.Animal.ClientID;
            Input.AnimalID = appointment.AnimalID;
            Input.ServiceID = appointment.ServiceID ?? 0;
            Input.ProviderID = appointment.VeterinarianID;
            Input.Date = ClinicTime.ToVietnam(appointment.AppointmentTime).Date;
            Input.Slot = ClinicTime.ToVietnam(appointment.AppointmentTime);
            Input.Reason = appointment.Reason;
        }

        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IsClient = User.IsInRole(AppRoles.Client);
        EditingId = id;
        await LoadOptionsAsync();

        if (Input.Slot is null)
        {
            ModelState.AddModelError("Input.Slot", "Vui lòng chọn một khung giờ còn trống.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = CurrentUserId;
        var request = new BookingRequest(
            Input.AnimalID,
            Input.ProviderID,
            Input.ServiceID,
            Input.Slot!.Value,
            Input.Reason,
            userId);

        var result = id is null
            ? await _booking.BookAsync(request)
            : await _booking.RescheduleAsync(id.Value, request, userId, IsClient);

        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await LoadOptionsAsync();
            return Page();
        }

        TempData["Flash"] = result.Message;
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnGetSlotsAsync(int providerId, int serviceId, DateTime date, int? ignoreId)
    {
        var slots = await _booking.GetAvailableSlotsAsync(providerId, serviceId, date, ignoreId);
        return new JsonResult(slots.Select(s => new
        {
            value = s.LocalStart.ToString("yyyy-MM-ddTHH:mm"),
            text = $"{s.LocalStart:HH:mm} - {s.LocalEnd:HH:mm}"
        }));
    }

    public async Task<IActionResult> OnGetAnimalsAsync(int clientId)
    {
        var animals = await _db.Animals
            .Where(a => a.ClientID == clientId && a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new { value = a.AnimalID, text = a.Name + " (" + a.Species + ")" })
            .ToListAsync();
        return new JsonResult(animals);
    }

    public async Task<IActionResult> OnGetProvidersAsync(int serviceId)
    {
        var service = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == serviceId && s.IsActive);
        if (service is null)
        {
            return new JsonResult(Array.Empty<object>());
        }

        var role = service.Category == ServiceCategories.Grooming ? AppRoles.PetGroomer : AppRoles.Veterinarian;
        var providers = await _db.Users
            .Where(u => u.IsActive && u.Role.RoleName == role)
            .OrderBy(u => u.FullName)
            .Select(u => new { value = u.UserID, text = u.FullName })
            .ToListAsync();
        return new JsonResult(providers);
    }

    private async Task<Appointment?> LoadEditableAsync(int id)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Animal).ThenInclude(a => a.Client)
            .FirstOrDefaultAsync(a => a.AppointmentID == id);
        if (appointment is null)
        {
            return null;
        }

        if (User.IsInRole(AppRoles.Client) && appointment.Animal.Client.UserID != CurrentUserId)
        {
            return null;
        }

        return appointment.Status is AppointmentStatuses.Pending or AppointmentStatuses.Confirmed
            ? appointment
            : null;
    }

    private async Task LoadOptionsAsync()
    {
        if (IsClient)
        {
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.UserID == CurrentUserId);
            Input.ClientID = client?.ClientID;
            AnimalOptions = client is null
                ? []
                : await _db.Animals
                    .Where(a => a.ClientID == client.ClientID && a.IsActive)
                    .OrderBy(a => a.Name)
                    .Select(a => new SelectListItem(a.Name + " (" + a.Species + ")", a.AnimalID.ToString()))
                    .ToListAsync();
        }
        else
        {
            ClientOptions = await _db.Clients
                .OrderBy(c => c.FullName)
                .Select(c => new SelectListItem(c.FullName + " - " + c.Phone, c.ClientID.ToString()))
                .ToListAsync();

            if (Input.ClientID is not null)
            {
                AnimalOptions = await _db.Animals
                    .Where(a => a.ClientID == Input.ClientID && a.IsActive)
                    .OrderBy(a => a.Name)
                    .Select(a => new SelectListItem(a.Name + " (" + a.Species + ")", a.AnimalID.ToString()))
                    .ToListAsync();
            }
        }

        ServiceOptions = await _db.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name + " (" + s.DurationMinutes + " phút)", s.ServiceID.ToString()))
            .ToListAsync();

        if (Input.ServiceID > 0)
        {
            var service = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == Input.ServiceID);
            var role = service?.Category == ServiceCategories.Grooming ? AppRoles.PetGroomer : AppRoles.Veterinarian;
            ProviderOptions = await _db.Users
                .Where(u => u.IsActive && u.Role.RoleName == role)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem(u.FullName, u.UserID.ToString()))
                .ToListAsync();
        }

        if (Input.ProviderID > 0 && Input.ServiceID > 0 && Input.Date is not null)
        {
            Slots = await _booking.GetAvailableSlotsAsync(Input.ProviderID, Input.ServiceID, Input.Date.Value, EditingId);
        }
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}

[Authorize(Roles = AppRoles.Bookers)]
public class CancelModel : PageModel
{
    private readonly BookingService _booking;

    public CancelModel(BookingService booking)
    {
        _booking = booking;
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        var result = await _booking.CancelAsync(id, userId, User.IsInRole(AppRoles.Client));
        TempData[result.Ok ? "Flash" : "FlashError"] = result.Message;
        return RedirectToPage("Index");
    }
}
