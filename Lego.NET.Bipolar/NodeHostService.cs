using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Bitcoin.Lego.Network;
using Bitcoin.BitcoinUtilities;
using System.Net;

namespace Lego.NET.Bipolar
{
	public partial class NodeHostService : ServiceBase
	{
		public NodeHostService()
		{
			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
			StarteMe();
		}

		protected override void OnStop()
		{
		}

		public async void StarteMe()
		{
			P2PNetworkParamaters netParams = new P2PNetworkParamaters(P2PNetworkParamaters.ProtocolVersion, false, 20966, (ulong)P2PNetworkParamaters.NODE_NETWORK.FULL_NODE, (int)P2PNetworkParamaters.RELAY.RELAY_ALWAYS);
			//start threaded loop listening for peers
			if (netParams.ListenForPeers)
			{
				await P2PConnectionManager.ListenForIncomingP2PConnectionsAsync(IPAddress.Any,netParams);
			}

			//start threaded loop maintaining max outbound connections to peers
			P2PConnectionManager.MaintainConnectionsOutbound(netParams);
		}
	}
}
