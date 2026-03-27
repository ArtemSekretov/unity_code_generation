using System.Collections.Generic;
using CodeGeneration.Runtime.Visitor;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.Visitor
{
    public static class VisitorProcessor
    {
        private struct VisitorMethod
        {
            public TypeReference OwnerType;
            public MethodReference Method;
        }
        
        public static bool Process(AssemblyDefinition assembly, ILogger logger, TypeDefinition generatedCodeClass, List<TypeDefinition> typesToProcess, ref bool bendingFailed)
        {
            bool modified = false;
            
            List<TypeDefinition> typesToGenerate = new List<TypeDefinition>();
            
            foreach (TypeDefinition klass in typesToProcess)
            {
                if (klass.HasCustomAttribute<VisitorAttribute>())
                {
                    typesToGenerate.Add(klass);
                }
            }

            if (typesToGenerate.Count > 0)
            {
                var visitorType = assembly.Import(typeof(IVisitor));
                var visitorVisitMethod = Resolvers.ResolveMethod(visitorType, assembly, logger, "Visit", ref bendingFailed);
                
                VisitorMethod[] visitorMethods = new VisitorMethod[typesToGenerate.Count];
                for (var i = 0; i < visitorMethods.Length; i++)
                {
                    var klass = typesToGenerate[i];
                    var visitorMethod = GenerateVisitorFunc(assembly, klass);

                    ILProcessor worker = visitorMethod.Body.GetILProcessor();

                    foreach (FieldDefinition field in klass.Fields)
                    {
                        worker.Emit(OpCodes.Ldarg_0);
                        worker.Emit(OpCodes.Ldstr, field.Name);
                        worker.Emit(OpCodes.Ldarg_1);
                        worker.Emit(OpCodes.Ldflda, field);
                        var instanceVisitorVisitMethod = visitorVisitMethod.MakeGeneric(assembly.MainModule, field.FieldType);
                        worker.Emit(OpCodes.Callvirt, instanceVisitorVisitMethod);
                    }

                    worker.Emit(OpCodes.Ret);

                    generatedCodeClass.Methods.Add(visitorMethod);

                    visitorMethods[i] = new VisitorMethod
                    {
                        OwnerType = klass,
                        Method = visitorMethod
                    };
                }

                {
                    MethodDefinition initVisitors = new MethodDefinition("InitVisitors", 
                        MethodAttributes.Public | MethodAttributes.Static, assembly.Import(typeof(void)));

                    // add [RuntimeInitializeOnLoad]
                    BenderUtility.AddRuntimeInitializeOnLoadAttribute(assembly, initVisitors);
                    
                    TypeReference genericVisitorClassRef = assembly.MainModule.ImportReference(typeof(VisitorCall<>));
                    System.Reflection.FieldInfo fieldInfo = typeof(VisitorCall<>).GetField(nameof(VisitorCall<object>.Visit));
                    FieldReference fieldRef = assembly.MainModule.ImportReference(fieldInfo);
                    
                    TypeReference visitorDelegateRef = assembly.MainModule.ImportReference(typeof(VisitorDelegate<>));
                    MethodReference visitorDelegateConstructorRef = assembly.MainModule.ImportReference(typeof(VisitorDelegate<>).GetConstructors()[0]);
                    
                    ILProcessor worker = initVisitors.Body.GetILProcessor();

                    foreach (var method in visitorMethods)
                    {
                        // create VisitorDelegate<T> delegate
                        worker.Emit(OpCodes.Ldnull);
                        worker.Emit(OpCodes.Ldftn, method.Method);
                        GenericInstanceType createGenericVisitorDelegateTypeInstance = new GenericInstanceType(visitorDelegateRef);
                        createGenericVisitorDelegateTypeInstance.GenericArguments.Add(method.OwnerType);
                        
                        MethodReference visitorDelegateInstance = visitorDelegateConstructorRef.MakeHostInstanceGeneric(assembly.MainModule, createGenericVisitorDelegateTypeInstance);
                        worker.Emit(OpCodes.Newobj, visitorDelegateInstance);

                        // save it in VisitorCall<T>.Visit
                        GenericInstanceType createGenericVisitorClassTypeInstance = new GenericInstanceType(genericVisitorClassRef);
                        createGenericVisitorClassTypeInstance.GenericArguments.Add(method.OwnerType);
                        
                        FieldReference specializedField = fieldRef.SpecializeField(assembly.MainModule, createGenericVisitorClassTypeInstance);
                        worker.Emit(OpCodes.Stsfld, specializedField);
                    }
                    
                    worker.Emit(OpCodes.Ret);

                    generatedCodeClass.Methods.Add(initVisitors);
                }
                
                modified = true;
            }
            
            return modified;
        }
        
        private static MethodDefinition GenerateVisitorFunc(AssemblyDefinition assembly, TypeReference variable)
        {
            string functionName = $"_Visitor_{variable.FullName}";
            MethodDefinition visitorFunc = new MethodDefinition(functionName,
                MethodAttributes.Public |
                MethodAttributes.Static |
                MethodAttributes.HideBySig,
                assembly.Import(typeof(void)));

            var refType = new ByReferenceType(variable);
            
            visitorFunc.Parameters.Add(new ParameterDefinition("visitor", ParameterAttributes.None, assembly.Import<IVisitor>()));
            visitorFunc.Parameters.Add(new ParameterDefinition("container", ParameterAttributes.None, refType));
            visitorFunc.Body.InitLocals = true;

            return visitorFunc;
        }
    }
}
