using System.Collections.Generic;
using System.IO;
using System.Text;
using CodeGeneration.Runtime.Profiler;
using CodeGeneration.Runtime.Serialization;
using CodeGeneration.Runtime.Visitor;
using UnityEngine;

public class Test : MonoBehaviour
{
    [Visitor]
    public struct TestStruct1
    {
        public byte Field1;

        public sbyte Field2;

        public ushort Field3;

        public short Field4;

        public uint Field5;

        public int Field6;

        public ulong Field7;

        public long Field8;

        public float Field9;

        private string Field10;

        public int[] Field11;

        public Vector3 Field12;

        public Vector3[] Field13;

        public List<Vector3> Field14;

        public void Set(string s)
        {
            Field10 = s;
        }
    }
    
    [Visitor]
    public struct TestStruct11
    {
        private string Field12;

        public void Set(string v)
        {
            Field12 = v;
        }
    }
    
    public interface IAction {}
    public interface IAction<T> : IAction
    {
        void Action(ref T v);
    }
    
    private class Visitor : IVisitor, IAction<int>, IAction<float>
    {
        private IAction _action;

        public Visitor()
        {
            _action = this;
        }
        
        public void Visit<T>(string fieldName, ref T v)
        {
            Debug.LogError($"{fieldName} {v.ToString()}");
            if (_action is IAction<T> action)
            {
                action.Action(ref v);
            }
        }

        public void Action(ref int v)
        {
        }

        public void Action(ref float v)
        {
            v += 3.14f;
        }
    }

    [Serialize(1, 1)]
    public struct TestStruct2
    {
        [ForSerialization(1)]
        public int Field1;
        [ForSerialization(1)]
        public TestStruct3 Field2;
    }    

    [Serialize(2, 1)]
    public struct TestStruct3
    {
        [ForSerialization(1)]
        public string Field1;
    }
    
    [Profile]
    private TestStruct1 ProfilerTest()
    {
        TestStruct1 r = new TestStruct1();
        r.Field9 = 1.0f;
        //r.Field10 = "Hello";
        r.Set( "Hello");
        r.Field11 = new[] { 9, 9 };
        r.Field13 = new[] { Vector3.back };
        r.Field14 = new List<Vector3>();
        
        return r;
    }

    [Profile]
    void Start()
    {
        var tt = ProfilerTest();
        Debug.LogError("tt " + tt.Field12);
        
        TestStruct1 r = new TestStruct1();
        r.Field9 = 1.0f;
        r.Set( "Hello");
        //r.Field10 = "Hello";
        r.Field11 = new[] { 9, 9 };
        r.Field13 = new[] { Vector3.back };
        r.Field14 = new List<Vector3>();
        
        Visitor v = new Visitor();
        VisitorCall<TestStruct1>.Visit(v, ref r);
        
        Debug.LogError($"r.Field9 {r.Field9}");
        
        TestStruct11 tt11 = new TestStruct11();
        tt11.Set("Hello");
        VisitorCall<TestStruct11>.Visit(v, ref tt11);
        
        
        MemoryStream initialMemoryStream = new MemoryStream();

        TSerializationWriter serializationWriter = new TSerializationWriter(initialMemoryStream, Encoding.Default);

        TSerializationReader serializationReader = new TSerializationReader(initialMemoryStream, Encoding.Default);
        
        TestStruct2 before = new TestStruct2
        {
            Field1 = 999,
            Field2 = new TestStruct3()
            {
                Field1 = "Hello World"
            }
        };
     
        Debug.LogError("before " + before.Field1);
        Debug.LogError("before " + before.Field2.Field1);
        
        serializationWriter.Serialize(before);

        initialMemoryStream.Seek(0, SeekOrigin.Begin);

        TestStruct2 after = serializationReader.Deserialize<TestStruct2>();
        
        Debug.LogError("after " + after.Field1);
        Debug.LogError("after " + after.Field2.Field1);

    }

    private bool _printProfiler = false;
    
    void Update()
    {
        if (!_printProfiler)
        {
            SimpleProfiler.EndProfile();

            var elapse = SimpleProfiler.Profiler.End - SimpleProfiler.Profiler.Start;

            StringBuilder sb = new StringBuilder();
            
            for (int i = 0; i < SimpleProfiler.Profiler.Zones.Length; i++)
            {
                sb.Clear();
                ref var zone = ref SimpleProfiler.Profiler.Zones[i];
                if (zone.ElapseInclusive != 0)
                {
                    var percent = 100.0 * ((double)zone.ElapseExclusive / elapse);
                    sb.Append(SimpleProfiler.Profiler.ZoneInfos[i].Name);
                    sb.Append("[");
                    sb.Append(zone.HitCount);
                    sb.Append("]: ");
                    sb.Append(zone.ElapseExclusive);
                    sb.Append(" (");
                    sb.Append(percent);
                    sb.Append("%");
                    if (zone.ElapseInclusive != zone.ElapseExclusive)
                    {
                        sb.Append(", ");
                        var percentWithChildren = 100.0 * ((double)zone.ElapseInclusive / elapse);
                        sb.Append(percentWithChildren);
                        sb.Append("% w/children");
                    }
                    sb.Append(")");
                    Debug.LogError(sb.ToString());
                }
            }

            var time = 1000.0 * ((double)elapse / SimpleProfiler.GetFrequency());
            Debug.LogError($"Total time: {time}ms");
            
            _printProfiler = true;
        }
    }
}
