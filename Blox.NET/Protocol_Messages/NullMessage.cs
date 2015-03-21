using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.Blox.Network;

namespace Bitcoin.Blox.Protocol_Messages
{
	/// <summary>
	/// Empty message to dismiss if recieve TCP error
	/// </summary>
	public class NullMessage :Message
	{
		public NullMessage(P2PNetworkParameters netParams) :base(netParams)
		{

		}
		protected override void Parse()
		{

		}
	}
}
