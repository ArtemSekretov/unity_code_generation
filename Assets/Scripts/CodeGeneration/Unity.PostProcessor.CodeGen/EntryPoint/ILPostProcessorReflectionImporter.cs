using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.EntryPoint
{
    // ILPostProcessorAssemblyRESOLVER does not find the .dll file for:
    // "System.Private.CoreLib"
    // we need this custom reflection importer to fix that.
    public class ILPostProcessorReflectionImporter : DefaultReflectionImporter
    {
        private const string SystemPrivateCoreLib = "System.Private.CoreLib";
        private readonly AssemblyNameReference _fixedCoreLib;

        public ILPostProcessorReflectionImporter(ModuleDefinition module) : base(module)
        {
            // find the correct library for "System.Private.CoreLib".
            // either mscorlib or netstandard.
            // defaults to System.Private.CoreLib if not found.
            _fixedCoreLib = module.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib" || a.Name == "netstandard" || a.Name == SystemPrivateCoreLib);
        }

        public override AssemblyNameReference ImportReference(AssemblyName name)
        {
            // System.Private.CoreLib?
            if (name.Name == SystemPrivateCoreLib && _fixedCoreLib != null)
            {
                return _fixedCoreLib;
            }

            // otherwise import as usual
            return base.ImportReference(name);
        }
    }
}
