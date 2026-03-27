using System.IO;
// to use Mono.Cecil here, we need to 'override references' in the
// Unity.Mirror.CodeGen assembly definition file in the Editor, and add Cecil.
// otherwise we get a reflection exception with 'file not found: Cecil'.
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

// IMPORTANT: 'using UnityEngine' does not work in here.
// Unity gives "(0,0): error System.Security.SecurityException: ECall methods must be packaged into a system module."
//using UnityEngine;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.EntryPoint
{
    public class ILPostProcessorHook : ILPostProcessor
    {
        // ILPostProcessor is invoked by Unity.
        // we can not tell it to ignore certain assemblies before processing.
        // add a 'ignore' define for convenience.
        private const string IgnoreDefine = "ILPP_IGNORE";

        // we can't use Debug.Log in ILPP, so we need a custom logger
        private readonly ILPostProcessorLogger _log = new ILPostProcessorLogger();

        // ???
        public override ILPostProcessor GetInstance() => this;

        // check if assembly has the 'ignore' define
        static bool HasDefine(ICompiledAssembly assembly, string define)
        {
            foreach (string defineName in assembly.Defines)
            {
                if (string.Equals(define, defineName))
                {
                    return true;
                }
            }

            return false;
        }
        
        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            bool ignore = HasDefine(compiledAssembly, IgnoreDefine);
            return !ignore;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            //_log.LogWarning("ILProcessor", $"ILPostProcess {compiledAssembly.Name}");

            // load the InMemoryAssembly peData into a MemoryStream
            byte[] peData = compiledAssembly.InMemoryAssembly.PeData;
            using (MemoryStream stream = new MemoryStream(peData))
            using (ILPostProcessorAssemblyResolver asmResolver = new ILPostProcessorAssemblyResolver(compiledAssembly, _log))
            {
                // we need to load symbols. otherwise we get:
                // "(0,0): error Mono.CecilX.Cil.SymbolsNotFoundException: No symbol found for file: "
                using (MemoryStream symbols = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData))
                {
                    ReaderParameters readerParameters = new ReaderParameters{
                        SymbolStream = symbols,
                        ReadWrite = true,
                        ReadSymbols = true,
                        AssemblyResolver = asmResolver,
                        // custom reflection importer to fix System.Private.CoreLib
                        // not being found in custom assembly resolver above.
                        ReflectionImporterProvider = new ILPostProcessorReflectionImporterProvider()
                    };
                    using (AssemblyDefinition asmDef = AssemblyDefinition.ReadAssembly(stream, readerParameters))
                    {
                        // resolving a dll type while
                        // changing dll does not work. it throws a
                        // NullReferenceException 
                        // when Resolve() is called on the type.
                        // need to add the AssemblyDefinition itself to use.
                        asmResolver.SetAssemblyDefinitionForCompiledAssembly(asmDef);

                        var bender = new Bender(_log);
                        if (bender.Bend(asmDef, asmResolver, out bool modified))
                        {
                            //_log.LogWarning("ILProcessor", $"Weaving succeeded for: {compiledAssembly.Name} -> {modified}");

                            // write if modified
                            if (modified)
                            {
                                // we used ImportReference for all type and can create reference to itself 
                                // this will case exception so we manually remove this references;
                                for (int i = asmDef.MainModule.AssemblyReferences.Count - 1; i >= 0; i--)
                                {
                                    var asmRef = asmDef.MainModule.AssemblyReferences[i];
                                    if (asmRef.Name == asmDef.Name.Name)
                                    {
                                        asmDef.MainModule.AssemblyReferences.RemoveAt(i);
                                    }
                                }

                                MemoryStream peOut = new MemoryStream();
                                MemoryStream pdbOut = new MemoryStream();
                                WriterParameters writerParameters = new WriterParameters
                                {
                                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                                    SymbolStream = pdbOut,
                                    WriteSymbols = true
                                };

                                asmDef.Write(peOut, writerParameters);

                                InMemoryAssembly inMemory = new InMemoryAssembly(peOut.ToArray(), pdbOut.ToArray());
                                return new ILPostProcessResult(inMemory, _log.Logs);
                            }
                        }
                    }
                }
            }

            // always return an ILPostProcessResult with Logs.
            // otherwise we won't see Logs if we failed.
            return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, _log.Logs);
        }
    }
}
