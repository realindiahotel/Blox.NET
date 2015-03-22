using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.Blox.Network;
using Bitcoin.BitcoinUtilities;
using System.IO;

namespace Bitcoin.Blox.Protocol_Messages
{
    public class TransactionInput : Message
    {
        public static readonly byte[] EmptyArray = new byte[0];

        // Allows for altering transactions after they were broadcast. Tx replacement is currently disabled in the C++
        // client so this is always the UINT_MAX.
        // TODO: Document this in more detail and build features that use it.
        private uint _sequence;
        // Data needed to connect to the output of the transaction we're gathering coins from.
        private byte[] _outpointHash;
        private uint _outpointIndex;
        // The "script bytes" might not actually be a script. In coinbase transactions where new coins are minted there
        // is no input transaction, so instead the scriptBytes contains some extra stuff (like a rollover nonce) that we
        // don't care about much. The bytes are turned into a Script object (cached below) on demand via a getter.
        internal byte[] ScriptBytes { get; set; }
        // A pointer to the transaction that owns this input.
        internal TransactionMessage ParentTransaction { get; private set; }

        /// <summary>
        /// Used only in creation of the genesis block.
        /// </summary>
        internal TransactionInput(TransactionMessage parentTransaction, byte[] scriptBytes, P2PNetworkParameters netParams): base(netParams)
        {
            ScriptBytes = scriptBytes;
            _sequence = uint.MaxValue;
            ParentTransaction = parentTransaction;
        }

        /// <summary>
        /// Creates an UNSIGNED input that links to the given output
        /// </summary>
        internal TransactionInput(TransactionMessage parentTransaction, Byte[] outpointHash, uint outpointIndex, P2PNetworkParameters netParams): base(netParams)
        {
            _outpointHash = outpointHash;
            _outpointIndex = outpointIndex;
            ScriptBytes = EmptyArray;
            _sequence = uint.MaxValue;
            ParentTransaction = parentTransaction;
        }

        /// <summary>
        /// Deserializes an input message. This is usually part of a transaction message.
        /// </summary>
        /// <exception cref="BitCoinSharp.ProtocolException" />
        public TransactionInput(TransactionMessage parentTransaction, byte[] payload, int offset, P2PNetworkParameters netParams): base(payload, offset, true, netParams)
        {
            ParentTransaction = parentTransaction;
        }

        /// <exception cref="BitCoinSharp.ProtocolException" />
        protected override void Parse()
        {
            _outpointHash = Utilities.ReverseBytes(ReadBytes(32));
            _outpointIndex = ReadUint32();
            Cursor += 36;
            var scriptLen = (int)ReadVarInt();
            ScriptBytes = ReadBytes(scriptLen);
            _sequence = ReadUint32();
        }

        /// <exception cref="System.IO.IOException" />
        public override void BitcoinSerializeToStream(Stream stream)
        {
            Byte[] outpointHashBytes = Utilities.ReverseBytes(_outpointHash);
            stream.Write(outpointHashBytes,0,outpointHashBytes.Length);
            Utilities.Uint32ToByteStreamLe(_outpointIndex, stream);
            Byte[] scriptLengthBytes = new VarInt((ulong)ScriptBytes.Length).Encode();
            stream.Write(scriptLengthBytes,0,scriptLengthBytes.Length);
            stream.Write(ScriptBytes, 0, ScriptBytes.Length);
            Utilities.Uint32ToByteStreamLe(_sequence, stream);
        }

        /// <summary>
        /// Coinbase transactions have special inputs with hashes of zero. If this is such an input, returns true.
        /// </summary>
        public bool IsCoinBase
        {
            get { return _outpointHash.All(t => t == 0); }
        }

        internal enum ConnectionResult
        {
            NoSuchTx,
            AlreadySpent,
            Success
        }
    }
}
