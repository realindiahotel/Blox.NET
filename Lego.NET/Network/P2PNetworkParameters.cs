using System;
using Org.BouncyCastle.Math;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitcoin.Lego.Network
{
	public class P2PNetworkParameters
	{
		public const uint TestPacketMagic = 0x0b110907;
        public const uint ProdPacketMagic = 0xf9beb4d9;
		public const ushort TestP2PPort = 18333;
		public const ushort ProdP2PPort = 8333;
		public static BigInteger ProofOfWorkLimit = new BigInteger("0000000fffffffffffffffffffffffffffffffffffffffffffffffffffffffff", 16);
		public static readonly String[] DNSSeedHosts = { "seed.bitcoin.sipa.be", "dnsseed.bitcoin.dashjr.org", "bitseed.xf2.org", "dnsseed.bluematt.me" }; //in future lego will also have it's own dns responder
		public static readonly String[] TestNetDNSSeedHosts = { "testnet-seed.bitcoin.petertodd.org" };
		public static readonly int MaxOutgoingP2PConnections = 8; //The maximum amount of P2P clients we will reach out and connect to
		public static readonly int MaxIncomingP2PConnections = 128; //The maximum amount of P2P clients we will allow to connect to us
		private static readonly String _legoVersionString = "0.0.0.0";
		private static readonly String _legoCodenameString = "Thashiznets-Testing";
		public static readonly String UserAgentString = @"/Lego.NET:" + _legoVersionString + @"/" + _legoCodenameString + @"/";
		public static uint ProtocolVersion = 70002;
		public static readonly uint MinimumAcceptedClientVersion = 70001;	//we don't connect to anything below this, AIDs.
		public static int RetrySendTCPOnError = 3; //number of attempts to send message on error;
		public static int RetryRecieveTCPOnError = 3;	//number of attempts to recieve message , after 3 attempts we kill the connection
		public static readonly int HeartbeatTimeout = 1800000; //30 minute interval, heartbeat
		public static readonly int AddrFartInterval = 86400000;	//24 hour interval to transmit my addr
		public static bool HeartbeatKeepAlive = true; //if true then send heartbeat to keep connection open every 30 minutes-ish
		public static int SeedNodeCount = 200; //the maximum number of seed nodes to attempt to connect too.	
		public static ulong VersionConnectNonce = Convert.ToUInt64(DateTime.UtcNow.Ticks);
		private bool _strictVerackOutbound;	//if true we force reciept of verack from peers we connect to outbound		
		public enum NODE_NETWORK : ulong { SPV_NODE = 0, FULL_NODE = 1 };
		public enum RELAY { RELAY_ON_DEMAND = 0, RELAY_ALWAYS = 1 };
		private ushort _p2PListeningPort;
		private int _addressMemPoolMax;	//once this value is reached we start overwriting addr entries in the begining of the peers address mempool	
		private bool _uPNPMapPort; //Attempts to send UPNP message to set up port forwarding in NAT of connected router, won't always work, especially if on VPN or behind multiple routers, but should work for most homes
		private readonly bool _listenForPeers;	//if this is true we listen for peers on startup
		private readonly bool _allowP2PConnectToSelf; //Allows this client to connect to itself if it gets served its own address
		private readonly bool _advertiseExternalIP; //If set to false we return local host instead of actual external IP
		private readonly bool _isTestNet;
		private uint _packetMagic;
		private ulong _services;
		private int _relay;
		private uint _clientVersion;

		/// <summary>
		/// This object defines all the parameters needed for operation on the P2P netword
		/// </summary>
		/// <param name="isTestNet">true for testnet false for mainnet (prodnet)</param>
		/// <param name="localListeningPort">set this to something different if we son't want to listen for P2P peers on the 8333/18333 ports</param>
		/// <param name="strictVerackForOutbound">true forces a verack from anyone we connect to. If we don't recieve a verack we close the connection</param>
		/// <param name="advertiseExternalIP">true to broadcast our public IP address to peers, set false if you don't want to broadcast your public IP address</param>
		/// <param name="allowP2PConnectToSelf">true allows us to connect to ourself</param>
		/// <param name="enableUPNPPortMap">true attempts to set port forwarding with the closest NAT device using UPnP</param>
		public P2PNetworkParameters(uint clientVersion, bool isTestNet = false, ushort listeningPort = ProdP2PPort, ulong services = (ulong)NODE_NETWORK.FULL_NODE, int relay = (int)RELAY.RELAY_ALWAYS, bool enableListenForPeers = true, bool strictVerackForOutbound = true, bool advertiseExternalIP = true, bool allowP2PConnectToSelf = false, bool enableUPNPPortMap = true, int addressMemPoolMaxSize = 5000)
		{
			_isTestNet = isTestNet;

			if (_isTestNet)
			{
				//set listening port
				if (_p2PListeningPort == ProdP2PPort) //if default mainnet port switch to testnet
				{
					_p2PListeningPort = TestP2PPort;
				}
				else
				{
					_p2PListeningPort = listeningPort; //if custom port provided use that
                }

				//set packet magic
				_packetMagic = TestPacketMagic;
			}
			else
			{
				//set port
				_p2PListeningPort = listeningPort;

				//set packet magic
				_packetMagic = ProdPacketMagic;
			}

			_clientVersion = clientVersion;
			_services = services;
			_relay = relay;
			_listenForPeers = enableListenForPeers;
			_strictVerackOutbound = strictVerackForOutbound;
			_advertiseExternalIP = advertiseExternalIP;
			_allowP2PConnectToSelf = allowP2PConnectToSelf;
			_uPNPMapPort = enableUPNPPortMap;
			_addressMemPoolMax = addressMemPoolMaxSize;
		}

		public ushort P2PListeningPort
		{
			get
			{
				return _p2PListeningPort;
			}
		}

		public bool StrictOutboundVerack
		{
			get
			{
				return _strictVerackOutbound;
			}
		}

		public bool AllowP2PConnectToSelf
		{
			get
			{
				return _allowP2PConnectToSelf;
			}
		}

		public bool ListenForPeers
		{
			get
			{
				return _listenForPeers;
			}
		}

		public int AddressMemPoolMax
		{
			get
			{
				return _addressMemPoolMax;
			}
		}

		public uint PacketMagic
		{
			get
			{
				return _packetMagic;
			}
		}

		public bool AdvertiseExternalIP
		{
			get
			{
				return _advertiseExternalIP;
			}
		}

		public ulong Services
		{
			get
			{
				return _services;
			}
		}

		public int Relay
		{
			get
			{
				return _relay;
			}
		}

		public bool UPnPMapPort
		{
			get
			{
				return _uPNPMapPort;
			}
		}

		public uint ClientVersion
		{
			get
			{
				return _clientVersion;
			}
		}

		public bool IsTestNet
		{
			get
			{
				return _isTestNet;
			}
		}

	}
}
