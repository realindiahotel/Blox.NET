using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Protocol_Messages;

namespace Bitcoin.Lego
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
