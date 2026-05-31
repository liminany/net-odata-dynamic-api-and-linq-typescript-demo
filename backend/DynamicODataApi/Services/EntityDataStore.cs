using System.Text.Json;
using DynamicODataApi.Models;

namespace DynamicODataApi.Services;

/// <summary>
/// 内存中管理所有 JSON 数据源，以强类型实体对象存储（不再用 Dictionary）。
/// 支持 IQueryable 返回，供 [EnableQuery] 进行 OData 查询处理。
/// </summary>
public class EntityDataStore(ILogger<EntityDataStore> logger)
{
    // entitySetName → List<typedObject>
    private readonly Dictionary<string, List<object>> _stores = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DataSourceConfig> _configs = new(StringComparer.OrdinalIgnoreCase);

    // entitySetName → Type（运行时生成的 CLR 类型）
    private readonly Dictionary<string, Type> _entityTypes = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 加载数据源：从 JSON 文件读取 → 实例化运行时 CLR 类型 → 填入属性值
    /// </summary>
    public void LoadDataSource(DataSourceConfig config, Type entityType)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, config.FilePath);
        logger.LogInformation("Loading data source {EntitySet} from {Path}", config.EntitySetName, fullPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Data file not found: {fullPath}");

        var json = File.ReadAllText(fullPath);
        var records = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, JsonOptions)
                      ?? [];

        var typedList = new List<object>(records.Count);
        var props = entityType.GetProperties();

        foreach (var record in records)
        {
            var instance = Activator.CreateInstance(entityType)!;
            foreach (var prop in props)
            {
                if (record.TryGetValue(prop.Name, out var je))
                {
                    var value = ConvertJsonElement(je, prop.PropertyType);
                    prop.SetValue(instance, value);
                }
            }
            typedList.Add(instance);
        }

        _stores[config.EntitySetName] = typedList;
        _configs[config.EntitySetName] = config;
        _entityTypes[config.EntitySetName] = entityType;

        logger.LogInformation("Loaded {Count} records for entity set {EntitySet} (type: {Type})",
            typedList.Count, config.EntitySetName, entityType.Name);
    }

    /// <summary>返回强类型实体的 IQueryable（非泛型），供 [EnableQuery] 处理 OData 查询</summary>
    public IQueryable GetQueryable(string entitySetName)
    {
        if (!_stores.TryGetValue(entitySetName, out var list))
            return Enumerable.Empty<object>().AsQueryable();
        return list.AsQueryable();
    }

    /// <summary>
    /// 返回强类型的 IQueryable&lt;T&gt;（通过反射 Cast 转换）。
    /// 这样 [EnableQuery] 才能通过 IQueryable&lt;T&gt; 找到正确的 CLR 元素类型。
    /// </summary>
    public IQueryable? GetTypedQueryable(string entitySetName)
    {
        if (!_stores.TryGetValue(entitySetName, out var list))
            return null;

        var entityType = GetEntityType(entitySetName);
        if (entityType == null) return list.AsQueryable();

        // 反射调用 Queryable.Cast<T>(IQueryable) → IQueryable<T>
        var castMethod = typeof(Queryable).GetMethod("Cast")!.MakeGenericMethod(entityType);
        return (IQueryable)castMethod.Invoke(null, [list.AsQueryable()])!;
    }

    /// <summary>返回单实体（按 ID 查找）</summary>
    public object? GetEntityById(string entitySetName, string idProperty, string keyValue)
    {
        if (!_stores.TryGetValue(entitySetName, out var list))
            return null;

        var idProp = _entityTypes.TryGetValue(entitySetName, out var type)
            ? type.GetProperty(idProperty)
            : null;

        foreach (var item in list)
        {
            var val = idProp?.GetValue(item);
            if (val != null && val.ToString() == keyValue)
                return item;
        }
        return null;
    }

    public IEnumerable<string> GetEntitySetNames() => _stores.Keys;
    public DataSourceConfig? GetConfig(string entitySetName)
        => _configs.TryGetValue(entitySetName, out var cfg) ? cfg : null;
    public Type? GetEntityType(string entitySetName)
        => _entityTypes.TryGetValue(entitySetName, out var t) ? t : null;

    public int GetCount(string entitySetName)
        => _stores.TryGetValue(entitySetName, out var list) ? list.Count : 0;

    // ====== JSON → CLR 值转换 ======

    private static object? ConvertJsonElement(JsonElement element, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (element.ValueKind == JsonValueKind.Null)
            return null;

        if (element.ValueKind == JsonValueKind.Array || element.ValueKind == JsonValueKind.Object)
            return element.GetRawText();

        return Type.GetTypeCode(underlyingType) switch
        {
            TypeCode.Int32 when element.TryGetInt32(out var i) => i,
            TypeCode.Int64 when element.TryGetInt64(out var l) => l,
            TypeCode.Double => element.GetDouble(),
            TypeCode.Decimal when element.TryGetDecimal(out var m) => m,
            TypeCode.Boolean when element.ValueKind == JsonValueKind.True => true,
            TypeCode.Boolean when element.ValueKind == JsonValueKind.False => false,
            TypeCode.String => TryParseDateTime(element.GetString()!, out var dto)
                ? (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?) ? (object)dto : dto.ToString("O"))
                : element.GetString(),
            TypeCode.DateTime => TryParseDateTime(element.GetString()!, out var dt) ? (object)dt.DateTime : element.GetString(),
            _ => underlyingType == typeof(DateTimeOffset)
                ? (TryParseDateTime(element.GetString()!, out var d) ? (object)d : element.GetString())
                : element.ToString()
        };
    }

    private static bool TryParseDateTime(string value, out DateTimeOffset result)
        => DateTimeOffset.TryParse(value, out result);
}
