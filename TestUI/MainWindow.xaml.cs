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
using System.Runtime;
using System.Threading;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net;
using Bitcoin.Lego;
using Bitcoin.Lego.Network;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Data_Interface;
using System.Net.Sockets;

namespace TestUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		P2PConnection p2p;
		P2PNetworkParameters _networkParameters = new P2PNetworkParameters(P2PNetworkParameters.ProtocolVersion, false, 20966,1,1,true,true);

		public MainWindow()
		{
			InitializeComponent();
		}

		private void button_Click(object sender, RoutedEventArgs e)
		{
			Thread connectThread = new Thread(new ThreadStart(() =>
			{
				ushort port = 8333;

				if (_networkParameters.IsTestNet)
				{
					port = 18333;
				}

				p2p = new P2PConnection(IPAddress.Parse("82.45.214.119"), _networkParameters, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),port);
                bool success = p2p.ConnectToPeer(1);

				if (!success)
				{
					MessageBox.Show("Not connected");
				}
				
			}));

			connectThread.IsBackground = true;
			connectThread.Start();

			Thread threadLable = new Thread(new ThreadStart(() =>
			{
				while (true)
				{
					Dispatcher.Invoke(() =>
					{
						label.Content = "Inbound: " + P2PConnectionManager.GetInboundP2PConnections().Count + " Outbound: " + P2PConnectionManager.GetOutboundP2PConnections().Count;
					});
					Thread.CurrentThread.Join(250);
				}
			}));
			threadLable.IsBackground = true;
			threadLable.Start();
		}

		private async void button1_Click(object sender, RoutedEventArgs e)
		{
			Thread threadLable = new Thread(new ThreadStart(() =>
			{
				while (true)
				{
					Dispatcher.Invoke(() =>
					{
						List<P2PConnection> inNodes = P2PConnectionManager.GetInboundP2PConnections();
                        label.Content = "Inbound: " + inNodes.Count + " Outbound: " + P2PConnectionManager.GetOutboundP2PConnections().Count;

						if (inNodes.Count > 0)
						{

						}
					});

                    Thread.CurrentThread.Join(1000);
				}
			}));
			threadLable.IsBackground = true;
			threadLable.Start();

			await P2PConnectionManager.ListenForIncomingP2PConnectionsAsync(IPAddress.Any,_networkParameters);
		}

		private void button2_Click(object sender, RoutedEventArgs e)
		{
			P2PConnectionManager.StopListeningForIncomingP2PConnections();
		}

		private void button3_Click(object sender, RoutedEventArgs e)
		{
			p2p.Send(new Bitcoin.Lego.Protocol_Messages.Ping(_networkParameters));
		}

		private async void button4_Click(object sender, RoutedEventArgs e)
		{
			List<PeerAddress> ips;

            if (!_networkParameters.IsTestNet)
			{
				ips = await P2PConnectionManager.GetDNSSeedIPAddressesAsync(P2PNetworkParameters.DNSSeedHosts, _networkParameters);
			}
			else
			{
				ips = await P2PConnectionManager.GetDNSSeedIPAddressesAsync(P2PNetworkParameters.TestNetDNSSeedHosts, _networkParameters);
			}
			MessageBox.Show(ips.Count.ToString());

		}

		private async void button5_Click(object sender, RoutedEventArgs e)
		{
			List<PeerAddress> ips = await P2PConnectionManager.GetDNSSeedIPAddressesAsync(P2PNetworkParameters.DNSSeedHosts, _networkParameters);

			Thread threadLable = new Thread(new ThreadStart(() =>
			{
			while (true)
			{
				Dispatcher.Invoke(()=>
				{
					label.Content = "Inbound: " + P2PConnectionManager.GetInboundP2PConnections().Count + " Outbound: " + P2PConnectionManager.GetOutboundP2PConnections().Count;
				});
					Thread.CurrentThread.Join(1000);
			}
			}));
			threadLable.IsBackground = true;
			threadLable.Start();

			foreach (PeerAddress ip in ips)
			{
				Thread connectThread = new Thread(new ThreadStart(() =>
				{
					P2PConnection p2p = new P2PConnection(ip.IPAddress, _networkParameters, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
					p2p.ConnectToPeer(1);
					
				}));
				connectThread.IsBackground = true;
				connectThread.Start();
            }
		}

		private async void button6_Click(object sender, RoutedEventArgs e)
		{
			PeerAddress myip = await p2p.GetMyExternalIPAsync();
			MessageBox.Show(myip.ToString());
		}

		private void button7_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show(new DatabaseConnection(_networkParameters).ConnectionString);
		}

		private async void button8_Click(object sender, RoutedEventArgs e)
		{
			await P2PConnection.SetNATPortForwardingUPnPAsync(_networkParameters.P2PListeningPort, _networkParameters.P2PListeningPort);
		}

		private async void button9_Click(object sender, RoutedEventArgs e)
		{
			Thread threadLable = new Thread(new ThreadStart(() =>
			{
				while (true)
				{
					Dispatcher.Invoke(() =>
					{
						List<P2PConnection> inNodes = P2PConnectionManager.GetInboundP2PConnections();
						label.Content = "Inbound: " + inNodes.Count + " Outbound: " + P2PConnectionManager.GetOutboundP2PConnections().Count;

						if (inNodes.Count > 0)
						{

						}
					});

					Thread.CurrentThread.Join(1000);
				}
			}));
			threadLable.IsBackground = true;
			threadLable.Start();

			await P2PConnectionManager.ListenForIncomingP2PConnectionsAsync(IPAddress.Any,_networkParameters);

			P2PConnectionManager.MaintainConnectionsOutbound(_networkParameters);
		}
	}
}
