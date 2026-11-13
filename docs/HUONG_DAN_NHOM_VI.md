# Hướng dẫn chạy bản tích hợp VCMS

Bản này tích hợp code hiện có của Tùng, Kiệt, Nghĩa và Linh, cùng 5 bản sửa lỗi từ đợt kiểm thử. Dashboard/audit administration của Linh chưa có màn hình riêng; billing, thanh toán/PDF và nhắc lịch chưa triển khai.

## 1. Lấy code

Trước khi PR được merge:

```powershell
git fetch origin
git switch --track origin/integration/nghia-kiet-linh-20261113
```

Nếu máy đã có branch này: `git switch integration/nghia-kiet-linh-20261113`, rồi `git pull --ff-only`. Cất/commit công việc đang sửa trước khi đổi branch. Sau khi PR được merge, nhóm có thể lấy bản mới bằng `git switch main` và `git pull --ff-only`.

## 2. Database và dữ liệu demo

- Cần Visual Studio có workload **ASP.NET**, .NET 8 SDK và SQL Server local.
- Dùng schema **24 bảng** của nhóm. Nếu chưa có DB, chạy `database/VCMS_Database.sql` trong SSMS **một lần trên DB mới**. Đây là script tạo mới, không chạy lại lên DB đang có dữ liệu.
- Mở `Project_Prn222_Group3/Project_Prn222_Group3.sln`. Cấu hình SQL Server của máy mình trong `Project_Prn222_Group3/Project_Prn222_Group3/appsettings.Development.json`; không gửi mật khẩu SQL lên GitHub. Development ưu tiên file này hơn appsettings.json.
- Tại thư mục gốc repo, thêm dữ liệu demo:

```powershell
.\scripts\Add-DemoData.ps1 -Server localhost
# Nếu SQL Express: .\scripts\Add-DemoData.ps1 -Server '.\SQLEXPRESS'
```

Script sử dụng Windows Authentication, chỉ chạy với SQL local/VeterinaryClinicDB; backup trước khi ghi, giữ nguyên schema và dữ liệu cũ. Script tạo lịch theo ngày chạy, hash mật khẩu đúng định dạng ứng dụng và từ chối ghi đè demo.* đã tồn tại. SQL account chạy seed cần quyền đọc/ghi DB và BACKUP DATABASE.

Mật khẩu ngẫu nhiên và toàn bộ tài khoản được ghi tại **`.local-demo/DEMO_ACCOUNTS.local.md`** trên máy người chạy. Không có một mật khẩu chung cố định cho tất cả máy. Thư mục này được Git bỏ qua.

| Tài khoản | Vai trò / phần cần thử |
|---|---|
| `demo.admin` | Quản trị, quản lý tài khoản |
| `demo.manager`, `demo.manager2` | Quản lý; dữ liệu nền thống kê/audit |
| `demo.client`, `demo.client2`, `demo.client3` | Chủ nuôi — Tùng |
| `demo.reception` | Check-in/hàng đợi — Kiệt |
| `demo.vet`, `demo.vet2` | Khám/kê đơn, phân quyền bác sĩ — Nghĩa |
| `demo.pharmacy` | Nhập kho/cấp phát — Nghĩa |
| `demo.cashier` | Đăng nhập thu ngân; thanh toán chưa có |

Nếu máy đã có bộ demo cũ, dùng file tài khoản cũ (`DEMO_ACCOUNTS.local.md` ở gốc repo hoặc `.local-demo/DEMO_ACCOUNTS.local.md`), không chạy lại script để reset. Không gửi backup hoặc manifest chứa mật khẩu lên GitHub.

## 3. Chạy và thử luồng

Chọn project web làm Startup Project, cấu hình **Debug**, profile **http**, nhấn **F5** (mặc định `http://localhost:5248`). Đổi role bằng đăng xuất/đăng nhập; các tab chung browser profile dùng chung cookie.

1. `demo.client`: đặt lịch cho thú cưng của mình, chọn `demo.vet`, **ngày hôm nay** và giờ tương lai còn trống; ghi lại AppointmentID. Lịch mới là Pending.
2. `demo.reception`: Check-in lịch đó → hàng đợi **Gọi → Bắt đầu**. Ghi lại VisitID.
3. `demo.vet`: mở lượt khám, điền nội dung mô phỏng → **Chốt bệnh án → Mở / tạo đơn thuốc → thêm Thuốc mẫu A → chốt đơn**. Ghi lại PrescriptionID. Kê đơn chưa trừ kho.
4. `demo.pharmacy`: mở đúng đơn, nhập số lượng ở một lô; các lô khác để 0 → **Xác nhận cấp thuốc**. Có thể cấp một phần rồi cấp phần còn lại.
5. `demo.reception`: **Hoàn tất** lượt khám. Queue=Done, Visit=Completed, Appointment=Completed. Xem lịch sử/audit của bệnh án và đơn thuốc.

## 4. Breakpoint nhanh

Các file dưới đây nằm trong `Project_Prn222_Group3/Project_Prn222_Group3/`.

| Luồng | File / hàm |
|---|---|
| Đặt lịch | `Pages/Appointments/Index.cshtml.cs` → `BookModel.OnPostAsync`; `Services/BookingService.cs` → `SaveAsync` |
| Check-in, hàng đợi | `Services/ReceptionService.cs` → `CheckInAsync`, `AdvanceQueueAsync` |
| Khám / đơn thuốc | `Services/ExaminationService.cs` → `SaveEncounterAsync`, `CreatePrescriptionAsync`, `SaveItemAsync`, `FinalizePrescriptionAsync` |
| Cấp phát | `Services/PharmacyService.cs` → `DispenseAsync`; theo dõi `lines`, `snapshot`, `QuantityOnHand`, `requestKey` |
| Commit / audit | `Services/ModuleService.cs` → `ExecuteAsync`, `Audit` |

F9 đặt breakpoint, F10 chạy từng dòng, F11 vào hàm, F5 tiếp tục. Khi dừng trong transaction, hoàn tất request trước khi gửi thao tác tiếp theo. UI dùng giờ Việt Nam, DB lưu UTC.

Chi tiết thay đổi frontend/backend theo thành viên: `docs/INTEGRATION_CHANGES_2026-11-13.md`. Bản tích hợp trước khi thêm công cụ seed đã đạt 303/303 kiểm tra SQL/HTTP và kiểm tra browser desktop/mobile; báo cáo chi tiết còn local trong thư mục Tests bị ignore.

Bộ script chia sẻ đã build sạch và đạt thêm **95 kiểm tra** trên DB dùng một lần: đăng nhập đủ role, schema không đổi, sổ kho/snapshot hợp lệ, giữ khách có sẵn và không tạo trùng khi chạy lại.
