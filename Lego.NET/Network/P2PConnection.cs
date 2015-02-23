using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Protocol_Messages;
using Bitcoin.Lego.Data_Interface;

namespace Bitcoin.Lego.Network
{
    public class P2PConnection : Connection
    {
		private VersionMessage _myVersionMessage;
		private VersionMessage _theirVersionMessage;
		private long _peerTimeOffset;
		private bool _inbound;
		private Thread _recieveMessagesThread;
		private Thread _heartbeatThread;
		private Thread _killDeadClientNoHeartbeatThread;
		private Thread _addrFartThread;
        private DateTime _lastRecievedMessage;
		private List<PeerAddress> _memAddressPool = new List<PeerAddress>();
		private int _addressCursor = 0;

		/// <summary>
		/// New P2PConnection Object
		/// </summary>
		/// <param name="remoteIp">IP Address we want to connect to</param>
		/// <param name="connectionTimeout">How many milliseconds to wait for a TCP message</param>
		/// <param name="socket">The socket to use for the data stream, if we don't have one use new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)</param>
		/// <param name="remotePort">The remote port to connect to</param>
		/// <param name="inbound">Is this an inbount P2P connection or are we connecting out</param>
		public P2PConnection(IPAddress remoteIp, int connectionTimeout, Socket socket, int remotePort = Globals.ProdP2PPort, bool inbound = false) :base(remoteIp, remotePort, connectionTimeout, socket)
		{
			_inbound = inbound;
			
			//fully loaded man https://www.youtube.com/watch?v=dLIIKrJ6nnI
		}

		public bool ConnectToPeer(ulong services, uint blockHeight, int relay)
		{
			try
			{
				_addrFartThread = new Thread(new ThreadStart(pSendAddrFart));
				_addrFartThread.IsBackground = true;	

				if (_inbound) //the connection is incoming so we recieve their version message first
				{
					pConnectAndSetVersionAndStreams(services, blockHeight, relay);

					var message = Recieve();

					if (message.GetType().Name.Equals("VersionMessage"))
					{
						_theirVersionMessage = (VersionMessage)message;

						//send my version message
						Send(_myVersionMessage);

						//not allowing strict verack on inbound as some don't respond with verack to their outbound connections

						//send verack
						if (pVerifyVersionMessage())
						{
							//send addr
							_addrFartThread.Start();

							//start listening for messages
							pMessageListener();
						}
						else
						{
							CloseConnection(true);
						}

					}
					else //something other then their version message...uh oh...not friend... kill the connection
					{
						CloseConnection(true);
					}
				}
				else //the connection is outgoing so we send our version message first
				{
					pConnectAndSetVersionAndStreams(services, blockHeight, relay);

					Send(_myVersionMessage);
					var message = Recieve();

					//we have their version message yay :)
					if (message.GetType().Name.Equals("VersionMessage"))
					{
						_theirVersionMessage = (VersionMessage)message;

						//if strict verack is on we listen for verack and if we don't get it we close the connection
						if (Globals.StrictVerackOutbound)
						{
							pCheckVerack();
						}

						if (!pVerifyVersionMessage())
						{
							CloseConnection(true);
						}
						else
						{
							//send addr will also happen every 24 hrs by default
							_addrFartThread.Start();

							//start listening for messages
							pMessageListener();
						}

					}
					else //something other then their version message...uh oh...not friend... kill the connection
					{
						CloseConnection(true);
					}
				}
			}
#if (!DEBUG)
			catch
			{

			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception Connect To Peer: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}
			}
#endif
			try
			{
				if (Socket.Connected)
				{
					P2PConnectionManager.AddP2PConnection(this);
					//calculate time offset and adjust accordingly
					_peerTimeOffset = (long)(TheirVersionMessage.Time - Utilities.ToUnixTime(DateTime.UtcNow));
					P2PConnectionManager.AddToNodeTimeOffset(_peerTimeOffset);
				}
			}
			catch
			{

			}	
			
			return Socket.Connected;
		}

		private void pConnectAndSetVersionAndStreams(ulong services, uint blockHeight, int relay)
		{
			if (!Socket.Connected)
			{
				Socket.Connect(RemoteEndPoint);
			}

			//our in and out streams to the underlying socket
			DataIn = new NetworkStream(Socket, FileAccess.Read);
			DataOut = new NetworkStream(Socket, FileAccess.Write);

			Socket.SendTimeout = Socket.ReceiveTimeout = ConnectionTimeout;

			//add packetMagic for testnet
			_myVersionMessage = new VersionMessage(RemoteIPAddress, Socket, services, RemotePort, blockHeight, relay);

			//set thread for heartbeat
			_heartbeatThread = new Thread(new ThreadStart(pSendHeartbeat));
			_heartbeatThread.IsBackground = true;
			_heartbeatThread.Start();

			//set thread for kill on no heartbeat
			_killDeadClientNoHeartbeatThread = new Thread(new ThreadStart(pNoHeartbeatKillDeadClient));
			_killDeadClientNoHeartbeatThread.IsBackground = true;
			_killDeadClientNoHeartbeatThread.Start();
		}

		private bool pCheckVerack()
		{
			var message = Recieve();

			if (!message.GetType().Name.Equals("VersionAck"))
			{
				CloseConnection(true);
				return false;
			}

			return true;
		}

		private bool pVerifyVersionMessage(bool sendVerack=true)
		{
			//I have their version so time to make sure everything is ok and either verack or reject
			if (_theirVersionMessage != null && Socket.Connected)
			{

				//The client is reporting a version too old for our liking
				if (_theirVersionMessage.ClientVersion < Globals.MinimumAcceptedClientVersion)
				{
					Send(new RejectMessage("version", RejectMessage.ccode.REJECT_OBSOLETE, "Client version needs to be at least " + Globals.MinimumAcceptedClientVersion));
					return false;
				}
				else if (!Utilities.UnixTimeWithin70MinuteThreshold(_theirVersionMessage.Time, out _peerTimeOffset)) //check the unix time timestamp isn't outside 70 minutes, we don't wan't anyone outside 70 minutes anyway....Herpes
				{
					//their time sucks sent a reject message and close connection
					Send(new RejectMessage("version", RejectMessage.ccode.REJECT_INVALID, "Your unix timestamp is fucked up", ""));
					return false;
				}
				else if (_theirVersionMessage.Nonce==_myVersionMessage.Nonce && !Globals.AllowP2PConnectToSelf)
				{
					Send(new RejectMessage("version", RejectMessage.ccode.REJECT_DUPLICATE, "Connecting to self has been disabled", ""));
					return false;
				}
				else //we're good send verack
				{
					if (sendVerack)
					{
						Send(new VersionAck());
					}
					return true;
				}
				
			}

			return false;
		}

		private void pMessageListener()
		{
			_recieveMessagesThread = new Thread(new ThreadStart(() =>
			{
				while (Socket.Connected)
				{
					try
					{
						var message = Recieve();

						//process the message appropriately
						switch (message.GetType().Name)
						{
							case "Ping":
								//send pong responce to ping
								Send(new Pong(((Ping)message).Nonce));
								break;

							case "Pong":
								//we have pong
								break;

							case "RejectMessage":
#if (DEBUG)						//if we run in debug I spew out to console, in production no to save from an attack that slows us down writing to the console
								Console.WriteLine(((RejectMessage)message).Message + " - " + ((RejectMessage)message).CCode + " - " + ((RejectMessage)message).Reason + " - " + ((RejectMessage)message).Data);
#endif
								break;

							case "AddressMessage":
								Thread addrThread = new Thread(new ThreadStart(() =>
								{
									AddToMemAddressPool(((AddressMessage)message).Addresses);
								}));
								addrThread.IsBackground = true;
								addrThread.Start();
								break;

							case "GetAddresses":
								Thread getAddrThread = new Thread(new ThreadStart(() =>
								{
									PeerAddress my_net_addr = GetMyExternalIP(_myVersionMessage.LocalServices);
									List<PeerAddress> addrOne = new List<PeerAddress>() { my_net_addr };
									List<PeerAddress> addrTwo = new List<PeerAddress>();
									List<PeerAddress> addrThree = new List<PeerAddress>();
									int maxAddresses = 2500;

									//to do spawn a new thread and handle dishing out addresses
									if (_memAddressPool.Count >= (maxAddresses-1))
									{
										addrOne.AddRange(_memAddressPool.GetRange(0, 999));
										addrTwo.AddRange(_memAddressPool.GetRange(999, 1000));
										addrThree.AddRange(_memAddressPool.GetRange(1999, 500));
									}
									else
									{
										int diff = (maxAddresses - _memAddressPool.Count);

										int idx = 0;
										while (idx < _memAddressPool.Count)
										{
											if (idx < 999)
											{
												addrOne.Add(_memAddressPool[idx]);
											}
											else if (idx < 1999)
											{
												addrTwo.Add(_memAddressPool[idx]);
											}
											else if(idx <2499)
											{
												addrThree.Add(_memAddressPool[idx]);
											}

											idx++;
										}

										using (DatabaseConnection dBC = new DatabaseConnection())
										{
											foreach(PeerAddress pa in dBC.GetTopXAddresses(diff))
                                            {
												if (idx < 999)
												{
													addrOne.Add(pa);
												}
												else if (idx < 1999)
												{
													addrTwo.Add(pa);
												}
												else if (idx < 2499)
												{
													addrThree.Add(pa);
												}

												idx++;
											}
                                        }

										Send(new AddressMessage(addrOne));
										Send(new AddressMessage(addrTwo));
										Send(new AddressMessage(addrThree));
									}														
								}));
								getAddrThread.IsBackground = true;
								getAddrThread.Start();
								break;

							case "NullMessage":
								try
								{
									//error or strange message, close connection, if aggressive reconnect on we shall reconnect and start anew
									this.CloseConnection(true);

									if (Globals.AggressiveReconnect)
									{
										P2PConnection p2pc = new P2PConnection(this.RemoteIPAddress, Globals.TCPMessageTimeout, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), TheirVersionMessage.TheirAddr.Port);
										if (p2pc.ConnectToPeer(MyVersionMessage.LocalServices, MyVersionMessage.StartBlockHeight, MyVersionMessage.Relay))
										{
											P2PConnectionManager.AddP2PConnection(p2pc);
										}
                                    }
								}
								catch
								{

								}
								return;
								
							default: //if it's something we don't know about we just ignore it
								break;
						}
					}
#if (!DEBUG)
					catch
					{

					}
#else
					catch (Exception ex)
					{
						Console.WriteLine("Exception Message Listener: " + ex.Message);
						if (ex.InnerException != null)
						{
							Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
						}
					}
#endif
				}
			}));
			_recieveMessagesThread.IsBackground = true;
			_recieveMessagesThread.Start();
		}

		public void CloseConnection(bool forget=false)
		{
			try
			{
				_heartbeatThread.Abort();
			}
			catch
			{

			}

			try
			{
				_killDeadClientNoHeartbeatThread.Abort();
			}
			catch
			{

			}

			if (Socket.Connected)
			{
				Socket.Close();
			}

			if (forget)
			{
				P2PConnectionManager.RemoveP2PConnection(this);
				P2PConnectionManager.SubtractFromNodeTimeOffset(_peerTimeOffset);
			}
		}

		private void pSendAddrFart()
		{
			PeerAddress their_net_addr = new PeerAddress(RemoteIPAddress, RemotePort, TheirVersionMessage.LocalServices);
			//make sure I put their address in the db if it's not in there
			using (DatabaseConnection dBC = new DatabaseConnection())
			{
				dBC.AddAddress(their_net_addr);
			}

			while (Socket.Connected)
			{
				try
				{	
					//send my addr to the peer I've just connected too	
					PeerAddress my_net_addr = GetMyExternalIP(_myVersionMessage.LocalServices);
					Send(new AddressMessage(new List<PeerAddress>() {my_net_addr }));

					//send the addr of the peer I have just connected too to all other peers					
					BradcastSend(new AddressMessage(new List<PeerAddress>() { their_net_addr }),new List<P2PConnection>() { this });

					Thread.CurrentThread.Join(Globals.AddrFartInterval);
				}
#if (!DEBUG)
				catch
				{

				}
#else
				catch (Exception ex)
				{
					Console.WriteLine("Exception Send My Addr Announce: " + ex.Message);
					if (ex.InnerException != null)
					{
						Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
					}
				}
#endif
			}
		}

		private void pSendHeartbeat()
		{
			while (Socket.Connected)
			{
				try
				{
					Thread.CurrentThread.Join(Globals.HeartbeatTimeout); //send a heartbeat after the specified interval

					if (Globals.HeartbeatKeepAlive)
					{
						Send(new Ping());
					}
				}
#if (!DEBUG)
				catch
				{

				}
#else
				catch (Exception ex)
				{
					Console.WriteLine("Exception Send Heartbeat: " + ex.Message);
					if (ex.InnerException != null)
					{
						Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
					}
				}
#endif
			}
		}

		private void pNoHeartbeatKillDeadClient()
		{
			int timeWait = (Globals.HeartbeatTimeout * 3); //90 minutes for kill (3 times specified heartbeat signal) so I just multiply heartbeat by 3

            while (Socket.Connected)
			{
				try
				{
					Thread.CurrentThread.Join(timeWait);
					
					if (Globals.DeadIfNoHeartbeat)
					{
						TimeSpan timeLapsed = DateTime.UtcNow - _lastRecievedMessage;

						TimeSpan timeOut = new TimeSpan(timeWait*10000);

						if (timeLapsed > timeOut)
						{
							//no heartbeat time to kill connection
							CloseConnection(true);
#if (DEBUG)
							Console.WriteLine("Inactive Client Killed: "+RemoteIPAddress+ ":"+RemotePort);
#endif
						}

						timeWait = (Globals.HeartbeatTimeout*3) - Convert.ToInt32(timeLapsed.TotalMilliseconds);
					}
				}
#if (!DEBUG)
				catch
				{

				}
#else
				catch (Exception ex)
				{
					Console.WriteLine("Exception No Heartbeat Kill: " + ex.Message);
					if (ex.InnerException != null)
					{
						Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
					}
				}
#endif
			}
		}

		public static async Task<List<IPAddress>> GetDNSSeedIPAddressesAsync(String[] DNSHosts)
		{
			List<IPAddress[]> dnsServerIPArrays = new List<IPAddress[]>();
			List<IPAddress> ipAddressesOut = new List<IPAddress>();

			foreach (String host in DNSHosts)
			{
				IPAddress[] addrs = await Dns.GetHostAddressesAsync(host);
				dnsServerIPArrays.Add(addrs);
			}

			foreach (IPAddress[] iparr in dnsServerIPArrays)
			{
				foreach (IPAddress ip in iparr)
				{
					if (!ipAddressesOut.Contains(ip))
					{
						ipAddressesOut.Add(ip);
					}
				}
			}

			//make sure I always have at least 100 seed nodes to check against
			pGetFillerIPs(ref ipAddressesOut);

			return ipAddressesOut;
		}

		private static void pGetFillerIPs(ref List<IPAddress> ipAddressesOut)
		{
			int weWantThisMany = 100;
			Random notCryptoRandom = new Random(DateTime.Now.Millisecond);
			int diff = (weWantThisMany - ipAddressesOut.Count);

			//get newest addresses from database
			if (diff > 0)
			{
				using (DatabaseConnection dBC = new DatabaseConnection())
				{
					foreach (PeerAddress pa in dBC.GetTopXAddresses(diff))
					{
						ipAddressesOut.Add(pa.IPAddress);
					}
				}
			}

			diff = weWantThisMany - ipAddressesOut.Count;

			//fallback on hardcoded seeds if need be
			if (diff > 0)
			{
				for (int i = 0; i < diff; i++)
				{
					int rIndx = notCryptoRandom.Next(0, (HardSeedList.SeedIPStrings.Length - 1));
					ipAddressesOut.Add(IPAddress.Parse(HardSeedList.SeedIPStrings[rIndx]));
				}
			}
		}

		public static List<IPAddress> GetDNSSeedIPAddresses(String[] DNSHosts)
		{
			List<IPAddress[]> dnsServerIPArrays = new List<IPAddress[]>();
			List<IPAddress> ipAddressesOut = new List<IPAddress>();

			foreach (String host in DNSHosts)
			{

				dnsServerIPArrays.Add(Dns.GetHostAddresses(host));
			}

			foreach (IPAddress[] iparr in dnsServerIPArrays)
			{
				foreach (IPAddress ip in iparr)
				{
					if (!ipAddressesOut.Contains(ip))
					{
						ipAddressesOut.Add(ip);
					}
				}
			}

			//make sure I always have at least 100 seed nodes to check against
			pGetFillerIPs(ref ipAddressesOut);

			return ipAddressesOut;
		}


		/// <summary>
		/// Broadcasts a message to all connected P2P peers
		/// </summary>
		/// <param name="message">Message to broadcast</param>
		/// <param name="exclusions">P2PConnections you don't want to recieve the message for example if its a relayed message we don't want to return to sender</param>
		public static void BradcastSend(Message message, List<P2PConnection> exclusions)
		{
			Thread broadcastMessageThread = new Thread(new ThreadStart(() =>
			{
				foreach (P2PConnection p2p in P2PConnectionManager.GetAllP2PConnections())
				{
					if (!exclusions.Contains(p2p))
					{
						p2p.Send(message);
					}
				}
			}));
			broadcastMessageThread.IsBackground = true;
			broadcastMessageThread.Start();
		}

		/// <summary>
		///  Broadcasts a message to all connected P2P peers
		/// </summary>
		/// <param name="message">Message to broadcast</param>
		public static void BradcastSend(Message message)
		{
			Thread broadcastMessageThread = new Thread(new ThreadStart(() =>
			{
				foreach (P2PConnection p2p in P2PConnectionManager.GetAllP2PConnections())
				{
					p2p.Send(message);
				}
			}));
			broadcastMessageThread.IsBackground = true;
			broadcastMessageThread.Start();
		}

		public bool Send(Message message)
		{
			if (Socket.Connected)
			{
				int attempt = 1;

				while (attempt <= Globals.RetrySendTCPOnError)
				{
					try
					{
						WriteMessage(message);
						return true;
					}
#if (!DEBUG)
					catch
					{

					}
#else
					catch (Exception ex)
					{
						Console.WriteLine("Exception Send Message Attempt "+attempt+ ": "+ ex.Message);
						if (ex.InnerException != null)
						{
							Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
						}
					}
#endif
					attempt++;
				}
			}
			else
			{
				this.CloseConnection(true);
			}

			return false;
		}

		public Message Recieve()
		{
			if (Socket.Connected)
			{
				int attempt = 1;

				while (attempt <= Globals.RetryRecieveTCPOnError)
				{
					try
					{
						var msg = ReadMessage();
						_lastRecievedMessage = DateTime.UtcNow;
						return msg;
					}
#if (!DEBUG)
					catch
					{

					}
#else
					catch (Exception ex)
					{
						Console.WriteLine("Exception Recieve Message Attempt " + attempt + ": " + ex.Message);
						if (ex.InnerException != null)
						{
							Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
						}
					}
#endif
					attempt++;
				}
			}
			
			return new NullMessage();
		}

		private void AddToMemAddressPool(IList<PeerAddress> addresses)
		{
			foreach(PeerAddress pa in addresses)
            {
				try
				{
					if (_addressCursor >= Globals.AddressMemPoolMax)
					{
						_addressCursor = 0;
					}

					if (!pa.IsExpired)
					{
						if (_memAddressPool.Count < Globals.AddressMemPoolMax)
						{
							_memAddressPool.Add(pa);
						}
						else
						{
							_memAddressPool[_addressCursor] = pa;
							_addressCursor++;
						}
					}
				}
#if (!DEBUG)
				catch
				{

				}
#else
				catch (Exception ex)
				{
					Console.WriteLine("Exception Add Addr To Mem Pool: "+ ex.Message);
					if (ex.InnerException != null)
					{
						Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
					}
				}
#endif
			}
		}

		public VersionMessage MyVersionMessage
		{
			get
			{
				return _myVersionMessage;
			}
		}

		public VersionMessage TheirVersionMessage
		{
			get
			{
				return _theirVersionMessage;
			}
		}
		
		public long PeerTimeOffset
		{
			get
			{
				return _peerTimeOffset;
			}
		}
		
		public bool Connected
		{
			get
			{
				return Socket.Connected;
			}
		}

		public bool InboundConnection
		{
			get
			{
				return _inbound;
			}
		}
	}
}
