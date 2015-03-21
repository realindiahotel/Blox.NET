CREATE TABLE [dbo].[TXs]
(
	[TXHashID] BINARY(32) NOT NULL , 
    [Version] DECIMAL(10) NOT NULL, 
    [TXInCount] DECIMAL(20) NOT NULL, 
    [TXOutCount] DECIMAL(20) NOT NULL, 
    [LockTime] DECIMAL(10) NOT NULL, 
    [BelongstoBlockHashID] BINARY(32) NOT NULL, 
    CONSTRAINT [PK_TXs] PRIMARY KEY ([BelongstoBlockHashID], [TXHashID]) 
)

GO

CREATE INDEX [IX_TXs_TXHashID] ON [dbo].[TXs] ([TXHashID])

GO

CREATE INDEX [IX_TXs_BlockHashID] ON [dbo].[TXs] ([BelongstoBlockHashID])
