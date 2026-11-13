using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;

// Explicit opt-in local fixtures only. No startup registration or schema DDL.
string? Option(string key)
{
    var index=Array.IndexOf(args,key);
    if(index<0)return null;
    if(index+1>=args.Length || args[index+1].StartsWith("--"))throw new ArgumentException("Missing value: "+key);
    return args[index+1];
}
var repo=Path.GetFullPath(Option("--repo-root") ?? Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var local=Path.GetFullPath(Option("--output-dir") ?? Path.Combine(repo,".local-demo"));
var connection=Option("--connection") ?? "Server=localhost;Database=VeterinaryClinicDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
var connectionInfo=new SqlConnectionStringBuilder(connection);
var host=connectionInfo.DataSource.Replace("tcp:","",StringComparison.OrdinalIgnoreCase).Split('\\',',')[0];
var localHost=new[]{"localhost","127.0.0.1",".","(local)","(localdb)",Environment.MachineName}.Contains(host,StringComparer.OrdinalIgnoreCase);
var isolatedTest=Regex.IsMatch(connectionInfo.InitialCatalog,"^VCMS_Test_[a-f0-9]{32}$");
if(!localHost || (connectionInfo.InitialCatalog!="VeterinaryClinicDB" && !isolatedTest))
    throw new InvalidOperationException("Seeder chỉ dành cho SQL Server local / VeterinaryClinicDB hoặc DB kiểm thử VCMS_Test_<32 hex>.");
if(args.Contains("--skip-backup") && !isolatedTest)
    throw new InvalidOperationException("Chỉ được bỏ backup cho database kiểm thử dùng một lần.");
Directory.CreateDirectory(local);
var options=new DbContextOptionsBuilder<VcmsDbContext>().UseSqlServer(connection).Options;
var json=new JsonSerializerOptions{WriteIndented=true,Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
var manifestPath=Path.Combine(local,"demo-manifest.json");
if(File.Exists(manifestPath))
    throw new InvalidOperationException("Đã có manifest demo tại "+local+". Không chạy lại/reset dữ liệu đang debug; dùng tài khoản đã tạo.");
await using var sql = new SqlConnection(connection);
await sql.OpenAsync();
async Task<string> Scalar(string query)
{
    await using var cmd = new SqlCommand(query, sql);
    await using var reader = await cmd.ExecuteReaderAsync();
    var value = new StringBuilder();
    while (await reader.ReadAsync()) value.Append(Convert.ToString(reader.GetValue(0)));
    return value.ToString();
}
async Task<string> Schema() => await Scalar("""
SELECT
 (SELECT o.name,o.type,OBJECT_DEFINITION(o.object_id) AS definition FROM sys.objects o WHERE o.is_ms_shipped=0 AND o.type IN ('U','C','D','F','PK','UQ','TR','V','P','FN') ORDER BY o.type,o.name FOR JSON PATH) AS objects,
 (SELECT OBJECT_NAME(c.object_id) AS tbl,c.name,c.column_id,c.system_type_id,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.collation_name FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id ORDER BY t.name,c.column_id FOR JSON PATH) AS columns,
 (SELECT t.name AS tbl,i.name,i.is_unique,i.type,i.filter_definition,ic.key_ordinal,ic.is_included_column,c.name AS col FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id ORDER BY t.name,i.index_id,ic.index_column_id FOR JSON PATH) AS indexes,
 (SELECT f.name,OBJECT_NAME(f.parent_object_id) AS tbl,pc.name AS col,OBJECT_NAME(f.referenced_object_id) AS target,rc.name AS targetCol,f.delete_referential_action,f.update_referential_action FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id ORDER BY f.name,fc.constraint_column_id FOR JSON PATH) AS foreignKeys
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER
""");
var schemaBefore = await Schema();
await File.WriteAllTextAsync(Path.Combine(local, "schema-before.json"), schemaBefore);
if(await Scalar("SELECT COUNT(*) FROM sys.tables")!="24")
    throw new InvalidOperationException("Cần schema nhóm 24 bảng. Xem database/VCMS_Database.sql; không chạy script tạo bảng lên DB có sẵn.");
var backupPath="(DB kiểm thử dùng một lần; bỏ backup theo --skip-backup)";
if(!args.Contains("--skip-backup"))
{
    var backupDir=await Scalar("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))");
    if(string.IsNullOrWhiteSpace(backupDir))throw new InvalidOperationException("Không xác định được thư mục backup mặc định của SQL Server.");
    backupPath=Path.Combine(backupDir,connectionInfo.InitialCatalog+"_before_demo_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")[..8]+".bak");
    var databaseIdentifier="["+connectionInfo.InitialCatalog.Replace("]","]]")+"]";
    await using var backup=new SqlCommand("BACKUP DATABASE "+databaseIdentifier+" TO DISK=@path WITH COPY_ONLY,CHECKSUM",sql);
    backup.Parameters.AddWithValue("@path",backupPath);backup.CommandTimeout=120;await backup.ExecuteNonQueryAsync();
}
await File.WriteAllTextAsync(Path.Combine(local,"pre-seed-backup.txt"),backupPath);
await using var db = new VcmsDbContext(options);
var roles = await db.Roles.ToDictionaryAsync(x => x.RoleName);
var accountSpecs = new[] {
    ("demo.admin", AppRoles.Admin, "[DEMO] Quản trị"),
    ("demo.manager", AppRoles.ClinicManager, "[DEMO] Quản lý - Linh"),
    ("demo.manager2", AppRoles.ClinicManager, "[DEMO] Quản lý dự phòng"),
    ("demo.reception", AppRoles.Receptionist, "[DEMO] Lễ tân - Kiệt"),
    ("demo.vet", AppRoles.Veterinarian, "[DEMO] Bác sĩ - Nghĩa"),
    ("demo.vet2", AppRoles.Veterinarian, "[DEMO] Bác sĩ thứ hai"),
    ("demo.pharmacy", AppRoles.DispensingStaff, "[DEMO] Cấp phát - Nghĩa"),
    ("demo.cashier", AppRoles.Cashier, "[DEMO] Thu ngân"),
    ("demo.client", AppRoles.Client, "[DEMO] Chủ nuôi An - Tùng"),
    ("demo.client2", AppRoles.Client, "[DEMO] Chủ nuôi Bình"),
    ("demo.client3", AppRoles.Client, "[DEMO] Chủ nuôi Chi")
};
// The team's nullable UNIQUE Clients.UserID permits only one anonymous client.
// If that slot is already occupied, link the extra demo owner to a new demo login.
var anonymousSlotUsed = await db.Clients.AnyAsync(x => x.UserID == null);
if (anonymousSlotUsed)
    accountSpecs = accountSpecs.Append(("demo.guest", AppRoles.Client, "[DEMO] Chủ nuôi bổ sung")).ToArray();
var today = ClinicTime.VnToday;
var tag = "[DEMO " + today.ToString("yyyy-MM-dd") + "]";
var password = "VcmsDemo!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
var oldMedicines = await db.Medicines.AsNoTracking().OrderBy(x => x.MedicineID).ToListAsync();
if (oldMedicines.Count(x => x.IsActive) < 3) throw new Exception("Expected the existing three active sample medicines.");
var originalMedicineJson = JsonSerializer.Serialize(oldMedicines, json);
await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='VCMS:LocalDemoSeed',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; IF @r<0 THROW 51000,'Demo seed busy',1;");
if (await db.Users.AnyAsync(x => x.Username.StartsWith("demo."))) throw new Exception("Existing demo.* accounts found; no accounts were overwritten.");
var users = accountSpecs.ToDictionary(x => x.Item1, x => {
    var user = new User { Username=x.Item1, RoleID=roles[x.Item2].RoleID, FullName=x.Item3, IsActive=true };
    user.PasswordHash = new PasswordHasher<User>().HashPassword(user,password); return user;
});
db.Users.AddRange(users.Values); await db.SaveChangesAsync();
int U(string name) => users[name].UserID;
void Audit(string entity, int id, string action, object after, int? actor=null, object? before=null) => db.AuditLogs.Add(new AuditLog {
    ActorUserID=actor??U("demo.admin"), EntityName=entity, EntityID=id.ToString(),Action=action,
    OldValues=before is null?null:JsonSerializer.Serialize(before,json),NewValues=JsonSerializer.Serialize(after,json),OccurredAt=ClinicTime.UtcNow
});
foreach (var user in users.Values) Audit("User",user.UserID,"DemoSeed",new{user.Username,user.RoleID,Note="Synthetic local debugging account; password omitted"});
var owners = new List<Client>();
for (var i=0;i<3;i++) owners.Add(new Client { UserID=U(i==0?"demo.client":"demo.client"+(i+1)),FullName=accountSpecs[8+i].Item3,Phone="090000000"+i,Address="Địa chỉ mô phỏng - không phải dữ liệu thật" });
owners.Add(new Client { UserID=anonymousSlotUsed?U("demo.guest"):null,FullName="[DEMO] Chủ nuôi bổ sung",Phone="0900000009",Address="Hồ sơ khách vãng lai mô phỏng" });
db.Clients.AddRange(owners); await db.SaveChangesAsync();
var pets = new[] {
    new Animal { ClientID=owners[0].ClientID,Name="[DEMO] Bông",Species="Mèo",Breed="Mèo ta",Gender="Female",WeightKg=3.25m },
    new Animal { ClientID=owners[0].ClientID,Name="[DEMO] Mực",Species="Chó",Breed="Chó ta",Gender="Male",WeightKg=8.5m },
    new Animal { ClientID=owners[1].ClientID,Name="[DEMO] Milo",Species="Chó",Gender="Male",WeightKg=6.2m },
    new Animal { ClientID=owners[2].ClientID,Name="[DEMO] Luna",Species="Mèo",Gender="Female",WeightKg=2.75m },
    new Animal { ClientID=owners[3].ClientID,Name="[DEMO] Nâu",Species="Chó",Gender="Unknown",WeightKg=5m },
    new Animal { ClientID=owners[1].ClientID,Name="[DEMO] Hồ sơ ngừng hoạt động",Species="Mèo",Gender="Unknown",WeightKg=4m,IsActive=false }
};
foreach(var pet in pets) { pet.DateOfBirth=today.AddYears(-2);pet.Notes="Dữ liệu mô phỏng để debug; không phải hồ sơ thật."; }
db.Animals.AddRange(pets);
for(var day=0;day<8;day++) foreach(var vetName in new[]{"demo.vet","demo.vet2"}) foreach(var hours in new[]{(8,12),(13,17)})
    db.VeterinarianSchedules.Add(new VeterinarianSchedule { VeterinarianID=U(vetName),StartAt=ClinicTime.VnLocalToUtc(today.AddDays(day).AddHours(hours.Item1)),EndAt=ClinicTime.VnLocalToUtc(today.AddDays(day).AddHours(hours.Item2)),IsAvailable=true,Notes=tag+" Ca demo, giờ Việt Nam" });
db.VeterinarianSchedules.Add(new VeterinarianSchedule{VeterinarianID=U("demo.vet"),StartAt=ClinicTime.VnLocalToUtc(today.AddDays(1).AddHours(10)),EndAt=ClinicTime.VnLocalToUtc(today.AddDays(1).AddHours(10.5)),IsAvailable=false,Notes=tag+" Khung giờ bị chặn demo"});
await db.SaveChangesAsync();
var appointments = new List<Appointment>();
Appointment Appt(int pet,int day,int minutes,string status,string scenario,bool otherVet=false) {
    var a=new Appointment{AnimalID=pets[pet].AnimalID,VeterinarianID=U(otherVet?"demo.vet2":"demo.vet"),AppointmentTime=ClinicTime.VnLocalToUtc(today.AddDays(day).AddMinutes(minutes)),ExpectedDurationMinutes=30,Status=status,Reason=tag+" "+scenario,CreatedByUserID=U("demo.reception"),CancelledAt=status=="Cancelled"?ClinicTime.UtcNow:null};
    db.Appointments.Add(a);appointments.Add(a);return a;
}
Appt(0,0,480,"Confirmed","01 - Chưa check-in, lễ tân có thể tiếp nhận");
var waiting=Appt(1,0,525,"CheckedIn","02 - Chờ gọi số, bác sĩ có thể mở bệnh án");
var draft=Appt(2,0,570,"CheckedIn","03 - Đang khám, bệnh án và đơn nháp");
var ready=Appt(0,0,615,"CheckedIn","04 - Đơn đã chốt, chưa cấp thuốc");
var partial=Appt(3,0,660,"CheckedIn","05 - Đơn đã cấp một phần");
var completed=Appt(2,0,780,"Completed","06 - Đã hoàn tất, xem lịch sử và audit");
var other=Appt(3,0,840,"CheckedIn","07 - Ca của bác sĩ thứ hai",true);
Appt(4,0,900,"Cancelled","08 - Lịch đã hủy");
Appt(1,0,945,"NoShow","09 - Khách không đến");
Appt(0,1,540,"Pending","10 - Lịch ngày mai, client có thể đổi hoặc hủy");
Appt(2,1,570,"Confirmed","11 - Lịch xác nhận ngày mai");
Appt(3,1,840,"Confirmed","12 - Lịch ngày mai của bác sĩ thứ hai",true);
await db.SaveChangesAsync();
var nextQueue=(await db.QueueEntries.Where(x=>x.QueueDate==today).MaxAsync(x=>(int?)x.QueueNumber)??0)+1;
var visits=new List<Visit>();
Visit VisitFor(Appointment a,string state,string queueState) {
    var checkIn=ClinicTime.UtcNow.AddMinutes(-90+visits.Count*5);
    var v=new Visit{AppointmentID=a.AppointmentID,ReceptionistID=U("demo.reception"),Status=state,CheckInAt=checkIn,CompletedAt=state=="Completed"?checkIn.AddMinutes(60):null};
    db.Visits.Add(v);db.QueueEntries.Add(new QueueEntry{Visit=v,QueueDate=today,QueueNumber=nextQueue++,Status=queueState,CalledAt=queueState=="Waiting"?null:checkIn.AddMinutes(5)});visits.Add(v);return v;
}
VisitFor(waiting,"Waiting","Waiting");
var draftVisit=VisitFor(draft,"InProgress","Serving");
var readyVisit=VisitFor(ready,"InProgress","Serving");
var partialVisit=VisitFor(partial,"InProgress","Serving");
var completedVisit=VisitFor(completed,"Completed","Done");
VisitFor(other,"Waiting","Waiting");
await db.SaveChangesAsync();
foreach(var a in appointments) Audit("Appointment",a.AppointmentID,"DemoSeed",new{a.Status,a.Reason,a.AppointmentTime},U("demo.reception"));
var inactive=new Medicine{MedicineName="[DEMO] Thuốc mô phỏng ngừng dùng",Unit="Viên",CurrentPrice=12000m,IsActive=false};
var lowStock=new Medicine{MedicineName="[DEMO] Thuốc mô phỏng tồn thấp",Unit="Hộp",CurrentPrice=25000m,IsActive=true};
db.Medicines.AddRange(inactive,lowStock);await db.SaveChangesAsync();
var medicines=oldMedicines.Where(x=>x.IsActive).Take(3).Append(lowStock).ToArray();
var batches=new Dictionary<int,MedicineBatch>();
foreach(var med in medicines) foreach(var seq in med.MedicineID==lowStock.MedicineID?new[]{1}:new[]{1,2}) {
    var quantity=med.MedicineID==lowStock.MedicineID?2:seq==1?100:50;
    var b=new MedicineBatch{MedicineID=med.MedicineID,BatchNumber="DEMO-"+today.ToString("yyyyMMdd")+"-"+med.MedicineID+"-"+seq,ExpiryDate=today.AddMonths(seq==1?3:12),ReceivedAt=ClinicTime.VnLocalToUtc(today.AddDays(-7).AddHours(10)),PurchaseUnitCost=decimal.Round(med.CurrentPrice*.6m,2),QuantityOnHand=quantity};
    db.MedicineBatches.Add(b);await db.SaveChangesAsync();if(seq==1)batches[med.MedicineID]=b;
    db.StockMovements.Add(new StockMovement{BatchID=b.BatchID,MovementType="Receipt",QuantityChange=quantity,PerformedByUserID=U("demo.pharmacy"),Reason=tag+" Nhập kho khởi tạo dữ liệu demo",CreatedAt=b.ReceivedAt});
    Audit("MedicineBatch",b.BatchID,"Receive",new{b.QuantityOnHand,quantity,Reason="Nhập dữ liệu demo"},U("demo.pharmacy"),new{QuantityOnHand=0});
}
var prescriptions=new List<Prescription>();
async Task<Prescription> Clinical(Visit v,bool final,bool corrected=false) {
    var e=new Encounter{VisitID=v.VisitID,VeterinarianID=U("demo.vet"),Symptoms="[DEMO] Triệu chứng mô phỏng",ExaminationNotes="Chỉ để kiểm tra phần mềm, không phải tư vấn y khoa.",Diagnosis=final?"[DEMO] Chẩn đoán mô phỏng":null,Status=corrected?"Corrected":final?"Finalized":"Draft",ExaminedAt=v.CheckInAt.AddMinutes(10),FinalizedAt=final?v.CheckInAt.AddMinutes(15):null};
    db.Encounters.Add(e);await db.SaveChangesAsync();
    Audit("Encounter",e.EncounterID,final?"Finalize":"SaveDraft",new{e.Symptoms,e.Diagnosis,Status=final?"Finalized":"Draft"},U("demo.vet"));
    if(corrected)Audit("Encounter",e.EncounterID,"Correct",new{e.Diagnosis,e.Status,CorrectionReason="[DEMO] Minh họa lưu lịch sử hiệu chỉnh"},U("demo.vet"),new{Diagnosis="[DEMO] Chẩn đoán trước hiệu chỉnh",Status="Finalized"});
    var rx=new Prescription{EncounterID=e.EncounterID,PrescribedByUserID=U("demo.vet"),Status=final?"Finalized":"Draft",Instructions="DỮ LIỆU MÔ PHỎNG - KHÔNG DÙNG ĐIỀU TRỊ",CreatedAt=e.ExaminedAt,FinalizedAt=final?e.FinalizedAt:null};
    db.Prescriptions.Add(rx);await db.SaveChangesAsync();prescriptions.Add(rx);
    foreach(var med in medicines.Take(2))db.PrescriptionItems.Add(new PrescriptionItem{PrescriptionID=rx.PrescriptionID,MedicineID=med.MedicineID,Quantity=6,Dosage="DEMO - không dùng điều trị",Frequency="Giá trị mô phỏng",DurationDays=3,Instructions="Chỉ thử giao diện và luồng cấp phát"});
    await db.SaveChangesAsync();
    Audit("Prescription",rx.PrescriptionID,"Create",new{e.EncounterID},U("demo.vet"));
    if(final) {
        var lines=await db.PrescriptionItems.Include(x=>x.Medicine).Where(x=>x.PrescriptionID==rx.PrescriptionID).ToListAsync();
        Audit("Prescription",rx.PrescriptionID,"Finalize",new PrescriptionSnapshot(rx.PrescriptionID,rx.Instructions,lines.Select(x=>new MedicineSnapshot(x.PrescriptionItemID,x.MedicineID,x.Medicine.MedicineName,x.Medicine.Unit,x.Medicine.CurrentPrice,x.Quantity)).ToArray()),U("demo.vet"));
    }
    return rx;
}
await Clinical(draftVisit,false);
await Clinical(readyVisit,true);
var partialRx=await Clinical(partialVisit,true);
var completedRx=await Clinical(completedVisit,true,true);
async Task Issue(Prescription rx,int quantity) {
    var d=new Dispensation{PrescriptionID=rx.PrescriptionID,DispensedByUserID=U("demo.pharmacy"),Status="Completed",RequestKey=Guid.NewGuid(),DispensedAt=rx.FinalizedAt!.Value.AddMinutes(5)};
    var lines=await db.PrescriptionItems.Include(x=>x.Medicine).Where(x=>x.PrescriptionID==rx.PrescriptionID).ToListAsync();
    foreach(var line in lines){var b=batches[line.MedicineID];b.QuantityOnHand-=quantity;var item=new DispensationItem{PrescriptionItemID=line.PrescriptionItemID,BatchID=b.BatchID,Quantity=quantity,UnitPrice=line.Medicine.CurrentPrice};d.Items.Add(item);db.StockMovements.Add(new StockMovement{BatchID=b.BatchID,DispensationItem=item,MovementType="Dispense",QuantityChange=-quantity,PerformedByUserID=U("demo.pharmacy"),Reason=tag+" Cấp phát demo",CreatedAt=ClinicTime.UtcNow});}
    db.Dispensations.Add(d);await db.SaveChangesAsync();Audit("Dispensation",d.DispensationID,"Complete",new{rx.PrescriptionID,d.RequestKey,Note="Synthetic demo dispensing"},U("demo.pharmacy"));
}
await Issue(partialRx,2);await Issue(completedRx,6);await db.SaveChangesAsync();
foreach(var b in await db.MedicineBatches.AsNoTracking().ToListAsync())
    if(b.QuantityOnHand<0 || b.QuantityOnHand!=await db.StockMovements.Where(x=>x.BatchID==b.BatchID).SumAsync(x=>x.QuantityChange))throw new Exception("Stock invariant failed; rollback.");
foreach(var rx in prescriptions.Where(x=>x.Status=="Finalized"))
    if(await PrescriptionSnapshots.ReadAsync(db,rx.PrescriptionID) is null)throw new Exception("Missing snapshot; rollback.");
if(JsonSerializer.Serialize(await db.Medicines.AsNoTracking().Where(x=>oldMedicines.Select(m=>m.MedicineID).Contains(x.MedicineID)).OrderBy(x=>x.MedicineID).ToListAsync(),json)!=originalMedicineJson)throw new Exception("Existing catalogue changed; rollback.");
var schemaAfter=await Schema();if(schemaBefore!=schemaAfter)throw new Exception("Schema changed; rollback.");
await tx.CommitAsync();
await File.WriteAllTextAsync(Path.Combine(local,"schema-after.json"),schemaAfter);
var manifestResult=new Manifest(password,accountSpecs.Select(x=>new Account(x.Item1,x.Item2,x.Item3,U(x.Item1))).ToArray(),prescriptions.Select(x=>x.PrescriptionID).ToArray(),appointments.Select(x=>x.AppointmentID).ToArray(),backupPath,today);
await File.WriteAllTextAsync(manifestPath,JsonSerializer.Serialize(manifestResult,json));
var note=new StringBuilder("# Tài khoản và dữ liệu demo local VCMS\n\n");
note.AppendLine($"Tạo ngày {today:dd/MM/yyyy}, DB `{connectionInfo.DataSource} / {connectionInfo.InitialCatalog}`, branch `integration/nghia-kiet-linh-20261113`. Chỉ dữ liệu mô phỏng để debug; giữ nguyên schema.\n");
note.AppendLine($"**Mật khẩu chung cho các tài khoản dưới đây:** `{password}`\n");
note.AppendLine("| Username | Role | Dùng để kiểm tra |\n|---|---|---|");
foreach(var spec in accountSpecs)note.AppendLine($"| `{spec.Item1}` | {spec.Item2} | {spec.Item3.Replace("[DEMO] ","")} |");
note.AppendLine("\nTất cả tài khoản active; mật khẩu lưu trong DB bằng ASP.NET Core PasswordHasher. Tài khoản manager2 giữ được ít nhất một quản lý hoạt động khi thử khóa tài khoản. vet2/client2/client3 hỗ trợ kiểm tra phân quyền theo bác sĩ/chủ nuôi. Không có role PetGroomer trong schema nhóm; không thêm role đó. Thu ngân đăng nhập được nhưng chức năng thanh toán chưa được triển khai.\n");
note.AppendLine("## Luồng demo theo thành viên\n\n- **Tùng:** 4 chủ nuôi, 6 thú cưng (1 hồ sơ inactive), lịch hẹn hôm nay/ngày mai gồm Pending/Confirmed/CheckedIn/Completed/Cancelled/NoShow; ca bác sĩ 8 ngày và khung giờ chặn 10:00–10:30 ngày mai. Đăng nhập demo.client để đặt/đổi/hủy lịch của mình.\n- **Kiệt:** demo.reception xem lịch hôm nay, tiếp nhận ca 01; hàng đợi có ca Waiting/Serving/Done. demo.admin/demo.manager quản lý tài khoản.\n- **Nghĩa:** demo.vet xem ca chờ mở bệnh án, ca có bệnh án/đơn nháp, đơn đã chốt; demo.pharmacy xem đơn chưa cấp, cấp một phần (2/6 mỗi dòng) và cấp đủ, lô/tồn kho/sổ kho. Có thuốc ngừng dùng và thuốc tồn thấp. Các nội dung thuốc/chẩn đoán chỉ là giá trị mô phỏng, không dùng điều trị.\n- **Linh:** có AuditLogs cho khởi tạo, chốt, hiệu chỉnh, nhập/xuất; dữ liệu lượt khám phục vụ thống kê. Code hiện chưa có màn hình dashboard/audit administration, nên đăng nhập manager không tạo ra những màn hình chưa triển khai. Có thể xem dữ liệu nền trong SSMS.\n");
note.AppendLine("## Điểm truy cập cụ thể\n");
foreach(var v in visits)note.AppendLine($"- `/Examination/Edit/{v.VisitID}` — lịch hẹn #{v.AppointmentID}.");
foreach(var rx in prescriptions)note.AppendLine($"- `/Examination/Prescription/{rx.PrescriptionID}`; `/Pharmacy/Dispense/{rx.PrescriptionID}` — đơn {rx.Status}; chỉ đơn Finalized mới được cấp phát.");
note.AppendLine("\n## Debug Visual Studio\n\nChạy project web đang mở bằng F5; dùng URL của profile http/https/IIS Express. Development ưu tiên appsettings.Development.json (localhost/VeterinaryClinicDB). Không cần chạy bootstrap-admin hoặc script tạo DB. Nếu trình duyệt đang giữ phiên cũ, đăng xuất rồi đăng nhập bằng tài khoản demo. Ngày hẹn/hàng đợi là ngày tạo bộ demo; khi xem ngày khác, chọn lại bộ lọc ngày.\n");
note.AppendLine($"Backup trước seed (COPY_ONLY/CHECKSUM): `{backupPath}`. Không tự restore đè DB đang debug. Dữ liệu cũ được giữ; seed chỉ INSERT dữ liệu và cập nhật tồn các lô demo vừa tạo trong một transaction. Không chạy CREATE/ALTER/DROP schema.\n");
note.AppendLine("Seeder và manifest: `.local-demo/`. File này và thư mục đó được loại khỏi Git bằng `.git/info/exclude`. Không push, không merge main, không sửa appsettings.json của bạn. Seeder từ chối chạy lại nếu đã có manifest/demo.* để tránh ghi đè dữ liệu bạn debug.\n");
await File.WriteAllTextAsync(Path.Combine(local,"DEMO_ACCOUNTS.local.md"),note.ToString());
Console.WriteLine($"Seed committed atomically: {users.Count} accounts, {owners.Count} clients, {pets.Length} pets, {appointments.Count} appointments, {visits.Count} visits, {prescriptions.Count} prescriptions.");
Console.WriteLine("Schema unchanged; original catalogue preserved; password written only to local account file/manifest.");
Console.WriteLine("Credentials and walkthrough: " + Path.Combine(local,"DEMO_ACCOUNTS.local.md"));

record Account(string Username,string Role,string FullName,int UserId);
record Manifest(string Password,Account[] Accounts,int[] PrescriptionIds,int[] AppointmentIds,string BackupPath,DateTime VietnamDate);
