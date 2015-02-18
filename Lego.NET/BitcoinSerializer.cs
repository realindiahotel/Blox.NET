using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Protocol_Messages;

namespace Bitcoin.Lego
{
	public class BitcoinSerializer
	{
		private const int _commandLen = 12;

		private static readonly IDictionary<Type, string> _names = new Dictionary<Type, string>();

		static BitcoinSerializer()
		{
			_names.Add(typeof(VersionMessage), "version");
			_names.Add(typeof(VersionAck), "verack");
			_names.Add(typeof(RejectMessage), "reject");
			_names.Add(typeof(Ping), "ping");
			_names.Add(typeof(Pong), "pong");
			_names.Add(typeof(InventoryMessage), "inv");
			_names.Add(typeof(AddressMessage), "addr");
			_names.Add(typeof(GetAddresses), "getaddr");/*
			_names.Add(typeof(Block), "block");
			_names.Add(typeof(GetDataMessage), "getdata");
			_names.Add(typeof(Transaction), "tx");						
			_names.Add(typeof(GetBlocksMessage), "getblocks");*/
		}

		/// <summary>
		/// Writes message to to the output stream.
		/// </summary>
		/// <exception cref="IOException"/>
		public void Serialize(Message message, Stream @out)
		{
			string name;
			uint packetMagic = message.PacketMagic;

			if (!_names.TryGetValue(message.GetType(), out name))
			{
				throw new Exception("BitcoinSerializer doesn't currently know how to serialize " + message.GetType());
			}

			var header = new byte[4 + _commandLen + 8];

			Utilities.Uint32ToByteArrayBe(packetMagic, header, 0);

			// The header array is initialized to zero so we don't have to worry about NULL terminating the string here.
			for (var i = 0; i < name.Length && i < _commandLen; i++)
			{
				header[4 + i] = (byte)name[i];
			}

			var payload = message.BitcoinSerialize();

			Utilities.Uint32ToByteArrayLe((uint)payload.Length, header, 4 + _commandLen);
						
			var hash = Utilities.DoubleDigest(payload);
			Array.Copy(hash, 0, header, 4 + _commandLen + 4, 4);		

			@out.Write(header,0,header.Length);
			@out.Write(payload,0,payload.Length);
			@out.Flush();
		}

		/// <summary>
		/// Reads a message from the given InputStream and returns it.
		/// </summary>
		/// <exception cref="IOException"/>
		public Message Deserialize(Stream @in, uint packetMagic)
		{
			// A BitCoin protocol message has the following format.
			//
			//   - 4 byte magic number: 0xfabfb5da for the testnet or
			//                          0xf9beb4d9 for production
			//   - 12 byte command in ASCII
			//   - 4 byte payload size
			//   - 4 byte checksum
			//   - Payload data
			//
			// The checksum is the first 4 bytes of a SHA256 hash of the message payload. It isn't
			// present for all messages, notably, the first one on a connection.
			//
			// Satoshi's implementation ignores garbage before the magic header bytes. We have to do the same because
			// sometimes it sends us stuff that isn't part of any message.
			SeekPastMagicBytes(@in, packetMagic);
			// Now read in the header.
			var header = new byte[_commandLen + 8];
			var readCursor = 0;
			while (readCursor < header.Length)
			{
				var bytesRead = @in.Read(header, readCursor, header.Length - readCursor);
				if (bytesRead == -1)
				{
					// There's no more data to read.
					throw new IOException("Socket is disconnected");
				}
				readCursor += bytesRead;
			}

			var cursor = 0;

			// The command is a NULL terminated string, unless the command fills all twelve bytes
			// in which case the termination is implicit.
			var mark = cursor;
			for (; header[cursor] != 0 && cursor - mark < _commandLen; cursor++)
			{
			}
			var commandBytes = new byte[cursor - mark];
			Array.Copy(header, mark, commandBytes, 0, commandBytes.Length);
			for (var i = 0; i < commandBytes.Length; i++)
			{
				// Emulate ASCII by replacing extended characters with question marks.
				if (commandBytes[i] >= 0x80)
				{
					commandBytes[i] = 0x3F;
				}
			}
			var command = Encoding.UTF8.GetString(commandBytes, 0, commandBytes.Length);
			cursor = mark + _commandLen;

			var size = Utilities.ReadUint32(header, cursor);
			cursor += 4;

			if (size > Message.MaxSize)
				throw new Exception("Message size too large: " + size);

			// Old clients don't send the checksum.
			var checksum = new byte[4];
			// Note that the size read above includes the checksum bytes.
			Array.Copy(header, cursor, checksum, 0, 4);

			// Now try to read the whole message.
			readCursor = 0;
			var payloadBytes = new byte[size];
			while (readCursor < payloadBytes.Length - 1)
			{
				var bytesRead = @in.Read(payloadBytes, readCursor, (int)(size - readCursor));
				if (bytesRead == -1)
				{
					throw new IOException("Socket is disconnected");
				}
				readCursor += bytesRead;
			}

			// Verify the checksum.
			
			var hash = Utilities.DoubleDigest(payloadBytes);
			if (checksum[0] != hash[0] || checksum[1] != hash[1] ||	checksum[2] != hash[2] || checksum[3] != hash[3])
			{
				throw new Exception("Checksum failed to verify, actual " + Utilities.BytesToHexString(hash) + " vs " + Utilities.BytesToHexString(checksum));
			}
			

			try
			{
				return MakeMessage(command, payloadBytes, packetMagic);
			}
			catch (Exception e)
			{
				throw new Exception("Error deserializing message " + Utilities.BytesToHexString(payloadBytes) + Environment.NewLine + e.Message, e);
			}
		}

		private Message MakeMessage(string command, byte[] payloadBytes, uint packetMagic)
		{
			// We use an if ladder rather than reflection because reflection can be slow on some platforms.
			if (command.Equals("version"))
			{
				return new VersionMessage(payloadBytes, packetMagic);
			}
			if (command.Equals("verack"))
			{
				return new VersionAck(payloadBytes, packetMagic);
			}
			if (command.Equals("reject"))
			{
				return new RejectMessage(payloadBytes, packetMagic);
			}
			if (command.Equals("ping"))
			{
				return new Ping(payloadBytes, packetMagic);
			}
			if (command.Equals("pong"))
			{
				return new Pong(payloadBytes, packetMagic);
			}
			if (command.Equals("inv"))
			{
				return new InventoryMessage(payloadBytes, packetMagic);
			}
			if (command.Equals("addr"))
			{
				return new AddressMessage(payloadBytes, packetMagic);
			}
			if (command.Equals("getaddr"))
			{
				return new GetAddresses(payloadBytes, packetMagic);
			}/*
			if (command.Equals("block"))
			{
				return new Block(_params, payloadBytes);
			}
			if (command.Equals("getdata"))
			{
				return new GetDataMessage(_params, payloadBytes);
			}
			if (command.Equals("tx"))
			{
				return new Transaction(_params, payloadBytes);
			}						
			*/

			throw new Exception("No support for deserializing message with name " + command);
		}

		/// <exception cref="IOException"/>
		private void SeekPastMagicBytes(Stream @in, uint packetMagic)
		{
			var magicCursor = 3; // Which byte of the magic we're looking for currently.
			while (true)
			{
				// Read a byte.
				var buffer = new byte[1];
				int b = @in.Read(buffer, 0, buffer.Length) == 1 ? buffer[0] : -1;
					
				if (b == -1)
				{
					// There's no more data to read.
					throw new IOException("Socket is disconnected");
				}
				// We're looking for a run of bytes that is the same as the packet magic but we want to ignore partial
				// magics that aren't complete. So we keep track of where we're up to with magicCursor.
				var expectedByte = (byte)(packetMagic >> magicCursor * 8);
				if (b == expectedByte)
				{
					magicCursor--;
					if (magicCursor < 0)
					{
						// We found the magic sequence.
						return;
					}
					// We still have further to go to find the next message.
				}
				else
				{
					magicCursor = 3;
				}
			}
		}
	}
}
