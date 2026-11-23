CREATE TABLE dbo.Destination (
    DestinationId uniqueidentifier NOT NULL PRIMARY KEY,
    Name nvarchar(120) NOT NULL,
    FareVnd bigint NOT NULL CHECK (FareVnd >= 0),
    IsActive bit NOT NULL DEFAULT 1
);
CREATE TABLE dbo.TicketOrder (
    OrderId uniqueidentifier NOT NULL PRIMARY KEY,
    DestinationId uniqueidentifier NOT NULL REFERENCES dbo.Destination(DestinationId),
    Quantity int NOT NULL CHECK (Quantity BETWEEN 1 AND 10),
    TotalVnd bigint NOT NULL CHECK (TotalVnd >= 0),
    Status varchar(32) NOT NULL CHECK (Status IN ('Pending','Paid','Declined','Cancelled','ReconciliationRequired')),
    IdempotencyKey nvarchar(120) NOT NULL UNIQUE,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE dbo.Payment (
    PaymentId uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.TicketOrder(OrderId),
    Method varchar(20) NOT NULL CHECK (Method IN ('Card','QrWallet')),
    ProviderReference nvarchar(160) NULL,
    AmountVnd bigint NOT NULL CHECK (AmountVnd >= 0),
    Status varchar(32) NOT NULL,
    ExpiresAt datetime2 NULL
);
CREATE TABLE dbo.Ticket (
    TicketId uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES dbo.TicketOrder(OrderId),
    Barcode nvarchar(160) NOT NULL UNIQUE,
    IssuedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
