namespace CodeGeneration.Runtime.Serialization
{
	public static class SerializationHelper
	{	
		public static uint EncodeZigZag32(int n)
		{
			return (uint) ((n << 1) ^ (n >> 31));
		}
		
		public static int DecodeZigZag32(uint n)
		{
			return (int)(n >> 1) ^ -(int)(n & 1);
		}
		
		public static ulong EncodeZigZag64(long n)
		{
			return (ulong) ((n << 1) ^ (n >> 63));
		}
		
		public static long DecodeZigZag64(ulong n)
		{
			return (long)(n >> 1) ^ -(long)(n & 1);
		}
	}
}