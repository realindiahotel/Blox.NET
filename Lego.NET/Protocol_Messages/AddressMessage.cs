using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Lego.Network;
using System.IO;

namespace Bitcoin.Lego.Protocol_Messages
{
	[Serializable]
	public class AddressMessage : Message
	{
		public const ulong MaxAddresses = 1000;

		internal IList<PeerAddress> Addresses { get; private set; }

		internal AddressMessage(byte[] payload, int offset, P2PNetworkParameters netParams): base(payload, offset,true,netParams)
		{

		}

		internal AddressMessage(byte[] payload, P2PNetworkParameters netParams) : base(payload, 0, true, netParams)
		{

		}

		internal AddressMessage(List<PeerAddress> payloadAddresses, P2PNetworkParameters netParams) : base(netParams)
		{
			Addresses = payloadAddresses;
		}

		protected override void Parse()
		{
			var numAddresses = ReadVarInt();
			// Guard against ultra large messages that will crash us.
			if (numAddresses > MaxAddresses) //boobs
				throw new Exception("Address message too large.");
			Addresses = new List<PeerAddress>((int)numAddresses);
			for (var i = 0UL; i < numAddresses; i++)
			{
				var addr = new PeerAddress(Bytes, Cursor, false, P2PNetParameters);
				Addresses.Add(addr);
				Cursor += addr.MessageSize;
			}
		}

		public override void BitcoinSerializeToStream(Stream stream)
		{
			byte[] payloadCount = new VarInt((ulong)Addresses.Count).Encode();
            stream.Write(payloadCount,0,payloadCount.Length);
			foreach (var addr in Addresses)
			{
				addr.BitcoinSerializeToStream(stream);
			}
		}

		public override string ToString()
		{
			var builder = new StringBuilder();
			builder.Append("addr: ");
			foreach (var a in Addresses)
			{
				builder.Append(a.ToString());
				builder.Append(" ");
			}
			return builder.ToString();
		}
	}
}
