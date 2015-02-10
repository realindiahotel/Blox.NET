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

		public P2PConnection(IPAddress remoteIp, int connectionTimeout, int remotePort = Globals.ProdP2PPort) :base(remoteIp, remotePort, connectionTimeout, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
		{			

		}

		public bool Connect(ulong services, uint blockHeight, int relay, bool strictVerAck)
		{
			if (!Socket.Connected)
			{
				Socket.Connect(RemoteEndPoint);
				Socket.SendTimeout = Socket.ReceiveTimeout = ConnectionTimeout;
				//add packetMagic for testnet
				_myVersionMessage = new VersionMessage(RemoteIPAddress, Socket, services, RemotePort, blockHeight, relay);
				Send(_myVersionMessage);
				var message = Recieve();

				//we have their version message yay :)
				if (message.GetType().Name.Equals("VersionMessage"))
				{
					_theirVersionMessage = (VersionMessage)message;

					//yay have their version message now if strict verack is on we listen for and send verack
					if (strictVerAck)
					{
						message = Recieve();

						if (message.GetType().Name.Equals("VersionAck"))
						{
							//go their verack, I have their version so time to send my verack
							Send(new VersionAck());

							message = Recieve(); //should be ping?
						}
						else //As we're strick on verack, I don't wan't to see anything but verack right now
						{
							Socket.Close();
						}
					}

				}
				else //something other then their version message...uh oh...not friend... kill the socket
				{
					Socket.Close();
				}
			}

			return Socket.Connected;
		}

		public void Send(Message message)
		{
			DataOut = new NetworkStream(Socket, FileAccess.Write);

			WriteMessage(message);
		}

		public Message Recieve()
		{
			DataIn = new NetworkStream(Socket, FileAccess.Read);

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
	}
}
