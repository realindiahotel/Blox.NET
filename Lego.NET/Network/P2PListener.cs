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
	public static class P2PListener
	{
		private static Socket _socket;
		private static IPEndPoint _localEndPoint;
		private static bool _listening = false;
		private static Thread _listenThread;
		private static List<P2PConnection> _p2pConnections = new List<P2PConnection>();

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
								//we've accepted a new peer create a new P2PConnection object to deal with them
								P2PConnection p2pconnecting = new P2PConnection(((IPEndPoint)newConnectedPeerSock.RemoteEndPoint).Address, Globals.TCPMessageTimeout, newConnectedPeerSock, ((IPEndPoint)newConnectedPeerSock.RemoteEndPoint).Port, true);

								if (p2pconnecting.ConnectToPeer(1, 1, 1, true))
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

					Console.WriteLine("Exception: " + ex.Message);
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
				Console.WriteLine("Exception: " + ex.Message);
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
			_p2pConnections.Add(p2pConnection);
		}

		public static void RemoveP2PConnection(P2PConnection p2pConnection)
		{
			try
			{
				_p2pConnections.Remove(p2pConnection);
			}
#if (!DEBUG)
			catch
			{

			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}
			}
#endif
		}

		public static List<P2PConnection> GetP2PConnections()
		{
			return _p2pConnections;
		}
	}
}
