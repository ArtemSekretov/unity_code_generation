using System.Collections.Generic;
using CodeGeneration.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.Serialization
{
    public class Writer
    {
        private readonly Dictionary<TypeReference, MethodReference> _typeTable = new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());
        private readonly AssemblyDefinition _assembly;
        
        private readonly MethodReference _isVersionValid;
        private readonly MethodReference _writeGeneric;
        private readonly Dictionary<TypeReference, ILGenerationData> _supportedTypes;
        private readonly ILogger _logger;
        
        public Writer(AssemblyDefinition assembly, ILogger logger, Dictionary<TypeReference, ILGenerationData> supportedTypes, ref bool bendingFailed)
        {
            _assembly = assembly;
            _supportedTypes = supportedTypes;
            _logger = logger;
            
            var serializationWriter = assembly.Import(typeof(TSerializationWriter));
            
            _typeTable.Add(assembly.Import(typeof(byte)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteU8", ref bendingFailed));
            _typeTable.Add(assembly.Import(typeof(sbyte)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteS8", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(short)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteS16", ref bendingFailed));
            _typeTable.Add(assembly.Import(typeof(ushort)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteU16", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(int)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteS32", ref bendingFailed));
            _typeTable.Add(assembly.Import(typeof(uint)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteU32", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(long)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteS64", ref bendingFailed));
            _typeTable.Add(assembly.Import(typeof(ulong)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteU64", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(float)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteF32", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(string)), 
                Resolvers.ResolveMethod(serializationWriter, assembly, logger, "WriteString", ref bendingFailed));
            
            _isVersionValid = Resolvers.ResolveMethod(serializationWriter, assembly, logger, "IsVersionValid", ref bendingFailed);
            _writeGeneric = Resolvers.ResolveMethod(serializationWriter, assembly, logger, "Write", ref bendingFailed);
        }
        
        private MethodDefinition GenerateSaveFunc(TypeReference variable)
        {
            string functionName = $"_Save_{variable.FullName}";
            MethodDefinition saveFunc = new MethodDefinition(functionName,
                MethodAttributes.Public |
                MethodAttributes.Static |
                MethodAttributes.HideBySig,
                _assembly.Import(typeof(void)));
            
            saveFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, variable));
            saveFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, _assembly.Import<TSerializationWriter>()));
            saveFunc.Body.InitLocals = true;
            
            _typeTable.Add(variable, saveFunc);
            
            return saveFunc;
        }
        
        public MethodDefinition GenerateSaveFunction(ILGenerationData generationData)
        {
            if (_typeTable.TryGetValue(generationData.OwnerType, out var methodReference))
            {
                return methodReference.Resolve();
            }
            
            var saveFunc = GenerateSaveFunc(generationData.OwnerType);
                        
            ILProcessor worker = saveFunc.Body.GetILProcessor();

            foreach (var ilGenerationFieldData in generationData.Fields)
            {
                var fieldType = ilGenerationFieldData.Owner.FieldType;
                
                var jumpInstruction = worker.Create(OpCodes.Nop);
                
                worker.Emit(OpCodes.Ldarg_1);
                worker.Emit(OpCodes.Ldc_I4, ilGenerationFieldData.AttributeData.MinVersion);
                worker.Emit(OpCodes.Ldc_I4, ilGenerationFieldData.AttributeData.MaxVersion);
                worker.Emit(OpCodes.Call, _isVersionValid);
                worker.Emit(OpCodes.Brfalse, jumpInstruction);
                
                if (_typeTable.TryGetValue(fieldType, out var method))
                {
                    worker.Emit(OpCodes.Ldarg_1);
                    worker.Emit(OpCodes.Ldarg_0);
                    
                    FieldReference fieldRef = _assembly.MainModule.ImportReference(ilGenerationFieldData.Owner);
                    worker.Emit(OpCodes.Ldfld, fieldRef);
                    
                    worker.Emit(OpCodes.Call, method);
                }
                else
                {
                    if (_supportedTypes.TryGetValue(fieldType, out var type))
                    {
                        GenerateSaveFunction(type);
                        
                        worker.Emit(OpCodes.Ldarg_1);
                        worker.Emit(OpCodes.Ldarg_0);
                    
                        FieldReference fieldRef = _assembly.MainModule.ImportReference(ilGenerationFieldData.Owner);
                        worker.Emit(OpCodes.Ldfld, fieldRef);
                    
                        var instanceWrite = _writeGeneric.MakeGeneric(_assembly.MainModule, fieldType);                        
                        worker.Emit(OpCodes.Call, instanceWrite);
                    }
                    else
                    {
                        _logger.LogError("Bender", $"Field {ilGenerationFieldData.Owner.Name} in object {generationData.OwnerType.FullName} has ForSerialization Attribute, but type {fieldType} does not have Serialize Attribute");
                    }
                }
                worker.Append(jumpInstruction);
            }
                        
            worker.Emit(OpCodes.Ret);

            return saveFunc;
        }
    }
}
