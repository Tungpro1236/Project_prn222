using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using ClientProfile = Project_Prn222_Group3.Models.Client;

namespace Project_Prn222_Group3.Pages.Customers;

[Authorize(Roles = AppRoles.RecordEditors)]
public class DetailsModel : PageModel
{
    private readonly VcmsDbContext _db;

    public DetailsModel(VcmsDbContext db)
    {
        _db = db;
    }

    public ClientProfile ClientRecord { get; set; } = null!;
    public IList<Appointment> Appointments { get; set; } = new List<Appointment>();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var client = await _db.Clients
            .Include(c => c.User)
            .Include(c => c.Animals)
            .FirstOrDefaultAsync(c => c.ClientID == id);
        if (client is null)
        {
            return NotFound();
        }

        ClientRecord = client;
        Appointments = await _db.Appointments
            .Include(a => a.Animal)
            .Include(a => a.Veterinarian)
            .Where(a => a.Animal.ClientID == id)
            .OrderByDescending(a => a.AppointmentTime)
            .Take(20)
            .ToListAsync();
        return Page();
    }
}

[Authorize(Roles = AppRoles.RecordEditors)]
public class PetModel : PageModel
{
    private readonly VcmsDbContext _db;

    public PetModel(VcmsDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ClientName { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public bool IsEdit { get; set; }

    public List<SelectListItem> GenderOptions { get; } =
    [
        new("Không rõ", "Unknown"),
        new("Đực", "Male"),
        new("Cái", "Female")
    ];

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên thú cưng.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập loài.")]
        [StringLength(60)]
        public string Species { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Breed { get; set; }

        [Required]
        public string Gender { get; set; } = "Unknown";

        public DateTime? DateOfBirth { get; set; }

        [Range(0.01, 500, ErrorMessage = "Cân nặng phải từ 0.01 đến 500 kg.")]
        public decimal? WeightKg { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int clientId, int? id)
    {
        return await LoadAsync(clientId, id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(int clientId, int? id)
    {
        if (!await LoadAsync(clientId, id))
        {
            return NotFound();
        }

        if (Input.DateOfBirth?.Date > ClinicTime.VnToday)
        {
            ModelState.AddModelError("Input.DateOfBirth", "Ngày sinh không được ở tương lai.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var animal = id is null
            ? new Animal { ClientID = clientId }
            : await _db.Animals.FirstAsync(a => a.AnimalID == id && a.ClientID == clientId);

        animal.Name = Input.Name.Trim();
        animal.Species = Input.Species.Trim();
        animal.Breed = string.IsNullOrWhiteSpace(Input.Breed) ? null : Input.Breed.Trim();
        animal.Gender = Input.Gender is "Male" or "Female" ? Input.Gender : "Unknown";
        animal.DateOfBirth = Input.DateOfBirth?.Date;
        animal.WeightKg = Input.WeightKg;
        animal.Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim();
        animal.IsActive = Input.IsActive;

        if (id is null)
        {
            _db.Animals.Add(animal);
        }

        await _db.SaveChangesAsync();
        TempData["Flash"] = id is null ? "Đã thêm thú cưng." : "Đã cập nhật thú cưng.";
        return RedirectToPage("Details", new { id = clientId });
    }

    private async Task<bool> LoadAsync(int clientId, int? id)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientID == clientId);
        if (client is null)
        {
            return false;
        }

        ClientId = clientId;
        ClientName = client.FullName;
        IsEdit = id is not null;

        if (id is null)
        {
            return true;
        }

        var animal = await _db.Animals.FirstOrDefaultAsync(a => a.AnimalID == id && a.ClientID == clientId);
        if (animal is null)
        {
            return false;
        }

        if (!Request.HasFormContentType)
        {
            Input = new InputModel
            {
                Name = animal.Name,
                Species = animal.Species,
                Breed = animal.Breed,
                Gender = animal.Gender,
                DateOfBirth = animal.DateOfBirth,
                WeightKg = animal.WeightKg,
                Notes = animal.Notes,
                IsActive = animal.IsActive
            };
        }

        return true;
    }
}
