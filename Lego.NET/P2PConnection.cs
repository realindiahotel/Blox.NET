using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Protocol_Messages;

namespace Bitcoin.Lego
{
    public class P2PConnection : Connection
    {
		private VersionMessage _myVersionMessage;
		private VersionMessage _theirVersionMessage;
		private long _peerTimeOffset;

		public P2PConnection(IPAddress remoteIp, int connectionTimeout, int remotePort = Globals.ProdP2PPort) :base(remoteIp, remotePort, connectionTimeout, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
		{
			//fully loaded man https://www.youtube.com/watch?v=dLIIKrJ6nnI
		}

		public bool Connect(ulong services, uint blockHeight, int relay, bool strictVerAck)
		{
			if (!Socket.Connected)
			{
				Socket.Connect(RemoteEndPoint);

				//our in and out streams to the underlying socket
				DataIn = new NetworkStream(Socket, FileAccess.Read);
				DataOut = new NetworkStream(Socket, FileAccess.Write);

				Socket.SendTimeout = Socket.ReceiveTimeout = ConnectionTimeout;
				//add packetMagic for testnet
				_myVersionMessage = new VersionMessage(RemoteIPAddress, Socket, services, RemotePort, blockHeight, relay);
				Send(_myVersionMessage);
				var message = Recieve();

				//we have their version message yay :)
				if (message.GetType().Name.Equals("VersionMessage"))
				{
					_theirVersionMessage = (VersionMessage)message;

					//if strict verack is on we listen for verack and if we don't get it we close the connection
					if (strictVerAck)
					{
						message = Recieve();

						//As we're strick on verack, I don't wan't to see anything but verack right now

						if (! message.GetType().Name.Equals("VersionAck"))
						{
							Socket.Close();
						}
					}

					//I have their version so time to make sure everything is ok and either verack or reject
					if (_theirVersionMessage != null && Socket.Connected)
					{
						//check the unix time timestamp
						if (Utilities.UnixTimeWithin70MinuteThreshold(_theirVersionMessage.Time, out _peerTimeOffset))
						{
							Send(new VersionAck());
						}
						else
						{
							//their time sucks sent a reject message and close connection
							Send(new RejectMessage("version", RejectMessage.ccode.REJECT_INVALID, "Your unix timestamp is fucked up", ""));
							Socket.Close();
						}
						
					}

				}
				else //something other then their version message...uh oh...not friend... kill the connection
				{
					Socket.Close();
				}
			}			

			return Socket.Connected;
		}

		public void Send(Message message)
		{			

			WriteMessage(message);
		}

		public Message Recieve()
		{		

			//on testnet send in the different packetMagic
			return ReadMessage();

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
	}
}
