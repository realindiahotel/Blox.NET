using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;

namespace Bitcoin.Lego.Protocol_Messages
{
	[Serializable]
	public class InventoryMessage : ListMessage
	{
		public InventoryMessage(byte[] bytes, uint packetMagic) : base(bytes,packetMagic)
		{
		}

		public InventoryMessage(uint packetMagic = Globals.ProdPacketMagic) : base(packetMagic)
		{
		}
	}
}
