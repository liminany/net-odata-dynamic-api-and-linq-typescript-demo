using System.Reflection;
using System.Reflection.Emit;
using DynamicODataApi.Models;

namespace DynamicODataApi.Services;

/// <summary>
/// 运行时类型工厂：根据 JSON Schema 用 Reflection.Emit 动态生成有真实 CLR 属性的实体类。
/// 
/// 为什么需要：
///   [EnableQuery] 通过反射 PropertyInfo.GetValue() 访问属性，Dictionary 只有索引器没有 CLR 属性。
///   运行时生成的类型有真实的 get_Xxx / set_Xxx 方法，[EnableQuery] 可以直接反射。
///   ODataConventionModelBuilder 也能基于这些 CLR 类型自动构建 EDM 模型（带上 CLR 类型注解）。
/// </summary>
public class DynamicTypeBuilder
{
    private readonly ModuleBuilder _module;
    private readonly Dictionary<string, Type> _typeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<DynamicTypeBuilder> _logger;

    public DynamicTypeBuilder(ILogger<DynamicTypeBuilder> logger)
    {
        _logger = logger;
        var assemblyName = new AssemblyName("DynamicODataTypes");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _module = assembly.DefineDynamicModule("EntityTypes");
    }

    /// <summary>
    /// 根据 Schema 生成实体 CLR 类型，并回填 Schema.ClrType
    /// </summary>
    public Type GetOrCreateType(EntitySchema schema)
    {
        if (_typeCache.TryGetValue(schema.EntitySetName, out var cached))
            return cached;

        var type = BuildType(schema);
        _typeCache[schema.EntitySetName] = type;
        schema.ClrType = type;

        _logger.LogInformation("Generated CLR type '{Name}' with {Count} properties for entity set {EntitySet}",
            type.Name, schema.Properties.Count, schema.EntitySetName);

        return type;
    }

    private Type BuildType(EntitySchema schema)
    {
        var typeBuilder = _module.DefineType(
            $"DynamicOData.{schema.EntitySetName}",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass);

        foreach (var prop in schema.Properties)
        {
            DefineAutoProperty(typeBuilder, prop);
        }

        return typeBuilder.CreateType()!;
    }

    /// <summary>
    /// 生成自动属性（backing field + getter/setter + IL 指令）
    /// </summary>
    private static void DefineAutoProperty(TypeBuilder typeBuilder, PropertySchema prop)
    {
        var propType = GetEffectiveType(prop);
        var fieldName = $"<{prop.Name}>k__BackingField";

        var field = typeBuilder.DefineField(fieldName, propType, FieldAttributes.Private);

        var propBuilder = typeBuilder.DefineProperty(
            prop.Name, PropertyAttributes.HasDefault, CallingConventions.HasThis, propType, null);

        // getter
        var getter = typeBuilder.DefineMethod(
            $"get_{prop.Name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propType, Type.EmptyTypes);
        var getIL = getter.GetILGenerator();
        getIL.Emit(OpCodes.Ldarg_0);
        getIL.Emit(OpCodes.Ldfld, field);
        getIL.Emit(OpCodes.Ret);
        propBuilder.SetGetMethod(getter);

        // setter
        var setter = typeBuilder.DefineMethod(
            $"set_{prop.Name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null, [propType]);
        var setIL = setter.GetILGenerator();
        setIL.Emit(OpCodes.Ldarg_0);
        setIL.Emit(OpCodes.Ldarg_1);
        setIL.Emit(OpCodes.Stfld, field);
        setIL.Emit(OpCodes.Ret);
        propBuilder.SetSetMethod(setter);
    }

    internal static Type GetEffectiveType(PropertySchema prop)
    {
        var t = prop.ClrType;
        if (prop.IsNullable && t.IsValueType && Nullable.GetUnderlyingType(t) == null)
            return typeof(Nullable<>).MakeGenericType(t);
        return t;
    }
}
