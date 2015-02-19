using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;
using System.Net;
using Bitcoin.Lego.Network;

namespace Lego.NET.Longarm
{
	public partial class LegoService : ServiceBase
	{
		public LegoService()
		{
			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
			Start();
		}

		protected override void OnStop()
		{
			Stop();
		}

		internal void Start()
		{
			if (!StartListening())
			{
				throw new Exception("Cannot Start Listening for Incoming Connections");
			}
		}

		new internal void Stop()
		{
			//do what we want here then stop the service
			StopListening();

			base.Stop();
		}

		private bool StartListening()
		{
			return P2PListener.ListenForIncomingP2PConnections(IPAddress.Any);
		}

		private bool StopListening()
		{
			return P2PListener.StopListeningForIncomingP2PConnections();
		}
	}
}
