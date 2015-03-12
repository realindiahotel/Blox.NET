using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Network;

namespace Bitcoin.Lego.Protocol_Messages
{
	[Serializable]
	public class VersionMessage : Message
	{
		private ulong _localServices;
		private ulong _time;		
		public PeerAddress _theirAddr;
		private PeerAddress _myAddr;
		private ulong _nonce;
		private string _userAgent;
		private uint _startBlockHeight;
		private int _relay;

			/// <summary>
			/// The version number of the protocol spoken.
			/// </summary>
		public uint ClientVersion
		{
			get
			{
				return ProtocolVersion;
			}
		}

		/// <summary>
		/// Flags defining what is supported. Right now <see cref="NodeNetwork"/> is the only flag defined.
		/// </summary>
		public ulong LocalServices
		{
			get
			{
				return _localServices;
			}
		}

		/// <summary>
		/// What the other side believes the current time to be, in seconds.
		/// </summary>
		public ulong Time
		{
			get
			{
				return _time;
			}
		}

		public PeerAddress MyAddr
		{
			get
			{
				return _myAddr;
			}

			private set
			{
				_myAddr = value;
			}
		}

		public PeerAddress TheirAddr
		{
			get
			{
				return _theirAddr;
			}

			private set
			{
				_theirAddr = value;
			}
		}

		public ulong Nonce
		{
			get
			{
				return _nonce;
			}
		}
			
		public String UserAgent
		{
			get
			{
				return _userAgent;
			}
		}

		public uint StartBlockHeight
		{
			get
			{
				return _startBlockHeight;
			}
		}

		public int Relay
		{
			get
			{
				return _relay;
			}
		}

		public VersionMessage(byte[] msg, P2PNetworkParamaters netParams)
			: base(msg, 0, true, netParams)
		{

		}

		public VersionMessage(IPAddress remoteIpAddress, int remotePort, Socket sock, uint newBestHeight, uint remoteClientVersion, P2PNetworkParamaters netParams, ulong remoteServices = (ulong)P2PNetworkParamaters.NODE_NETWORK.FULL_NODE) :base(netParams)
		{
			_localServices = P2PNetParameters.Services;
			_time = P2PConnectionManager.GetUTCNowWithOffset();
			_myAddr = new PeerAddress(IPAddress.Loopback, ((IPEndPoint)sock.LocalEndPoint).Port, netParams.Services, P2PNetParameters,true);
			_theirAddr = new PeerAddress(remoteIpAddress, remotePort,remoteServices , P2PNetParameters,true);
			_nonce = P2PNetworkParamaters.VersionConnectNonce;
			_userAgent = P2PNetworkParamaters.UserAgentString;
			_startBlockHeight = newBestHeight;
			_relay = P2PNetParameters.Relay;
		}

		protected override void Parse()
		{
			ProtocolVersion = ReadUint32();
			_localServices = ReadUint64();
			_time = ReadUint64();
			MyAddr = new PeerAddress(Bytes, Cursor, true, new P2PNetworkParamaters(P2PNetworkParamaters.ProtocolVersion));
			Cursor += MyAddr.MessageSize;
			TheirAddr = new PeerAddress(Bytes, Cursor, true, new P2PNetworkParamaters(P2PNetworkParamaters.ProtocolVersion));
			Cursor += MyAddr.MessageSize;
			_nonce = ReadUint64();
			_userAgent = ReadStr();
			_startBlockHeight = ReadUint32();
			//Relay flag added in 70001
			if (ProtocolVersion >= 70001)
			{
				try
				{
					_relay = ReadBytes(1)[0];
				}
				catch
				{
					//I think if the relay is '0' it gets seen as end of data with the rest of the trailing 0's so this fixes that if we can't read the 0 make it 0 anyway
					_relay = ((int)P2PNetworkParamaters.RELAY.RELAY_ON_DEMAND);
				}
			}
			else
			{
				_relay = ((int)P2PNetworkParamaters.RELAY.RELAY_ALWAYS);
			}
		}

		/// <exception cref="IOException"/>
		public override void BitcoinSerializeToStream(Stream buf)
		{
			Utilities.Uint32ToByteStreamLe(ClientVersion, buf);
			Utilities.Uint64ToByteStreamLe(LocalServices, buf);
			Utilities.Uint64ToByteStreamLe(Time, buf);				
			// Their address.
			TheirAddr.BitcoinSerializeToStream(buf);
			// My address.
			MyAddr.BitcoinSerializeToStream(buf);
			// Next up is the "local host nonce", this is to detect the case of connecting
			// back to yourself. We don't care about this as we won't be accepting inbound
			// connections.
			Utilities.Uint64ToByteStreamLe(_nonce, buf);
			// Now comes subVer.
			var subVerBytes = Encoding.UTF8.GetBytes(_userAgent);
			//this was fucking me.....I'm like what even but seeing the message in wireshark switched me on to the fact we need size byte of string before string
			buf.WriteByte(Convert.ToByte(subVerBytes.Length));
			buf.Write(subVerBytes,0, subVerBytes.Length);
			// Size of known block chain.
			Utilities.Uint32ToByteStreamLe(_startBlockHeight, buf);
			buf.WriteByte((byte)_relay);
		}

		// <summary>
		/// Returns true if the version message indicates the sender has a full copy of the block chain,
		/// or if it's running in client mode (only has the headers).
		/// </summary>
		public bool HasBlockChain()
		{
			return (LocalServices & ((ulong)P2PNetworkParamaters.NODE_NETWORK.FULL_NODE)) == ((ulong)P2PNetworkParamaters.NODE_NETWORK.FULL_NODE);
        }
	}
}

