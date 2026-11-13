using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;

// Real SQL Server integration, isolated from VeterinaryClinicDB. No mock/in-memory provider.
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var database = "VCMS_Test_" + Guid.NewGuid().ToString("N");
var master = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True";
var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = database }.ConnectionString;
var options = new DbContextOptionsBuilder<VcmsDbContext>().UseSqlServer(connection).Options;
Process? server = null;
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); checks++; }
async Task With(Func<VcmsDbContext, Task> action) { await using var db = new VcmsDbContext(options); await action(db); }
async Task<OperationResult> Care(Func<ExaminationService, Task<OperationResult>> action)
{ await using var db = new VcmsDbContext(options); return await action(new ExaminationService(db)); }
async Task<OperationResult> Pharmacy(Func<PharmacyService, Task<OperationResult>> action)
{ await using var db = new VcmsDbContext(options); return await action(new PharmacyService(db)); }
async Task<int> Balance(int id) { await using var db = new VcmsDbContext(options); return await db.MedicineBatches.Where(x => x.BatchID == id).Select(x => x.QuantityOnHand).SingleAsync(); }
try
{
    var sql = File.ReadAllText(Path.Combine(repo, "VCMS_Database.sql")).Replace("VeterinaryClinicDB", database);
    await using (var conn = new SqlConnection(master))
    {
        await conn.OpenAsync();
        foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) { await using var cmd = new SqlCommand(batch, conn); await cmd.ExecuteNonQueryAsync(); }
    }
    Console.WriteLine("Isolated database: " + database);
    var password = "Integration-Only!" + Guid.NewGuid().ToString("N");
    var bootstrapAppDir = Path.Combine(repo, "Project_Prn222_Group3/Project_Prn222_Group3");
    var bootstrapStart = new ProcessStartInfo("dotnet") { WorkingDirectory = bootstrapAppDir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
    bootstrapStart.ArgumentList.Add(Path.Combine(bootstrapAppDir, "bin/Debug/net8.0/Project_Prn222_Group3.dll"));
    bootstrapStart.ArgumentList.Add("--bootstrap-admin");
    bootstrapStart.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    bootstrapStart.Environment["ConnectionStrings__Vcms"] = connection;
    bootstrapStart.Environment["VCMS_BOOTSTRAP_PASSWORD"] = password;
    using (var bootstrapProcess = Process.Start(bootstrapStart)!)
    {
        await bootstrapProcess.WaitForExitAsync();
        Check(bootstrapProcess.ExitCode == 0, "explicit admin bootstrap command exits successfully");
    }
    await With(async db =>
    {
        var bootstrapUser = await db.Users.Include(x => x.Role).SingleAsync();
        Check(bootstrapUser.Username == "admin" && bootstrapUser.Role.RoleName == AppRoles.Admin
            && new PasswordHasher<User>().VerifyHashedPassword(bootstrapUser, bootstrapUser.PasswordHash, password) != PasswordVerificationResult.Failed,
            "bootstrap creates only a hashed admin account");
    });
    int vet = 0, otherVet = 0, staff = 0, admin = 0, clientUser = 0, inactive = 0, visit = 0, animal = 0;
    await With(async db =>
    {
        // Detect phantom EF foreign keys and stale mapped columns before testing workflows.
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var columns = entity.GetProperties().Select(p => "[" + p.GetColumnName(store) + "]");
            #pragma warning disable EF1002 // SQL identifiers are read solely from the compiled EF model.
            await db.Database.ExecuteSqlRawAsync($"SELECT TOP(0) {string.Join(',', columns)} FROM [dbo].[{entity.GetTableName()}]");
            #pragma warning restore EF1002
        }
        Check(true, "all mapped tables and columns exist in team SQL");
        var roles = await db.Roles.ToDictionaryAsync(x => x.RoleName);
        User Add(string name, string role, bool active = true)
        {
            var row = new User { Username = name, FullName = name, RoleID = roles[role].RoleID, IsActive = active };
            row.PasswordHash = new PasswordHasher<User>().HashPassword(row, password); db.Users.Add(row); return row;
        }
        var v = Add("test-vet", AppRoles.Veterinarian); var ov = Add("other-vet", AppRoles.Veterinarian);
        var s = Add("test-pharmacy", AppRoles.DispensingStaff); var a = Add("test-admin", AppRoles.Admin);
        var c = Add("test-client", AppRoles.Client); var locked = Add("locked-vet", AppRoles.Veterinarian, false);
        await db.SaveChangesAsync(); vet = v.UserID; otherVet = ov.UserID; staff = s.UserID; admin = a.UserID; clientUser = c.UserID; inactive = locked.UserID;
        var owner = new Client { UserID = c.UserID, FullName = "Chủ nuôi kiểm thử", Phone = "0909999999" }; db.Clients.Add(owner); await db.SaveChangesAsync();
        var pet = new Animal { ClientID = owner.ClientID, Name = "Mèo kiểm thử", Species = "Mèo", Gender = "Unknown" }; db.Animals.Add(pet); await db.SaveChangesAsync(); animal = pet.AnimalID;
        var appt = new Appointment { AnimalID = animal, VeterinarianID = vet, AppointmentTime = ClinicTime.UtcNow, ExpectedDurationMinutes = 30, Status = "CheckedIn" }; db.Appointments.Add(appt); await db.SaveChangesAsync();
        var vrow = new Visit { AppointmentID = appt.AppointmentID, ReceptionistID = admin, Status = "InProgress" }; db.Visits.Add(vrow); await db.SaveChangesAsync(); visit = vrow.VisitID;
    });
    var notes = new EncounterInput("Bỏ ăn", "Ghi chú kiểm thử", "Chẩn đoán kiểm thử");
    Check(!(await Care(s => s.SaveEncounterAsync(visit, otherVet, notes))).Ok, "other veterinarian cannot edit encounter");
    Check(!(await Care(s => s.SaveEncounterAsync(visit, clientUser, notes))).Ok, "client cannot write clinical records");
    Check(!(await Care(s => s.SaveEncounterAsync(visit, admin, notes))).Ok, "admin has read-only clinical access");
    Check(!(await Care(s => s.SaveEncounterAsync(visit, inactive, notes))).Ok, "inactive actor rejected at service layer");
    var encounter = await Care(s => s.SaveEncounterAsync(visit, vet, notes)); Check(encounter.Ok, "create encounter draft");
    var rx = await Care(s => s.CreatePrescriptionAsync(encounter.Id!.Value, vet)); Check(rx.Ok, "create prescription draft");
    Check((await Care(s => s.CreatePrescriptionAsync(encounter.Id!.Value, vet))).Id == rx.Id, "repeat create reuses current prescription");
    var medResult = await Pharmacy(s => s.SaveMedicineAsync(null, staff, new("Thuốc kiểm thử", "Viên", 12000.50m, true))); Check(medResult.Ok, "create Vietnamese medicine");
    var med = medResult.Id!.Value;
    Check(!(await Pharmacy(s => s.SaveMedicineAsync(med, vet, new("Forbidden", "Viên", 1, true)))).Ok, "veterinarian cannot mutate pharmacy catalogue");
    var receipt = await Pharmacy(s => s.ReceiveAsync(med, staff, "LOT-A", ClinicTime.VnToday.AddDays(30), 20, 5000, "Nhập thử")); Check(receipt.Ok, "receive batch with stock movement");
    var batchId = receipt.Id!.Value;
    Check(!(await Pharmacy(s => s.ReceiveAsync(med, staff, "BAD", ClinicTime.VnToday.AddDays(-1), 1, null, null))).Ok, "expired receipt rejected");
    Check(!(await Pharmacy(s => s.AdjustAsync(batchId, staff, -21, "Kiểm thử"))).Ok && await Balance(batchId) == 20, "negative stock adjustment rejected atomically");
    Check((await Pharmacy(s => s.AdjustAsync(batchId, staff, -2, "Kiểm kê"))).Ok && await Balance(batchId) == 18, "valid adjustment changes balance");
    var line = new PrescriptionLineInput(med, 10, "Liều dùng kiểm thử", "Theo đơn", 2, null);
    Check((await Care(s => s.SaveItemAsync(rx.Id!.Value, null, vet, line))).Ok, "add prescription item");
    Check(!(await Care(s => s.FinalizePrescriptionAsync(rx.Id!.Value, vet, null))).Ok, "prescription requires finalized encounter");
    Check(!(await Care(s => s.SaveEncounterAsync(visit, vet, notes with { Diagnosis = "" }, true))).Ok, "finalization requires diagnosis");
    Check((await Care(s => s.SaveEncounterAsync(visit, vet, notes, true))).Ok, "finalize encounter");
    Check(!(await Care(s => s.SaveEncounterAsync(visit, vet, notes))).Ok, "finalized encounter requires correction reason");
    Check((await Care(s => s.SaveEncounterAsync(visit, vet, notes with { Diagnosis = "Chẩn đoán hiệu chỉnh" }, correctionReason: "Bổ sung kết quả"))).Ok, "correct encounter with preserved history");
    Check((await Care(s => s.FinalizePrescriptionAsync(rx.Id!.Value, vet, "Hướng dẫn kiểm thử"))).Ok && await Balance(batchId) == 18, "finalize prescription snapshots without deducting stock");
    Check(!(await Care(s => s.SaveItemAsync(rx.Id!.Value, null, vet, line))).Ok, "finalized prescription items locked");
    Check((await Pharmacy(s => s.SaveMedicineAsync(med, staff, new("Tên thuốc mới", "Viên", 99999, true)))).Ok, "catalogue price change after finalization");
    int itemId = 0, expiredId = 0, wrongId = 0;
    await With(async db =>
    {
        itemId = await db.PrescriptionItems.Where(x => x.PrescriptionID == rx.Id).Select(x => x.PrescriptionItemID).SingleAsync();
        var expired = new MedicineBatch { MedicineID = med, BatchNumber = "EXPIRED", ExpiryDate = ClinicTime.VnToday.AddDays(-1), QuantityOnHand = 100 };
        var wrong = new MedicineBatch { MedicineID = await db.Medicines.Where(x => x.MedicineID != med).Select(x => x.MedicineID).FirstAsync(), BatchNumber = "WRONG", ExpiryDate = ClinicTime.VnToday.AddDays(10), QuantityOnHand = 100 };
        db.MedicineBatches.AddRange(expired, wrong); await db.SaveChangesAsync(); expiredId = expired.BatchID; wrongId = wrong.BatchID;
    });
    Check(!(await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, Guid.NewGuid(), [new(itemId, expiredId, 1)]))).Ok, "expired batch cannot be dispensed");
    Check(!(await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, Guid.NewGuid(), [new(itemId, wrongId, 1)]))).Ok, "wrong medicine batch rejected");
    Check(!(await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, clientUser, Guid.NewGuid(), [new(itemId, batchId, 1)]))).Ok, "client cannot dispense");
    Check(!(await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, Guid.NewGuid(), [new(itemId, batchId, 11)]))).Ok, "over-prescribed quantity rejected");
    Check(!(await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, Guid.NewGuid(), []))).Ok, "empty completed dispense impossible through service");
    var key = Guid.NewGuid(); var first = await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, key, [new(itemId, batchId, 3)]));
    Check(first.Ok && await Balance(batchId) == 15, "partial dispense changes stock");
    Check((await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, key, [new(itemId, batchId, 3)]))).Id == first.Id && await Balance(batchId) == 15, "same idempotency key never deducts twice");
    Check(!(await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, key, [new(itemId, batchId, 2)]))).Ok, "idempotency key cannot change payload");
    var racing = await Task.WhenAll(Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, Guid.NewGuid(), [new(itemId, batchId, 5)])), Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, Guid.NewGuid(), [new(itemId, batchId, 5)])));
    Check(racing.Count(x => x.Ok) == 1 && await Balance(batchId) == 10, "concurrent dispense cannot exceed remaining prescription");
    Check(!(await Care(s => s.CancelPrescriptionAsync(rx.Id!.Value, vet, "Hủy thử"))).Ok, "issued prescription cannot be cancelled");
    var secondBatch = await Pharmacy(s => s.ReceiveAsync(med, staff, "LOT-B", ClinicTime.VnToday.AddDays(20), 10, 4000, null));
    Check((await Pharmacy(s => s.DispenseAsync(rx.Id!.Value, staff, Guid.NewGuid(), [new(itemId, batchId, 1), new(itemId, secondBatch.Id!.Value, 1)]))).Ok, "remaining amount can be split across batches");
    await With(async db =>
    {
        Check(await db.DispensationItems.Where(x => x.PrescriptionItemID == itemId).AllAsync(x => x.UnitPrice == 12000.50m), "dispense uses finalized price snapshot");
        Check(await db.DispensationItems.Where(x => x.PrescriptionItemID == itemId).SumAsync(x => x.Quantity) == 10, "cumulative dispensed equals prescribed");
        var history = await db.AuditLogs.Where(x => x.EntityName == "Encounter" && x.Action == "Correct").SingleAsync();
        Check(history.OldValues!.Contains("Diagnosis") && history.NewValues!.Contains("CorrectionReason"), "correction retains previous values and reason");
        var stored = await db.MedicineBatches.SingleAsync(x => x.BatchID == batchId);
        Check(stored.QuantityOnHand == await db.StockMovements.Where(x => x.BatchID == batchId).SumAsync(x => x.QuantityChange), "stock movement ledger reconciles balance");
        history.Action = "Tamper";
        try { await db.SaveChangesAsync(); throw new Exception("Audit modification allowed"); }
        catch (InvalidOperationException) { Check(true, "audit edits rejected by DbContext"); }
    });

    // Run real cookie/antiforgery HTTP requests against the isolated database.
    var appDir = Path.Combine(repo, "Project_Prn222_Group3/Project_Prn222_Group3");
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = appDir, UseShellExecute = false, CreateNoWindow = true };
    start.ArgumentList.Add(Path.Combine(appDir, "bin/Debug/net8.0/Project_Prn222_Group3.dll"));
    start.ArgumentList.Add("--urls"); start.ArgumentList.Add("http://127.0.0.1:5249");
    start.Environment["Logging__LogLevel__Default"] = "Warning";
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["ConnectionStrings__Vcms"] = connection;
    server = Process.Start(start)!;
    using var probe = new HttpClient();
    for (var i = 0; i < 60; i++)
    {
        try { using var ready = await probe.GetAsync("http://127.0.0.1:5249/Account/Login"); if (ready.IsSuccessStatusCode) break; }
        catch (HttpRequestException) { }
        if (server.HasExited) throw new Exception("Web server exited");
        await Task.Delay(250);
    }
    async Task<HttpClient> Login(string username)
    {
        var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri("http://127.0.0.1:5249") };
        var html = await http.GetStringAsync("/Account/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var response = await http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["Username"] = username, ["Password"] = password, ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
        Check(response.StatusCode == HttpStatusCode.Found, username + " cookie login"); return http;
    }
    using var vetHttp = await Login("test-vet"); using var staffHttp = await Login("test-pharmacy"); using var clientHttp = await Login("test-client");
    foreach (var path in new[] { "/", "/Examination", $"/Examination/Edit/{visit}", $"/Examination/Prescription/{rx.Id}", "/Appointments", "/Appointments/Book" })
    {
        using var response = await vetHttp.GetAsync(path);
        Check(path == "/Appointments/Book" ? response.StatusCode == HttpStatusCode.Found : response.IsSuccessStatusCode, "vet route " + path);
    }
    foreach (var path in new[] { "/Pharmacy", "/Pharmacy/Medicine", "/Pharmacy/Stock", $"/Pharmacy/Dispense/{rx.Id}" })
    {
        using var response = await staffHttp.GetAsync(path); Check(response.IsSuccessStatusCode, "pharmacy route " + path);
        using var denied = await clientHttp.GetAsync(path); Check(denied.StatusCode == HttpStatusCode.Found && denied.Headers.Location!.ToString().Contains("AccessDenied"), "client denied " + path);
    }
    using (var response = await clientHttp.GetAsync($"/Examination/Edit/{visit}")) Check(response.StatusCode == HttpStatusCode.Found, "client denied clinical records");
    using (var response = await staffHttp.PostAsync("/Pharmacy/Medicine", new FormUrlEncodedContent(new Dictionary<string,string> { ["Input.MedicineName"] = "No CSRF" }))) Check(response.StatusCode == HttpStatusCode.BadRequest, "antiforgery required for mutations");
    async Task<HttpResponseMessage> Post(HttpClient http, string path, Dictionary<string, string> values)
    {
        var html = await http.GetStringAsync(path.Split('?')[0]);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await http.PostAsync(path, new FormUrlEncodedContent(values));
    }
    int nextVisit = 0;
    await With(async db =>
    {
        var appt = new Appointment { AnimalID = animal, VeterinarianID = vet, AppointmentTime = ClinicTime.UtcNow.AddHours(2), ExpectedDurationMinutes = 30, Status = "CheckedIn" };
        db.Appointments.Add(appt); await db.SaveChangesAsync();
        var v = new Visit { AppointmentID = appt.AppointmentID, ReceptionistID = admin, Status = "InProgress" };
        db.Visits.Add(v);
        var tomorrow = ClinicTime.VnToday.AddDays(1);
        db.VeterinarianSchedules.AddRange(new VeterinarianSchedule { VeterinarianID = vet, StartAt = ClinicTime.VnLocalToUtc(tomorrow.AddHours(8)), EndAt = ClinicTime.VnLocalToUtc(tomorrow.AddHours(17)), IsAvailable = true },
            new VeterinarianSchedule { VeterinarianID = vet, StartAt = ClinicTime.VnLocalToUtc(tomorrow.AddHours(12)), EndAt = ClinicTime.VnLocalToUtc(tomorrow.AddHours(13)), IsAvailable = false });
        await db.SaveChangesAsync(); nextVisit = v.VisitID;
    });
    await With(async db =>
    {
        var booking = new BookingService(db);
        var tomorrow = ClinicTime.VnToday.AddDays(1);
        var slots = await booking.GetAvailableSlotsAsync(vet, 30, tomorrow);
        Check(slots.Count > 0 && slots.All(x => x.LocalEnd <= tomorrow.AddHours(12) || x.LocalStart >= tomorrow.AddHours(13)), "dated availability excludes unavailable intervals");
        Check((await booking.GetAvailableSlotsAsync(clientUser, 30, tomorrow)).Count == 0, "client cannot be assigned as veterinarian");
        var request = new BookingRequest(animal, vet, 30, tomorrow.AddHours(9), "Kiểm thử", clientUser);
        Check((await booking.BookAsync(request)).Ok, "booking writes team appointment columns");
        Check(!(await booking.BookAsync(request)).Ok, "duplicate booking slot rejected");
    });
    using (var response = await Post(vetHttp, $"/Examination/Edit/{nextVisit}", new() { ["Input.Symptoms"] = "Triệu chứng HTTP", ["Input.ExaminationNotes"] = "Kết quả HTTP", ["Input.Diagnosis"] = "Chẩn đoán HTTP", ["finalize"] = "true" }))
        Check(response.StatusCode == HttpStatusCode.Found, "HTTP form finalizes encounter");
    int nextRx;
    using (var response = await Post(vetHttp, $"/Examination/Edit/{nextVisit}?handler=Prescription", new()))
    {
        Check(response.StatusCode == HttpStatusCode.Found && response.Headers.Location!.ToString().Contains("Prescription"), "HTTP form creates prescription");
        nextRx = int.Parse(response.Headers.Location!.ToString().Split('/').Last());
    }
    using (var response = await Post(vetHttp, $"/Examination/Prescription/{nextRx}?handler=Item", new() { ["medicineId"] = med.ToString(), ["quantity"] = "4", ["dosage"] = "Theo hướng dẫn", ["frequency"] = "Theo đơn", ["durationDays"] = "2" }))
        Check(response.StatusCode == HttpStatusCode.Found, "HTTP form adds prescription item");
    using (var response = await Post(vetHttp, $"/Examination/Prescription/{nextRx}?handler=Finalize", new() { ["instructions"] = "Hướng dẫn HTTP" }))
        Check(response.StatusCode == HttpStatusCode.Found, "HTTP form finalizes prescription");
    using (var response = await Post(staffHttp, "/Pharmacy/Medicine", new() { ["Input.MedicineName"] = "Thuốc tạo qua HTTP", ["Input.Unit"] = "Viên", ["Input.CurrentPrice"] = "123.45", ["Input.IsActive"] = "true" }))
        Check(response.StatusCode == HttpStatusCode.Found, "HTTP form creates medicine");
    await With(async db => Check(await db.Medicines.AnyAsync(x => x.MedicineName == "Thuốc tạo qua HTTP" && x.CurrentPrice == 123.45m), "HTML decimal price is locale-independent"));
    int httpMedicineId = 0;
    await With(async db => httpMedicineId = await db.Medicines.Where(x => x.MedicineName == "Thuốc tạo qua HTTP").Select(x => x.MedicineID).SingleAsync());
    var medicineHtml = await staffHttp.GetStringAsync($"/Pharmacy/Medicine/{httpMedicineId}");
    var renderedPrice = Regex.Match(medicineHtml, "name=\"Input.CurrentPrice\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    Check(decimal.TryParse(renderedPrice, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var renderedAmount) && renderedAmount == 123.45m,
        "edit form renders fractional number input without locale corruption");
    using (var response = await Post(staffHttp, "/Pharmacy/Stock?handler=Receive", new() { ["medicineId"] = med.ToString(), ["batchNumber"] = "HTTP-BATCH", ["expiry"] = ClinicTime.VnToday.AddDays(40).ToString("yyyy-MM-dd"), ["quantity"] = "5", ["cost"] = "100" }))
        Check(response.StatusCode == HttpStatusCode.Found, "HTTP form records stock receipt");
    int nextItem = 0;
    await With(async db => nextItem = await db.PrescriptionItems.Where(x => x.PrescriptionID == nextRx).Select(x => x.PrescriptionItemID).SingleAsync());
    var issueHtml = await staffHttp.GetStringAsync($"/Pharmacy/Dispense/{nextRx}");
    var issueKey = Regex.Match(issueHtml, "name=\"RequestKey\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    using (var response = await Post(staffHttp, $"/Pharmacy/Dispense/{nextRx}", new() { ["RequestKey"] = issueKey, ["Lines[0].PrescriptionItemId"] = nextItem.ToString(), ["Lines[0].BatchId"] = batchId.ToString(), ["Lines[0].Quantity"] = "2" }))
        Check(response.StatusCode == HttpStatusCode.Found, "HTTP form binds dispense lines and request key");
    await With(async db => Check(await db.Dispensations.AnyAsync(x => x.PrescriptionID == nextRx && x.Status == "Completed" && x.Items.Any()), "HTTP dispense persists completed parent and children"));
    using (var response = await Post(clientHttp, "/Appointments/Book", new() { ["Input.AnimalID"] = animal.ToString(), ["Input.ProviderID"] = vet.ToString(), ["Input.DurationMinutes"] = "30", ["Input.Date"] = ClinicTime.VnToday.AddDays(1).ToString("yyyy-MM-dd"), ["Input.Slot"] = ClinicTime.VnToday.AddDays(1).AddHours(11).ToString("yyyy-MM-ddTHH:mm") }))
        Check(response.StatusCode == HttpStatusCode.Found, "client booking form works with updated DB");
    using var otherHttp = await Login("other-vet");
    using (var response = await otherHttp.GetAsync($"/Examination/Prescription/{nextRx}")) Check(response.StatusCode == HttpStatusCode.NotFound, "unassigned vet cannot read another prescription");
    if (args.Contains("--preview"))
    {
        Console.WriteLine($"PREVIEW http://127.0.0.1:5249 | test-vet / test-pharmacy | Password: {password}");
        Console.WriteLine("Press Enter to stop preview and delete isolated test database.");
        Console.ReadLine();
    }
    Console.WriteLine($"SUCCESS: {checks} integration checks passed.");
}
finally
{
    if (server is not null && !server.HasExited) { server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
    // Only this run's randomly generated database can be dropped.
    if (!Regex.IsMatch(database, "^VCMS_Test_[a-f0-9]{32}$")) throw new Exception("Unsafe cleanup target");
    SqlConnection.ClearAllPools();
    await using var conn = new SqlConnection(master); await conn.OpenAsync();
    await using var cmd = new SqlCommand($"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END", conn);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("Isolated database removed.");
}
