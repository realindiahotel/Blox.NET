# Lego.NET
A C# full node for processing the Bitcoin block chain

NOT READY FOR USE - STILL UNDER ACTIVE DEVELOPMENT

Current Achieved Milestones (Protocol version 70002):

* Listening and Accepting P2P connections
* Initiating Outbound P2P connections
* version
* verack
* ping
* pong
* reject
* inv
* addr
* getaddr
* Heartbeat (optional)
* DNS Seed Fetching
* Check if remote client timestamp <> 70 minutes and dissallow
* Check nonce and dissallow connection to self (optional)
* Get external IP
