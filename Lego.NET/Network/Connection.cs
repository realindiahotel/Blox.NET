using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Protocol_Messages;
using HtmlAgilityPack;
using Open.Nat;

namespace Bitcoin.Lego.Network
{
	public class Connection
	{
		private Socket _socket;
		private Stream _dataOut;
		private Stream _dataIn;
		private int _connectionTimeout;
		private IPEndPoint _remoteEndPoint;
		private readonly IPAddress _remoteIp;
		private readonly int _remotePort;

		private readonly BitcoinSerializer _serializer = new BitcoinSerializer();

		public Connection(IPAddress remoteIp, int remotePort, int connectionTimeout, Socket socket)
		{
			_remoteIp = remoteIp;
			_remotePort = remotePort;
			_connectionTimeout = connectionTimeout;
			_remoteEndPoint = new IPEndPoint(remoteIp, remotePort);
			_socket = socket;
		}

		/// <summary>
		/// Reads a network message from the wire, blocking until the message is fully received.
		/// </summary>
		/// <returns>An instance of a Message subclass</returns>
		/// <exception cref="ProtocolException">If the message is badly formatted, failed checksum or there was a TCP failure.</exception>
		/// <exception cref="IOException"/>
		public virtual Message ReadMessage(uint packetMagic = Globals.ProdPacketMagic)
		{
			return _serializer.Deserialize(_dataIn,packetMagic);
		}

		/// <summary>
		/// Writes the given message out over the network using the protocol tag. For a Transaction
		/// this should be "tx" for example. It's safe to call this from multiple threads simultaneously,
		/// the actual writing will be serialized.
		/// </summary>
		/// <exception cref="IOException"/>
		public virtual void WriteMessage(Message message)
		{
			lock (_dataOut)
			{
				_serializer.Serialize(message, _dataOut);
			}
		}

		public static async Task<PeerAddress> GetMyExternalIPAsync(ulong services, int port = Globals.LocalP2PListeningPort)
		{
			//todo eventually create a ip checker in Azure controlled by us for Lego (but others can use as well)

			if (Globals.AdvertiseExternalIP) //I envisiage a scenario behind the onion or something where we don't want inadvertant IP leaky, may be usefull later if we create something other than P2P.
			{
				String page = "";

				using (WebClient webCli = new WebClient())
				{
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
								return new PeerAddress(IPAddress.Parse(ip), port, services, Globals.ClientVersion, false);
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
								return new PeerAddress(IPAddress.Parse(ip), port, services, Globals.ClientVersion, false);
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
			return new PeerAddress(IPAddress.Parse("127.0.0.1"), Globals.ProdP2PPort, services, Globals.ClientVersion, false);					
		}

		public static PeerAddress GetMyExternalIP(ulong services, int port = Globals.LocalP2PListeningPort)
		{

			if (Globals.AdvertiseExternalIP)
			{
				String page = "";

				using (WebClient webCli = new WebClient())
				{
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
								return new PeerAddress(IPAddress.Parse(ip), port, services, Globals.ClientVersion, false);
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
								return new PeerAddress(IPAddress.Parse(ip), port, services, Globals.ClientVersion, false);
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
			return new PeerAddress(IPAddress.Parse("127.0.0.1"), port, services, Globals.ClientVersion, false);

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
				await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, internalPort,externalPort));

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

		internal int ConnectionTimeout
		{
			get
			{
				return _connectionTimeout;
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
	}
}
