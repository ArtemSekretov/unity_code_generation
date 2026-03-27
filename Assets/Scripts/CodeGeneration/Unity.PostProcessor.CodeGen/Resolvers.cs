using Mono.Cecil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen
{
    public static class Resolvers
    {
        public static MethodReference ResolveMethod(TypeReference tr, AssemblyDefinition assembly, ILogger log, string name, ref bool bendingFailed)
        {
            if (tr == null)
            {
                log.LogError("Resolver", $"Cannot resolve method {name} without a class");
                bendingFailed = true;
                return null;
            }
            MethodReference method = ResolveMethod(tr, assembly, log, m => m.Name == name, ref bendingFailed);
            if (method == null)
            {
                log.LogError("Resolver",$"Method not found with name {name} in type {tr.Name}");
                bendingFailed = true;
            }
            return method;
        }

        private static MethodReference ResolveMethod(TypeReference t, AssemblyDefinition assembly, ILogger log, System.Func<MethodDefinition, bool> predicate, ref bool bendingFailed)
        {
            var resolvedType = t.Resolve();
            if (resolvedType == null)
            {
                log.LogError("Resolver", $"Cannot resolve {t.Name} assembly: {t.Module.Assembly.FullName}");
                bendingFailed = true;
                return null;                
            }
            
            foreach (MethodDefinition methodRef in resolvedType.Methods)
            {
                if (predicate(methodRef))
                {
                    return assembly.MainModule.ImportReference(methodRef);
                }
            }

            log.LogError("Resolver", $"Method not found in type {t.Name}");
            bendingFailed = true;
            return null;
        }
    }
}
