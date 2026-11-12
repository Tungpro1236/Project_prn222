/*
    VCMS schema for the features implemented so far:
    staff accounts, reception check-in/queue, clinic configuration,
    customer and pet records, and availability booking.

    Safe to run more than once. It creates VeterinaryClinicDB when missing
    and adds only the columns/tables that are not already present.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF DB_ID(N'VeterinaryClinicDB') IS NULL
BEGIN
    CREATE DATABASE VeterinaryClinicDB;
END
GO

USE VeterinaryClinicDB;
GO

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        RoleID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        RoleName nvarchar(40) NOT NULL,
        Description nvarchar(200) NULL
    );
END
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        RoleID int NOT NULL,
        Username nvarchar(80) NOT NULL,
        PasswordHash nvarchar(255) NOT NULL,
        FullName nvarchar(120) NOT NULL,
        Email nvarchar(150) NULL,
        Phone nvarchar(20) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleID) REFERENCES dbo.Roles(RoleID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Username' AND object_id = OBJECT_ID(N'dbo.Users'))
    CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users(Username);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Email' AND object_id = OBJECT_ID(N'dbo.Users'))
    CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users(Email) WHERE Email IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clients
    (
        ClientID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clients PRIMARY KEY,
        UserID int NULL,
        FullName nvarchar(120) NOT NULL,
        Phone nvarchar(20) NOT NULL,
        Email nvarchar(150) NULL,
        Address nvarchar(255) NULL,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Clients_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Clients_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Clients_UserID' AND object_id = OBJECT_ID(N'dbo.Clients'))
    CREATE UNIQUE INDEX IX_Clients_UserID ON dbo.Clients(UserID) WHERE UserID IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.Animals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Animals
    (
        AnimalID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Animals PRIMARY KEY,
        ClientID int NOT NULL,
        Name nvarchar(100) NOT NULL,
        Species nvarchar(60) NOT NULL,
        Breed nvarchar(100) NULL,
        Gender nvarchar(10) NOT NULL CONSTRAINT DF_Animals_Gender DEFAULT (N'Unknown'),
        DateOfBirth date NULL,
        WeightKg decimal(7,2) NULL,
        Notes nvarchar(500) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Animals_IsActive DEFAULT (1),
        CONSTRAINT FK_Animals_Clients FOREIGN KEY (ClientID) REFERENCES dbo.Clients(ClientID)
    );
END
GO

IF OBJECT_ID(N'dbo.Appointments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Appointments
    (
        AppointmentID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Appointments PRIMARY KEY,
        AnimalID int NOT NULL,
        ClientID int NULL,
        ServiceID int NULL,
        VeterinarianID int NOT NULL,
        AppointmentTime datetime2(0) NOT NULL,
        ExpectedDurationMinutes int NOT NULL CONSTRAINT DF_Appointments_Duration DEFAULT (30),
        Reason nvarchar(500) NULL,
        Status nvarchar(20) NOT NULL,
        CreatedByUserID int NULL,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Appointments_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CancelledAt datetime2(0) NULL,
        CONSTRAINT FK_Appointments_Animals FOREIGN KEY (AnimalID) REFERENCES dbo.Animals(AnimalID),
        CONSTRAINT FK_Appointments_Veterinarians FOREIGN KEY (VeterinarianID) REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_Appointments_CreatedBy FOREIGN KEY (CreatedByUserID) REFERENCES dbo.Users(UserID)
    );
END
GO

IF COL_LENGTH(N'dbo.Appointments', N'ClientID') IS NULL
    ALTER TABLE dbo.Appointments ADD ClientID int NULL;
GO

IF COL_LENGTH(N'dbo.Appointments', N'ServiceID') IS NULL
    ALTER TABLE dbo.Appointments ADD ServiceID int NULL;
GO

IF OBJECT_ID(N'dbo.Visits', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Visits
    (
        VisitID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Visits PRIMARY KEY,
        AppointmentID int NOT NULL,
        CheckInAt datetime2(0) NOT NULL,
        ReceptionistID int NOT NULL,
        Status nvarchar(20) NOT NULL,
        CompletedAt datetime2(0) NULL,
        CONSTRAINT FK_Visits_Appointments FOREIGN KEY (AppointmentID) REFERENCES dbo.Appointments(AppointmentID),
        CONSTRAINT FK_Visits_Receptionists FOREIGN KEY (ReceptionistID) REFERENCES dbo.Users(UserID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Visits_AppointmentID' AND object_id = OBJECT_ID(N'dbo.Visits'))
    CREATE UNIQUE INDEX IX_Visits_AppointmentID ON dbo.Visits(AppointmentID);
GO

IF OBJECT_ID(N'dbo.QueueEntries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QueueEntries
    (
        QueueEntryID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_QueueEntries PRIMARY KEY,
        VisitID int NOT NULL,
        QueueDate date NOT NULL,
        QueueNumber int NOT NULL,
        Status nvarchar(20) NOT NULL,
        CalledAt datetime2(0) NULL,
        CONSTRAINT FK_QueueEntries_Visits FOREIGN KEY (VisitID) REFERENCES dbo.Visits(VisitID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QueueEntries_VisitID' AND object_id = OBJECT_ID(N'dbo.QueueEntries'))
    CREATE UNIQUE INDEX IX_QueueEntries_VisitID ON dbo.QueueEntries(VisitID);
GO

IF OBJECT_ID(N'dbo.ClinicSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClinicSettings
    (
        ClinicSettingID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClinicSettings PRIMARY KEY,
        ClinicName nvarchar(120) NOT NULL,
        Address nvarchar(255) NULL,
        Phone nvarchar(20) NULL,
        Email nvarchar(150) NULL,
        SlotMinutes int NOT NULL CONSTRAINT DF_ClinicSettings_Slot DEFAULT (30),
        OpenHour int NOT NULL CONSTRAINT DF_ClinicSettings_Open DEFAULT (8),
        CloseHour int NOT NULL CONSTRAINT DF_ClinicSettings_Close DEFAULT (17),
        UpdatedAt datetime2(0) NOT NULL CONSTRAINT DF_ClinicSettings_Updated DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'dbo.Services', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Services
    (
        ServiceID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Services PRIMARY KEY,
        Name nvarchar(120) NOT NULL,
        Category nvarchar(30) NOT NULL,
        DurationMinutes int NOT NULL,
        Price decimal(12,0) NOT NULL,
        Description nvarchar(500) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Services_IsActive DEFAULT (1)
    );
END
GO

IF OBJECT_ID(N'dbo.StaffSchedules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StaffSchedules
    (
        StaffScheduleID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StaffSchedules PRIMARY KEY,
        UserID int NOT NULL,
        DayOfWeek tinyint NOT NULL,
        StartTime time(0) NOT NULL,
        EndTime time(0) NOT NULL,
        IsWorking bit NOT NULL CONSTRAINT DF_StaffSchedules_IsWorking DEFAULT (1),
        CONSTRAINT FK_StaffSchedules_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StaffSchedules_User_Day' AND object_id = OBJECT_ID(N'dbo.StaffSchedules'))
    CREATE UNIQUE INDEX IX_StaffSchedules_User_Day ON dbo.StaffSchedules(UserID, DayOfWeek);
GO

IF OBJECT_ID(N'dbo.BlockedTimes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BlockedTimes
    (
        BlockedTimeID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BlockedTimes PRIMARY KEY,
        UserID int NOT NULL,
        StartAt datetime2(0) NOT NULL,
        EndAt datetime2(0) NOT NULL,
        Reason nvarchar(200) NULL,
        CONSTRAINT FK_BlockedTimes_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Appointments_Clients')
    AND COL_LENGTH(N'dbo.Appointments', N'ClientID') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Appointments
        ADD CONSTRAINT FK_Appointments_Clients FOREIGN KEY (ClientID) REFERENCES dbo.Clients(ClientID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Appointments_Services')
    AND OBJECT_ID(N'dbo.Services', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.Appointments', N'ServiceID') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Appointments
        ADD CONSTRAINT FK_Appointments_Services FOREIGN KEY (ServiceID) REFERENCES dbo.Services(ServiceID);
END
GO
