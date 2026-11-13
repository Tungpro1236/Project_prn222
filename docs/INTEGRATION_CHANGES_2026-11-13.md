# Local four-member integration — 2026-11-13

Branch: `integration/nghia-kiet-linh-20261113`.
Integration base: Nghĩa `567b3d7`, already containing Kiệt `995fde1` and Linh `781a78d`.
Incoming branch: `dev_tung` at `0022e16` (also the file content of `main` at `6969d7a`).

The local merge reconciles Tung's useful changes with the newer team-schema application. All five issues from the earlier integration report are fixed. Nothing was pushed and `main` was not moved.

## Changes by member

Paths below are relative to `Project_Prn222_Group3/Project_Prn222_Group3/`.

### Tùng — clients, pets and appointments

**Frontend**

- `Pages/Client/Index.cshtml`: incorporate Tung's two portal links, **Đặt lịch** and **Xem và quản lý lịch hẹn**, shown only when a linked client profile exists.
- `Pages/Appointments/Book.cshtml`: explicitly associate the six labels with their actual custom control IDs. Existing JavaScript selectors continue to work.
- In the same booking view, render the slot field with its existing `Input.Slot` name and compare parsed DateTime values when selecting an option. The initial edit form and validation returns retain the current time. Saving only a note no longer requires choosing the time again. The existing input binder and server-side availability checks remain in use.

**Backend**

- `Services/BookingService.cs`: adapt Tung's no-tracking read and conditional cancellation UPDATE to the current team-schema service. The UPDATE rechecks Pending/Confirmed status and client ownership; zero affected rows return a conflict message. Preserve the Serializable transaction, `VCMS:Booking` application lock and active-actor authorization.
- `Pages/Customers/CustomerPages.cs`: retain the already-integrated null-safe phone normalization. Tung's equivalent fix required no additional file change.
- Keep `AnimalID`, `VeterinarianID`, `ExpectedDurationMinutes` and dated `VeterinarianSchedules`. Do not restore the old ClientID/ServiceID appointment model or weekly schedule tables.

### Kiệt — authentication, staff and reception

**Frontend**

- No Razor layout or staff/reception view was redesigned. Empty staff usernames now return the existing inline validation form instead of a server error. Revoked sessions return to login.

**Backend**

- New `Services/ActiveUserCookieEvents.cs`, registered in `Program.cs`: revalidate authenticated requests against the current user, active flag, role, username and password fingerprint. Reject/sign out invalid sessions before page authorization and handlers. No database column or migration is added.
- `Pages/Account/Login.cshtml.cs`: issue the password fingerprint claim after password verification/rehashing. Password resets invalidate existing tickets on the next request; changed roles require a fresh login. Cookies issued before this change lack the claim and require a fresh login too. The password hash itself is not placed in the ticket.
- `Pages/Staff/Create.cshtml.cs`: normalize a null/empty username safely and honor existing ModelState validation before creating a user. Empty and whitespace usernames do not create accounts.
- `Services/ReceptionService.cs`: load the linked appointment. Queue **Done** requires that appointment to be CheckedIn and saves queue Done, visit Completed/CompletedAt and appointment Completed together in one SaveChanges transaction. This uses the existing reception completion action; it does not introduce new billing or clinical completion prerequisites.

### Nghĩa — examination, pharmacy and shared schema compatibility

**Frontend**

- Preserve examination, prescription, medicine, stock and dispensing screens. No direct edits to these screens in this merge.

**Backend**

- Preserve entity types, EF mappings, explicit first-admin bootstrap, decimal binder, immutable prescription snapshots, stock/audit rules and clinical/pharmacy services.
- The shared authentication change applies before clinical/pharmacy routes too; their existing active-user service checks remain.
- The booking cancellation change is reconciled into Nghĩa's schema-compatible service rather than replacing that service with the older incoming implementation.

### Linh — dashboard and audit

**Frontend**

- No new dashboard or audit-administration screen. Linh's current branch contains only a template-homepage greeting; preserve the integrated clinic homepage instead of restoring that template.

**Backend**

- No direct edit to an implemented Linh module. Preserve shared append-only audit writes, clinical history and the DbContext audit edit/delete guard. A merged branch does not imply the missing dashboard/audit-administration features are complete.

## Shared conflict decisions

- Preserve the existing team database schema. Root `VCMS_Database.sql` matches `Context/VCMS_Database.sql` after BOM/newline normalization; both original file hashes remain unchanged. Do not introduce Tung's nested legacy 11-table creation script.
- Preserve `Data/DbSeeder.cs`, `Data/VcmsDbContext.cs`, `Models/Entities.cs`, `Models/AppRoles.cs`, integrated home/layout and team-compatible appointment handlers except for the explicit changes above. Keep obsolete `/Clinic/*` routes as unavailable notices.
- Preserve existing `appsettings.json` and `appsettings.Development.json`; no connection or live database changes. Keep the currently tested EF Core 8.0.11 project dependency rather than adopting Tung's separate 8.0.33 dependency bump. A package upgrade is not included in this merge.
- Keep `.gitignore`; do not track incoming `.vs`, `bin`, `obj`, `.user` or `.suo` artifacts. Generated files were removed from the merge index, not used as application source.
- Tests and detailed reports remain local under the existing `/Tests/VCMS.Integration/` ignore rule. This note is tracked so the team can review the changes if the integration branch is later shared.

## Verification

- Debug app build: **0 warnings, 0 errors**.
- Full SQL/HTTP integration suite: **303 passed, 0 failed** — original clinical/pharmacy suite 77; expanded full-app suite 226. A second run retained a disposable preview for browser checks and also passed 303/303.
- Existing six failed assertions now pass. Added checks cover unchanged-time note edits, validation-return slot selection, portal navigation, password-reset revocation, role-change revocation, blank/whitespace staff usernames, and revoked-cookie POSTs with preissued antiforgery tokens.
- Browser: real receptionist login, AJAX pet/slot loading, booking, date-filtered appointment confirmation, initial reschedule time selection and note-only save. All six controls expose one associated label.
- Browser SQL readback: a synthetic 08:05 Vietnam booking remained `2026-11-14 01:05:00` UTC, Confirmed, after saving only its note.
- Desktop 1280×900 and emulated mobile 390×844 inspected. Booking document width equals 390 at the mobile viewport; mobile menu opens. Public queue width is 390 and exposes no test owner/pet names. No physical phone or concurrent reception-load test was performed.
- Impeccable detector on the two changed Razor views: no findings (`[]`).
- Screenshot evidence: `C:/Users/PC/.codex/visualizations/2026/11/13/01a1d31a-35c5-7753-9dd0-8c33556556ed/vcms-reschedule-desktop.jpg` and `vcms-reschedule-mobile.jpg`.
- SQL tests use randomly named `VCMS_Test_<32 hex characters>` databases. Both test databases and preview servers were cleaned up. Live VeterinaryClinicDB remained at 24 tables and zero users, appointments, encounters and stock movements.

Local detail: `Tests/VCMS.Integration/INTEGRATION_RUN.md`, `latest-full-app-results.json`, `latest-console.log`, and `dev-tung-merge-conflicts.log`.
