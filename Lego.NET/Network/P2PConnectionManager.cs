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

		public static bool ListenForIncomingP2PConnections(IPAddress ipInterfaceToBind, int portToBind = Globals.LocalP2PListeningPort)
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
					_listenThread = new Thread(new ThreadStart(() =>
					{
						while (_listening)
						{
							try
							{
								Socket newConnectedPeerSock = _socket.Accept();
								//we've accepted a new peer create a new P2PConnection object to deal with them and we need to be sure to mark it as incoming so it gets stored appropriately
								P2PConnection p2pconnecting = new P2PConnection(((IPEndPoint)newConnectedPeerSock.RemoteEndPoint).Address, Globals.TCPMessageTimeout, newConnectedPeerSock, ((IPEndPoint)newConnectedPeerSock.RemoteEndPoint).Port, true);

								if (p2pconnecting.ConnectToPeer((ulong)Globals.Services.NODE_NETWORK, 1,(int)Globals.Relay.RELAY_ALWAYS))
								{
									AddP2PConnection(p2pconnecting);
								}
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
					_p2pInboundConnections.Remove(p2pConnection);
					return;
				}

				_p2pOutboundConnections.Remove(p2pConnection);
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
			foreach (P2PConnection pc in GetAllP2PConnections())
			{
				if (pc.RemoteIPAddress.ToString().Contains(peer.IPAddress.ToString()))
                {
					return true;
				}
			}

			return false;
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
			int peerCount = P2PConnectionManager.GetAllP2PConnections().Count;
			if (peerCount > 0) //protect from divide by zero
			{
				return Utilities.ToUnixTime(DateTime.UtcNow) + ((ulong)(P2PConnectionManager.NodeTimeOffset / P2PConnectionManager.GetAllP2PConnections().Count));
			}

			return Utilities.ToUnixTime(DateTime.UtcNow);
        }
	}
}
