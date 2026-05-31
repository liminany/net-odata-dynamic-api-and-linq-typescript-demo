using System.Text.Json;
using DynamicODataApi.Models;

namespace DynamicODataApi.Services;

/// <summary>
/// 内存中管理所有 JSON 数据源，提供按实体集名称存取数据的能力
/// 数据以 Dictionary&lt;string, object&gt; 形式存储，适配动态 OData 查询
/// </summary>
public class EntityDataStore(ILogger<EntityDataStore> logger)
{
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _stores = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DataSourceConfig> _configs = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void LoadDataSource(DataSourceConfig config)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, config.FilePath);
        logger.LogInformation("Loading data source {EntitySet} from {Path}", config.EntitySetName, fullPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Data file not found: {fullPath}");

        var json = File.ReadAllText(fullPath);
        var records = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, JsonOptions)
                      ?? new List<Dictionary<string, JsonElement>>();

        // 转换 JsonElement → object
        var converted = records.Select(ConvertElementDict).ToList();
        _stores[config.EntitySetName] = converted;
        _configs[config.EntitySetName] = config;

        logger.LogInformation("Loaded {Count} records for entity set {EntitySet}", converted.Count, config.EntitySetName);
    }

    public List<Dictionary<string, object?>> GetEntities(string entitySetName)
    {
        return _stores.TryGetValue(entitySetName, out var list) ? list : [];
    }

    public IQueryable<Dictionary<string, object?>> GetQueryable(string entitySetName)
    {
        return GetEntities(entitySetName).AsQueryable();
    }

    public IEnumerable<string> GetEntitySetNames() => _stores.Keys;

    public DataSourceConfig? GetConfig(string entitySetName)
    {
        return _configs.TryGetValue(entitySetName, out var cfg) ? cfg : null;
    }

    private static Dictionary<string, object?> ConvertElementDict(Dictionary<string, JsonElement> source)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in source)
        {
            result[kv.Key] = ConvertJsonElement(kv.Value);
        }
        return result;
    }

    private static object? ConvertJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => TryParseDateTime(element.GetString()!, out var dt)
            ? (object)dt
            : element.GetString()!,
        JsonValueKind.Number => element.TryGetInt32(out var i)
            ? (object)i
            : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => JsonSerializer.Serialize(element),
        JsonValueKind.Object => JsonSerializer.Serialize(element),
        _ => null
    };

    private static bool TryParseDateTime(string value, out DateTimeOffset result)
        => DateTimeOffset.TryParse(value, out result);
}
