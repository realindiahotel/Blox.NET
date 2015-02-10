using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;

namespace Bitcoin.Lego.Protocol_Messages
{
	public class Ping : Message
	{
		private ulong _nonce;
		public Ping(byte[] payload, uint packetMagic = Globals.ProdPacketMagic) : base(payload, 0, true, packetMagic)
		{
			
		}

		public Ping(uint packetMagic = Globals.ProdPacketMagic) : base(packetMagic)
		{
			_nonce = Convert.ToUInt64(DateTime.UtcNow.Ticks);
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
