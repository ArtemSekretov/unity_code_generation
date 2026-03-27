using Mono.Cecil;
using UnityEngine;

namespace CodeGeneration.Unity.PostProcessor.CodeGen
{
    public static class BenderUtility
    {
        // helper function to add [RuntimeInitializeOnLoad] attribute to method
        public static void AddRuntimeInitializeOnLoadAttribute(AssemblyDefinition assembly, MethodDefinition method)
        {
            // [RuntimeInitializeOnLoadMethod]
            TypeReference runtimeInitializeOnLoadMethodAttributeRef = assembly.Import(typeof(RuntimeInitializeOnLoadMethodAttribute));
            var runtimeInitializeOnLoadMethodAttribute = runtimeInitializeOnLoadMethodAttributeRef.Resolve();

            // to add a CustomAttribute, we need the attribute's constructor.
            // in this case, there are two: empty, and RuntimeInitializeOnLoadType.
            // we want the last one, with the type parameter.
            var ctors = runtimeInitializeOnLoadMethodAttribute.GetConstructors();
            var ctor = ctors[ctors.Count - 1];
            //MethodDefinition ctor = weaverTypes.runtimeInitializeOnLoadMethodAttribute.GetConstructors().First();
            // using ctor directly throws: ArgumentException: Member 'System.Void UnityEditor.InitializeOnLoadMethodAttribute::.ctor()' is declared in another module and needs to be imported
            // we need to import it first.
            CustomAttribute attribute = new CustomAttribute(assembly.MainModule.ImportReference(ctor));
            // add the RuntimeInitializeLoadType.BeforeSceneLoad argument to ctor
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(assembly.Import<RuntimeInitializeLoadType>(),
                RuntimeInitializeLoadType.BeforeSceneLoad));
            method.CustomAttributes.Add(attribute);
        }
    }
}
