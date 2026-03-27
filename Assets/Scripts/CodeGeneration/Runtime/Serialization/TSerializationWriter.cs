using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace CodeGeneration.Runtime.Serialization
{
	public class TSerializationWriter
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

	    private readonly BinaryWriter _writer;

	    private readonly Stack<ushort> _versionStack;

		public TSerializationWriter(Stream stream, Encoding encoding)
		{
			_versionStack = new Stack<ushort>();
#if TSERIALIZATION_DEBUG
			Operations = new List<Operation>();
			operationIndexStack = new Stack<int>();
#endif
			_writer = new BinaryWriter(stream, encoding);
		}

		public void Serialize<T>(T value)
		{
			_versionStack.Clear();

#if TSERIALIZATION_DEBUG
			Operations.Clear();
			operationIndexStack.Clear();
			long pos = BeginWriteOperation();
#endif

			Write(value);

#if TSERIALIZATION_DEBUG
			EndWriteOperation(nameof(Serialize) + " (include key and version)", pos);
#endif
		}
		
		public void Write<T>(T value)
		{
			bool isValueZero = TSerialization.IsEquals(value, default);

			if (!isValueZero)
			{
				Serialization<T> serialization = TSerialization.GetSerialization<T>();

				if (serialization.Key != TSerialization.NullKey)
				{
					ushort fieldVersion = serialization.Version;
					_writer.Write7BitEncodedInt(serialization.Key);

					_writer.Write(fieldVersion);

					_versionStack.Push(fieldVersion);

					serialization.Save(value, this);

					_versionStack.Pop();
				}
				else
				{
					if (TSerialization.GetBoxLoader(value.GetType(), out TSerializationBaseBox serializationBox))
					{
						ushort fieldVersion = serializationBox.Version;
						_writer.Write7BitEncodedInt(serializationBox.Key);

						_writer.Write(fieldVersion);

						_versionStack.Push(fieldVersion);

						serializationBox.Save(value, this);

						_versionStack.Pop();
					}
					else
					{
						_writer.Write7BitEncodedInt(TSerialization.NullKey);
						UnityEngine.Debug.Assert(false, $"Type {value.GetType()} don't registrate in serialization");
					}
				}
			}
			else
			{
				_writer.Write7BitEncodedInt(TSerialization.NullKey);
			}
		}

#if TSERIALIZATION_DEBUG
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long BeginWriteOperation()
		{
			operationIndexStack.Push(Operations.Count);
			Operations.Add(new Operation());
			return writer.BaseStream.Position;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EndWriteOperation(string operationName, long position)
		{
			int operationIndex = operationIndexStack.Pop();
			var stack = Operations[operationIndex];

			int depth = operationIndexStack.Count;
			stack.OperationName = operationName;
			stack.Size = (int) (writer.BaseStream.Position - position);
			stack.Depth = depth;
			Operations[operationIndex] = stack;
		}
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteS32(int value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Write7BitEncodedS32(int value)
		{
			_writer.Write7BitEncodedInt(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteU32(uint value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteF32(float value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteU8(byte value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteS8(sbyte value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteU16(ushort value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteS16(short value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteU64(ulong value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteS64(long value)
		{
			_writer.Write(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteString(string value)
		{
			bool isNull = value == default;

			_writer.Write(isNull);

			if (!isNull)
			{
				_writer.Write(value);
			}
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