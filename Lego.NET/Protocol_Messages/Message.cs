using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;
using System.IO;

namespace Bitcoin.Lego.Protocol_Messages
{
	[Serializable]
	public abstract class Message
	{
		public const uint MaxSize = 0x2000000;

		[NonSerialized]
		private int _offset;
		[NonSerialized]
		private int _cursor;
		[NonSerialized]
		private byte[] _bytes;
		[NonSerialized]
		private uint _protocolVersion;
		[NonSerialized]
		private uint _packetMagic;

		// The offset is how many bytes into the provided byte array this message starts at.
		protected int Offset
		{
			get { return _offset; }
			private set { _offset = value; }
		}

		// The cursor keeps track of where we are in the byte array as we parse it.
		// Note that it's relative to the start of the array NOT the start of the message.
		protected int Cursor
		{
			get { return _cursor; }
			set { _cursor = value; }
		}

		// The raw message bytes themselves.
		protected byte[] Bytes
		{
			get { return _bytes; }
			set { _bytes = value; }
		}

		protected uint ProtocolVersion
		{
			get { return _protocolVersion; }
			set { _protocolVersion = value; }
		}

		public uint PacketMagic
		{
			get
			{
				return _packetMagic;
			}
		}

		internal Message(uint packetMagic = Globals.ProdPacketMagic)
		{
			_packetMagic = packetMagic;
		}
		
		internal Message(byte[] msg, int offset, bool runParse, uint packetMagic = Globals.ProdPacketMagic, uint protocolVersion = Globals.ClientVersion)
		{
			_protocolVersion = protocolVersion;
			_bytes = msg;
			_cursor = _offset = offset;
			_packetMagic = packetMagic;
			if (runParse)
			{
				Parse();
				_bytes = null;
			}			
		}

		protected abstract void Parse();

		public virtual byte[] BitcoinSerialize()
		{
			using (var stream = new MemoryStream())
			{
				BitcoinSerializeToStream(stream);
				return stream.ToArray();
			}
		}

		/// <summary>
		/// Serializes this message to the provided stream. If you just want the raw bytes use bitcoinSerialize().
		/// </summary>
		/// <exception cref="IOException"/>
		public virtual void BitcoinSerializeToStream(Stream stream)
		{
		}

		internal int MessageSize
		{
			get { return Cursor - Offset; }
		}

		internal uint ReadUint32()
		{
			var u = Utilities.ReadUint32(Bytes, Cursor);
			Cursor += 4;
			return u;
		}

		internal Byte[] ReadHash()
		{
			var hash = new byte[32];
			Array.Copy(Bytes, Cursor, hash, 0, 32);
			// We have to flip it around, as it's been read off the wire in little endian.
			// Not the most efficient way to do this but the clearest.
			hash = Utilities.ReverseBytes(hash);
			Cursor += 32;
			return hash;
		}

		internal ulong ReadUint64()
		{
			return (((ulong)Bytes[Cursor++]) << 0) |
				   (((ulong)Bytes[Cursor++]) << 8) |
				   (((ulong)Bytes[Cursor++]) << 16) |
				   (((ulong)Bytes[Cursor++]) << 24) |
				   (((ulong)Bytes[Cursor++]) << 32) |
				   (((ulong)Bytes[Cursor++]) << 40) |
				   (((ulong)Bytes[Cursor++]) << 48) |
				   (((ulong)Bytes[Cursor++]) << 56);
		}

		internal ulong ReadVarInt()
		{
			var varint = new VarInt(Bytes, Cursor);
			Cursor += varint.SizeInBytes;
			return varint.Value;
		}

		internal byte[] ReadBytes(int length)
		{
			var b = new byte[length];
			Array.Copy(Bytes, Cursor, b, 0, length);
			Cursor += length;
			return b;
		}

		internal string ReadStr()
		{
			if (Cursor >= Bytes.Length)
			{
				Cursor++;
				return "";
			}
			var varInt = new VarInt(Bytes, Cursor);
			Cursor++;
			if (varInt.Value == 0)
			{				
				return "";
			}
			var characters = new byte[varInt.Value];
			Array.Copy(Bytes, Cursor, characters, 0, characters.Length);
			Cursor += characters.Length;
			return Encoding.UTF8.GetString(characters, 0, characters.Length);
		}
	}
}
