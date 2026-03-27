using System;
using System.Collections.Generic;
using CodeGeneration.Unity.PostProcessor.CodeGen.Profiler;
using CodeGeneration.Unity.PostProcessor.CodeGen.Serialization;
using CodeGeneration.Unity.PostProcessor.CodeGen.Visitor;
using Mono.Cecil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen
{
    public class Bender
    {
        public const string GeneratedCodeNamespace = "CodeGeneration";
        public const string GeneratedCodeClassName = "GeneratedCode";
        private TypeDefinition _generatedCodeClass;
        
        private readonly ILogger _logger;

        public Bender(ILogger logger)
        {
            _logger = logger;
        }

        private void GetType(Mono.Collections.Generic.Collection<TypeDefinition> types, List<TypeDefinition> result)
        {
            foreach (TypeDefinition klass in types)
            {
                result.Add(klass);
                GetType(klass.NestedTypes, result);
            }           
        }
        
        public bool Bend(AssemblyDefinition assembly, IAssemblyResolver resolver, out bool modified)
        {
            bool bendingFailed = false;
            modified = false;

            try
            {
                if (assembly.MainModule.ContainsClass(GeneratedCodeNamespace, GeneratedCodeClassName))
                {
                    return true;
                }
                
                List<TypeDefinition> typeDefinitions = new List<TypeDefinition>();
                GetType(assembly.MainModule.Types, typeDefinitions);

                _generatedCodeClass = new TypeDefinition(GeneratedCodeNamespace, GeneratedCodeClassName,
                    TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Abstract | TypeAttributes.Sealed,
                    assembly.Import<object>());
                
                
                modified |= SerializationProcessor.Process(assembly, _logger, _generatedCodeClass, typeDefinitions, ref bendingFailed);
                modified |= VisitorProcessor.Process(assembly, _logger, _generatedCodeClass, typeDefinitions, ref bendingFailed);
                modified |= SimpleProfilerProcessor.Process(assembly, _logger, _generatedCodeClass, typeDefinitions, ref bendingFailed);

                if (bendingFailed)
                {
                    return  false;
                }
                
                if (modified)
                {
                    assembly.MainModule.Types.Add(_generatedCodeClass);
                }
                
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError("Bender",$"Exception :{e}");
                bendingFailed = true;
                return false;
            }
        }
    }
}