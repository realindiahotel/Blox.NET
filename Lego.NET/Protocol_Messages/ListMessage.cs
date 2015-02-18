using System;
using System.Collections.Generic;
using System.IO;
using Bitcoin.BitcoinUtilities;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitcoin.Lego.Protocol_Messages
{
	/// <summary>
	/// Abstract super class of classes with list based payload, i.e. InventoryMessage and GetDataMessage.
	/// </summary>
	public abstract class ListMessage : Message
	{
		// For some reason the compiler complains if this is inside InventoryItem
		private IList<InventoryItem> _items;

		//maximum amount of items as defined in protocol specification
		private const ulong _maxInventoryItems = 50000;

		protected ListMessage(byte[] bytes, uint packetMagic) : base(bytes, 0, true, packetMagic)
		{

		}

		protected ListMessage(uint packetMagic = Globals.ProdPacketMagic) : base(packetMagic)
		{
			_items = new List<InventoryItem>();
		}

		public IList<InventoryItem> Items
		{
			get { return _items; }
		}

		public void AddItem(InventoryItem item)
		{
			_items.Add(item);
		}

		protected override void Parse()
		{
			// An inv is vector<CInv> where CInv is int+hash. The int is either 1 or 2 for tx or block.
			var arrayLen = ReadVarInt();
			if (arrayLen > _maxInventoryItems)
				throw new Exception("Too many items in INV message: " + arrayLen);
			_items = new List<InventoryItem>((int)arrayLen);
			for (var i = 0UL; i < arrayLen; i++)
			{
				if (Cursor + 4 + 32 > Bytes.Length)
				{
					throw new Exception("Ran off the end of the INV");
				}
				var typeCode = ReadUint32();
				InventoryItem.ItemType type;
				// See ppszTypeName in net.h
				switch (typeCode)
				{
					case 0:
						type = InventoryItem.ItemType.Error;
						break;
					case 1:
						type = InventoryItem.ItemType.Transaction;
						break;
					case 2:
						type = InventoryItem.ItemType.Block;
						break;
					default:
						throw new Exception("Unknown CInv type: " + typeCode);
				}
				var item = new InventoryItem(type, ReadHash());
				_items.Add(item);
			}
			Bytes = null;
		}

		/// <exception cref="IOException"/>
		public override void BitcoinSerializeToStream(Stream stream)
		{
			byte[] payloadCount = new VarInt((ulong)_items.Count).Encode();
            stream.Write(payloadCount,0,payloadCount.Length);
			foreach (var i in _items)
			{
				// Write out the type code.
				Utilities.Uint32ToByteStreamLe((uint)i.Type, stream);
				// And now the hash.
				byte[] payload = Utilities.ReverseBytes(i.Hash);
                stream.Write(payload,0,payload.Length);
			}
		}
	}
}
