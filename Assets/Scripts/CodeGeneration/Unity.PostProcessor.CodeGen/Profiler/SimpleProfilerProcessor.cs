using System;
using System.Collections.Generic;
using System.Reflection;
using CodeGeneration.Runtime.Profiler;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.Profiler
{
    public static class SimpleProfilerProcessor
    {
        
        public static bool Process(AssemblyDefinition assembly, ILogger logger, TypeDefinition generatedCodeClass, List<TypeDefinition> typesToProcess, ref bool bendingFailed)
        {
            bool modified = false;
            
            List<MethodDefinition> typesToGenerate = new List<MethodDefinition>();

            foreach (TypeDefinition klass in typesToProcess)
            {
                foreach (MethodDefinition method in klass.Methods)
                {
                    if (method.HasCustomAttribute<ProfileAttribute>())
                    {
                        typesToGenerate.Add(method);
                    }
                }
            }

            if (typesToGenerate.Count > 0)
            {
                var zoneType = assembly.Import(typeof(Zone));
                var zoneInfoType = assembly.Import(typeof(ZoneInfo));
                var currentZoneType = assembly.Import(typeof(CurrentZone));
                
                FieldInfo profilerFieldInfo = typeof(SimpleProfiler).GetField(nameof(SimpleProfiler.Profiler));
                FieldReference profilerFieldRef = assembly.MainModule.ImportReference(profilerFieldInfo);
                
                FieldInfo zoneInfosNameFieldInfo = typeof(ZoneInfo).GetField(nameof(ZoneInfo.Name));
                FieldReference zoneInfosNameFieldRef = assembly.MainModule.ImportReference(zoneInfosNameFieldInfo);
                
                FieldInfo zonesFieldInfo = typeof(ProfilerData).GetField(nameof(ProfilerData.Zones));
                FieldReference zoneFieldRef = assembly.MainModule.ImportReference(zonesFieldInfo);
                
                FieldInfo zoneInfosFieldInfo = typeof(ProfilerData).GetField(nameof(ProfilerData.ZoneInfos));
                FieldReference zoneInfosFieldRef = assembly.MainModule.ImportReference(zoneInfosFieldInfo);
                
                FieldInfo startFieldInfo = typeof(ProfilerData).GetField(nameof(ProfilerData.Start));
                FieldReference startFieldRef = assembly.MainModule.ImportReference(startFieldInfo);
                
                // This code does not work if Api compatibility set to .NET Framework in ProjectSetting -> Player
                // TypeLoadException: Could not resolve type with token 010001d9 from typeref (expected class 'System.Diagnostics.Stopwatch' in assembly 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089')
                /*MethodReference getTimestampMethodReference = assembly.MainModule.ImportReference(
                    typeof(System.Diagnostics.Stopwatch).GetMethod("GetTimestamp")
                );*/
                
                AssemblyNameReference systemAssemblyRef = null;
                foreach (var nameReference in assembly.MainModule.AssemblyReferences)
                {
                    if (string.Equals(nameReference.Name, "System"))
                    {
                        systemAssemblyRef = nameReference;
                    }
                }
                
                if (systemAssemblyRef == null)
                {
                    logger.LogWarning("SimpleProfilerProcessor", "System is missing");

                    systemAssemblyRef = new AssemblyNameReference("System",
                        new Version(4, 0, 0, 0));
                    assembly.MainModule.AssemblyReferences.Add(systemAssemblyRef);
                }

                var stopwatchType = new TypeReference("System.Diagnostics", "Stopwatch", 
                    assembly.MainModule, systemAssemblyRef);

                var getTimestampMethodReference = new MethodReference("GetTimestamp", 
                    assembly.MainModule.TypeSystem.Int64, stopwatchType)
                {
                    HasThis = false   // static method
                };
                
                MethodReference beginTimedBlockMethodReference = assembly.MainModule.ImportReference(
                    typeof(SimpleProfiler).GetMethod("BeginTimedBlock")
                );
                MethodReference endTimedBlockMethodReference = assembly.MainModule.ImportReference(
                    typeof(SimpleProfiler).GetMethod("EndTimedBlock")
                );

                {
                    MethodDefinition initProfiler = new MethodDefinition("InitProfiler", 
                        Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, assembly.Import(typeof(void)));

                    // add [RuntimeInitializeOnLoad]
                    BenderUtility.AddRuntimeInitializeOnLoadAttribute(assembly, initProfiler);

                    ILProcessor worker = initProfiler.Body.GetILProcessor();
                    
                    worker.Emit(OpCodes.Ldsfld, profilerFieldRef);
                    worker.Emit(OpCodes.Ldc_I4, typesToGenerate.Count + 1);
                    worker.Emit(OpCodes.Newarr, zoneType);
                    worker.Emit(OpCodes.Stfld, zoneFieldRef);
                    
                    worker.Emit(OpCodes.Ldsfld, profilerFieldRef);
                    worker.Emit(OpCodes.Call, getTimestampMethodReference);
                    worker.Emit(OpCodes.Stfld, startFieldRef);
                    
                    worker.Emit(OpCodes.Ldsfld, profilerFieldRef);
                    worker.Emit(OpCodes.Ldc_I4, typesToGenerate.Count + 1);
                    worker.Emit(OpCodes.Newarr, zoneInfoType);
                    worker.Emit(OpCodes.Stfld, zoneInfosFieldRef);
                    
                    worker.Emit(OpCodes.Ldsfld, profilerFieldRef);
                    worker.Emit(OpCodes.Ldfld, zoneInfosFieldRef);
                    worker.Emit(OpCodes.Ldc_I4, 0);
                    worker.Emit(OpCodes.Ldelema, zoneInfoType);
                    worker.Emit(OpCodes.Ldstr, "root");
                    worker.Emit(OpCodes.Stfld, zoneInfosNameFieldRef);   
                    
                    for (var i = 0; i < typesToGenerate.Count; i++)
                    {
                        var methodDefinition = typesToGenerate[i];
                        worker.Emit(OpCodes.Ldsfld, profilerFieldRef);
                        worker.Emit(OpCodes.Ldfld, zoneInfosFieldRef);
                        worker.Emit(OpCodes.Ldc_I4, i + 1);
                        worker.Emit(OpCodes.Ldelema, zoneInfoType);
                        worker.Emit(OpCodes.Ldstr, methodDefinition.Name);
                        worker.Emit(OpCodes.Stfld, zoneInfosNameFieldRef);
                    }
                    
                    worker.Emit(OpCodes.Ret);

                    generatedCodeClass.Methods.Add(initProfiler);
                }
                
                for (var i = 0; i < typesToGenerate.Count; i++)
                {
                    var methodDefinition = typesToGenerate[i];

                    ChangeFunction(methodDefinition, i + 1,
                        currentZoneType,
                        beginTimedBlockMethodReference, endTimedBlockMethodReference);
                }

                modified = true;
            }
            
            return modified;
        }

        private static void ChangeFunction(MethodDefinition method, int index, 
            TypeReference currentZoneType,
            MethodReference beginTimedBlockMethodReference,
            MethodReference endTimedBlockMethodReference)
        {
            var currentZoneVariable = new VariableDefinition(currentZoneType);
            method.Body.Variables.Add(currentZoneVariable);
            
            method.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Stloc, currentZoneVariable));
            method.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, beginTimedBlockMethodReference));
            method.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldc_I4, index));
            
            List<int> insertions = new List<int>();
            
            for (var i = 0; i < method.Body.Instructions.Count; i++)
            {
                var instruction = method.Body.Instructions[i];
                if (instruction.OpCode == OpCodes.Ret)
                {
                    insertions.Add(i);
                }
            }

            for (var i = insertions.Count - 1; i >= 0; i--)
            {
                var insertion = insertions[i];
                method.Body.Instructions.Insert(insertion,
                    Instruction.Create(OpCodes.Call, endTimedBlockMethodReference));
                method.Body.Instructions.Insert(insertion, Instruction.Create(OpCodes.Ldc_I4, index));
                method.Body.Instructions.Insert(insertion, Instruction.Create(OpCodes.Ldloc, currentZoneVariable));
            }
        }
    }
}
