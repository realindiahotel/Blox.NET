using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using Bitcoin.BitcoinUtilities;
using Org.BouncyCastle.Math;
using Bitcoin.Blox.Protocol_Messages;
using Bitcoin.Blox.Network;

namespace Bitcoin.Blox
{
	/// <summary>
	/// A PeerAddress holds an IP address and port number representing the network location of a peer in the BitCoin P2P network.
	/// </summary>
	[Serializable]
	public class PeerAddress : Message
	{
		private IPAddress _addr;
		private ushort _port;
		private ulong _services;
		private uint _time;
		private bool _isInVersionMessage;

		/// <summary>
		/// Construct a peer address from a serialized payload.
		/// </summary>
		public PeerAddress(byte[] payload, int offset, bool isInVersionMessage, P2PNetworkParameters netParams):base(payload, offset, false, netParams)
		{
			_isInVersionMessage = isInVersionMessage;
			Parse();
		}

		/// <summary>
		/// Construct a peer address from a memorized or hardcoded address.
		/// </summary>
		public PeerAddress(IPAddress addr, ushort port, ulong services, P2PNetworkParameters netParams, bool isInVersionMessage = false) :base(netParams)
		{
			_addr = addr;
			_port = port;
			_time = (uint)P2PConnectionManager.GetUTCNowWithOffset();			
			ProtocolVersion = netParams.ClientVersion;
			_services = services;
			_isInVersionMessage = isInVersionMessage;
		}

		public PeerAddress(IPAddress addr, ushort port, ulong services, uint time, P2PNetworkParameters netParams, bool isInVersionMessage = false) :base(netParams)
		{
			_addr = addr;
			_port = port;
			_time = time;
			ProtocolVersion = netParams.ClientVersion;
			_services = services;
			_isInVersionMessage = isInVersionMessage;
		}

		public override void BitcoinSerializeToStream(Stream stream)
		{
			if (ProtocolVersion >= 31402 && !_isInVersionMessage)
			{				
				Utilities.Uint32ToByteStreamLe(_time, stream);
			}
			Utilities.Uint64ToByteStreamLe(_services, stream); // nServices.
			var ipBytes = _addr.GetAddressBytes();
			if (ipBytes.Length == 4)
			{
				var v6Addr = new byte[16];
				Array.Copy(ipBytes, 0, v6Addr, 12, 4);
				v6Addr[10] = 0xFF;
				v6Addr[11] = 0xFF;
				ipBytes = v6Addr;
			}
			stream.Write(ipBytes, 0, ipBytes.Length);
			// And write out the port. Unlike the rest of the protocol, address and port is in big endian byte order.
			stream.Write((new byte[] { (byte)(_port >> 8) }), 0, (new byte[] { (byte)(_port >> 8) }).Length);
			stream.Write((new byte[] { (byte)_port }),0, (new byte[] { (byte)_port }).Length);
		}

		protected override void Parse()
		{
			// Format of a serialized address:
			//   uint32 timestamp
			//   uint64 services   (flags determining what the node can do)
			//   16 bytes IP address
			//   2 bytes port num
			if (!_isInVersionMessage)
			{
				if (ProtocolVersion > 31402)
					_time = ReadUint32();
				else
					_time = uint.MaxValue;
			}
			else
			{
				_time = (uint)P2PConnectionManager.GetUTCNowWithOffset();				
			}
			_services = ReadUint64();
			var addrBytes = ReadBytes(16);
			if (new BigInteger(addrBytes, 0, 12).Equals(BigInteger.ValueOf(0xFFFF)))
			{
				var newBytes = new byte[4];
				Array.Copy(addrBytes, 12, newBytes, 0, 4);
				addrBytes = newBytes;
			}
			_addr = new IPAddress(addrBytes);
			_port = Convert.ToUInt16((Bytes[Cursor++] << 8) | Bytes[Cursor++]);

			Bytes = null;
		}

		/// <summary>
		/// Used to determine if we relay this address or not
		/// </summary>
		public bool IsRelayExpired
		{
			get
			{
				//if older than 3 hours
				if (_time <= (((uint)P2PConnectionManager.GetUTCNowWithOffset()) - 10800))
                {
					return true;
				}

				return false;
			}
		}

		/// <summary>
		/// If it is older than 24 hours we don't include in getaddr responses
		/// </summary>
		public bool Is24hrExpired
		{
			get
			{
				//if older than 3 hours
				if (_time <= (((uint)P2PConnectionManager.GetUTCNowWithOffset()) - 86400))
				{
					return true;
				}

				return false;
			}
		}

		public override string ToString()
		{
			return "[" + _addr + "]:" + _port;
		}

		public IPAddress IPAddress
		{
			get
			{
				return _addr;
			}
		}

		public ushort Port
		{
			get
			{
				return _port;
			}
		}

		public uint Time
		{
			get
			{
				return _time;
			}

			set
			{
				_time = value;
			}
		}

		public ulong Services
		{
			get
			{
				return _services;
			}	

			set
			{
				_services = value;
			}
		}
	}
}
