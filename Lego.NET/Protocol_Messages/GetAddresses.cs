using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Network;

namespace Bitcoin.Lego.Protocol_Messages
{
	public class GetAddresses : Message
	{
		public GetAddresses(P2PNetworkParamaters netParams) : base(new byte[] { }, 0, true, netParams)
		{
		}

		// this is needed by the BitcoinSerializer
		public GetAddresses(byte[] payload, P2PNetworkParamaters netParams) : base(payload, 0, true, netParams)
		{
		}
		protected override void Parse()
		{
			// nothing to parse for now
		}
	}
}
