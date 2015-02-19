using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using Bitcoin.BitcoinUtilities;
using Org.BouncyCastle.Math;
using Bitcoin.Lego.Protocol_Messages;

namespace Bitcoin.Lego
{
	/// <summary>
	/// A PeerAddress holds an IP address and port number representing the network location of a peer in the BitCoin P2P network.
	/// </summary>
	[Serializable]
	public class PeerAddress : Message
	{
		internal IPAddress Addr { get; private set; }
		internal int Port { get; private set; }
		private ulong _services;
		private uint _time;
		private bool _isInVersionMessage;

		/// <summary>
		/// Construct a peer address from a serialized payload.
		/// </summary>
		public PeerAddress(byte[] payload, int offset, uint protocolVersion, bool isInVersionMessage, uint packetMagic = Globals.ProdPacketMagic)
			: base(payload, offset, false, packetMagic, protocolVersion)
		{
			_isInVersionMessage = isInVersionMessage;
			Parse();
		}

		/// <summary>
		/// Construct a peer address from a memorized or hardcoded address.
		/// </summary>
		public PeerAddress(IPAddress addr, int port, ulong services, uint protocolVersion = Globals.ClientVersion, bool isInVersionMessage = false)
		{
			Addr = addr;
			Port = port;
			ProtocolVersion = protocolVersion;
			_services = services;
			_isInVersionMessage = isInVersionMessage;
		}

		
		public PeerAddress(IPEndPoint addr, ulong services)
			: this(addr.Address, addr.Port, services)
		{
		}

		/// <exception cref="IOException"/>
		public override void BitcoinSerializeToStream(Stream stream)
		{
			//this is fucked up...version messages will not have uint32 timestamp so must find a way to strip from version message
			if (ProtocolVersion >= 31402 && !_isInVersionMessage)
			{
				var secs = Utilities.ToUnixTime(DateTime.UtcNow);
				Utilities.Uint32ToByteStreamLe((uint)secs, stream);
			}
			Utilities.Uint64ToByteStreamLe(_services, stream); // nServices.
			var ipBytes = Addr.GetAddressBytes();
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
			stream.Write((new byte[] { (byte)(Port >> 8) }), 0, (new byte[] { (byte)(Port >> 8) }).Length);
			stream.Write((new byte[] { (byte)Port }),0, (new byte[] { (byte)Port }).Length);
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
				_time = Convert.ToUInt32(Utilities.ToUnixTime(DateTime.UtcNow));
			}
			_services = ReadUint64();
			var addrBytes = ReadBytes(16);
			if (new BigInteger(addrBytes, 0, 12).Equals(BigInteger.ValueOf(0xFFFF)))
			{
				var newBytes = new byte[4];
				Array.Copy(addrBytes, 12, newBytes, 0, 4);
				addrBytes = newBytes;
			}
			Addr = new IPAddress(addrBytes);
			Port = (Bytes[Cursor++] << 8) | Bytes[Cursor++];

			Bytes = null;
		}

		public override string ToString()
		{
			return "[" + Addr + "]:" + Port;
		}
	}
}
