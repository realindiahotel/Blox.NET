CREATE TABLE [dbo].[AddressPool]
(
	[IPAddress] VARCHAR(45) NOT NULL PRIMARY KEY, 
    [Time] INT NOT NULL, 
    [Services] BIGINT NOT NULL, 
    [Port] INT NOT NULL
)

GO

CREATE INDEX [IX_AddressPool_Time] ON [dbo].[AddressPool] ([Time] DESC)
