using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace CodeGeneration.Runtime.Serialization
{

	public struct Serialization<T>
	{
		public int Key;
		public ushort Version;

		public Save<T> Save;
		public Load<T> Load;
		public Create<T> Create;
	}

	public delegate T Create<T>();
	public delegate void Save<T>(T obj, TSerializationWriter writer);
	public delegate void Load<T>(ref T obj, TSerializationReader reader);
	
	public class TSerializationBox<T> : TSerializationBaseBox
	{
		public override Type BoxType => typeof(T);

		public TSerializationBox(int key, ushort version) : base(key, version)
		{

		}

		public override object Load(TSerializationReader reader)
		{
			Serialization<T> serialization = TSerialization.GetSerialization<T>();
			T result = serialization.Create();
			serialization.Load(ref result, reader);

			return result;
		}

		public override void Save(object value, TSerializationWriter writer)
		{
			Serialization<T> serialization = TSerialization.GetSerialization<T>();
			serialization.Save((T)value, writer);
		}
	}

	public abstract class TSerializationBaseBox
	{
		public abstract Type BoxType { get; }
		public int Key;
		public ushort Version;

		protected TSerializationBaseBox(int key, ushort version)
		{
			Key = key;
			Version = version;
		}

		public abstract object Load(TSerializationReader reader);

		public abstract void Save(object value, TSerializationWriter writer);
	}

	public static class TSerialization 
	{
		public struct StaticSerializationLookup<T>
		{
			public static Serialization<T> Serialization;
		}

		public const int NullKey = 0;

		private static readonly Dictionary<int, TSerializationBaseBox> _boxLoadersByKey = new Dictionary<int, TSerializationBaseBox>();
		private static readonly Dictionary<Type, TSerializationBaseBox> _boxLoadersByType = new Dictionary<Type, TSerializationBaseBox>();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Serialization<T> GetSerialization<T>()
		{
			Serialization<T> result = StaticSerializationLookup<T>.Serialization;
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool GetBoxLoader(int key, out TSerializationBaseBox boxLoader)
		{
			return _boxLoadersByKey.TryGetValue(key, out boxLoader);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool GetBoxLoader(Type type, out TSerializationBaseBox boxLoader)
		{
			return _boxLoadersByType.TryGetValue(type, out boxLoader);
		}

		public static void RegisterType<T>(Serialization<T> serialization)
		{
			int key = serialization.Key;
			ushort version = serialization.Version;

			Type targetType = typeof(T);

			if (key == NullKey && version == 0)
			{
				throw new Exception("Type [ " + targetType + " ] don't have valid key and version");
			}

			Serialization<T> oldSerialization = StaticSerializationLookup<T>.Serialization;

			UnityEngine.Debug.Assert(oldSerialization.Key == NullKey, $"Type {targetType} already declare");

			StaticSerializationLookup<T>.Serialization = serialization;

			if (_boxLoadersByKey.TryGetValue(key, out var value))
			{
				UnityEngine.Debug.Assert(false, $"In Type {targetType} key [{key}] is already used by type: {value.BoxType}");
			}
			else
			{
				TSerializationBox<T> serializationBox = new TSerializationBox<T>(key, version);
				_boxLoadersByKey.Add(key, serializationBox);
				_boxLoadersByType.Add(targetType, serializationBox);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsVersionValid(ushort version, ushort minVersion, ushort maxVersion)
		{
			bool result = version >= minVersion && version < maxVersion;

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsEquals<T>(T a, T b)
		{
			return EqualityComparer<T>.Default.Equals(a, b);
		}
	}
}