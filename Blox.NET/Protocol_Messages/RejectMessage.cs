using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Blox.Network;

namespace Bitcoin.Blox.Protocol_Messages
{
	[Serializable]
	public class RejectMessage :Message
	{
		private string _message;
		private ccode _ccode;
		private string _reason;
		private string _data;

		public enum ccode
		{
			REJECT_MALFORMED = 0x01,
			REJECT_INVALID = 0x10,
			REJECT_OBSOLETE = 0x11,
			REJECT_DUPLICATE = 0x12,
			REJECT_NONSTANDARD = 0x40,
			REJECT_DUST = 0x41,
			REJECT_INSUFFICIENTFEE = 0x42,
			REJECT_CHECKPOINT = 0x43
		}

		public string Message
		{
			get
			{
				return _message;
			}
		}

		public ccode CCode
		{
			get
			{
				return _ccode;
			}
		}

		public string Reason
		{
			get
			{
				return _reason;
			}
		}

		public string Data
		{
			get
			{
				return _data;
			}
		}

		public RejectMessage(byte[] msg , P2PNetworkParameters netParams) : base(msg, 0, true, netParams)
		{

		}

		public RejectMessage(string message, ccode ccode, string reason, P2PNetworkParameters netParams, string data="") :base(netParams)
		{
			_message = message;
			_ccode = ccode;
			_reason = reason;
			_data = data;

		}

		protected override void Parse()
		{
			_message = ReadStr();
			_ccode = (ccode)ReadBytes(1)[0];
			_reason= ReadStr();
			_data = ReadStr();
		}

		public override void BitcoinSerializeToStream(Stream buf)
		{
			byte[] messageBytes = Encoding.UTF8.GetBytes(_message);
			buf.Write(messageBytes, 0, messageBytes.Length);
			buf.WriteByte((byte)_ccode);
			byte[] reasonBytes = Encoding.UTF8.GetBytes(_reason);
			buf.Write(reasonBytes, 0, reasonBytes.Length);
			byte[] dataBytes = Encoding.UTF8.GetBytes(_data);
			buf.Write(dataBytes, 0, dataBytes.Length);
		}
	}
}
