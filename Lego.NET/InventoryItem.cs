using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;

namespace Bitcoin.Lego
{
	public class InventoryItem
	{
		public enum ItemType
		{
			Error,
			Transaction,
			Block
		}

		public ItemType Type { get; private set; }
		public byte[] Hash { get; private set; }

		public InventoryItem(ItemType type, byte[] hash)
		{
			Type = type;
			Hash = hash;
		}

		public override string ToString()
		{
			return Type + ": " + Utilities.BytesToHexString(Hash);
		}
	}
}
