using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Net;
using Bitcoin.Lego;
using Bitcoin.BitcoinUtilities;
using System.Net.Sockets;

namespace TestUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		P2PConnection p2p;
        public MainWindow()
		{
			InitializeComponent();
		}

		private void button_Click(object sender, RoutedEventArgs e)
		{
			 p2p = new P2PConnection(IPAddress.Parse("66.188.35.226"), Globals.TCPMessageTimeout, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
            bool success = p2p.ConnectToPeer(Globals.NodeNetwork,1,Globals.RelayTransactionsAlways,true);

			if (!success)
			{
				MessageBox.Show("Not connected");
			}


		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			P2PListener.ListenForIncomingP2PConnections(IPAddress.Any);
		}

		private void button2_Click(object sender, RoutedEventArgs e)
		{
			P2PListener.StopListeningForIncomingP2PConnections();
		}

		private void button3_Click(object sender, RoutedEventArgs e)
		{
			p2p.Send(new Bitcoin.Lego.Protocol_Messages.Ping());
		}

		private async void button4_Click(object sender, RoutedEventArgs e)
		{
			List<IPAddress> ips = await Connection.GetDNSSeedIPAddressesAsync(Globals.DNSSeedHosts);
			MessageBox.Show(ips.Count.ToString());

		}

		private async void button5_Click(object sender, RoutedEventArgs e)
		{
			List<IPAddress> ips = await Connection.GetDNSSeedIPAddressesAsync(Globals.DNSSeedHosts);

			foreach (IPAddress ip in ips)
			{
				Thread connectThread = new Thread(new ThreadStart(() =>
				{
					P2PConnection p2p = new P2PConnection(ip, Globals.TCPMessageTimeout, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
					if (p2p.ConnectToPeer(Globals.NodeNetwork, 1, 1, true))
					{
						P2PListener.AddP2PConnection(p2p);

					}
				}));
				connectThread.Start();
            }
		}
	}
}
