-- PayableFlow schema in T-SQL (SQL Server, the database Acumatica runs on).
-- The API itself uses SQLite through EF Core; this script shows the same model in SQL Server.

CREATE TABLE Vendors (
    Id                   INT IDENTITY(1,1) PRIMARY KEY,
    Name                 NVARCHAR(200) NOT NULL UNIQUE,
    PaymentTermsDays     INT           NOT NULL DEFAULT 30,
    EarlyDiscountPercent DECIMAL(5,2)  NOT NULL DEFAULT 0,
    EarlyDiscountDays    INT           NOT NULL DEFAULT 0
);

CREATE TABLE Invoices (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    VendorId      INT            NOT NULL REFERENCES Vendors(Id),
    InvoiceNumber NVARCHAR(50)   NOT NULL,
    InvoiceDate   DATE           NOT NULL,
    DueDate       DATE           NOT NULL,
    Amount        DECIMAL(18,2)  NOT NULL CHECK (Amount > 0),
    Status        TINYINT        NOT NULL DEFAULT 0,  -- 0 = Open, 1 = Paid, 2 = Void
    PaidOn        DATE           NULL,
    CONSTRAINT UQ_Invoices_Vendor_Number UNIQUE (VendorId, InvoiceNumber)
);

CREATE INDEX IX_Invoices_Status_DueDate ON Invoices (Status, DueDate);
