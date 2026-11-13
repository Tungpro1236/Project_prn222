/*
 VCMS - Veterinary Clinic Management System
 PRN222 | SQL Server 2022 | Initial schema v1.0
 Proposed implementation based on Report 1 and team module plan.
 Synthetic/demo data only. Review operational policies before production use.
 Run in SSMS against a SQL Server instance with CREATE DATABASE permission.
*/
IF DB_ID(N'VeterinaryClinicDB') IS NULL
    CREATE DATABASE VeterinaryClinicDB;
GO
USE VeterinaryClinicDB;
GO

/* 01 - Identity */
CREATE TABLE dbo.Roles (
 RoleID INT IDENTITY(1,1) PRIMARY KEY,
 RoleName VARCHAR(40) NOT NULL UNIQUE,
 Description NVARCHAR(200) NULL
);
GO
CREATE TABLE dbo.Users (
 UserID INT IDENTITY(1,1) PRIMARY KEY,
 RoleID INT NOT NULL,
 Username VARCHAR(80) NOT NULL UNIQUE,
 PasswordHash NVARCHAR(255) NOT NULL,
 FullName NVARCHAR(120) NOT NULL,
 Email VARCHAR(150) NULL,
 Phone VARCHAR(20) NULL,
 IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
 CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleID) REFERENCES dbo.Roles(RoleID)
);
GO
CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email) WHERE Email IS NOT NULL;
GO

/* 02 - Owner, animal, booking */
CREATE TABLE dbo.Clients (
 ClientID INT IDENTITY(1,1) PRIMARY KEY,
 UserID INT NULL UNIQUE,
 FullName NVARCHAR(120) NOT NULL,
 Phone VARCHAR(20) NOT NULL,
 Email VARCHAR(150) NULL,
 Address NVARCHAR(255) NULL,
 CreatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_Clients_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID)
);
GO
CREATE TABLE dbo.Animals (
 AnimalID INT IDENTITY(1,1) PRIMARY KEY,
 ClientID INT NOT NULL,
 Name NVARCHAR(100) NOT NULL,
 Species NVARCHAR(60) NOT NULL,
 Breed NVARCHAR(100) NULL,
 Gender VARCHAR(10) NOT NULL DEFAULT 'Unknown',
 DateOfBirth DATE NULL,
 WeightKg DECIMAL(7,2) NULL,
 Notes NVARCHAR(500) NULL,
 IsActive BIT NOT NULL DEFAULT (1),
 CONSTRAINT FK_Animals_Clients FOREIGN KEY (ClientID) REFERENCES dbo.Clients(ClientID),
 CONSTRAINT CK_Animals_Gender CHECK (Gender IN ('Male','Female','Unknown')),
 CONSTRAINT CK_Animals_Weight CHECK (WeightKg IS NULL OR WeightKg > 0)
);
GO
CREATE TABLE dbo.Appointments (
 AppointmentID INT IDENTITY(1,1) PRIMARY KEY,
 AnimalID INT NOT NULL,
 VeterinarianID INT NOT NULL,
 AppointmentTime DATETIME2(0) NOT NULL,
 ExpectedDurationMinutes INT NOT NULL DEFAULT (30),
 Reason NVARCHAR(500) NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
 CreatedByUserID INT NULL,
 CreatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CancelledAt DATETIME2(0) NULL,
 CONSTRAINT FK_Appointments_Animals FOREIGN KEY (AnimalID) REFERENCES dbo.Animals(AnimalID),
 CONSTRAINT FK_Appointments_Vet FOREIGN KEY (VeterinarianID) REFERENCES dbo.Users(UserID),
 CONSTRAINT FK_Appointments_Creator FOREIGN KEY (CreatedByUserID) REFERENCES dbo.Users(UserID),
 CONSTRAINT CK_Appointments_Status CHECK (Status IN ('Pending','Confirmed','CheckedIn','Completed','Cancelled','NoShow')),
 CONSTRAINT CK_Appointments_Duration CHECK (ExpectedDurationMinutes BETWEEN 5 AND 480)
);
GO
/* Prevent identical confirmed slots; overlapping intervals require application-side transactional validation. */
CREATE UNIQUE INDEX UX_Appointments_Vet_ConfirmedTime
 ON dbo.Appointments(VeterinarianID, AppointmentTime)
 WHERE Status IN ('Confirmed','CheckedIn');
GO

/* 03 - Reception and queue */
CREATE TABLE dbo.Visits (
 VisitID INT IDENTITY(1,1) PRIMARY KEY,
 AppointmentID INT NOT NULL UNIQUE,
 CheckInAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 ReceptionistID INT NOT NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Waiting',
 CompletedAt DATETIME2(0) NULL,
 CONSTRAINT FK_Visits_Appointment FOREIGN KEY (AppointmentID) REFERENCES dbo.Appointments(AppointmentID),
 CONSTRAINT FK_Visits_Receptionist FOREIGN KEY (ReceptionistID) REFERENCES dbo.Users(UserID),
 CONSTRAINT CK_Visits_Status CHECK (Status IN ('Waiting','InProgress','Completed','Cancelled'))
);
GO
CREATE TABLE dbo.QueueEntries (
 QueueEntryID INT IDENTITY(1,1) PRIMARY KEY,
 VisitID INT NOT NULL UNIQUE,
 QueueDate DATE NOT NULL,
 QueueNumber INT NOT NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Waiting',
 CalledAt DATETIME2(0) NULL,
 CONSTRAINT FK_QueueEntries_Visits FOREIGN KEY (VisitID) REFERENCES dbo.Visits(VisitID),
 CONSTRAINT UQ_QueueEntries_DateNumber UNIQUE (QueueDate, QueueNumber),
 CONSTRAINT CK_QueueEntries_Number CHECK (QueueNumber > 0),
 CONSTRAINT CK_QueueEntries_Status CHECK (Status IN ('Waiting','Called','Serving','Done','Skipped'))
);
GO

/* 04 - Clinical encounter and prescriptions */
CREATE TABLE dbo.Encounters (
 EncounterID INT IDENTITY(1,1) PRIMARY KEY,
 VisitID INT NOT NULL UNIQUE,
 VeterinarianID INT NOT NULL,
 Symptoms NVARCHAR(MAX) NULL,
 ExaminationNotes NVARCHAR(MAX) NULL,
 Diagnosis NVARCHAR(MAX) NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Draft',
 ExaminedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 FinalizedAt DATETIME2(0) NULL,
 CONSTRAINT FK_Encounters_Visits FOREIGN KEY (VisitID) REFERENCES dbo.Visits(VisitID),
 CONSTRAINT FK_Encounters_Vet FOREIGN KEY (VeterinarianID) REFERENCES dbo.Users(UserID),
 CONSTRAINT CK_Encounters_Status CHECK (Status IN ('Draft','Finalized','Corrected'))
);
GO
CREATE TABLE dbo.Prescriptions (
 PrescriptionID INT IDENTITY(1,1) PRIMARY KEY,
 EncounterID INT NOT NULL,
 PrescribedByUserID INT NOT NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Draft',
 Instructions NVARCHAR(1000) NULL,
 CreatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 FinalizedAt DATETIME2(0) NULL,
 CONSTRAINT FK_Prescriptions_Encounters FOREIGN KEY (EncounterID) REFERENCES dbo.Encounters(EncounterID),
 CONSTRAINT FK_Prescriptions_User FOREIGN KEY (PrescribedByUserID) REFERENCES dbo.Users(UserID),
 CONSTRAINT CK_Prescriptions_Status CHECK (Status IN ('Draft','Finalized','Cancelled'))
);
GO

/* 05 - Service catalogue, schedules, orders, results */
CREATE TABLE dbo.Services (
 ServiceID INT IDENTITY(1,1) PRIMARY KEY,
 ServiceName NVARCHAR(150) NOT NULL UNIQUE,
 Description NVARCHAR(500) NULL,
 CurrentPrice DECIMAL(18,2) NOT NULL,
 IsActive BIT NOT NULL DEFAULT (1),
 CONSTRAINT CK_Services_Price CHECK (CurrentPrice >= 0)
);
GO
CREATE TABLE dbo.VeterinarianSchedules (
 ScheduleID INT IDENTITY(1,1) PRIMARY KEY,
 VeterinarianID INT NOT NULL,
 StartAt DATETIME2(0) NOT NULL,
 EndAt DATETIME2(0) NOT NULL,
 IsAvailable BIT NOT NULL DEFAULT (1),
 Notes NVARCHAR(255) NULL,
 CONSTRAINT FK_VetSchedules_Users FOREIGN KEY (VeterinarianID) REFERENCES dbo.Users(UserID),
 CONSTRAINT CK_VetSchedules_Time CHECK (EndAt > StartAt)
);
GO
CREATE TABLE dbo.ServiceOrders (
 ServiceOrderID INT IDENTITY(1,1) PRIMARY KEY,
 EncounterID INT NOT NULL,
 ServiceID INT NOT NULL,
 OrderedByUserID INT NOT NULL,
 Quantity INT NOT NULL DEFAULT (1),
 UnitPrice DECIMAL(18,2) NOT NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Ordered',
 OrderedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CancelledAt DATETIME2(0) NULL,
 CONSTRAINT FK_ServiceOrders_Encounter FOREIGN KEY (EncounterID) REFERENCES dbo.Encounters(EncounterID),
 CONSTRAINT FK_ServiceOrders_Service FOREIGN KEY (ServiceID) REFERENCES dbo.Services(ServiceID),
 CONSTRAINT FK_ServiceOrders_User FOREIGN KEY (OrderedByUserID) REFERENCES dbo.Users(UserID),
 CONSTRAINT CK_ServiceOrders_Quantity CHECK (Quantity > 0),
 CONSTRAINT CK_ServiceOrders_Price CHECK (UnitPrice >= 0),
 CONSTRAINT CK_ServiceOrders_Status CHECK (Status IN ('Ordered','InProgress','Completed','Cancelled'))
);
GO
CREATE TABLE dbo.ServiceResults (
 ServiceResultID INT IDENTITY(1,1) PRIMARY KEY,
 ServiceOrderID INT NOT NULL UNIQUE,
 ResultText NVARCHAR(MAX) NOT NULL,
 RecordedByUserID INT NOT NULL,
 RecordedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_ServiceResults_Order FOREIGN KEY (ServiceOrderID) REFERENCES dbo.ServiceOrders(ServiceOrderID),
 CONSTRAINT FK_ServiceResults_User FOREIGN KEY (RecordedByUserID) REFERENCES dbo.Users(UserID)
);
GO

/* 06 - Pharmacy and batches */
CREATE TABLE dbo.Medicines (
 MedicineID INT IDENTITY(1,1) PRIMARY KEY,
 MedicineName NVARCHAR(150) NOT NULL,
 Unit NVARCHAR(30) NOT NULL,
 CurrentPrice DECIMAL(18,2) NOT NULL DEFAULT (0),
 IsActive BIT NOT NULL DEFAULT (1),
 CONSTRAINT CK_Medicines_Price CHECK (CurrentPrice >= 0)
);
GO
CREATE TABLE dbo.MedicineBatches (
 BatchID INT IDENTITY(1,1) PRIMARY KEY,
 MedicineID INT NOT NULL,
 BatchNumber VARCHAR(80) NOT NULL,
 ExpiryDate DATE NOT NULL,
 ReceivedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 QuantityOnHand INT NOT NULL DEFAULT (0),
 PurchaseUnitCost DECIMAL(18,2) NULL,
 CONSTRAINT FK_Batches_Medicine FOREIGN KEY (MedicineID) REFERENCES dbo.Medicines(MedicineID),
 CONSTRAINT UQ_Batches_MedicineNumber UNIQUE (MedicineID, BatchNumber),
 CONSTRAINT CK_Batches_Quantity CHECK (QuantityOnHand >= 0),
 CONSTRAINT CK_Batches_Cost CHECK (PurchaseUnitCost IS NULL OR PurchaseUnitCost >= 0)
);
GO
CREATE TABLE dbo.PrescriptionItems (
 PrescriptionItemID INT IDENTITY(1,1) PRIMARY KEY,
 PrescriptionID INT NOT NULL,
 MedicineID INT NOT NULL,
 Quantity INT NOT NULL,
 Dosage NVARCHAR(200) NOT NULL,
 Frequency NVARCHAR(200) NULL,
 DurationDays INT NULL,
 Instructions NVARCHAR(500) NULL,
 CONSTRAINT FK_PrescriptionItems_Prescription FOREIGN KEY (PrescriptionID) REFERENCES dbo.Prescriptions(PrescriptionID),
 CONSTRAINT FK_PrescriptionItems_Medicine FOREIGN KEY (MedicineID) REFERENCES dbo.Medicines(MedicineID),
 CONSTRAINT CK_PrescriptionItems_Quantity CHECK (Quantity > 0),
 CONSTRAINT CK_PrescriptionItems_Duration CHECK (DurationDays IS NULL OR DurationDays > 0)
);
GO
CREATE TABLE dbo.Dispensations (
 DispensationID INT IDENTITY(1,1) PRIMARY KEY,
 PrescriptionID INT NOT NULL,
 DispensedByUserID INT NOT NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Draft',
 DispensedAt DATETIME2(0) NULL,
 RequestKey UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
 CONSTRAINT FK_Dispensations_Prescription FOREIGN KEY (PrescriptionID) REFERENCES dbo.Prescriptions(PrescriptionID),
 CONSTRAINT FK_Dispensations_User FOREIGN KEY (DispensedByUserID) REFERENCES dbo.Users(UserID),
 CONSTRAINT UQ_Dispensations_RequestKey UNIQUE (RequestKey),
 CONSTRAINT CK_Dispensations_Status CHECK (Status IN ('Draft','Completed','Cancelled'))
);
GO
CREATE TABLE dbo.DispensationItems (
 DispensationItemID INT IDENTITY(1,1) PRIMARY KEY,
 DispensationID INT NOT NULL,
 PrescriptionItemID INT NOT NULL,
 BatchID INT NOT NULL,
 Quantity INT NOT NULL,
 UnitPrice DECIMAL(18,2) NOT NULL,
 CONSTRAINT FK_DispItems_Dispensation FOREIGN KEY (DispensationID) REFERENCES dbo.Dispensations(DispensationID),
 CONSTRAINT FK_DispItems_PrescriptionItem FOREIGN KEY (PrescriptionItemID) REFERENCES dbo.PrescriptionItems(PrescriptionItemID),
 CONSTRAINT FK_DispItems_Batch FOREIGN KEY (BatchID) REFERENCES dbo.MedicineBatches(BatchID),
 CONSTRAINT UQ_DispItems_LineBatch UNIQUE (DispensationID, PrescriptionItemID, BatchID),
 CONSTRAINT CK_DispItems_Quantity CHECK (Quantity > 0),
 CONSTRAINT CK_DispItems_Price CHECK (UnitPrice >= 0)
);
GO
CREATE TABLE dbo.StockMovements (
 StockMovementID INT IDENTITY(1,1) PRIMARY KEY,
 BatchID INT NOT NULL,
 DispensationItemID INT NULL,
 MovementType VARCHAR(20) NOT NULL,
 QuantityChange INT NOT NULL,
 Reason NVARCHAR(500) NULL,
 PerformedByUserID INT NOT NULL,
 CreatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_StockMovements_Batch FOREIGN KEY (BatchID) REFERENCES dbo.MedicineBatches(BatchID),
 CONSTRAINT FK_StockMovements_DispItem FOREIGN KEY (DispensationItemID) REFERENCES dbo.DispensationItems(DispensationItemID),
 CONSTRAINT FK_StockMovements_User FOREIGN KEY (PerformedByUserID) REFERENCES dbo.Users(UserID),
 CONSTRAINT CK_StockMovements_Type CHECK (MovementType IN ('Receipt','Dispense','Adjustment','Return')),
 CONSTRAINT CK_StockMovements_Quantity CHECK (QuantityChange <> 0),
 CONSTRAINT CK_StockMovements_Sign CHECK ((MovementType = 'Receipt' AND QuantityChange > 0) OR (MovementType = 'Dispense' AND QuantityChange < 0) OR MovementType IN ('Adjustment','Return'))
);
GO
CREATE UNIQUE INDEX UX_StockMovements_DispenseOnce ON dbo.StockMovements(DispensationItemID)
 WHERE DispensationItemID IS NOT NULL AND MovementType = 'Dispense';
GO

/* 07 - Billing */
CREATE TABLE dbo.Bills (
 BillID INT IDENTITY(1,1) PRIMARY KEY,
 EncounterID INT NOT NULL UNIQUE,
 TotalAmount DECIMAL(18,2) NOT NULL DEFAULT (0),
 Status VARCHAR(20) NOT NULL DEFAULT 'Unpaid',
 CreatedByUserID INT NOT NULL,
 CreatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_Bills_Encounter FOREIGN KEY (EncounterID) REFERENCES dbo.Encounters(EncounterID),
 CONSTRAINT FK_Bills_User FOREIGN KEY (CreatedByUserID) REFERENCES dbo.Users(UserID),
 CONSTRAINT CK_Bills_Total CHECK (TotalAmount >= 0),
 CONSTRAINT CK_Bills_Status CHECK (Status IN ('Unpaid','Partial','Paid','Cancelled'))
);
GO
CREATE TABLE dbo.BillItems (
 BillItemID INT IDENTITY(1,1) PRIMARY KEY,
 BillID INT NOT NULL,
 ServiceOrderID INT NULL,
 DispensationItemID INT NULL,
 Description NVARCHAR(255) NOT NULL,
 Quantity INT NOT NULL,
 UnitPrice DECIMAL(18,2) NOT NULL,
 LineTotal AS (CONVERT(DECIMAL(18,2), Quantity * UnitPrice)) PERSISTED,
 CONSTRAINT FK_BillItems_Bill FOREIGN KEY (BillID) REFERENCES dbo.Bills(BillID),
 CONSTRAINT FK_BillItems_ServiceOrder FOREIGN KEY (ServiceOrderID) REFERENCES dbo.ServiceOrders(ServiceOrderID),
 CONSTRAINT FK_BillItems_DispItem FOREIGN KEY (DispensationItemID) REFERENCES dbo.DispensationItems(DispensationItemID),
 CONSTRAINT CK_BillItems_Source CHECK ((CASE WHEN ServiceOrderID IS NULL THEN 0 ELSE 1 END) + (CASE WHEN DispensationItemID IS NULL THEN 0 ELSE 1 END) = 1),
 CONSTRAINT CK_BillItems_Quantity CHECK (Quantity > 0),
 CONSTRAINT CK_BillItems_Price CHECK (UnitPrice >= 0)
);
GO
CREATE UNIQUE INDEX UX_BillItems_ServiceOrder ON dbo.BillItems(ServiceOrderID) WHERE ServiceOrderID IS NOT NULL;
CREATE UNIQUE INDEX UX_BillItems_DispItem ON dbo.BillItems(DispensationItemID) WHERE DispensationItemID IS NOT NULL;
GO
CREATE TABLE dbo.Payments (
 PaymentID INT IDENTITY(1,1) PRIMARY KEY,
 BillID INT NOT NULL,
 Amount DECIMAL(18,2) NOT NULL,
 PaymentMethod VARCHAR(20) NOT NULL DEFAULT 'Cash',
 Status VARCHAR(20) NOT NULL DEFAULT 'Completed',
 CollectedByUserID INT NOT NULL,
 PaidAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 RequestKey UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
 CONSTRAINT FK_Payments_Bill FOREIGN KEY (BillID) REFERENCES dbo.Bills(BillID),
 CONSTRAINT FK_Payments_User FOREIGN KEY (CollectedByUserID) REFERENCES dbo.Users(UserID),
 CONSTRAINT UQ_Payments_RequestKey UNIQUE (RequestKey),
 CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
 CONSTRAINT CK_Payments_Method CHECK (PaymentMethod = 'Cash'),
 CONSTRAINT CK_Payments_Status CHECK (Status IN ('Completed','Voided'))
);
GO

/* 08 - Reminders, audit */
CREATE TABLE dbo.Reminders (
 ReminderID INT IDENTITY(1,1) PRIMARY KEY,
 AppointmentID INT NOT NULL,
 ReminderType VARCHAR(30) NOT NULL,
 DueAt DATETIME2(0) NOT NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
 ProcessedAt DATETIME2(0) NULL,
 Outcome NVARCHAR(500) NULL,
 CreatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_Reminders_Appointment FOREIGN KEY (AppointmentID) REFERENCES dbo.Appointments(AppointmentID),
 CONSTRAINT UQ_Reminders_AppointmentTypeDue UNIQUE (AppointmentID, ReminderType, DueAt),
 CONSTRAINT CK_Reminders_Type CHECK (ReminderType IN ('Appointment','FollowUp','Overdue')),
 CONSTRAINT CK_Reminders_Status CHECK (Status IN ('Pending','Processed','Failed','Cancelled'))
);
GO
CREATE TABLE dbo.AuditLogs (
 AuditLogID BIGINT IDENTITY(1,1) PRIMARY KEY,
 ActorUserID INT NULL,
 EntityName VARCHAR(100) NOT NULL,
 EntityID VARCHAR(80) NOT NULL,
 Action VARCHAR(40) NOT NULL,
 OldValues NVARCHAR(MAX) NULL,
 NewValues NVARCHAR(MAX) NULL,
 PreviousAuditLogID BIGINT NULL,
 OccurredAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_AuditLogs_Actor FOREIGN KEY (ActorUserID) REFERENCES dbo.Users(UserID),
 CONSTRAINT FK_AuditLogs_Previous FOREIGN KEY (PreviousAuditLogID) REFERENCES dbo.AuditLogs(AuditLogID),
 CONSTRAINT CK_AuditLogs_OldJson CHECK (OldValues IS NULL OR ISJSON(OldValues) = 1),
 CONSTRAINT CK_AuditLogs_NewJson CHECK (NewValues IS NULL OR ISJSON(NewValues) = 1)
);
GO

/* Query indexes */
CREATE INDEX IX_Animals_Client ON dbo.Animals(ClientID);
CREATE INDEX IX_Appointments_AnimalTime ON dbo.Appointments(AnimalID, AppointmentTime DESC);
CREATE INDEX IX_Appointments_VetTime ON dbo.Appointments(VeterinarianID, AppointmentTime);
CREATE INDEX IX_QueueEntries_DateStatus ON dbo.QueueEntries(QueueDate, Status, QueueNumber);
CREATE INDEX IX_Encounters_VetTime ON dbo.Encounters(VeterinarianID, ExaminedAt DESC);
CREATE INDEX IX_Prescriptions_Encounter ON dbo.Prescriptions(EncounterID);
CREATE INDEX IX_PrescriptionItems_Prescription ON dbo.PrescriptionItems(PrescriptionID);
CREATE INDEX IX_VetSchedules_VetStart ON dbo.VeterinarianSchedules(VeterinarianID, StartAt);
CREATE INDEX IX_ServiceOrders_Encounter ON dbo.ServiceOrders(EncounterID);
CREATE INDEX IX_Batches_Expiry ON dbo.MedicineBatches(ExpiryDate, MedicineID);
CREATE INDEX IX_StockMovements_BatchTime ON dbo.StockMovements(BatchID, CreatedAt DESC);
CREATE INDEX IX_Dispensations_Prescription ON dbo.Dispensations(PrescriptionID);
CREATE INDEX IX_BillItems_Bill ON dbo.BillItems(BillID);
CREATE INDEX IX_Payments_Bill ON dbo.Payments(BillID, PaidAt DESC);
CREATE INDEX IX_Reminders_DueStatus ON dbo.Reminders(Status, DueAt);
CREATE INDEX IX_AuditLogs_Entity ON dbo.AuditLogs(EntityName, EntityID, OccurredAt DESC);
GO

/* Seed: roles, services, medicine catalogue. No hardcoded demo passwords. */
INSERT INTO dbo.Roles (RoleName, Description) VALUES
 ('Admin',N'Quản trị hệ thống'),
 ('Client',N'Chủ thú cưng'),
 ('Receptionist',N'Nhân viên lễ tân'),
 ('Veterinarian',N'Bác sĩ thú y'),
 ('DispensingStaff',N'Nhân viên cấp phát thuốc'),
 ('Cashier',N'Nhân viên thu ngân'),
 ('ClinicManager',N'Quản lý phòng khám');
GO
INSERT INTO dbo.Services (ServiceName, Description, CurrentPrice) VALUES
 (N'Khám tổng quát',N'Khám lâm sàng cơ bản',150000),
 (N'Xét nghiệm máu',N'Nhập kết quả xét nghiệm thủ công',250000),
 (N'Siêu âm',N'Nhập kết quả siêu âm thủ công',300000),
 (N'Tiêm phòng',N'Dịch vụ tiêm phòng thú y',200000),
 (N'Tái khám',N'Khám lại theo lịch hẹn',100000);
GO
INSERT INTO dbo.Medicines (MedicineName, Unit, CurrentPrice) VALUES
 (N'Thuốc mẫu A',N'Viên',10000),
 (N'Thuốc mẫu B',N'Chai',65000),
 (N'Dung dịch mẫu C',N'Ống',30000);
GO

/* Developer notes:
 1) Create Users via the app with a secure password hasher (e.g., ASP.NET Core PasswordHasher).
 2) A FK cannot enforce User.RoleName = 'Veterinarian'; enforce via service layer and authorization.
 3) Booking overlap, vet schedule availability, owner authorization and late-arrival policy require service logic.
 4) Dispense: in ONE SERIALIZABLE/locked transaction validate finalized prescription, matching
    medicine, cumulative prescribed quantity, batch expiry and on-hand stock; insert item/movement,
    update QuantityOnHand, complete dispense. Do not deduct stock on prescription finalization.
 5) Bill creation: only eligible completed service orders / completed dispenses; capture immutable
    price snapshots. Recompute Bills.TotalAmount from BillItems inside transaction.
 6) Payment: within a transaction verify remaining balance, insert once using RequestKey,
    update bill status; never equate 'Paid' with 'Dispensed'.
 7) Keep AuditLogs append-only via application permissions; preserve clinical corrections.
 8) Times use UTC in defaults; convert to Vietnam local time in the UI.
 9) This is a first-release schema, not a validated production medical system.
*/
PRINT N'VCMS database schema and seed data created successfully.';
GO
