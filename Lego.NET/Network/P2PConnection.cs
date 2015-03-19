using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using Open.Nat;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Protocol_Messages;
using Bitcoin.Lego.Data_Interface;

namespace Bitcoin.Lego.Network
{
    public class P2PConnection
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
		private Socket _socket;
		private Stream _dataOut;
		private Stream _dataIn;
		private IPEndPoint _remoteEndPoint;
		private readonly IPAddress _remoteIp;
		private readonly ushort _remotePort;
		private P2PNetworkParameters _networkParameters;

		private readonly BitcoinSerializer _serializer = new BitcoinSerializer();

		/// <summary>
		/// New P2PConnection Object
		/// </summary>
		/// <param name="remoteIp">IP Address we want to connect to</param>
		/// <param name="connectionTimeout">How many milliseconds to wait for a TCP message</param>
		/// <param name="socket">The socket to use for the data stream, if we don't have one use new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)</param>
		/// <param name="remotePort">The remote port to connect to</param>
		/// <param name="inbound">Is this an inbount P2P connection or are we connecting out</param>
		public P2PConnection(IPAddress remoteIp, P2PNetworkParameters netParams, Socket socket, ushort remotePort = P2PNetworkParameters.ProdP2PPort, bool inbound = false)
		{
			_inbound = inbound;
			_networkParameters = netParams;
			_remoteIp = remoteIp;
			_remotePort = remotePort;
			_networkParameters = netParams;
			_remoteEndPoint = new IPEndPoint(remoteIp, remotePort);
			_socket = socket;

			//fully loaded man https://www.youtube.com/watch?v=dLIIKrJ6nnI
		}

		public bool ConnectToPeer(uint blockHeight, ulong remotePeerServices = (ulong)P2PNetworkParameters.NODE_NETWORK.FULL_NODE)
		{
			try
			{
				_addrFartThread = new Thread(new ThreadStart(pSendAddrFart));
				_addrFartThread.IsBackground = true;	

				if (_inbound) //the connection is incoming so we recieve their version message first
				{
					//check it's not a duplicate connection

					if (P2PConnectionManager.ConnectedToPeer(new PeerAddress(_remoteIp, _remotePort,_networkParameters.Services,new P2PNetworkParameters(P2PNetworkParameters.ProtocolVersion,_networkParameters.IsTestNet,_remotePort))))
					{
						throw new Exception("Already Inbound Connection Matching "+ _remoteIp.ToString()+ ":"+_remotePort + " Not Attempting To Connect");
					}

					pConnectAndSetVersionAndStreams(blockHeight);

					var message = Recieve();

					if (message.GetType().Name.Equals("VersionMessage"))
					{
						_theirVersionMessage = (VersionMessage)message;

						//we know their services from their version message
						_myVersionMessage.TheirAddr.Services = _theirVersionMessage.LocalServices;

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
					if (P2PConnectionManager.ConnectedToPeer(new PeerAddress(_remoteIp, _remotePort, _networkParameters.Services, new P2PNetworkParameters(P2PNetworkParameters.ProtocolVersion, _networkParameters.IsTestNet, _remotePort))))
					{
						throw new Exception("Already Outbound Connection Matching " + _remoteIp.ToString() + ":" + _remotePort + " Not Attempting To Connect");
					}

					pConnectAndSetVersionAndStreams(blockHeight);

					//if I know their services I'll report them in version
					_myVersionMessage.TheirAddr.Services = remotePeerServices;

					Send(_myVersionMessage);
					var message = Recieve();

					//we have their version message yay :)
					if (message.GetType().Name.Equals("VersionMessage"))
					{
						_theirVersionMessage = (VersionMessage)message;

						//if strict verack is on we listen for verack and if we don't get it we close the connection
						if (_networkParameters.StrictOutboundVerack)
						{
							pCheckVerack();
						}

						if (!pVerifyVersionMessage())
						{
							CloseConnection(true);
						}
						else
						{
							PeerAddress their_net_addr = new PeerAddress(_remoteIp, _remotePort, _theirVersionMessage.LocalServices, new P2PNetworkParameters(_theirVersionMessage.ClientVersion, _networkParameters.IsTestNet, Convert.ToUInt16(_theirVersionMessage.TheirAddr.Port), _theirVersionMessage.TheirAddr.Services));

							//send addr will also happen every 24 hrs by default
							_addrFartThread.Start();

							//send the addr of the peer I have just connected too to all other peers as I'm sure they'd like to know about a connectable peer as well :)					
							BradcastSend(new AddressMessage(new List<PeerAddress>() { their_net_addr }, _networkParameters), new List<P2PConnection>() { this });

							//start listening for messages
							pMessageListener();

							//make sure I put their address in the db if it's not in there, because we love to remember peers we can connect to :)
							using (DatabaseConnection dBC = new DatabaseConnection(_networkParameters))
							{
								dBC.AddAddress(their_net_addr);
							}
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

		private void pConnectAndSetVersionAndStreams( uint blockHeight)
		{
			if (!Socket.Connected)
			{
				Socket.Connect(RemoteEndPoint);
			}

			//our in and out streams to the underlying socket
			DataIn = new NetworkStream(Socket, FileAccess.Read);
			DataOut = new NetworkStream(Socket, FileAccess.Write);

			Socket.ReceiveTimeout = P2PNetworkParameters.HeartbeatTimeout;
			Socket.SendTimeout = 2000; //2 second timeout for sending messages
			Socket.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);

			_myVersionMessage = new VersionMessage(_remoteIp, _remotePort, Socket, blockHeight, P2PNetworkParameters.ProtocolVersion, new P2PNetworkParameters(_networkParameters.ClientVersion, _networkParameters.IsTestNet,Convert.ToUInt16(((IPEndPoint)Socket.LocalEndPoint).Port),_networkParameters.Services));

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
				if (_theirVersionMessage.ClientVersion < P2PNetworkParameters.MinimumAcceptedClientVersion)
				{
					Send(new RejectMessage("version", RejectMessage.ccode.REJECT_OBSOLETE, "Client version needs to be at least " + P2PNetworkParameters.MinimumAcceptedClientVersion,_networkParameters));
					return false;
				}
				else if (!Utilities.UnixTimeWithin70MinuteThreshold(_theirVersionMessage.Time, out _peerTimeOffset)) //check the unix time timestamp isn't outside 70 minutes, we don't wan't anyone outside 70 minutes anyway....Herpes
				{
					//their time sucks sent a reject message and close connection
					Send(new RejectMessage("version", RejectMessage.ccode.REJECT_INVALID, "Your unix timestamp is fucked up", _networkParameters));
					return false;
				}
				else if (_theirVersionMessage.Nonce==_myVersionMessage.Nonce && !_networkParameters.AllowP2PConnectToSelf)
				{
					Send(new RejectMessage("version", RejectMessage.ccode.REJECT_DUPLICATE, "Connecting to self has been disabled",_networkParameters));
					return false;
				}
				else //we're good send verack
				{
					if (sendVerack)
					{
						Send(new VersionAck(_networkParameters));
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
								Send(new Pong(((Ping)message).Nonce,_networkParameters));
								break;

							case "Pong":
								//we have pong
								break;

							case "RejectMessage":
#if (DEBUG)						//if we run in debug I spew out to console, in production no to save from an attack that slows us down writing to the console
								Console.WriteLine(((RejectMessage)message).Message + " - " + ((RejectMessage)message).CCode + " - " + ((RejectMessage)message).Reason + " - " + ((RejectMessage)message).Data);
#endif
								break;

							case "InventoryMessage":
								//just relaying now, in future we well check if we need to getdata and then getdata then relay if ok
								BradcastSend(message, new List<P2PConnection> { this });
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

										AddressMessage addrOut = new AddressMessage(peers, _networkParameters);						

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

									if (_networkParameters.ListenForPeers)
									{
										//if I'm listening for peers add my addr to the response
										PeerAddress my_net_addr = GetMyExternalIP();
										addressesToFireOut.Add(my_net_addr);
									}

									try
									{
										using (DatabaseConnection dbC = new DatabaseConnection(_networkParameters))
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
											Send(new AddressMessage(addressesToFireOut.Take(1000).ToList(),_networkParameters));
										}
										else
										{
											//send what we can
											Send(new AddressMessage(addressesToFireOut.Take(addressesToFireOut.Count).ToList(),_networkParameters));
										}
									}

									if (addressesToFireOut.Count > 1000)
									{
										if (addressesToFireOut.Count >= 2000)
										{
											//send the second lot of 1000
											Send(new AddressMessage(addressesToFireOut.Skip(1000).Take(1000).ToList(),_networkParameters));
										}
										else
										{
											//send what we can
											Send(new AddressMessage(addressesToFireOut.Skip(1000).Take(addressesToFireOut.Count - 1000).ToList(),_networkParameters));
										}
									}

									if (addressesToFireOut.Count > 2000)
									{
										if (addressesToFireOut.Count >= maxGetAddresses)
										{
											//send the final 500 message address
											Send(new AddressMessage(addressesToFireOut.Skip(2000).Take(maxGetAddresses-2000).ToList(), _networkParameters));
										}
										else
										{
											//send what we can
											Send(new AddressMessage(addressesToFireOut.Skip(2000).Take(addressesToFireOut.Count - 2000).ToList(), _networkParameters));
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

			try
			{
				if (Socket.Connected)
				{
					Socket.Close();
				}
			}
			catch
			{

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
					if (_networkParameters.ListenForPeers)
					{
						//send my addr to the peer I've just connected too, remember i include the port I am LISTENING on so they can connect to me	
						PeerAddress my_net_addr = GetMyExternalIP();
						Send(new AddressMessage(new List<PeerAddress>() { my_net_addr }, _networkParameters));
						int getaddrDelay = new Random(Environment.TickCount).Next(500, 5001);
						Thread.Sleep(getaddrDelay);
						Send(new GetAddresses(_networkParameters));
						Thread.Sleep((P2PNetworkParameters.AddrFartInterval - getaddrDelay));
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
					Thread.Sleep(P2PNetworkParameters.HeartbeatTimeout); //send a heartbeat after the specified interval

					if (P2PNetworkParameters.HeartbeatKeepAlive)
					{
						Send(new Ping(_networkParameters));

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

				while (attempt <= P2PNetworkParameters.RetrySendTCPOnError)
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

				while (attempt <= P2PNetworkParameters.RetryRecieveTCPOnError)
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
			
			return new NullMessage(_networkParameters);
		}

		private List<PeerAddress> AddToMemAddressPool(IList<PeerAddress> addresses)
		{
			List<PeerAddress> relayPeers = new List<PeerAddress>();

			foreach (PeerAddress pa in addresses)
			{
				
				try
				{
					if (_addressCursor >= _networkParameters.AddressMemPoolMax)
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
						if (_memAddressPool.Count < _networkParameters.AddressMemPoolMax)
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

		/// <summary>
		/// Reads a network message from the wire, blocking until the message is fully received.
		/// </summary>
		/// <returns>An instance of a Message subclass</returns>
		public virtual Message ReadMessage()
		{
			return _serializer.Deserialize(_dataIn, _networkParameters);
		}

		/// <summary>
		/// Writes the given message out over the network using the protocol tag. For a Transaction
		/// this should be "tx" for example. It's safe to call this from multiple threads simultaneously,
		/// the actual writing will be serialized.
		/// </summary>
		public virtual void WriteMessage(Message message)
		{
			lock (_dataOut)
			{
				_serializer.Serialize(message, _dataOut);
			}
		}

		public async Task<PeerAddress> GetMyExternalIPAsync()
		{
			//todo eventually create a ip checker in Azure controlled by us for Lego (but others can use as well)

			if (_networkParameters.AdvertiseExternalIP) //I envisiage a scenario behind the onion or something where we don't want inadvertant IP leaky, may be usefull later if we create something other than P2P.
			{
				String page = "";

				using (WebClient webCli = new WebClient())
				{
					try
					{
						page = await webCli.DownloadStringTaskAsync(new Uri("http://ipconfig.io", UriKind.Absolute));
						page = page.Trim();

						if (!page.Equals(""))
						{
							if ((page.Split(new char[] { '.' }).Length == 4) || (page.Split(new char[] { ':' }).Length == 8))
							{
								return new PeerAddress(IPAddress.Parse(page),_networkParameters.P2PListeningPort,_networkParameters.Services,_networkParameters);
							}
						}
					}
					catch
					{
						//allows falling through to next http request
					}

					try
					{
						page = await webCli.DownloadStringTaskAsync(new Uri("http://checkip.dyndns.org", UriKind.Absolute));
						if (!page.Equals(""))
						{
							HtmlDocument pg = new HtmlDocument();
							pg.LoadHtml(page);
							string ip = pg.DocumentNode.SelectSingleNode("//body").InnerText.Split(new char[] { ':' })[1].Trim();

							if ((ip.Split(new char[] { '.' }).Length == 4) || (ip.Split(new char[] { ':' }).Length == 8))
							{
								return new PeerAddress(IPAddress.Parse(ip), _networkParameters.P2PListeningPort,_networkParameters.Services,_networkParameters);
							}
						}
					}
					catch
					{
						//allows falling through to next http request
					}

					try
					{
						page = await webCli.DownloadStringTaskAsync(new Uri("http://www.showmemyip.com", UriKind.Absolute));

						if (!page.Equals(""))
						{
							HtmlDocument pg = new HtmlDocument();
							pg.LoadHtml(page);
							string ip = pg.DocumentNode.SelectSingleNode("//title").InnerText.Split(new char[] { ':' })[1].Trim();

							if (!(ip.Split(new char[] { '.' }).Length == 4) || (ip.Split(new char[] { ':' }).Length == 8))
							{
								ip = pg.DocumentNode.SelectSingleNode("//span[@id=\"IPAddress\"]").InnerText.Trim();
							}

							if ((ip.Split(new char[] { '.' }).Length == 4) || (ip.Split(new char[] { ':' }).Length == 8))
							{
								return new PeerAddress(IPAddress.Parse(ip), _networkParameters.P2PListeningPort, _networkParameters.Services,_networkParameters);
							}
						}
					}
					catch
					{
						//allows falling through to using localhost
					}
				}
			}
			//when all else fails just return localhost
			return new PeerAddress(IPAddress.Parse("127.0.0.1"), P2PNetworkParameters.ProdP2PPort, (ulong)P2PNetworkParameters.NODE_NETWORK.SPV_NODE, new P2PNetworkParameters(P2PNetworkParameters.ProtocolVersion, _networkParameters.IsTestNet));
		}

		public PeerAddress GetMyExternalIP()
		{

			if (_networkParameters.AdvertiseExternalIP)
			{
				String page = "";

				using (WebClient webCli = new WebClient())
				{
					try
					{
						page = webCli.DownloadString(new Uri("http://ipconfig.io", UriKind.Absolute));
						page = page.Trim();

						if (!page.Equals(""))
						{
							if ((page.Split(new char[] { '.' }).Length == 4) || (page.Split(new char[] { ':' }).Length == 8))
							{
								return new PeerAddress(IPAddress.Parse(page), _networkParameters.P2PListeningPort, _networkParameters.Services,_networkParameters);
							}
						}
					}
					catch
					{
						//allows falling through to next http request
					}

					try
					{
						page = webCli.DownloadString(new Uri("http://checkip.dyndns.org", UriKind.Absolute));
						if (!page.Equals(""))
						{
							HtmlDocument pg = new HtmlDocument();
							pg.LoadHtml(page);
							string ip = pg.DocumentNode.SelectSingleNode("//body").InnerText.Split(new char[] { ':' })[1].Trim();

							if ((ip.Split(new char[] { '.' }).Length == 4) || (ip.Split(new char[] { ':' }).Length == 8))
							{
								return new PeerAddress(IPAddress.Parse(ip), _networkParameters.P2PListeningPort, _networkParameters.Services, _networkParameters);
							}
						}
					}
					catch
					{
						//allows falling through to next http request
					}

					try
					{
						page = webCli.DownloadString(new Uri("http://www.showmemyip.com", UriKind.Absolute));

						if (!page.Equals(""))
						{
							HtmlDocument pg = new HtmlDocument();
							pg.LoadHtml(page);
							string ip = pg.DocumentNode.SelectSingleNode("//title").InnerText.Split(new char[] { ':' })[1].Trim();

							if (!(ip.Split(new char[] { '.' }).Length == 4) || (ip.Split(new char[] { ':' }).Length == 8))
							{
								ip = pg.DocumentNode.SelectSingleNode("//span[@id=\"IPAddress\"]").InnerText.Trim();
							}

							if ((ip.Split(new char[] { '.' }).Length == 4) || (ip.Split(new char[] { ':' }).Length == 8))
							{
								return new PeerAddress(IPAddress.Parse(ip), _networkParameters.P2PListeningPort, _networkParameters.Services, _networkParameters);
							}
						}
					}
					catch
					{
						//allows falling through to using localhost
					}
				}
			}
			//when all else fails just return localhost
			return new PeerAddress(IPAddress.Parse("127.0.0.1"), _networkParameters.P2PListeningPort, _networkParameters.Services, new P2PNetworkParameters(P2PNetworkParameters.ProtocolVersion,_networkParameters.IsTestNet));

			//'Live Wire' - https://soundcloud.com/excision/live-wire
		}

		public static async Task<bool> SetNATPortForwardingUPnPAsync(int externalPort, int internalPort)
		{
			try
			{
				var nat = new NatDiscoverer();
				var cts = new CancellationTokenSource(5000);
				var device = await nat.DiscoverDeviceAsync(PortMapper.Upnp, cts);

				//purge any old port mapping 
				await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, internalPort, externalPort));

				//now we create the port mapping
				await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, internalPort, externalPort, 0, "Lego.NET Bitcoin Node Port Forward Rule"));

				return true;

			}
#if (!DEBUG)
			catch
			{
				return false;
			}
#else
			catch (Exception ex)
			{
				Console.WriteLine("Exception UPnP Port Forwarding: " + ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
				}

				return false;
			}
#endif

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

		internal BitcoinSerializer Serializer
		{
			get
			{
				return _serializer;
			}
		}

		internal Stream DataOut
		{
			get
			{
				return _dataOut;
			}

			set
			{
				_dataOut = value;
			}
		}

		internal int RemotePort
		{
			get
			{
				return _remotePort;
			}
		}

		internal IPAddress RemoteIPAddress
		{
			get
			{
				return _remoteIp;
			}
		}

		internal Stream DataIn
		{
			get
			{
				return _dataIn;
			}

			set
			{
				_dataIn = value;
			}
		}

		internal Socket Socket
		{
			get
			{
				return _socket;
			}

			set
			{
				_socket = value;
			}
		}

		public IPEndPoint RemoteEndPoint
		{
			get
			{
				return _remoteEndPoint;
			}
		}

		public P2PNetworkParameters NetworkParameters
		{
			get
			{
				return _networkParameters;
			}
		}
	}
}
