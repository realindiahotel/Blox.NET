CREATE TABLE [dbo].[AddressPool]
(
	[IPAddress] VARCHAR(45) NOT NULL PRIMARY KEY, 
    [Time] DECIMAL(10) NOT NULL, 
    [Services] DECIMAL(20) NOT NULL, 
    [Port] DECIMAL(5) NOT NULL
)

GO

CREATE INDEX [IX_AddressPool_Time] ON [dbo].[AddressPool] ([Time] DESC)
