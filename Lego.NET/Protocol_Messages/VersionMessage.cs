using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Bitcoin.BitcoinUtilities;

namespace Bitcoin.Lego.Protocol_Messages
{
	[Serializable]
	public class VersionMessage : Message
	{
		private uint _clientVersion = Globals.ClientVersion;
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
				return _clientVersion;
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

		/// <exception cref="ProtocolException"/>
		public VersionMessage(byte[] msg, uint packetMagic)
			: base(msg, 0, true, packetMagic)
		{
		}

		public VersionMessage(IPAddress remoteIpAddress, Socket sock, ulong services, int remotePort, uint newBestHeight, int relay, uint packetMagic = Globals.ProdPacketMagic) :base(packetMagic)
		{
			_localServices = services;
			_time = Utilities.ToUnixTime(DateTime.UtcNow);
			_myAddr = new PeerAddress(IPAddress.Loopback, ((IPEndPoint)sock.LocalEndPoint).Port, services,Globals.ClientVersion,true);
			_theirAddr = new PeerAddress(remoteIpAddress, remotePort, services, Globals.ClientVersion,true);
			_nonce = Globals.NotCryptoRandomNonce;
			_userAgent = Globals.UserAgentString;
			_startBlockHeight = newBestHeight;
			_relay = relay;
		}

			/// <exception cref="ProtocolException"/>
		protected override void Parse()
		{
			_clientVersion = ReadUint32();
			_localServices = ReadUint64();
			_time = ReadUint64();
			MyAddr = new PeerAddress(Bytes, Cursor, Globals.ClientVersion,true);
			Cursor += MyAddr.MessageSize;
			TheirAddr = new PeerAddress(Bytes, Cursor, _clientVersion, true);
			Cursor += MyAddr.MessageSize;
			_nonce = ReadUint64();
			_userAgent = ReadStr();
			_startBlockHeight = ReadUint32();
			_relay = ReadBytes(1)[0];
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
			return (LocalServices & Globals.NodeNetwork) == Globals.NodeNetwork;
		}
	}
}

