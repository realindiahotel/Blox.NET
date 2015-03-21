using System;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Blox.Network;
using System.IO;

namespace Bitcoin.Blox.Protocol_Messages
{
	public class Ping : Message
	{
		private ulong _nonce;
		public Ping(byte[] payload, P2PNetworkParameters netParams) : base(payload, 0, true, netParams)
		{
			
		}

		public Ping(P2PNetworkParameters netParams) : base(netParams)
		{
			_nonce = Convert.ToUInt64(DateTime.UtcNow.Ticks);
		}

		public override void BitcoinSerializeToStream(Stream buf)
		{
			Utilities.Uint64ToByteStreamLe(_nonce, buf);
		}

		protected override void Parse()
		{
			_nonce = ReadUint64();
		}

		public ulong Nonce
		{
			get
			{
				return _nonce;
			}
		}
	}
}
