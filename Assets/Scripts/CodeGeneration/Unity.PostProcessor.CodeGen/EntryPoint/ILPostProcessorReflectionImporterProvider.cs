using Mono.Cecil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.EntryPoint
{
    // ILPostProcessorAssemblyRESOLVER does not find the .dll file for:
    // "System.Private.CoreLib"
    // we need this custom reflection importer to fix that.
    public class ILPostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition module) =>
            new ILPostProcessorReflectionImporter(module);
    }
}
