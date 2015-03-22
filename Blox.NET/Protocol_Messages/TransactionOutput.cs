using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BitcoinUtilities;
using Bitcoin.Blox.Network;
using System.IO;

namespace Bitcoin.Blox.Protocol_Messages
{
    public class TransactionOutput : Message
    {
        // A transaction output has some value and a script used for authenticating that the redeemer is allowed to spend
        // this output.
        private ulong _value;
        private byte[] _scriptBytes;

        // A reference to the transaction which holds this output.
        internal TransactionMessage ParentTransaction { get; set; }

        /// <summary>
        /// Deserializes a transaction output message. This is usually part of a transaction message.
        /// </summary>
        /// <exception cref="ProtocolException"/>
        public TransactionOutput(TransactionMessage parent, byte[] payload, int offset, P2PNetworkParameters netParams): base(payload, offset,true, netParams)
        {
            ParentTransaction = parent;
        }

        protected override void Parse()
        {
            _value = ReadUint64();
            var scriptLen = (int)ReadVarInt();
            _scriptBytes = ReadBytes(scriptLen);
        }

        public override void BitcoinSerializeToStream(Stream stream)
        {
            if (_scriptBytes != null)
            {
                Utilities.Uint64ToByteStreamLe(Value, stream);
                // TODO: Move script serialization into the Script class, where it belongs.
                Byte[] scriptLengthBytes = new VarInt((ulong)_scriptBytes.Length).Encode();
                stream.Write(scriptLengthBytes,0,scriptLengthBytes.Length);
                stream.Write(_scriptBytes,0,_scriptBytes.Length);
            }
            else
            {
#if (DEBUG)
                Console.WriteLine("Script Bytes were null so no script to serialize :(");
#endif
            }
        }

        /// <summary>
        /// Returns the value of this output in nanocoins. This is the amount of currency that the destination address
        /// receives.
        /// </summary>
        public ulong Value
        {
            get { return _value; }
        }

        internal int Index
        {
            get
            {
                if (ParentTransaction != null)
                {
                    for (var i = 0; i < ParentTransaction.Outputs.Count; i++)
                    {
                        if (ParentTransaction.Outputs[i] == this)
                            return i;
                    }
                }

                return -1; //Output linked to wrong parent transaction? Should never happen.
            }
        }

        public byte[] ScriptBytes
        {
            get { return _scriptBytes; }
        }
    }
}
