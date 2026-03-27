using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.Serialization
{
    public class Create
    {
        private readonly AssemblyDefinition _assembly;
        private readonly ILogger _logger;
        
        public Create(AssemblyDefinition assembly, ILogger logger)
        {
            _assembly = assembly;
            _logger = logger;
        }
        
        private MethodDefinition GenerateCreateFunc(TypeReference variable)
        {
            string functionName = $"_Create_{variable.FullName}";
            MethodDefinition createFunc = new MethodDefinition(functionName,
                MethodAttributes.Public |
                MethodAttributes.Static |
                MethodAttributes.HideBySig, variable);

            createFunc.Body.InitLocals = true;

            return createFunc;
        }
        
        public MethodDefinition GenerateCreateFunction(TypeReference targetType)
        {
            var createFunc = GenerateCreateFunc(targetType);
                        
            ILProcessor worker = createFunc.Body.GetILProcessor();
            
            if(targetType.IsValueType)
            {
                VariableDefinition variableDefinition = new VariableDefinition(targetType);
                worker.Body.Variables.Add(variableDefinition);
                
                worker.Emit(OpCodes.Ldloca_S, variableDefinition);
                worker.Emit(OpCodes.Initobj, targetType);
                worker.Emit(OpCodes.Ldloc_0);
            }
            else
            {
                var targetTypeDefinition = targetType.Resolve();
                var constructors = targetTypeDefinition.GetConstructors();

                MethodDefinition ctor = null;
                
                foreach (var constructor in constructors)
                {
                    if (!constructor.IsStatic && constructor.Parameters.Count == 0)
                    {
                        ctor = constructor;
                        break;
                    }
                }

                if (ctor != null)
                {
                    worker.Emit(OpCodes.Newobj, ctor);
                }
                else
                {
                    _logger.LogError("Bender",$"Does not find parameterless constructor for {targetType.FullName}");
                }
            }
            
            worker.Emit(OpCodes.Ret);

            return createFunc;
        }
    }
}
