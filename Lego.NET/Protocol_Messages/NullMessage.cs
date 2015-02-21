using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitcoin.Lego.Protocol_Messages
{
	/// <summary>
	/// Empty message to dismiss if recieve TCP error
	/// </summary>
	public class NullMessage :Message
	{
		public NullMessage()
		{

		}
		protected override void Parse()
		{

		}
	}
}
