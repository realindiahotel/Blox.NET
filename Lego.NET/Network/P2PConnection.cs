using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
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
		private Thread _addrFartThread;
        private ulong _lastRecievedMessageTime;
		private ulong _lastRelayedAddrMessageTime;
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
					//check it's not a duplicate connection

					if (P2PConnectionManager.ConnectedToPeer(new PeerAddress(RemoteIPAddress, RemotePort, services)))
					{
						throw new Exception("Already Inbound Connection Matching "+RemoteIPAddress.ToString()+ ":"+RemotePort + " Not Attempting To Connect");
					}

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
					//check it's not a duplicate connection
					if (P2PConnectionManager.ConnectedToPeer(new PeerAddress(RemoteIPAddress, RemotePort, services)))
					{
						throw new Exception("Already Outbound Connection Matching " + RemoteIPAddress.ToString() + ":" + RemotePort + " Not Attempting To Connect");
					}

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

							PeerAddress their_net_addr = new PeerAddress(RemoteIPAddress, RemotePort, TheirVersionMessage.LocalServices);

							//make sure I put their address in the db if it's not in there, because we love to remember peers we can connect to :)
							using (DatabaseConnection dBC = new DatabaseConnection())
							{
								dBC.AddAddress(their_net_addr);
							}

							//send the addr of the peer I have just connected too to all other peers as I'm sure they'd like to know about a connectable peer as well :)					
							BradcastSend(new AddressMessage(new List<PeerAddress>() { their_net_addr }), new List<P2PConnection>() { this });
						}

					}
					else //something other then their version message...uh oh...not friend... kill the connection
					{
						CloseConnection(true);
					}
				}

				if (Socket.Connected)
				{
					//calculate time offset and adjust accordingly
					_peerTimeOffset = (long)(TheirVersionMessage.Time - Utilities.ToUnixTime(DateTime.UtcNow));
					P2PConnectionManager.AddToNodeTimeOffset(_peerTimeOffset);
					P2PConnectionManager.AddP2PConnection(this);
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

			Socket.ReceiveTimeout = ConnectionTimeout;
			Socket.SendTimeout = 2000; //2 second timeout for sending messages
			Socket.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);

			//add packetMagic for testnet
			_myVersionMessage = new VersionMessage(RemoteIPAddress, Socket, services, RemotePort, blockHeight, relay);

			//set thread for heartbeat
			_heartbeatThread = new Thread(new ThreadStart(pSendHeartbeat));
			_heartbeatThread.IsBackground = true;
			_heartbeatThread.Start();

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
					var message = Recieve();

					try
					{						

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
									//add to mempool and get unseen addrs
									List<PeerAddress> paList = AddToMemAddressPool(((AddressMessage)message).Addresses);

									//we will relay any unexpired addrs if the message has no more than 100 unseen addrs in it
									if (paList.Count <= 100)
									{
										List<PeerAddress> peers = paList.Where(delegate (PeerAddress pa)
										{
											return pa.IsRelayExpired.Equals(false);
										}).ToList();

										AddressMessage addrOut = new AddressMessage(peers);						

										//we wont relay at a rate faster than every 3 seconds from a connection to avoid a broadcast flood attack
										if (addrOut.Addresses.Count > 0 && _lastRelayedAddrMessageTime < (P2PConnectionManager.GetUTCNowWithOffset()-3))
										{
											BradcastSend(addrOut, new List<P2PConnection> { this });
											_lastRelayedAddrMessageTime = P2PConnectionManager.GetUTCNowWithOffset();
										}
									}
                                }));
								addrThread.IsBackground = true;
								addrThread.Start();
								break;

							//here we first get trusted addressed from DB that we have connected too and know are safe (as defined by blacklist behavior)
							//then if we are connected to at least three other peers we will get addresses from each and every peer untill we hit out 2500 getaddr limit
							case "GetAddresses":
								Thread getAddrThread = new Thread(new ThreadStart(() =>
								{
									List<PeerAddress> addressesToFireOut = new List<PeerAddress>();
									int maxGetAddresses = 2500;

									List<P2PConnection> allConnections = new List<P2PConnection>(P2PConnectionManager.GetAllP2PConnections());

									//get newest addrs from trusted addresses in DB as this is better than untrusted addresses relayed

									if (Globals.EnableListenForPeers)
									{
										//if I'm listening for peers add my addr to the response
										PeerAddress my_net_addr = GetMyExternalIP(_myVersionMessage.LocalServices);
										addressesToFireOut.Add(my_net_addr);
									}

									try
									{
										using (DatabaseConnection dbC = new DatabaseConnection())
										{
											addressesToFireOut.AddRange(dbC.GetTopXAddresses(maxGetAddresses - addressesToFireOut.Count));
										}
									}
#if (!DEBUG)
									catch
									{

									}
#else
									catch (Exception ex)
									{
										Console.WriteLine("Exception Getting Addresses From Database For Getaddr Response: " + ex.Message);
										if (ex.InnerException != null)
										{
											Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
										}
									}
#endif
									//now we pull addresses from our connected peers buckets, we get from every bucket no discrimination, we only do this if connected to at least 3 peers

									if (allConnections.Count >= 3)
									{

										//max addresses we still need to find
										int diff = maxGetAddresses - addressesToFireOut.Count;
										List<List<PeerAddress>> buckets = new List<List<PeerAddress>>();

										int largestBucketSize = 0;

										//build the buckets list
										foreach (P2PConnection p2p in allConnections)
										{
											List<PeerAddress> bucketResidenceLadyOfTheHouseSpeaking = p2p.MemAddressPool;

											if (bucketResidenceLadyOfTheHouseSpeaking.Count > largestBucketSize)
											{
												largestBucketSize = bucketResidenceLadyOfTheHouseSpeaking.Count;
											}

											//sort this bucket by time
											bucketResidenceLadyOfTheHouseSpeaking.Sort(delegate (PeerAddress px, PeerAddress py)
											{
												return py.Time.CompareTo(px.Time);
											});

											//add time sorted bucket to bucket list
											buckets.Add(bucketResidenceLadyOfTheHouseSpeaking);
										}

										//add from each bucket
										for (int i = 0; i < largestBucketSize; i++)
										{
											foreach (List<PeerAddress> bucket in buckets)
											{
												if (diff > 0)
												{
													try
													{
														//we are not out of bounds of the bucket ant the addr doesn't already exist in output set
														if (i < bucket.Count && !addressesToFireOut.Any(delegate (PeerAddress pa) { return bucket[i].IPAddress.Equals(pa.IPAddress) && bucket[i].Port.Equals(pa.Port) && bucket[i].Time.Equals(pa.Time); }))
														{
															addressesToFireOut.Add(bucket[i]);
															diff--;
														}
													}
													catch
													{

													}
												}
											}
										}
									}

									//send our up to three messages/2500 addresses
									if (addressesToFireOut.Count > 0 )
									{
										if (addressesToFireOut.Count >= 1000)
										{
											//send the first lot of 1000
											Send(new AddressMessage(addressesToFireOut.Take(1000).ToList()));
										}
										else
										{
											//send what we can
											Send(new AddressMessage(addressesToFireOut.Take(addressesToFireOut.Count).ToList()));
										}
									}

									if (addressesToFireOut.Count > 1000)
									{
										if (addressesToFireOut.Count >= 2000)
										{
											//send the second lot of 1000
											Send(new AddressMessage(addressesToFireOut.Skip(1000).Take(1000).ToList()));
										}
										else
										{
											//send what we can
											Send(new AddressMessage(addressesToFireOut.Skip(1000).Take(addressesToFireOut.Count - 1000).ToList()));
										}
									}

									if (addressesToFireOut.Count > 2000)
									{
										if (addressesToFireOut.Count >= maxGetAddresses)
										{
											//send the final 500 message address
											Send(new AddressMessage(addressesToFireOut.Skip(2000).Take(maxGetAddresses-2000).ToList()));
										}
										else
										{
											//send what we can
											Send(new AddressMessage(addressesToFireOut.Skip(2000).Take(addressesToFireOut.Count - 2000).ToList()));
										}
									}													
								}));
								getAddrThread.IsBackground = true;
								getAddrThread.Start();
								break;

							case "NullMessage":
								try
								{
#if (DEBUG)
									Console.WriteLine("Killing Connection " + RemoteIPAddress.ToString() + " : " + RemotePort + " Couldn't Recieve Message");
#endif
									//error or strange message, close connection, if aggressive reconnect on we shall reconnect and start anew if outbound
									CloseConnection(true);

									if (Globals.AggressiveReconnect && ! InboundConnection)
									{
										P2PConnection p2pc = new P2PConnection(RemoteIPAddress, Globals.HeartbeatTimeout, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), TheirVersionMessage.TheirAddr.Port);
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
						if (message.GetType().Name.Equals("GetAddresses"))
                        {

						}
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
			_recieveMessagesThread.Priority=ThreadPriority.Highest;
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
			while (Socket.Connected)
			{
				try
				{   
					//I only want to send my addr if I am listening for peers, this saves me sending myself out across the network when I am uncontactable
					if (Globals.EnableListenForPeers)
					{
						//send my addr to the peer I've just connected too, remember i include the port I am LISTENING on so they can connect to me	
						PeerAddress my_net_addr = GetMyExternalIP(_myVersionMessage.LocalServices, Globals.LocalP2PListeningPort);
						Send(new AddressMessage(new List<PeerAddress>() { my_net_addr }));
						int getaddrDelay = new Random(Environment.TickCount).Next(500, 5001);
						Thread.Sleep(getaddrDelay);
						Send(new GetAddresses());
						Thread.Sleep((Globals.AddrFartInterval - getaddrDelay));
#if (DEBUG)
						Console.WriteLine("Send Addr Wakes Up");
#endif
					}
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
					Thread.Sleep(Globals.HeartbeatTimeout); //send a heartbeat after the specified interval

					if (Globals.HeartbeatKeepAlive)
					{
						Send(new Ping());

#if (DEBUG)
						Console.WriteLine("Send Heartbeat Ping");
#endif
					}

#if (DEBUG)
					else
					{
						Console.WriteLine("Heartbeat Send Is Off");
					}
#endif
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

		public static void BradcastSendToOutbound(Message message, List<P2PConnection> exclusions)
		{
			Thread broadcastMessageThread = new Thread(new ThreadStart(() =>
			{
                // by creating a new list we allow for the case where clients disconnect during broadcast
                List < P2PConnection > allOutConnections = new List<P2PConnection>();
				allOutConnections.AddRange(P2PConnectionManager.GetOutboundP2PConnections());
 
				foreach (P2PConnection exl in exclusions)
				{
					allOutConnections.RemoveAll(delegate (P2PConnection p2p)
					{
						return p2p.RemoteIPAddress.Equals(exl.RemoteIPAddress) && p2p.RemotePort.Equals(exl.RemotePort);
					});
				}

				foreach (P2PConnection p2p in allOutConnections)
				{
					p2p.Send(message);
				}
			}));
			broadcastMessageThread.IsBackground = true;
			broadcastMessageThread.Start();
		}

		public static void BradcastSendToOutbound(Message message)
		{
			Thread broadcastMessageThread = new Thread(new ThreadStart(() =>
			{
				//by creating a new list we allow for the case where clients disconnect during broadcast
				List<P2PConnection> allOutConnections = new List<P2PConnection>();
				allOutConnections.AddRange(P2PConnectionManager.GetOutboundP2PConnections());

				foreach (P2PConnection p2p in allOutConnections)
				{
					p2p.Send(message);
				}
			}));
			broadcastMessageThread.IsBackground = true;
			broadcastMessageThread.Start();
		}

		public static void BradcastSendToInbound(Message message, List<P2PConnection> exclusions)
		{
			Thread broadcastMessageThread = new Thread(new ThreadStart(() =>
			{
				//by creating a new list we allow for the case where clients disconnect during broadcast
				List<P2PConnection> allInConnections = new List<P2PConnection>();
				allInConnections.AddRange(P2PConnectionManager.GetInboundP2PConnections());

				foreach (P2PConnection exl in exclusions)
				{
					allInConnections.RemoveAll(delegate (P2PConnection p2p)
					{
						return p2p.RemoteIPAddress.Equals(exl.RemoteIPAddress) && p2p.RemotePort.Equals(exl.RemotePort);
					});
				}

				foreach (P2PConnection p2p in allInConnections)
				{
                    p2p.Send(message);
				}
			}));
			broadcastMessageThread.IsBackground = true;
			broadcastMessageThread.Start();
		}

		public static void BradcastSendToInbound(Message message)
		{
			Thread broadcastMessageThread = new Thread(new ThreadStart(() =>
			{
				//by creating a new list we allow for the case where clients disconnect during broadcast
				List<P2PConnection> allInConnections = new List<P2PConnection>();
				allInConnections.AddRange(P2PConnectionManager.GetInboundP2PConnections());

				foreach (P2PConnection p2p in allInConnections)
				{
					p2p.Send(message);
				}
			}));
			broadcastMessageThread.IsBackground = true;
			broadcastMessageThread.Start();
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
				//by creating a new list we allow for the case where clients disconnect during broadcast
				List<P2PConnection> allConnections = new List<P2PConnection>(P2PConnectionManager.GetAllP2PConnections());

				foreach (P2PConnection exl in exclusions)
				{
					allConnections.RemoveAll(delegate (P2PConnection p2p)
					{
						return p2p.RemoteIPAddress.Equals(exl.RemoteIPAddress) && p2p.RemotePort.Equals(exl.RemotePort);
					});
				}
	
				foreach (P2PConnection p2p in allConnections)
				{
					p2p.Send(message);
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
				//by creating a new list we allow for the case where clients disconnect during broadcast
				List<P2PConnection> allConnections = new List<P2PConnection>();
				allConnections.AddRange(P2PConnectionManager.GetAllP2PConnections());

				foreach (P2PConnection p2p in allConnections)
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
#if (DEBUG)
				Console.WriteLine("Killing Connection " + this.RemoteIPAddress.ToString() + " : "+this.RemotePort + " Couldn't Send Message Socket Disconnected");
#endif
				CloseConnection(true);
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
						_lastRecievedMessageTime = P2PConnectionManager.GetUTCNowWithOffset();
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

		private List<PeerAddress> AddToMemAddressPool(IList<PeerAddress> addresses)
		{
			List<PeerAddress> relayPeers = new List<PeerAddress>();

			foreach (PeerAddress pa in addresses)
			{
				
				try
				{
					if (_addressCursor >= Globals.AddressMemPoolMax)
					{
						_addressCursor = 0;
					}

					//remove any duplicate older addresses exist if it is older
					int deleted = _memAddressPool.RemoveAll(delegate (PeerAddress pa2)
					{
						return pa.IPAddress.Equals(pa2.IPAddress) && pa.Port.Equals(pa2.Port) && pa.Time > pa2.Time;
					});

					//now we have removed old ones see if any remainh
					List<PeerAddress> duplicates = _memAddressPool.FindAll(delegate (PeerAddress pa2)
					{
						return pa.IPAddress.Equals(pa2.IPAddress) && pa.Port.Equals(pa2.Port);
					});

					deleted += duplicates.Count;

					//if none remain we add
					if (duplicates.Count <= 0)
					{
						//we removed older duplicates so add new
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

					if (deleted == 0)
					{
						relayPeers.Add(pa);
					}
				}
#if (!DEBUG)
				catch
				{
				
				}
#else
				catch (Exception ex)
				{
					Console.WriteLine("Exception Add Addr To Mem Pool: " + ex.Message);
					if (ex.InnerException != null)
					{
						Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
					}
				}
#endif
			}

			return relayPeers;
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

		public List<PeerAddress> MemAddressPool
		{
			get
			{
				return _memAddressPool;
			}
		}
	}
}
