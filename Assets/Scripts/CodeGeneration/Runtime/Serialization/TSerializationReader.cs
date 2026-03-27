using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace CodeGeneration.Runtime.Serialization
{
	public class TSerializationReader
    {
#if TSERIALIZATION_DEBUG
		public struct Operation
		{
			public string OperationName;
			public int Size;
			public int Depth;
		}

		public List<Operation> Operations;
		protected Stack<int> operationIndexStack;
#endif

	    private readonly BinaryReader _reader;

	    private readonly Stack<ushort> _versionStack;

		public TSerializationReader(Stream stream, Encoding encoding)
		{
#if TSERIALIZATION_DEBUG
			Operations = new List<Operation>();
			operationIndexStack = new Stack<int>();
#endif
			_versionStack = new Stack<ushort>();
			_reader = new BinaryReader(stream, encoding);
		}

		public T Deserialize<T>()
		{
			_versionStack.Clear();

#if TSERIALIZATION_DEBUG
			Operations.Clear();
			operationIndexStack.Clear();
			long pos = BeginWriteOperation();
#endif

			T result = Read<T>();

#if TSERIALIZATION_DEBUG
			EndWriteOperation(nameof(Deserialize), pos);
#endif

			return result;
		}

#if TSERIALIZATION_DEBUG
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long BeginWriteOperation()
		{
			operationIndexStack.Push(Operations.Count);
			Operations.Add(new Operation());
			return reader.BaseStream.Position;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EndWriteOperation(string operationName, long position)
		{
			int operationIndex = operationIndexStack.Pop();
			var stack = Operations[operationIndex];

			int depth = operationIndexStack.Count;
			stack.OperationName = operationName;
			stack.Size = (int) (reader.BaseStream.Position - position);
			stack.Depth = depth;
			Operations[operationIndex] = stack;
		}
#endif

		public T Read<T>()
		{
			T result = default(T);

			int key = _reader.Read7BitEncodedInt();

			if (key != TSerialization.NullKey)
			{
				ushort safeVersion = _reader.ReadUInt16();

				Serialization<T> serialization = TSerialization.GetSerialization<T>();

				_versionStack.Push(safeVersion);

				if (serialization.Key == key)
				{
					result = serialization.Create();
					serialization.Load(ref result, this);
				}
				else
				{
					if (TSerialization.GetBoxLoader(key, out TSerializationBaseBox serializationBox))
					{
						result = (T) serializationBox.Load(this);
					}
					else
					{
						UnityEngine.Debug.Assert(false, $"Unknow key [{key}]");
					}
				}

				_versionStack.Pop();
			}

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int ReadS32()
		{
			int result = _reader.ReadInt32();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte ReadU8()
		{
			byte result = _reader.ReadByte();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public sbyte ReadS8()
		{
			sbyte result = _reader.ReadSByte();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ushort ReadU16()
		{
			ushort result = _reader.ReadUInt16();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public short ReadS16()
		{
			short result = _reader.ReadInt16();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ReadU64()
		{
			ulong result = _reader.ReadUInt64();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long ReadS64()
		{
			long result = _reader.ReadInt64();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read7BitEncodedS32()
		{
			int result = _reader.Read7BitEncodedInt();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadU32()
		{
			uint result = _reader.ReadUInt32();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float ReadF32()
		{
			float result = _reader.ReadSingle();

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ReadString()
		{
			string result;
			bool isNull = _reader.ReadBoolean();

			if (isNull)
			{
				result = default;
			}
			else
			{
				result = _reader.ReadString();
			}

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsVersionValid(ushort minVersion, ushort maxVersion = ushort.MaxValue)
		{
			ushort holderVersion = _versionStack.Peek();
			bool result = TSerialization.IsVersionValid(holderVersion, minVersion, maxVersion);
			return result;
		}
	}
}
