# Examination & Pharmacy implementation report

Completed locally on 2026-11-13 for Nguyễn Trung Nghĩa (UC-12/13/14/18/19/20).

## Delivered behavior

The application now maps to the team's 24-table SQL schema. Appointment owner access follows Animal.Client; there are no mapped Appointment.ClientID/ServiceID properties. The service catalogue uses ServiceName/CurrentPrice. Existing booking reads dated VeterinarianSchedules and excludes unavailable intervals. It neither manages schedules nor invents clinic-hours/grooming tables. The obsolete Claude clinic-management pages display an unavailable notice, and their navigation was retired.

Examination pages let the assigned veterinarian record drafts, finalize with a diagnosis, correct a finalized/corrected record with a reason, and inspect previous values. Administrators have read-only clinical access. Corrections use append-only audit rows because the team schema permits only one encounter per visit.

Prescription pages support draft creation, item add/edit/remove, finalization after the encounter is finalized/corrected, and cancellation before any completed dispensing. One current non-cancelled prescription per encounter is enforced by the service. Finalization does not touch stock. It records name/unit/quantity/price snapshots in the existing AuditLogs JSON structure, preserving the team DB schema. Finalized displays and dispensing read these snapshots; externally-finalized prescriptions lacking a valid snapshot are rejected for reconciliation.

Pharmacy pages support medicine create/edit/deactivation; receipts into new/existing consistent batches; signed stock adjustments with mandatory reasons; batch balances and movement history; expiry-ordered batch selection; partial/multi-batch dispensing; remaining prescription quantities; and completed dispensing history. The app enforces active staff roles and checks expired/wrong batches, insufficient stock, cumulative over-dispensing and request-key replay. Completion, child items, stock deductions, ledger movements and audit entries are atomic.

All care/pharmacy mutations use an exclusive SQL application lock plus a Serializable transaction. The lock is shared across these two services so cancelling a prescription cannot race dispensing through this app. Booking has its own transactional lock. Failure rolls back all work. This favors predictable integrity over throughput in the coursework app.

UI labels and primary validation messages are Vietnamese. An invariant decimal model binder fixes the Windows vi-VN form-parsing problem where HTML `123.45` could otherwise be persisted as `12345`. Mobile navigation now has a visible menu icon.

## Scope boundaries

No implementation was added for Huy's or Hoàng's service/schedule management, billing, payments, PDF receipts, service orders/results or reminders. Existing Tùng/Kiệt functionality was retained with only required schema/security compatibility changes. Appending audit events for this module and reading its clinical history does not implement Linh's audit-administration or dashboard work.

No live DB schema or data was modified. The root SQL file was replaced with the supplied team's fresh-install script; it must not be rerun on an existing DB as an upgrade. No migrations, new tables, triggers, supplier structures, purchase orders or reorder fields were introduced.

## Verification

- App build: 0 warnings, 0 errors.
- Real SQL Server integration harness: 77 checks passed, using disposable databases named `VCMS_Test_<random GUID>` rather than VeterinaryClinicDB.
- Mapping checks against the team SQL catch missing tables, stale columns and phantom EF foreign keys.
- Tests exercise assigned/other/disabled actors, clinical drafts/finalization/correction, prescription creation/items/finalization, no stock deduction while prescribing, expired receipts, adjustments, mismatched/expired batch rejection, over-dispensing, partial and multi-batch dispensing, duplicate request keys, concurrent dispensing, snapshot prices, stock-ledger reconciliation and audit edit rejection.
- HTTP tests use real cookies, antiforgery tokens and form posts for clinical finalization, prescription creation/items/finalization, medicine creation, stock receipt, batch dispensing and client booking. They verify the exact fractional price in storage and in the edit form, not just HTTP status codes.
- Explicit first-admin provisioning was tested against an empty disposable DB, including secure password hashing and role assignment.
- Desktop and 390px mobile browser review covered examination, finalized prescription, pharmacy and dispensing, plus the stock forms. The menu icon and price-display issues found during review were corrected. The UI detector reported no findings.
- Final live DB read-only check: 24 tables, 40 foreign keys, 37 checks; Users remained empty. No disposable test DBs or preview servers were left running.
- Git HEAD remained `8e329d1`; work is uncommitted and was not pushed.

Run the suite from the repository root:

```powershell
dotnet run --project .\Tests\VCMS.Integration\VCMS.Integration.csproj
```

It requires a local SQL Server and Windows permission to create/drop the isolated test DB. The harness stops its server and removes only its own randomly named database in cleanup.

## First use on your team database

Normal startup no longer seeds demo users, passwords, appointments, services or stock. The current DB has no user accounts. Use the explicit one-time bootstrap command in AGENTS.md to create `admin`, then use existing staff management to provision veterinarian and dispensing staff accounts. Reception must check in a valid appointment before it appears in examination.

## Limits retained from the team schema

- Visits still require an appointment: no walk-in redesign.
- One encounter per visit: corrections preserve history in AuditLogs, rather than create another encounter row.
- Grooming and standalone non-clinical billing remain unresolved team work.
- Supplier, purchase-order and reorder-level structures are absent; no substitute schema was invented.
- Audit immutability, one finalized prescription, stock/ledger consistency and valid completed dispensations are protected through this app's services; a privileged SQL client or another application can bypass them. The app does not claim database-wide enforcement.
- Bills, payments and service orders remain outside this module. Their integrity gaps are not repaired here.
- The team Clients.UserID unique constraint allows only one unlinked client. The existing form reports that limitation without altering the DB.
- Prescription snapshots use the audit payload as a schema-compatible bridge. A future team migration can introduce dedicated snapshot columns, with explicit migration of existing finalized prescriptions.
- New finalized prescriptions are protected; externally-created historical invalid rows require reconciliation rather than automatic repair.

## Main files

- `AGENTS.md`: corrected scope, DB setup, architecture, rules and test workflow.
- `Models/Entities.cs`, `Data/VcmsDbContext.cs`: team-schema mappings and audit edit guard.
- `Services/ExaminationService.cs`, `Services/PharmacyService.cs`, `Services/ModuleService.cs`: transactional rules.
- `Services/PrescriptionSnapshots.cs`: finalized snapshot retrieval.
- `Pages/Examination/`, `Pages/Pharmacy/`: usable Vietnamese screens.
- `Infrastructure/InvariantDecimalModelBinder.cs`: decimal form integrity.
- `Tests/VCMS.Integration/`: reproducible isolated integration harness.
