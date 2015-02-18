using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;

namespace Bitcoin.Lego.Protocol_Messages
{
	public class GetAddresses : Message
	{
		public GetAddresses(uint packetMagic = Globals.ProdPacketMagic) : base(new byte[] { }, 0, true, packetMagic)
		{
		}

		// this is needed by the BitcoinSerializer
		public GetAddresses(byte[] payload, uint packetMagic = Globals.ProdPacketMagic) : base(payload, 0, true, packetMagic)
		{
		}
		protected override void Parse()
		{
			// nothing to parse for now
		}
	}
}
