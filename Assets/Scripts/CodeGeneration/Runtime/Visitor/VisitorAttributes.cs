using System;

namespace CodeGeneration.Runtime.Visitor
{
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class VisitorAttribute : Attribute
    {
        public VisitorAttribute()
        {
        }
    }
}
