CREATE TABLE [dbo].[TXIns]
(
	[TXInHashID] BINARY(32) NOT NULL PRIMARY KEY, 
    [OutpointTXHash] BINARY(32) NOT NULL, 
    [OutpointTXIndex] DECIMAL(10) NOT NULL, 
    [ScriptLength] DECIMAL(20) NOT NULL, 
    [SignatureScript] VARBINARY(MAX) NOT NULL, 
    [Sequence] DECIMAL(10) NOT NULL, 
    [BelongstoTXHashID] BINARY(32) NOT NULL
)
