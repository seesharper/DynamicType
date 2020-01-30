using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace DynamicType
{
    public class DynamicTypeBuilder
    {
        private readonly List<DynamicMemberInfo> dynamicMembers = new List<DynamicMemberInfo>();

        private static ConcurrentDictionary<DynamicMemberInfo[], Type> cache = new ConcurrentDictionary<DynamicMemberInfo[], Type>(new TypeArrayEqualityComparer());

        public DynamicTypeBuilder From<T>(T value)
        {
            var extractor = new DynamicValueExtractor();
            var extractedDynamicMembers = extractor.GetDynamicMembers(value);
            dynamicMembers.AddRange(extractedDynamicMembers);
            return this;
        }

        public DynamicTypeBuilder Add<T>(string name, T value)
        {
            dynamicMembers.Add(new DynamicMemberInfo<T>(name, value));
            return this;
        }

        public IDynamicType Build()
        {
            var type = cache.GetOrAdd(dynamicMembers.ToArray(), args => CreateType());
            var activator = new DynamicTypeActivator();
            var instance = activator.Activate(type, dynamicMembers.ToArray());
            return instance;
        }

        private Type CreateType()
        {
            var typeBuilder = GetTypeBuilder();
            ImplementMembers(typeBuilder);
            ImplementDynamicTypeInterface(typeBuilder);
            var type = typeBuilder.CreateTypeInfo();
            return type;
        }

        private TypeBuilder GetTypeBuilder()
        {
            AssemblyName dynamicAssemblyName = new AssemblyName("DynamicTypeAssembly");
            AssemblyBuilder dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(dynamicAssemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder dynamicModule = dynamicAssembly.DefineDynamicModule("DynamicTypeModule");
            TypeBuilder dynamicAnonymousType = dynamicModule.DefineType("DynamicType", TypeAttributes.Public);
            return dynamicAnonymousType;
        }

        private FieldBuilder[] ImplementMembers(TypeBuilder typeBuilder)
        {
            var fields = new List<FieldBuilder>();
            var members = dynamicMembers.ToArray();
            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.HasThis, members.Select(a => a.Type).ToArray());
            var constructorGenerator = constructorBuilder.GetILGenerator();
            for (int i = 0; i < members.Length; i++)
            {
                DynamicMemberInfo argumentInfo = members[i];
                var fieldBuilder = typeBuilder.DefineField($"_{argumentInfo.Name}", argumentInfo.Type, FieldAttributes.Private);
                var argumentBuilder = constructorBuilder.DefineParameter(
                    i + 1,
                    ParameterAttributes.None,
                    $"{argumentInfo.Name.Substring(0, 1)}{argumentInfo.Name.Substring(1)}");
                constructorGenerator.Emit(OpCodes.Ldarg, 0);
                constructorGenerator.Emit(OpCodes.Ldarg, i + 1);
                constructorGenerator.Emit(OpCodes.Stfld, fieldBuilder);

                var propertyBuilder = typeBuilder.DefineProperty(argumentInfo.Name, PropertyAttributes.None, argumentInfo.Type, Type.EmptyTypes);
                var getMethodBuilder = typeBuilder.DefineMethod($"get_{argumentInfo.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, argumentInfo.Type, Type.EmptyTypes);
                var getMethodGenerator = getMethodBuilder.GetILGenerator();
                getMethodGenerator.Emit(OpCodes.Ldarg_0);
                getMethodGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
                getMethodGenerator.Emit(OpCodes.Ret);
                propertyBuilder.SetGetMethod(getMethodBuilder);
                fields.Add(fieldBuilder);
            }

            constructorGenerator.Emit(OpCodes.Ret);

            return fields.ToArray();;
        }

        private static void ImplementDynamicTypeInterface(TypeBuilder typeBuilder)
        {
            typeBuilder.AddInterfaceImplementation(typeof(IDynamicType));
            var openGenericGetMethod = typeof(IDynamicType).GetMethod(nameof(IDynamicType.Get));
            var openGenericGetValueMethod = typeof(ValueAccessor).GetMethod(nameof(ValueAccessor.GetValue));

            var methodAttributes = openGenericGetMethod.Attributes ^ MethodAttributes.Abstract;
            // var genericArgument =  openGenericGetMethod.GetGenericArguments()[0];
            // var test = openGenericGetMethod.ReturnType;

            var methodBuilder = typeBuilder.DefineMethod(openGenericGetMethod.Name,methodAttributes, openGenericGetMethod.ReturnType, new []{typeof(string)});
            var genericArgumentName = openGenericGetMethod.GetGenericArguments()[0].Name;

            var genericTypeParameterBuilder = methodBuilder.DefineGenericParameters(genericArgumentName)[0];
            var closedGenericGetValueMethod = openGenericGetValueMethod.MakeGenericMethod(typeBuilder, genericTypeParameterBuilder);

            var generator = methodBuilder.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Call, closedGenericGetValueMethod);
            generator.Emit(OpCodes.Ret);

        }
    }


    public interface IDynamicType
    {
        T Get<T>(string memberName);
    }


    public class DynamicMemberInfo
    {
        public readonly string Name;

        public readonly Type Type;

        public DynamicMemberInfo(string name, Type type)
        {
            this.Name = name;
            this.Type = type;;
        }

         public bool Equals(DynamicMemberInfo other)
        {
            return (Name, Type).Equals((other.Name, other.Type));
        }

        public override int GetHashCode()
        {
            return (Name, Type).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DynamicMemberInfo);
        }

    }

    public class DynamicMemberInfo<T> : DynamicMemberInfo
    {
        public readonly T Value;

        public DynamicMemberInfo(string name, T value) : base(name, typeof(T))
        {
            this.Value = value;
        }
    }

    internal class TypeArrayEqualityComparer : IEqualityComparer<DynamicMemberInfo[]>
    {
        public bool Equals(DynamicMemberInfo[] x, DynamicMemberInfo[] y)
        {
            if (x.Length != y.Length)
            {
                return false;
            }
            for (int i = 0; i < x.Length; i++)
            {
                if (!x[i].Equals(y[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(DynamicMemberInfo[] obj)
        {
            int result = 17;
            for (int i = 0; i < obj.Length; i++)
            {
                unchecked
                {
                    result = result * 23 + obj[i].GetHashCode();
                }
            }
            return result;
        }
    }

    public class DynamicTypeActivator
    {
        public IDynamicType Activate(Type dynamicType, DynamicMemberInfo[] members)
        {
            var activatorMethod = new DynamicMethod("Activate", typeof(IDynamicType), parameterTypes: new Type[] { typeof(DynamicMemberInfo[]) }, typeof(DynamicTypeActivator), true);
            var generator = activatorMethod.GetILGenerator();

            for (int i = 0; i < members.Length; i++)
            {
                DynamicMemberInfo member = members[i];

                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Ldelem_Ref);
                var closedGenericMemberInfoType = typeof(DynamicMemberInfo<>).MakeGenericType(member.Type);
                generator.Emit(OpCodes.Castclass, closedGenericMemberInfoType);
                var fieldInfo = closedGenericMemberInfoType.GetField("Value");
                generator.Emit(OpCodes.Ldfld, fieldInfo);

            }

            var constructorInfo = dynamicType.GetConstructors()[0];
            generator.Emit(OpCodes.Newobj, constructorInfo);

            generator.Emit(OpCodes.Ret);



            var activatorDelegate = (Func<DynamicMemberInfo[], IDynamicType>)activatorMethod.CreateDelegate(typeof(Func<DynamicMemberInfo[], IDynamicType>));

            var instance = activatorDelegate(members);

            return instance;
        }
    }

    public class DynamicValueExtractor
    {
        private static MethodInfo AddMethod = typeof(List<DynamicMemberInfo>).GetMethod(nameof(List<DynamicMemberInfo>.Add));
        private static ConcurrentDictionary<Type, Delegate> delegateCache = new ConcurrentDictionary<Type, Delegate>();


        public DynamicMemberInfo[] GetDynamicMembers<T>(T value)
        {
            var dynamicMembers = new List<DynamicMemberInfo>();
            var extractDelegate = delegateCache.GetOrAdd(typeof(T), d => CreateExtractDelegate<T>());
            var typedDelegate = (Action<T, List<DynamicMemberInfo>>)extractDelegate;
            typedDelegate(value, dynamicMembers);
            return dynamicMembers.ToArray();
        }

        private Delegate CreateExtractDelegate<T>()
        {
            var fieldsAndProperties = typeof(T).GetMembers(BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public).
            Where(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property).OrderByDeclaration().ToArray();

            var extractMethod = new DynamicMethod("Extract", typeof(void), new Type[]{typeof(T), typeof(List<DynamicMemberInfo>)}, typeof(DynamicValueExtractor), true);
            var generator = extractMethod.GetILGenerator();
            Type memberType = null;
            foreach (var memberInfo in fieldsAndProperties)
            {
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Ldstr, memberInfo.Name);
                generator.Emit(OpCodes.Ldarg_0);
                if (memberInfo.MemberType == MemberTypes.Property)
                {
                    var propertyInfo = (PropertyInfo)memberInfo;
                    var getMethod = propertyInfo.GetGetMethod();
                    memberType  = propertyInfo.PropertyType;

                    generator.Emit(OpCodes.Callvirt, getMethod);
                }
                else
                {
                    var fieldInfo = (FieldInfo)memberInfo;
                    memberType = fieldInfo.FieldType;
                    generator.Emit(OpCodes.Ldfld, fieldInfo);
                }

                var closedGenericMemberInfoType = typeof(DynamicMemberInfo<>).MakeGenericType(memberType);
                var constructorInfo = closedGenericMemberInfoType.GetConstructors()[0];
                generator.Emit(OpCodes.Newobj, constructorInfo);
                generator.Emit(OpCodes.Callvirt, AddMethod);
            }

            generator.Emit(OpCodes.Ret);
            var extractDelegate = extractMethod.CreateDelegate(typeof(Action<T, List<DynamicMemberInfo>>));
            return extractDelegate;
        }
    }


    /// <summary>
    /// Adds functionality for ordering properties by their declaration order.
    /// </summary>
    internal static class MemberInfoExtensions
    {
        /// <summary>
        /// Orders the <paramref name="properties"/> by their declaration order.
        /// </summary>
        /// <param name="properties">The properties for which to be ordered.</param>
        /// <returns>The <paramref name="properties"/> ordered by declaration.</returns>
        public static IEnumerable<MemberInfo> OrderByDeclaration(this IEnumerable<MemberInfo> properties)
        {
            return properties.OrderBy(p => p, new MetadataTokenComparer());
        }

        private class MetadataTokenComparer : IComparer<MemberInfo>
        {
            public int Compare(MemberInfo x, MemberInfo y)
            {

                var xToken = x.MetadataToken;
                var ytoken = y.MetadataToken;

                if (xToken < ytoken)
                {
                    return -1;
                }
                if (xToken > ytoken)
                {
                    return 1;
                }
                return 0;
            }
        }
    }

    public static class ValueAccessor
    {
        private static ConcurrentDictionary<string, Delegate> delegateCache = new ConcurrentDictionary<string, Delegate>();

        public static TValue GetValue<TTarget,TValue>(TTarget target, string propertyName)
        {
            var accessorDelegate = (Func<TTarget, TValue>)delegateCache.GetOrAdd(propertyName, n => CreateAccessorDelegate<TTarget, TValue>(n));
            return accessorDelegate(target);
        }

        private static Delegate CreateAccessorDelegate<TTarget, TValue>(string propertyName)
        {
            var propertyInfo = typeof(TTarget).GetProperty(propertyName);
            var getMethod = propertyInfo.GetGetMethod();
            var valueAccessorMethod = new DynamicMethod("GetValue", typeof(TValue), new[] { typeof(TTarget) }, typeof(ValueAccessor), true);
            var generator = valueAccessorMethod.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Callvirt, getMethod);
            generator.Emit(OpCodes.Ret);

            var accessorDelegate = valueAccessorMethod.CreateDelegate(typeof(Func<TTarget, TValue>));
            return accessorDelegate;
        }
    }

}
