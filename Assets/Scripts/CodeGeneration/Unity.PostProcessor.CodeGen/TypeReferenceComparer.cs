using System.Collections.Generic;
using Mono.Cecil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen
{
    public class TypeReferenceComparer : IEqualityComparer<TypeReference>
    {
        public bool Equals(TypeReference x, TypeReference y)
        {
            return string.Equals(x.FullName, y.FullName);
        }

        public int GetHashCode(TypeReference obj)
        {
            return obj.FullName.GetHashCode();
        }
    }
}
