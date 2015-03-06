using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Bitcoin.BitcoinUtilities;
using System.Threading;
using Bitcoin.Lego.Data_Interface;

namespace Bitcoin.Lego.Network
{
	public static class P2PConnectionManager
	{
		private static Socket _socket;
		private static IPEndPoint _localEndPoint;
		private static bool _listening = false;
		private static Thread _listenThread;
		private static List<P2PConnection> _p2pInboundConnections = new List<P2PConnection>();
		private static List<P2PConnection> _p2pOutboundConnections = new List<P2PConnection>();
		private static long _nodeNetworkOffset = 0;

		public static async Task<bool> ListenForIncomingP2PConnectionsAsync(IPAddress ipInterfaceToBind, int portToBind = Globals.LocalP2PListeningPort)
		{
			if (!_listening)
			{
				try
				{
					LingerOption lo = new LingerOption(false, 0);
					_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					_socket.LingerState = lo;
					_listening = true;
					_localEndPoint = new IPEndPoint(ipInterfaceToBind, portToBind);
					if (_socket.IsBound)
					{
						_socket.Close();
					}
					_socket.Bind(_localEndPoint);
					_socket.Listen(1000);

					if (Globals.UPNPMapPort)
					{
						//try upnp port forward mapping
						await Connection.SetNATPortForwardingUPnPAsync(Globals.LocalP2PListeningPort, Globals.LocalP2PListeningPort);
					}
				
					_listenThread = new Thread(new ThreadStart(() =>
					{
						while (_listening)
						{
							try
							{
								Socket newConnectedPeerSock = _socket.Accept();

								//we've accepted a new peer create a new P2PConnection object to deal with them and we need to be sure to mark it as incoming so it gets stored appropriately
								P2PConnection p2pconnecting = new P2PConnection(((IPEndPoint)newConnectedPeerSock.RemoteEndPoint).Address, Globals.TCPMessageTimeout, newConnectedPeerSock, ((IPEndPoint)newConnectedPeerSock.RemoteEndPoint).Port, true);
							
								p2pconnecting.ConnectToPeer((ulong)Globals.Services.NODE_NETWORK, 1, (int)Globals.Relay.RELAY_ALWAYS);
                            }
							catch(SocketException sex)
							{
								//trap the exception "A blocking operation was interrupted by a call to WSACancelBlockingCall" thrown when we kill the listening socket but throw any others
								if (sex.ErrorCode != 10004)
								{
									//he said sex hehehehehehe
									throw sex;
								}
                            }							
						}
					}));
					_listenThread.IsBackground = true;
					_listenThread.Start();
				}
#if (!DEBUG)
				catch
				{
					_listening = false;
				}
#else
				catch (Exception ex)
				{
					_listening = false;

					Console.WriteLine("Exception Listening For Incoming Connections: " + ex.Message);
					if (ex.InnerException != null)
					{
						Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
					}
				}
#endif
			}

			return _listening;
		}

		public static bool StopListeningForIncomingP2PConnections()
		{
			try
			{
				_listening = false;
			
				_socket.Close();
            }
#if (!DEBUG)
			catch
			{
				return false;
			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception Stopping Listening For Incoming Connections: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}

				return false;
			}
#endif

			return true;
		}

		public static async void ConnectToPeers(List<IPAddress> peers)
		{
			//List<IPAddress> ips = await P2PConnection.GetDNSSeedIPAddressesAsync(Globals.DNSSeedHosts);
		}

		public static void AddP2PConnection(P2PConnection p2pConnection)
		{
			if (p2pConnection.InboundConnection)
			{
				_p2pInboundConnections.Add(p2pConnection);
				return;
			}		
				
			_p2pOutboundConnections.Add(p2pConnection);
		}

		public static void RemoveP2PConnection(P2PConnection p2pConnection)
		{
			try
			{
				if (p2pConnection.InboundConnection)
				{
					//is inbound
					_p2pInboundConnections.RemoveAll(delegate (P2PConnection p2p)
					{
						return p2p.RemoteIPAddress.Equals(p2pConnection.RemoteIPAddress) && p2p.RemotePort.Equals(p2pConnection.RemotePort);
					});

					return;
				}

				//is outbound 
				_p2pOutboundConnections.RemoveAll(delegate (P2PConnection p2p)
				{
					return p2p.RemoteIPAddress.Equals(p2pConnection.RemoteIPAddress) && p2p.RemotePort.Equals(p2pConnection.RemotePort);
				});	
			}
#if (!DEBUG)
			catch
			{

			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception Removing P2P Connection: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}
			}
#endif
		}

		public static List<P2PConnection> GetInboundP2PConnections()
		{
			return _p2pInboundConnections;
		}

		public static List<P2PConnection> GetOutboundP2PConnections()
		{
			return _p2pOutboundConnections;
		}

		public static List<P2PConnection> GetAllP2PConnections()
		{
			List<P2PConnection> poolConnections = new List<P2PConnection>(_p2pOutboundConnections);
			if (_p2pInboundConnections.Count > 0)
			{
				poolConnections.AddRange(_p2pInboundConnections);
			}
			return poolConnections;
		}

		public static bool ConnectedToPeer(PeerAddress peer)
		{
			List<P2PConnection> exists = GetAllP2PConnections().FindAll(delegate (P2PConnection pc)
			{
				return pc.RemoteIPAddress.ToString().Equals(peer.IPAddress.ToString()) && pc.RemotePort.Equals(peer.Port);
            });

			if (exists.Count > 0)
			{
				return true;
			}

			return false;
		}

		public static async Task<List<PeerAddress>> GetDNSSeedIPAddressesAsync(String[] DNSHosts)
		{
			List<IPAddress[]> dnsServerIPArrays = new List<IPAddress[]>();
			List<PeerAddress> ipAddressesOut = new List<PeerAddress>();

			pGetDatabaseIPs(ref ipAddressesOut);

			//couldn't get enough seeds from the db so get from dns
			if (ipAddressesOut.Count < Globals.SeedNodeCount)
			{
				try
				{

					foreach (String host in DNSHosts)
					{
						try
						{
							IPAddress[] addrs = await Dns.GetHostAddressesAsync(host);
							dnsServerIPArrays.Add(addrs);
						}
						catch
						{
							//allows for continuation if any dns server goes down
						}
					}

					foreach (IPAddress[] iparr in dnsServerIPArrays)
					{
						foreach (IPAddress ip in iparr)
						{
							if (ipAddressesOut.Count >= Globals.SeedNodeCount)
							{
								//we have enough break the loop
								break;
							}

							PeerAddress pa = new PeerAddress(ip, Globals.ProdP2PPort, (ulong)Globals.Services.NODE_NETWORK);

							if (!ipAddressesOut.Contains(pa))
							{
								ipAddressesOut.Add(pa);
							}
						}
					}
				}
				catch
				{
					//failed doing dns get so we drop through to hardcoded
				}

				//make sure I always have enough seed nodes else scrounge from hardcoded list
				pGetFillerIPsFromHardcoded(ref ipAddressesOut);
			}

			return ipAddressesOut;
		}

		public static List<PeerAddress> GetDNSSeedIPAddresses(String[] DNSHosts)
		{
			List<IPAddress[]> dnsServerIPArrays = new List<IPAddress[]>();
			List<PeerAddress> ipAddressesOut = new List<PeerAddress>();

			pGetDatabaseIPs(ref ipAddressesOut);

			//couldn't get enough seeds from the db so get from dns
			if (ipAddressesOut.Count < Globals.SeedNodeCount)
			{
				try
				{

					foreach (String host in DNSHosts)
					{
						try
						{
							dnsServerIPArrays.Add(Dns.GetHostAddresses(host));
						}
						catch
						{
							//allows for continuation if any dns server goes down
						}
					}

					foreach (IPAddress[] iparr in dnsServerIPArrays)
					{
						foreach (IPAddress ip in iparr)
						{
							if (ipAddressesOut.Count >= Globals.SeedNodeCount)
							{
								//we have enough break the loop
								break;
							}

							PeerAddress pa = new PeerAddress(ip, Globals.ProdP2PPort, (ulong)Globals.Services.NODE_NETWORK);

							if (!ipAddressesOut.Contains(pa))
							{
								ipAddressesOut.Add(pa);
							}
						}
					}
				}
				catch
				{
					//failed doing dns get so we drop through to hardcoded
				}

				//make sure I always have enough seed nodes else scrounge from hardcoded list
				pGetFillerIPsFromHardcoded(ref ipAddressesOut);
			}

			return ipAddressesOut;
		}

		private static void pGetDatabaseIPs(ref List<PeerAddress> ipAddressesOut)
		{
			int diff = (Globals.SeedNodeCount - ipAddressesOut.Count);

			//get newest addresses from database
			if (diff > 0)
			{
				using (DatabaseConnection dBC = new DatabaseConnection())
				{
					ipAddressesOut.AddRange(dBC.GetTopXAddresses(diff));
				}
			}
		}

		private static void pGetFillerIPsFromHardcoded(ref List<PeerAddress> ipAddressesOut)
		{
			int diff = Globals.SeedNodeCount - ipAddressesOut.Count;
			Random notCryptoRandom = new Random(DateTime.Now.Millisecond);

			//fallback on hardcoded seeds if need be
			if (diff > 0)
			{
				for (int i = 0; i < diff; i++)
				{
					int rIndx = notCryptoRandom.Next(0, HardSeedList.SeedIPStrings.Length);
					PeerAddress pa = new PeerAddress(IPAddress.Parse(HardSeedList.SeedIPStrings[rIndx]), Globals.ProdP2PPort, (ulong)Globals.Services.NODE_NETWORK);
					ipAddressesOut.Add(pa);
				}
			}
		}

		public static void AddToNodeTimeOffset(long add)
		{
			_nodeNetworkOffset += add;
		}

		public static void SubtractFromNodeTimeOffset(long subtract)
		{
			_nodeNetworkOffset -= subtract;
		}

		public static long NodeTimeOffset
		{
			get
			{
				return _nodeNetworkOffset;
			}
		}

		public static ulong GetUTCNowWithOffset()
		{
			int peerCount = GetAllP2PConnections().Count;
			if (peerCount > 0) //protect from divide by zero
			{
				return Utilities.ToUnixTime(DateTime.UtcNow) + ((ulong)(NodeTimeOffset / GetAllP2PConnections().Count));
			}

			return Utilities.ToUnixTime(DateTime.UtcNow);
        }
	}
}
