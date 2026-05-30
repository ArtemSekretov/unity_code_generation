using System.Collections.Generic;
using CodeGeneration.Runtime.Visitor;
using UnityEngine;

namespace CodeGenerationTestNamespace
{
    public partial class Wrapper
    {
        [Visitor]
        public partial struct TestStruct1
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
    }
}

public class RoslynCodeGenerationTest : MonoBehaviour
{
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
    
    void Start()
    {
        CodeGenerationTestNamespace.Wrapper.TestStruct1 r = new CodeGenerationTestNamespace.Wrapper.TestStruct1();
        r.Field9 = 1.0f;
        r.Set( "Hello");
        //r.Field10 = "Hello";
        r.Field11 = new[] { 9, 9 };
        r.Field13 = new[] { Vector3.back };
        r.Field14 = new List<Vector3>();
        
        Visitor v = new Visitor();
        VisitorCall<CodeGenerationTestNamespace.Wrapper.TestStruct1>.Visit(v, ref r);
        
        
        Debug.LogError($"r.Field9 {r.Field9}");
    }
}
