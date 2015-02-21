using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.Lego.Network;
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

		public void StarteMe()
		{
			P2PListener.ListenForIncomingP2PConnections(IPAddress.Any);
		}
	}
}
