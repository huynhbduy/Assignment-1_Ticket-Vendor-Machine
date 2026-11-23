-- Run after schema.sql in the database selected by your connection string.
INSERT dbo.Destination(DestinationId,Name,FareVnd,IsActive) VALUES
('11111111-1111-1111-1111-111111111111',N'District 1 Central Station',7000,1),
('22222222-2222-2222-2222-222222222222',N'Tan Son Nhat Airport',15000,1),
('33333333-3333-3333-3333-333333333333',N'University Campus',9000,1);
