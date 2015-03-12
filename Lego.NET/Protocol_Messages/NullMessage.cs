using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.Lego.Network;

namespace Bitcoin.Lego.Protocol_Messages
{
	/// <summary>
	/// Empty message to dismiss if recieve TCP error
	/// </summary>
	public class NullMessage :Message
	{
		public NullMessage(P2PNetworkParamaters netParams) :base(netParams)
		{

		}
		protected override void Parse()
		{

		}
	}
}
