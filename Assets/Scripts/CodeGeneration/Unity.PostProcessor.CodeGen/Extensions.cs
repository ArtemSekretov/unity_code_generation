using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace CodeGeneration.Unity.PostProcessor.CodeGen
{
    public static class Extensions
    {
        public static bool Is(this TypeReference td, Type type) =>
            type.IsGenericType
                ? td.GetElementType().FullName == type.FullName
                : td.FullName == type.FullName;

        // check if 'td' is exactly of type T.
        // it does not check if any base type is of <T>, only the specific type.
        public static bool Is<T>(this TypeReference td) => Is(td, typeof(T));
        
        public static CustomAttribute GetCustomAttribute<TAttribute>(this ICustomAttributeProvider method)
        {
            foreach (var attribute in method.CustomAttributes)
            {
                if (attribute.AttributeType.Is<TAttribute>())
                {
                    return attribute;
                }
            }
            
            return null;
        }

        public static bool HasCustomAttribute<TAttribute>(this ICustomAttributeProvider attributeProvider)
        {
            foreach (var attribute in attributeProvider.CustomAttributes)
            {
                if (attribute.AttributeType.Is<TAttribute>())
                {
                    return true;
                }
            }
            
            return false;
        }

        public static T GetField<T>(this CustomAttribute ca, string field, T defaultValue)
        {
            foreach (CustomAttributeNamedArgument customField in ca.Fields)
            {
                if (customField.Name == field)
                {
                    return (T)customField.Argument.Value;
                }
            
            }
            return defaultValue;
        }

        public static bool ContainsClass(this ModuleDefinition module, string nameSpace, string className)
        {
            foreach (var type in module.Types)
            {
                if (type.Namespace == nameSpace && type.Name == className)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        // Makes T => Variable and imports function
        public static MethodReference MakeGeneric(this MethodReference generic, ModuleDefinition module, TypeReference variableReference)
        {
            GenericInstanceMethod instance = new GenericInstanceMethod(generic);
            instance.GenericArguments.Add(variableReference);

            MethodReference readFunc = module.ImportReference(instance);
            return readFunc;
        }
        
        // Given a method of a generic class such as ArraySegment`T.get_Count,
        // and a generic instance such as ArraySegment`int
        // Creates a reference to the specialized method  ArraySegment`int`.get_Count
        // Note that calling ArraySegment`T.get_Count directly gives an invalid IL error
        public static MethodReference MakeHostInstanceGeneric(this MethodReference self, ModuleDefinition module, GenericInstanceType instanceType)
        {
            MethodReference reference = new MethodReference(self.Name, self.ReturnType, instanceType)
            {
                CallingConvention = self.CallingConvention,
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis
            };

            foreach (ParameterDefinition parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (GenericParameter generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return module.ImportReference(reference);
        }
        
        public static FieldReference MakeHostInstanceGeneric(this FieldReference self)
        {
            var declaringType = new GenericInstanceType(self.DeclaringType);
            foreach (var parameter in self.DeclaringType.GenericParameters)
            {
                declaringType.GenericArguments.Add(parameter);
            }
            return new FieldReference(self.Name, self.FieldType, declaringType);
        }

        // Given a field of a generic class such as Writer<T>.write,
        // and a generic instance such as ArraySegment`int
        // Creates a reference to the specialized method  ArraySegment`int`.get_Count
        // Note that calling ArraySegment`T.get_Count directly gives an invalid IL error
        public static FieldReference SpecializeField(this FieldReference self, ModuleDefinition module, GenericInstanceType instanceType)
        {
            FieldReference reference = new FieldReference(self.Name, self.FieldType, instanceType);
            return module.ImportReference(reference);
        }
        
        public static TypeReference Import<T>(this AssemblyDefinition assembly)
        {
            return Import(assembly, typeof(T));  
        }

        public static TypeReference Import(this AssemblyDefinition assembly, Type t)
        {
            return assembly.MainModule.ImportReference(t);  
        }

        public static List<MethodDefinition> GetConstructors(this TypeDefinition type)
        {
            List<MethodDefinition> result = new List<MethodDefinition>();

            foreach (var method in type.Methods)
            {
                if (method.IsConstructor)
                {
                    result.Add(method);
                }
            }

            return result;
        }
    }
}
