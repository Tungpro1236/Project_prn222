# VCMS — Codex Handoff Context

## 1. What this project is

PRN222 group project: **Veterinary Clinic Management System (VCMS)**.

- ASP.NET Core **Razor Pages**, .NET 8
- EF Core 8 + SQL Server
- Cookie authentication with role-based authorization
- UI text is **Vietnamese**
- No EF migrations: schema is created by `VCMS_Database.sql`, demo data by `DbSeeder`

Local repo: `E:\Study\Fall26\PRN222\GroupProject\Project_prn222`
Remote: `https://github.com/Tungpro1236/Project_prn222.git`
Current branch: `nghia`

## 2. Git state and constraints

- `nghia` was created from `Kiet`; `linh` was merged.
- Merge commit `5611020` is **local only**.
- The FE-01/FE-02/FE-03 work listed below is **uncommitted** in the working tree.
- `dev_tung` (`7a6f3ce`) has an **empty tree**. Do **not** merge it — it would delete the project.
- Do **not** push unless the user explicitly asks.
- Remote branches `Kiet`, `linh`, `main` are already merged into `nghia`.
- `Project_Prn222_Group3` is nested twice on disk:
  `Project_prn222\Project_Prn222_Group3\Project_Prn222_Group3\Project_Prn222_Group3.csproj`

## 3. Build and run

```powershell
dotnet build "E:\Study\Fall26\PRN222\GroupProject\Project_prn222\Project_Prn222_Group3\Project_Prn222_Group3\Project_Prn222_Group3.csproj"

dotnet run --project "E:\Study\Fall26\PRN222\GroupProject\Project_prn222\Project_Prn222_Group3\Project_Prn222_Group3\Project_Prn222_Group3.csproj" --urls "http://127.0.0.1:5248"
```

The last verified build had **0 warnings and 0 errors**.

## 4. Local database

- SQL Server instance: `localhost` / `DESKTOP-I74VCAM`
- Database: `VeterinaryClinicDB`
- Local auth: **Windows authentication** (the `sa` login does not work locally)
- `appsettings.Development.json` uses:
  `Server=localhost;Database=VeterinaryClinicDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True`
- Shared `appsettings.json` still targets `LAPTOP-NMGC06E7` with `sa`/`123`; leave it unless the user asks.
- Apply schema with:

```powershell
sqlcmd -S localhost -d master -E -i "E:\Study\Fall26\PRN222\GroupProject\Project_prn222\VCMS_Database.sql"
```

`VCMS_Database.sql` is idempotent and creates:
`Roles`, `Users`, `Clients`, `Animals`, `Appointments`, `Visits`, `QueueEntries`,
`ClinicSettings`, `Services`, `StaffSchedules`, `BlockedTimes`.

`DbSeeder` runs on startup and seeds roles, demo users, one client with two pets,
clinic settings, three services, weekday schedules, a lunch block, and two demo appointments.

### Demo accounts

| Username | Password | Role |
|---|---|---|
| `manager` | `Manager@123` | ClinicManager |
| `reception` | `Reception@123` | Receptionist |
| `vet1` | `Vet@123` | Veterinarian |
| `groomer1` | `Groomer@123` | PetGroomer |
| `client1` | `Client@123` | Client (owns Miu and Bông) |

## 5. Architecture map

| Area | Path |
|---|---|
| Startup, DI, auth | `Program.cs` |
| DbContext | `Data\VcmsDbContext.cs` |
| Seed data | `Data\DbSeeder.cs` |
| Entities | `Models\Entities.cs` |
| Roles / role groups | `Models\AppRoles.cs` |
| Status constants + display text | `Models\Statuses.cs` |
| UTC <-> Vietnam time (+7) | `Models\ClinicTime.cs` |
| Booking rules | `Services\BookingService.cs` |
| Check-in / queue | `Services\ReceptionService.cs` (teammate code) |
| Appointment pages | `Pages\Appointments\` |
| Customer / pet pages | `Pages\Customers\` |
| Clinic configuration pages | `Pages\Clinic\` |
| Staff account pages | `Pages\Staff\` |
| Reception pages | `Pages\Reception\` |
| Client portal | `Pages\Client\` |

## 6. Implemented work (FE-01, FE-02, FE-03)

### FE-01 — Clinic configuration (managers only)

- `Pages\Clinic\Settings` — clinic name, address, phone, email, slot size, open/close hours.
- `Pages\Clinic\Services` — service create/edit, category (`Veterinary` / `Grooming`), duration, price, active flag.
- `Pages\Clinic\Schedules` — weekly staff schedule plus blocked time ranges.
- Authorization: `AppRoles.Managers` (`Admin`, `ClinicManager`).
- Schedule/block forms use separate POST handlers and `ModelState.Clear()` + `TryUpdateModelAsync` so the two forms validate independently.

### FE-02 — Customer and pet records (record editors)

- `Pages\Customers\Index` — search by name / phone / email, client list with pet counts.
- `Pages\Customers\Create` — create client, optional client login account.
- `Pages\Customers\Details` — client profile, pets, linked login.
- `Pages\Customers\Edit` — edit client.
- `Pages\Customers\Pet` — add/edit pet (species, breed, gender, DOB, weight, notes, active).
- Authorization: `AppRoles.RecordEditors` (`Admin`, `Receptionist`, `ClinicManager`).
- `CreateClient` validates duplicate phone, optional login fields only when `CreateLogin` is checked, and duplicate username/email.
- Clients cannot access these staff pages.

### FE-03 — Availability and booking

- `Pages\Appointments\Index` — list appointments.
  - Clients see only their own pets' appointments (all dates).
  - Veterinarians / groomers see only their own appointments for one selected day.
  - Bookers see the selected day's appointments.
  - `CanBook` controls whether the "Đặt lịch" button and row actions render.
- `Pages\Appointments\Book` — create or reschedule.
  - AJAX handlers: `?handler=Animals`, `?handler=Providers`, `?handler=Slots`.
  - Provider role is chosen from service category: grooming -> `PetGroomer`, otherwise `Veterinarian`.
- `Pages\Appointments\Cancel` — POST-only cancel.
- Authorization: viewers = `AppRoles.AppointmentViewers`;
  book/reschedule/cancel = `AppRoles.Bookers` (`Admin`, `Receptionist`, `ClinicManager`, `Client`).

## 7. Business rules (must stay intact)

- **All appointment times are stored in UTC.** Vietnam is a fixed +7 offset via `ClinicTime`.
- Active statuses that occupy a slot: `Pending`, `Confirmed`, `CheckedIn`.
- Available slots are the intersection of:
  - clinic open/close hours,
  - the provider's `StaffSchedules` row for that weekday (`IsWorking`),
  - not overlapping active appointments,
  - not overlapping `BlockedTimes`,
  - starting after "now" in Vietnam time.
- Slot step comes from `ClinicSettings.SlotMinutes` (minimum 5); service duration decides slot length.
- Booking validation requires the start minute to be divisible by 5 and seconds to be 0.
- Staff-created bookings are `Confirmed`; client-created bookings are `Pending`.
- Rescheduling a client booking sets it back to `Pending`; staff reschedules keep `Confirmed`.
- Clients can only book, reschedule, or cancel appointments for their **own** pets.
  This is enforced server-side in `BookingService.ValidateAsync`, `RescheduleAsync`, and `CancelAsync`.
- Veterinarians and groomers are **read-only**: they may view their own appointments but cannot book, reschedule, or cancel.
- Only `Pending` or `Confirmed` appointments can be rescheduled or cancelled.
- Booking uses a `Serializable` transaction with one retry on `DbUpdateException`.

## 8. Verification already done

The running app was tested over HTTP (login, antiforgery tokens, real POSTs):

- Reception books an open slot successfully; the same slot is rejected as taken.
- Client books own pet successfully; booking another client's pet is rejected.
- Reschedule works and updates the stored UTC time.
- Cancel works; cancelled slots become available again.
- The seeded 12:00–13:00 lunch block is excluded from slots.
- Duplicate customer phone is rejected; new customer + pet can be created.
- Access control:
  - `vet1` / `groomer1`: `/Appointments` 200; `/Appointments/Book`, `/Customers`, `/Clinic/*` redirect to AccessDenied.
  - `client1`: `/Appointments/Book` 200; `/Customers`, `/Clinic/*` AccessDenied.
  - `reception`: `/Clinic/*` AccessDenied.
- Temporary test appointments, client, and pet were deleted afterwards; demo data is clean.

There are **no automated tests** in the repo; the above was manual HTTP verification.

## 9. Known gaps / likely next work

- No written personal assignment exists; FE-01..FE-03 were inferred for Nguyễn Trung Nghĩa (Backend Developer).
- Do **not** build clinical encounters, pharmacy, billing, or reports — outside the assigned scope.
- `appsettings.json` is still machine-specific to a teammate's laptop.
- No EF migrations; if the schema changes, update both `VCMS_Database.sql` and `VcmsDbContext`.
- The local app process is stopped; port 5248 is free.

## 10. Instructions for Codex

1. Read this file before changing code.
2. Keep the existing structure, naming, and Vietnamese UI copy.
3. Run `dotnet build` after changes and report warnings/errors.
4. Keep appointment timestamps in UTC; use `ClinicTime` for any local conversion.
5. Enforce authorization in the page model / service, not only in the UI.
6. Do not merge `dev_tung`.
7. Do not push or commit unless the user asks.
8. Do not delete or rewrite teammates' unrelated work; investigate first.
