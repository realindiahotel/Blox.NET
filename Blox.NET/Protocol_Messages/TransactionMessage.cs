using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bitcoin.Blox.Network;
using Bitcoin.BitcoinUtilities;
using System.IO;

namespace Bitcoin.Blox.Protocol_Messages
{
	public class TransactionMessage : Message
	{
		// These are serialized in both BitCoin and java serialization.
		private uint _version;
		private List<TransactionInput> _inputs;
		private List<TransactionOutput> _outputs;
		private uint _lockTime;

		// This is an in memory helper only.
		[NonSerialized]
		private Byte[] _hash;

		internal TransactionMessage(P2PNetworkParameters netParams): base(netParams)
		{
			_version = 1;
			_inputs = new List<TransactionInput>();
			_outputs = new List<TransactionOutput>();
			// We don't initialize appearsIn deliberately as it's only useful for transactions stored in the wallet.
		}

		/// <summary>
		/// Creates a transaction from the given serialized bytes, eg, from a block or a tx network message.
		/// </summary>
		/// <exception cref="ProtocolException"/>
		public TransactionMessage(byte[] payloadBytes, P2PNetworkParameters netParams) : base(payloadBytes, 0, true, netParams)
		{
		}

		/// <summary>
		/// Creates a transaction by reading payload starting from offset bytes in. Length of a transaction is fixed.
		/// </summary>
		/// <exception cref="ProtocolException"/>
		public TransactionMessage(byte[] payload, int offset, P2PNetworkParameters netParams)
			: base(payload, offset, true, netParams)
		{
			// inputs/outputs will be created in parse()
		}

		/// <summary>
		/// Returns a read-only list of the inputs of this transaction.
		/// </summary>
		public IList<TransactionInput> Inputs
		{
			get { return _inputs.AsReadOnly(); }
		}

		/// <summary>
		/// Returns a read-only list of the outputs of this transaction.
		/// </summary>
		public IList<TransactionOutput> Outputs
		{
			get { return _outputs.AsReadOnly(); }
		}

		/// <summary>
		/// Returns the transaction hash as you see them in the block explorer.
		/// </summary>
		public Byte[] Hash
		{
			get { return _hash ?? (_hash = Utilities.ReverseBytes(Utilities.DoubleDigest(BitcoinSerialize()))); }
		}

		public string HashAsString
		{
			get { return Hash.ToString(); }
		}

		/// <exception cref="ProtocolException"/>
		protected override void Parse()
		{
			_version = ReadUint32();
			// First come the inputs.
			var numInputs = ReadVarInt();
			_inputs = new List<TransactionInput>((int)numInputs);
			for (var i = 0UL; i < numInputs; i++)
			{
				var input = new TransactionInput(this, Bytes, Cursor, P2PNetParameters);
				_inputs.Add(input);
				Cursor += input.MessageSize;
			}
			// Now the outputs
			var numOutputs = ReadVarInt();
			_outputs = new List<TransactionOutput>((int)numOutputs);
			for (var i = 0UL; i < numOutputs; i++)
			{
				var output = new TransactionOutput(this, Bytes, Cursor, P2PNetParameters);
				_outputs.Add(output);
				Cursor += output.MessageSize;
			}
			_lockTime = ReadUint32();
		}

		/// <summary>
		/// A coinbase transaction is one that creates a new coin. They are the first transaction in each block and their
		/// value is determined by a formula that all implementations of BitCoin share. In 2011 the value of a coinbase
		/// transaction is 50 coins, but in future it will be less. A coinbase transaction is defined not only by its
		/// position in a block but by the data in the inputs.
		/// </summary>
		public bool IsCoinBase
		{
			get { return _inputs[0].IsCoinBase; }
		}

		/// <summary>
		/// Adds an input to this transaction that imports value from the given output. Note that this input is NOT
		/// complete and after every input is added with addInput() and every output is added with addOutput(),
		/// signInputs() must be called to finalize the transaction and finish the inputs off. Otherwise it won't be
		/// accepted by the network.
		/// </summary>
		public void AddInput(TransactionOutput from)
		{
			AddInput(new TransactionInput(this,from.ReadHash(), from.Index, P2PNetParameters));
		}

		/// <summary>
		/// Adds an input directly, with no checking that it's valid.
		/// </summary>
		public void AddInput(TransactionInput input)
		{
			_inputs.Add(input);
		}

		/// <summary>
		/// Adds the given output to this transaction. The output must be completely initialized.
		/// </summary>
		public void AddOutput(TransactionOutput to)
		{
			to.ParentTransaction = this;
			_outputs.Add(to);
		}

		public override void BitcoinSerializeToStream(Stream stream)
		{
			Utilities.Uint32ToByteStreamLe(_version, stream);
            Byte[] inputCountBytes = new VarInt((ulong)_inputs.Count).Encode();
            stream.Write(inputCountBytes,0,inputCountBytes.Length);
            foreach (var @in in _inputs)
            {
                @in.BitcoinSerializeToStream(stream);
            }

            Byte[] outputCountBytes = new VarInt((ulong)_outputs.Count).Encode();
            stream.Write(outputCountBytes, 0 ,outputCountBytes.Length);
            foreach (var @out in _outputs)
            {
                @out.BitcoinSerializeToStream(stream);
            }
			Utilities.Uint32ToByteStreamLe(_lockTime, stream);
		}

		public override bool Equals(object other)
		{
			if (!(other is TransactionMessage)) return false;
			var t = (TransactionMessage)other;

			return t.Hash.Equals(Hash);
		}

		public override int GetHashCode()
		{
			return Hash.GetHashCode();
		}
	}
}
