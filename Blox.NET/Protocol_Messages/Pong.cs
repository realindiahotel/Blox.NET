using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Blox.Network;

namespace Bitcoin.Blox.Protocol_Messages
{
	public class Pong : Message
	{
		private ulong _nonce;
		public Pong(byte[] payload, P2PNetworkParameters netParams) : base(payload, 0, true, netParams)
		{
			
		}

		public Pong(ulong nonce, P2PNetworkParameters netParams) : base(netParams)
		{
			_nonce = nonce;
		}

		public override void BitcoinSerializeToStream(Stream buf)
		{
			Utilities.Uint64ToByteStreamLe(_nonce, buf);
		}

		protected override void Parse()
		{
			//we get the nonce of the ping they sent us, not really necessary but I'm OCD haha
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
