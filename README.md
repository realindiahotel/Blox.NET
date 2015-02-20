# Lego.NET
A C# full node for processing the Bitcoin block chain, aim is to have the Lego.NET library contain all the business logic/database interface and then Lego.NET.Arcus is a Worker Service that has TCP:8333 defined up in Azure and it will run as a full node service in Azure. Lego.NET.Nacreous is a Web API/ASP.NET front end which will be an exposed endpoint for accepting HTTP requests to control Arcus.

NOT READY FOR USE - STILL UNDER ACTIVE DEVELOPMENT

PLEASE ENSURE YOU ALSO DOWNLOAD, BUILD AND REFERENCE THE BitcoinUtilities.NET PROJECT LOCATED HERE: https://github.com/Thashiznets/BitcoinUtilities.NET 

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
