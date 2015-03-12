using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Network;
using System.IO;

namespace Bitcoin.Lego.Protocol_Messages
{
	public class Ping : Message
	{
		private ulong _nonce;
		public Ping(byte[] payload, P2PNetworkParamaters netParams) : base(payload, 0, true, netParams)
		{
			
		}

		public Ping(P2PNetworkParamaters netParams) : base(netParams)
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
