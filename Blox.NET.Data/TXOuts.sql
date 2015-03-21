CREATE TABLE [dbo].[TXOuts]
(
	[TXOutHashID] BINARY(32) NOT NULL PRIMARY KEY, 
    [SpendingValue] BIGINT NOT NULL, 
    [RedeemScriptLength] DECIMAL(20) NOT NULL, 
    [RedeemScript] VARBINARY(MAX) NOT NULL, 
    [BelongstoTXHashID] BINARY(32) NOT NULL
)
