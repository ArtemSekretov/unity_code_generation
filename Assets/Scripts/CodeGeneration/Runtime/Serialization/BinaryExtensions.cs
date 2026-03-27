using System;
using System.IO;

namespace CodeGeneration.Runtime.Serialization
{
	public static class BinaryExtension
	{
		public static void Write7BitEncodedInt(this BinaryWriter writer, int value)
		{
			uint encodeZigZag32 = SerializationHelper.EncodeZigZag32(value);
			Write7BitEncodedUInt(writer, encodeZigZag32);
		}

		public static void Write7BitEncodedLong(this BinaryWriter writer, long value)
		{
			ulong encodeZigZag32 = SerializationHelper.EncodeZigZag64(value);
			Write7BitEncodedULong(writer, encodeZigZag32);
		}

		public static int Read7BitEncodedInt(this BinaryReader reader)
		{
			uint u = Read7BitEncodedUInt(reader);
			return SerializationHelper.DecodeZigZag32(u);
		}

		public static long Read7BitEncodedLong(this BinaryReader reader)
		{
			ulong read7BitEncodedULong = Read7BitEncodedULong(reader);
			return SerializationHelper.DecodeZigZag64(read7BitEncodedULong);
		}

		public static void Write7BitEncodedUInt(this BinaryWriter writer, uint value)
		{
			uint v = value;
			while (v >= 0x80) {
				writer.Write((byte) (v | 0x80));
				v >>= 7;
			}
			writer.Write((byte)v);
		}
		
		public static uint Read7BitEncodedUInt(this BinaryReader reader)
		{
			uint count = 0;
			int shift = 0;
			byte b;
			do {
				if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
					throw new Exception("Bad7BitInt32 Format");
 
				b = reader.ReadByte();
				count |= (uint)(b & 0x7F) << shift;
				shift += 7;
			} while ((b & 0x80) != 0);
			return count;
		}
		
		public static void Write7BitEncodedULong(this BinaryWriter writer, ulong value)
		{
			ulong v = value;
			while (v >= 0x80) {
				writer.Write((byte) (v | 0x80));
				v >>= 7;
			}
			writer.Write((byte)v);
		}
		
		public static ulong Read7BitEncodedULong(this BinaryReader reader)
		{
			ulong count = 0;
			int shift = 0;
			byte b;
			do {
				if (shift == 10 * 7)  // 5 bytes max per Int32, shift += 7
					throw new Exception("Bad7BitInt32 Format");
 
				b = reader.ReadByte();
				count |= (ulong)(b & 0x7F) << shift;
				shift += 7;
			} while ((b & 0x80) != 0);
			return count;
		}
	}
}