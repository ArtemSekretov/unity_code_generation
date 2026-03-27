using System.Collections.Generic;
using CodeGeneration.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.Serialization
{
    public class Reader
    {
        private readonly Dictionary<TypeReference, MethodReference> _typeTable = new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());
        private readonly AssemblyDefinition _assembly;

        private readonly MethodReference _isVersionValid;
        private readonly MethodReference _readGeneric;
        private readonly Dictionary<TypeReference, ILGenerationData> _supportedTypes;
        private readonly ILogger _logger;
        
        public Reader(AssemblyDefinition assembly, ILogger logger, Dictionary<TypeReference, ILGenerationData> supportedTypes, ref bool bendingFailed)
        {
            _assembly = assembly;
            _supportedTypes = supportedTypes;
            _logger = logger;
            var serializationReader = assembly.Import(typeof(TSerializationReader));
            
            _typeTable.Add(assembly.Import(typeof(byte)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadU8", ref bendingFailed));
            _typeTable.Add(assembly.Import(typeof(sbyte)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadS8", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(short)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadS16", ref bendingFailed));
            _typeTable.Add(assembly.Import(typeof(ushort)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadU16", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(int)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadS32", ref bendingFailed));
            _typeTable.Add(assembly.Import(typeof(uint)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadU32", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(long)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadS64", ref bendingFailed));
            _typeTable.Add(assembly.Import(typeof(ulong)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadU64", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(float)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadF32", ref bendingFailed));
            
            _typeTable.Add(assembly.Import(typeof(string)), 
                Resolvers.ResolveMethod(serializationReader, assembly, logger, "ReadString", ref bendingFailed));
            
            _isVersionValid = Resolvers.ResolveMethod(serializationReader, assembly, logger, "IsVersionValid", ref bendingFailed);
            _readGeneric = Resolvers.ResolveMethod(serializationReader, assembly, logger, "Read", ref bendingFailed);
        }
        
        private MethodDefinition GenerateLoadFunc(TypeReference variable)
        {
            string functionName = $"_Load_{variable.FullName}";
            MethodDefinition loadFunc = new MethodDefinition(functionName,
                MethodAttributes.Public |
                MethodAttributes.Static |
                MethodAttributes.HideBySig,
                _assembly.Import(typeof(void)));

            var refType = new ByReferenceType(variable);
            
            loadFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, refType));
            loadFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, _assembly.Import<TSerializationReader>()));
            loadFunc.Body.InitLocals = true;

            _typeTable.Add(variable, loadFunc);
            
            return loadFunc;
        }
        
        public MethodDefinition GenerateLoadFunction(ILGenerationData generationData)
        {
            if (_typeTable.TryGetValue(generationData.OwnerType, out var methodReference))
            {
                return methodReference.Resolve();
            }
            
            var loadFunc = GenerateLoadFunc(generationData.OwnerType);
                        
            ILProcessor worker = loadFunc.Body.GetILProcessor();

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
                    worker.Emit(OpCodes.Ldarg_0);
                    worker.Emit(OpCodes.Ldarg_1);
                                
                    worker.Emit(OpCodes.Call, method);
                                    
                    FieldReference fieldRef = _assembly.MainModule.ImportReference(ilGenerationFieldData.Owner);
                    worker.Emit(OpCodes.Stfld, fieldRef);
                }
                else
                {
                    if (_supportedTypes.TryGetValue(fieldType, out var type))
                    {
                        GenerateLoadFunction(type);
                        
                        worker.Emit(OpCodes.Ldarg_0);
                        worker.Emit(OpCodes.Ldarg_1);
                        
                        var instanceRead = _readGeneric.MakeGeneric(_assembly.MainModule, fieldType);                        
                        worker.Emit(OpCodes.Call, instanceRead);
                                
                        FieldReference fieldRef = _assembly.MainModule.ImportReference(ilGenerationFieldData.Owner);
                        worker.Emit(OpCodes.Stfld, fieldRef);
                    }
                    else
                    {
                        _logger.LogError("Bender", $"Field {ilGenerationFieldData.Owner.Name} has ForSerialization Attribute, but type {fieldType} does not have Serialize Attribute");
                    }
                }
                
                worker.Append(jumpInstruction);
            }
                        
            worker.Emit(OpCodes.Ret);

            return loadFunc;
        }
    }
}
