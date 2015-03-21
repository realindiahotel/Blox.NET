CREATE TABLE [dbo].[Blocks]
(
	[BlockHashID] BINARY(32) NOT NULL , 
    [Version] DECIMAL(10) NOT NULL, 
    [PreviousBlockHash] BINARY(32) NOT NULL, 
    [MerkleRoot] BINARY(32) NOT NULL, 
    [Time] DECIMAL(10) NOT NULL, 
    [BitsDifficulty] DECIMAL(10) NOT NULL, 
    [Nonce] DECIMAL(10) NOT NULL, 
    [TXCount] DECIMAL(20) NOT NULL, 
    [Height] DECIMAL(10) NOT NULL, 
    CONSTRAINT [PK_Blocks] PRIMARY KEY ([BlockHashID], [Height])
)

GO

CREATE INDEX [IX_Blocks_Height] ON [dbo].[Blocks] ([Height] DESC)
