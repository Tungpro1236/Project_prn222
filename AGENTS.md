# VCMS — Nghĩa: Examination & Pharmacy

## Current scope and ownership (2026-11-13)

ASP.NET Core Razor Pages, .NET 8, EF Core 8, SQL Server; Vietnamese UI; UTC persistence and Vietnam (+7) display via `ClinicTime`.
Repository: `E:\Study\Fall26\PRN222\GroupProject\Project_prn222`, branch `nghia`.

Authoritative assignment: `E:\Study\Fall26\PRN222\Context\luồng từng người.txt`.
- Nguyễn Trung Nghĩa: Examination & Pharmacy, UC-12/13/14/18/19/20.
- Tùng: clients, pets, appointments. Existing pages received only DB compatibility/security fixes.
- Kiệt: authentication, reception/check-in/queue. Preserve teammate implementation.
- Linh: dashboard and audit module. This module only appends audit records and reads its own clinical history; no audit administration/dashboard is implemented.
- Huy and Hoàng have left. Their services/schedule management, billing/payments/PDF, service orders/results and reminders are **not reassigned to Nghĩa**. Do not implement these without a new assignment.

Previous FE-01..FE-03 scope was inferred incorrectly. The previous prohibition on examination/pharmacy is obsolete. The previous handoff's uncommitted/merge-only Git descriptions and demo credentials are obsolete too.

The implementation was initially kept uncommitted for review. The user subsequently authorized committing and pushing it to `nghia`. Future commits/pushes still require a user request. Do not merge `dev_tung`; historically it had an empty tree. Never reset or overwrite others' work.

## Team database

Authoritative input: `E:\Study\Fall26\PRN222\Context\VCMS_Database.sql`.
Root `VCMS_Database.sql` is now the same team schema, replacing the earlier incompatible 11-table script.
The team SQL creates **24 tables**. It is a **fresh-database creation script**, not an idempotent upgrade script. Do not rerun it on an existing DB, drop/recreate a user's DB, or introduce migrations/schema changes without explicit authorization.

Development connection: `localhost`, `VeterinaryClinicDB`, Windows authentication, TrustServerCertificate=True. Keep teammate `appsettings.json` intact; `appsettings.Development.json` supplies the local override.

Actual schema conventions:
- `Appointments` has AnimalID/VeterinarianID and ExpectedDurationMinutes; **no ClientID or ServiceID**. Owner comes from Animal.Client.
- `Services`: ServiceName, CurrentPrice decimal(18,2), Description, IsActive. No category or duration.
- `VeterinarianSchedules`: individual UTC intervals, IsAvailable. Booking reads available intervals and subtracts unavailable intervals; 5-minute slot step. No invented clinic hours, weekly schedule or grooming support.
- No ClinicSettings, StaffSchedules or BlockedTimes tables. Obsolete `/Clinic/*` management pages show an unavailable notice and have no DB writes or navigation links.
- Clinical/pharmacy mapping uses the exact team types, lengths, nullability and table names. Unowned billing/orders/reminder models are not implemented.

Normal startup calls only the database connectivity check in `DbSeeder`. It does **not** create schemas, demo users, roles, clinical data, services or stock.
The current team DB initially had no user accounts. Explicit one-time admin provisioning, only when Users is empty:

```powershell
$env:VCMS_BOOTSTRAP_PASSWORD = '<choose a unique password of at least 12 characters>'
dotnet run --project .\Project_Prn222_Group3\Project_Prn222_Group3 -- --bootstrap-admin
Remove-Item Env:\VCMS_BOOTSTRAP_PASSWORD
```

This command creates username `admin` with ASP.NET Core PasswordHasher. It is never run implicitly. Then use existing Staff pages to create veterinarian/dispensing accounts. Do not print passwords in logs or restore old demo credentials.

## Implemented module

- `/Examination`: recent checked-in visits, assigned-vet filtering; admins can read.
- `/Examination/Edit/{visitId}`: draft examination, finalization, reasoned correction, old/new history; one encounter per visit as required by the current unique key.
- `/Examination/Prescription/{id}`: one current non-cancelled prescription per encounter, draft item add/edit/remove, finalization and cancellation before any completed dispensing.
- `/Pharmacy`: medicines and finalized prescription queue.
- `/Pharmacy/Medicine/{id?}`: medicine create/edit/deactivate.
- `/Pharmacy/Stock`: receive batches, signed adjustments with reasons, lot balances and movement history.
- `/Pharmacy/Dispense/{prescriptionId}`: remaining prescribed quantities, expiry-ordered lot choices, partial/multi-batch dispensing and completed history.

Architecture:
- `Models/Entities.cs`, `Data/VcmsDbContext.cs`: shared/team and owned module mappings.
- `Services/ModuleService.cs`: active-user authorization, transaction and SQL application lock helper, append-only audit writer.
- `Services/ExaminationService.cs`: assigned-vet clinical/prescription rules.
- `Services/PharmacyService.cs`: catalogue, stock and dispensing rules.
- `Services/PrescriptionSnapshots.cs`: immutable prescription name/unit/price/quantity snapshot reader from audit.
- `Infrastructure/InvariantDecimalModelBinder.cs`: HTML number fields use dot decimals regardless of Windows vi-VN locale. Never revert to locale-dependent decimal parsing (123.45 can otherwise become 12345).
- `Services/BookingService.cs`: team-schema compatibility for the existing booking workflow; not ownership of Tùng's feature.

## Rules to preserve

1. Only an active Veterinarian assigned to the appointment and encounter may mutate clinical records. Admin clinical access is read-only. ClinicManager/DispensingStaff/Admin may manage pharmacy; clients/receptionists cannot.
2. Finalizing an encounter requires a diagnosis. Finalized notes require a correction reason; retain old/new values in an audit entry. Do not create a second encounter for the same visit or silently rewrite history.
3. Prescription finalization requires a finalized/corrected encounter and active valid medicines. Finalized items cannot be edited. Never deduct stock while prescribing. Cancellation is blocked after any completed dispense.
4. Snapshot medicine names, units, prices and quantities in the prescription's `Finalize` audit JSON because the schema has no snapshot columns. Read those names/prices in finalized views and dispensing. Reject dispensing of legacy externally-finalized prescriptions without a valid snapshot instead of silently using the latest price.
5. Clinical and pharmacy writes use a shared `VCMS:Care` SQL application lock inside a Serializable transaction. This coordinates prescription cancellation/finalization with stock operations across app processes. Booking uses `VCMS:Booking`. All validation and parent/child/stock/audit writes share the transaction; failure rolls back.
6. Dispense only finalized prescriptions for non-cancelled visits, correct active medicine, unexpired batch (expiry date is valid through that Vietnam calendar day), adequate lot stock, and cumulative quantity no greater than prescribed. Allow partial and multi-batch issuance.
7. Dispense RequestKey replay with the identical actor/prescription/lines returns the original operation; another payload with that key is rejected. Completed parent records cannot be created without child items through this service.
8. Stock receipt/adjustment/dispensing must change QuantityOnHand and append matching StockMovements atomically; stock cannot become negative. Never expose a direct balance edit field.
9. Audit rows are append-only in this app: DbContext rejects modifications/deletions. Do not add audit edit/delete endpoints. This is not a SQL privilege restriction; direct SQL/admin writes can bypass application checks.
10. Retain UI Vietnamese, antiforgery on POST, authorization in services as well as page models, and UTC timestamps.

## Known schema limits / non-goals

The schema still requires appointments for visits and one encounter per visit; no walk-in redesign is included. Corrections use audit history rather than another encounter row. Grooming, recurring weekly schedules/clinic hours, independent non-clinical billing, service orders/results, reminders, payments and PDF receipts remain out of scope. Suppliers/purchase orders/reorder levels need schema/team agreement and are not invented here. Application rules cannot prevent an administrator or another app from writing invalid rows directly in SQL. DB-wide audit immutability and billing integrity require the owning team's work.

Clients.UserID is an unfiltered nullable UNIQUE key in the team SQL, permitting only one client without a login. The existing customer form reports this constraint rather than assuming unlimited anonymous profiles. No schema change was made.

## Build, run and verification

```powershell
dotnet build .\Project_Prn222_Group3\Project_Prn222_Group3\Project_Prn222_Group3.csproj
dotnet run --project .\Project_Prn222_Group3\Project_Prn222_Group3 --urls http://127.0.0.1:5248
dotnet run --project .\Tests\VCMS.Integration\VCMS.Integration.csproj
```

Integration tests need local SQL Server and Windows permission to create/drop a test database. They create a random `VCMS_Test_<32 hex characters>` DB from the team SQL, generate synthetic users/data, test real SQL transactions/concurrency and real cookie/antiforgery HTTP forms, then stop their server and drop only that generated DB. They never write to VeterinaryClinicDB. Port 5249 is used for their temporary server. `--preview` keeps that disposable server open until Enter is pressed; always stop the preview so its DB is cleaned up.

See `IMPLEMENTATION_REPORT.md` for final verification, assumptions and remaining integration boundaries. Run build/tests after relevant backend changes. Do not claim that a successful build alone proves DB compatibility.
