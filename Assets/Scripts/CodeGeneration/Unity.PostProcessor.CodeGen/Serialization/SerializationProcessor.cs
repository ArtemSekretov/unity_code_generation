using System;
using System.Collections.Generic;
using CodeGeneration.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.Serialization
{
    public struct SerializeAttributeData
    {
        public TypeDefinition[] Types;
        public int Key;
        public ushort Version;
    }
        
    public struct ForSerializeAttributeData
    {
        public ushort MinVersion;
        public ushort MaxVersion;
    }
        
    public struct ILGenerationFieldData
    {
        public FieldReference Owner;
        public ForSerializeAttributeData AttributeData;
    }
        
    public struct ILGenerationData
    {
        public int Key;
        public ushort Version;
        public TypeReference OwnerType;
        public List<ILGenerationFieldData> Fields;
    }
    
    public static class SerializationProcessor
    {
        private struct MethodsToInit
        {
            public int Key;
            public ushort Version;
            public TypeReference Owner;
            public MethodDefinition Load;
            public MethodDefinition Create;
            public MethodDefinition Save;
        }
        
        private static SerializeAttributeData ExtractDataSerializeAttribute(CustomAttribute attribute)
        {
            SerializeAttributeData result = new SerializeAttributeData()
            {
                Key = -1,
                Version = 0,
                Types = Array.Empty<TypeDefinition>()
            };

            if (attribute.ConstructorArguments.Count >= 2)
            {
                var attribute0 = attribute.ConstructorArguments[0];
                if (attribute0.Type.Is<int>())
                {
                    result.Key = (int)attribute0.Value;
                }
                
                var attribute1 = attribute.ConstructorArguments[1];
                if (attribute1.Type.Is<ushort>())
                {
                    result.Version = (ushort)attribute1.Value;
                }

                if (attribute.ConstructorArguments.Count > 2)
                {
                    var attribute2 = attribute.ConstructorArguments[2];
                    if (attribute2.Type.Is<Type[]>())
                    {
                        var typeParams = (CustomAttributeArgument[])attribute2.Value;
                        result.Types = new TypeDefinition[typeParams.Length];

                        for (int i = 0; i < typeParams.Length; i++)
                        {
                            var argument = typeParams[i];
                            if (argument.Type.Is<Type>())
                            {
                                result.Types[i] = (TypeDefinition)argument.Value;
                            }
                        }
                    }
                }
            }
            
            return result;
        }
        
        private static ForSerializeAttributeData ExtractDataForSerializeAttribute(CustomAttribute attribute)
        {
            ForSerializeAttributeData result = new ForSerializeAttributeData()
            {
                MinVersion = 0,
                MaxVersion = ushort.MaxValue,
            };

            if (attribute.ConstructorArguments.Count == 2)
            {
                var attribute0 = attribute.ConstructorArguments[0];
                if (attribute0.Type.Is<ushort>())
                {
                    result.MinVersion = (ushort)attribute0.Value;
                }
                
                var attribute1 = attribute.ConstructorArguments[1];
                if (attribute1.Type.Is<ushort>())
                {
                    result.MaxVersion = (ushort)attribute1.Value;
                }
            }
            
            return result;
        }
        
        public static bool Process(AssemblyDefinition assembly, ILogger logger, TypeDefinition generatedCodeClass, List<TypeDefinition> typesToProcess, ref bool bendingFailed)
        {
            bool modified = false;
            
            Dictionary<TypeReference, ILGenerationData> typesToGenerate = new Dictionary<TypeReference, ILGenerationData>(new TypeReferenceComparer());

            foreach (TypeDefinition klass in typesToProcess)
            {
                if (klass.HasCustomAttribute<SerializeAttribute>())
                {
                    var classAttr = klass.GetCustomAttribute<SerializeAttribute>();
                    var classAttrData = ExtractDataSerializeAttribute(classAttr);

                    List<ILGenerationFieldData> fields = new List<ILGenerationFieldData>();

                    foreach (FieldDefinition field in klass.Fields)
                    {
                        if (field.HasCustomAttribute<ForSerializationAttribute>())
                        {
                            var fieldAttr = field.GetCustomAttribute<ForSerializationAttribute>();
                            var fieldAttrData = ExtractDataForSerializeAttribute(fieldAttr);

                            fields.Add(new ILGenerationFieldData()
                            {
                                Owner = field,
                                AttributeData = fieldAttrData
                            });
                        }
                    }

                    typesToGenerate.Add(klass, new ILGenerationData()
                    {
                        Fields = fields,
                        Key = classAttrData.Key,
                        Version = classAttrData.Version,
                        OwnerType = klass
                    });
                }
            }

            if (typesToGenerate.Count > 0)
            {
                Reader reader = new Reader(assembly, logger, typesToGenerate, ref bendingFailed);
                Writer writer = new Writer(assembly, logger, typesToGenerate, ref bendingFailed);
                Create create = new Create(assembly, logger);

                List<MethodsToInit> methods = new List<MethodsToInit>();

                foreach (var type in typesToGenerate)
                {
                    var generationData = type.Value;
                    
                    var loadFunc = reader.GenerateLoadFunction(generationData);
                    var createFunc = create.GenerateCreateFunction(generationData.OwnerType);
                    var saveFunc = writer.GenerateSaveFunction(generationData);

                    generatedCodeClass.Methods.Add(loadFunc);
                    generatedCodeClass.Methods.Add(createFunc);
                    generatedCodeClass.Methods.Add(saveFunc);

                    methods.Add(new MethodsToInit
                    {
                        Key = generationData.Key,
                        Version = generationData.Version,
                        Owner = generationData.OwnerType,
                        Load = loadFunc,
                        Create = createFunc,
                        Save = saveFunc
                    });
                }

                {
                    MethodDefinition initReadWriters = new MethodDefinition("InitReadWriters", 
                        MethodAttributes.Public | MethodAttributes.Static, assembly.Import(typeof(void)));

                    // add [RuntimeInitializeOnLoad]
                    BenderUtility.AddRuntimeInitializeOnLoadAttribute(assembly, initReadWriters);

                    ILProcessor worker = initReadWriters.Body.GetILProcessor();

                    TypeReference serializationClassRef = assembly.MainModule.ImportReference(typeof(TSerialization));
                    var registerType = Resolvers.ResolveMethod(serializationClassRef, assembly, logger, "RegisterType", ref bendingFailed);

                    TypeReference genericSerializationClassRef = assembly.MainModule.ImportReference(typeof(Serialization<>));

                    System.Reflection.FieldInfo serializationKeyFieldInfo = typeof(Serialization<>).GetField(nameof(Serialization<object>.Key));
                    FieldReference serializationKeyFieldRef = assembly.MainModule.ImportReference(serializationKeyFieldInfo);
                    System.Reflection.FieldInfo serializationVersionFieldInfo = typeof(Serialization<>).GetField(nameof(Serialization<object>.Version));
                    FieldReference serializationVersionFieldRef = assembly.MainModule.ImportReference(serializationVersionFieldInfo);
                    System.Reflection.FieldInfo serializationSaveFieldInfo = typeof(Serialization<>).GetField(nameof(Serialization<object>.Save));
                    FieldReference serializationSaveFieldRef = assembly.MainModule.ImportReference(serializationSaveFieldInfo);
                    System.Reflection.FieldInfo serializationLoadFieldInfo = typeof(Serialization<>).GetField(nameof(Serialization<object>.Load));
                    FieldReference serializationLoadFieldRef = assembly.MainModule.ImportReference(serializationLoadFieldInfo);
                    System.Reflection.FieldInfo serializationCreateFieldInfo = typeof(Serialization<>).GetField(nameof(Create));
                    FieldReference serializationCreateFieldRef = assembly.MainModule.ImportReference(serializationCreateFieldInfo);

                    TypeReference loadRef = assembly.MainModule.ImportReference(typeof(Load<>));
                    MethodReference loadConstructorRef = assembly.MainModule.ImportReference(typeof(Load<>).GetConstructors()[0]);

                    TypeReference createRef = assembly.MainModule.ImportReference(typeof(Create<>));
                    MethodReference createConstructorRef = assembly.MainModule.ImportReference(typeof(Create<>).GetConstructors()[0]);

                    TypeReference saveRef = assembly.MainModule.ImportReference(typeof(Save<>));
                    MethodReference saveConstructorRef = assembly.MainModule.ImportReference(typeof(Save<>).GetConstructors()[0]);

                    for (var index = 0; index < methods.Count; index++)
                    {
                        var method = methods[index];
                        GenericInstanceType genericInstanceType = new GenericInstanceType(genericSerializationClassRef);
                        genericInstanceType.GenericArguments.Add(method.Owner);

                        VariableDefinition variableDefinition = new VariableDefinition(genericInstanceType);

                        worker.Body.Variables.Add(variableDefinition);

                        worker.Emit(OpCodes.Ldloca_S, variableDefinition);
                        worker.Emit(OpCodes.Initobj, genericInstanceType);

                        worker.Emit(OpCodes.Ldloca_S, variableDefinition);
                        // Serialization<>.Key = method.Key;
                        {
                            worker.Emit(OpCodes.Ldc_I4, method.Key);
                            FieldReference specializedField = serializationKeyFieldRef.SpecializeField(assembly.MainModule, genericInstanceType);
                            worker.Emit(OpCodes.Stfld, specializedField);
                        }

                        worker.Emit(OpCodes.Ldloca_S, variableDefinition);
                        // Serialization<>.Version = method.Key;
                        {
                            worker.Emit(OpCodes.Ldc_I4, method.Version);
                            FieldReference specializedField = serializationVersionFieldRef.SpecializeField(assembly.MainModule, genericInstanceType);
                            worker.Emit(OpCodes.Stfld, specializedField);
                        }

                        worker.Emit(OpCodes.Ldloca_S, variableDefinition);
                        // Serialization<>.Load
                        {
                            worker.Emit(OpCodes.Ldnull);
                            worker.Emit(OpCodes.Ldftn, method.Load);
                            GenericInstanceType loadGenericInstanceType = new GenericInstanceType(loadRef);

                            loadGenericInstanceType.GenericArguments.Add(method.Owner);
                            MethodReference loadRefInstance = loadConstructorRef.MakeHostInstanceGeneric(assembly.MainModule, loadGenericInstanceType);
                            worker.Emit(OpCodes.Newobj, loadRefInstance);

                            FieldReference specializedField = serializationLoadFieldRef.SpecializeField(assembly.MainModule, genericInstanceType);
                            worker.Emit(OpCodes.Stfld, specializedField);
                        }

                        worker.Emit(OpCodes.Ldloca_S, variableDefinition);
                        // Serialization<>.Save
                        {
                            worker.Emit(OpCodes.Ldnull);
                            worker.Emit(OpCodes.Ldftn, method.Save);
                            GenericInstanceType saveGenericInstanceType = new GenericInstanceType(saveRef);

                            saveGenericInstanceType.GenericArguments.Add(method.Owner);
                            MethodReference saveRefInstance = saveConstructorRef.MakeHostInstanceGeneric(assembly.MainModule, saveGenericInstanceType);
                            worker.Emit(OpCodes.Newobj, saveRefInstance);

                            FieldReference specializedField = serializationSaveFieldRef.SpecializeField(assembly.MainModule, genericInstanceType);
                            worker.Emit(OpCodes.Stfld, specializedField);
                        }

                        worker.Emit(OpCodes.Ldloca_S, variableDefinition);
                        // Serialization<>.Create
                        {
                            worker.Emit(OpCodes.Ldnull);
                            worker.Emit(OpCodes.Ldftn, method.Create);
                            GenericInstanceType createGenericInstanceType = new GenericInstanceType(createRef);
                            createGenericInstanceType.GenericArguments.Add(method.Owner);

                            MethodReference createRefInstance = createConstructorRef.MakeHostInstanceGeneric(assembly.MainModule, createGenericInstanceType);
                            worker.Emit(OpCodes.Newobj, createRefInstance);

                            FieldReference specializedField = serializationCreateFieldRef.SpecializeField(assembly.MainModule, genericInstanceType);
                            worker.Emit(OpCodes.Stfld, specializedField);
                        }

                        // call RegisterType
                        worker.Emit(OpCodes.Ldloc, variableDefinition);
                        {
                            var instanceRegisterType = registerType.MakeGeneric(assembly.MainModule, method.Owner);

                            worker.Emit(OpCodes.Call, instanceRegisterType);
                        }
                    }

                    worker.Emit(OpCodes.Ret);

                    generatedCodeClass.Methods.Add(initReadWriters);
                }

                modified = true;
            }

            return modified;
        }
    }
}
