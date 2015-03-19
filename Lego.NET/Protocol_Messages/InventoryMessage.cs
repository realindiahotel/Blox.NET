using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Network;

namespace Bitcoin.Lego.Protocol_Messages
{
	[Serializable]
	public class InventoryMessage : ListMessage
	{
		public InventoryMessage(byte[] bytes, P2PNetworkParameters netParams) : base(bytes,netParams)
		{
		}

		public InventoryMessage(P2PNetworkParameters netParams) : base(netParams)
		{
		}
	}
}
