using System;

namespace CodeGeneration.Runtime.Profiler
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProfileAttribute : Attribute
    {
        public ProfileAttribute()
        {
        }
    }
}
