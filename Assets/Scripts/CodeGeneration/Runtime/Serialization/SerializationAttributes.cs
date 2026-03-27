using System;

namespace CodeGeneration.Runtime.Serialization
{
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class SerializeAttribute : Attribute
    {
        public Type[] Types;
        public int Key;
        public ushort Version;

        public SerializeAttribute(int key, ushort version, params Type[] types)
        {
            Types = types;
            Key = key;
            Version = version;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class ForSerializationAttribute : Attribute
    {
        public ushort MinVersion;
        public ushort MaxVersion;

        public ForSerializationAttribute(ushort minVersion, ushort maxVersion = ushort.MaxValue)
        {
            MinVersion = minVersion;
            MaxVersion = maxVersion;
        }
    }
}
