using System.Text.Json;
using DynamicODataApi.Models;

namespace DynamicODataApi.Services;

/// <summary>
/// 从 JSON 数据文件推断实体 Schema（属性名、类型、是否可空）
/// </summary>
public class JsonSchemaInferenceService(ILogger<JsonSchemaInferenceService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EntitySchema InferSchema(string filePath, string entitySetName, string idProperty)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, filePath);
        logger.LogInformation("Inferring schema from {Path} for entity set {EntitySet}", fullPath, entitySetName);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Data file not found: {fullPath}");

        var json = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"JSON must be an array. File: {filePath}");

        var properties = new Dictionary<string, PropertySchema>(StringComparer.OrdinalIgnoreCase);

        // 遍历所有元素，聚合属性
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;

            foreach (var prop in element.EnumerateObject())
            {
                if (!properties.ContainsKey(prop.Name))
                {
                    properties[prop.Name] = new PropertySchema
                    {
                        Name = prop.Name,
                        ClrType = InferClrType(prop.Value),
                        IsNullable = prop.Value.ValueKind == JsonValueKind.Null,
                        IsKey = string.Equals(prop.Name, idProperty, StringComparison.OrdinalIgnoreCase)
                    };
                }
                else
                {
                    // 合并类型推断（取更宽泛的类型）
                    var existing = properties[prop.Name];
                    if (prop.Value.ValueKind != JsonValueKind.Null)
                    {
                        var inferredType = InferClrType(prop.Value);
                        existing.ClrType = MergeTypes(existing.ClrType, inferredType);
                    }
                }
            }
        }

        return new EntitySchema
        {
            EntitySetName = entitySetName,
            IdProperty = idProperty,
            Properties = properties.Values.OrderByDescending(p => p.IsKey).ThenBy(p => p.Name).ToList()
        };
    }

    private static Type InferClrType(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => TryParseDateTime(element.GetString()!, out _) ? typeof(DateTimeOffset) : typeof(string),
        JsonValueKind.Number => element.TryGetInt32(out _) ? typeof(int) : typeof(double),
        JsonValueKind.True or JsonValueKind.False => typeof(bool),
        JsonValueKind.Array => typeof(string),     // 数组序列化为 JSON string
        JsonValueKind.Object => typeof(string),     // 嵌套对象序列化为 JSON string
        _ => typeof(string)
    };

    private static Type MergeTypes(Type a, Type b)
    {
        if (a == b) return a;
        if (a == typeof(double) || b == typeof(double)) return typeof(double);
        if (a == typeof(int) && b == typeof(int)) return typeof(int);
        return typeof(string);
    }

    private static bool TryParseDateTime(string value, out DateTimeOffset result)
    {
        return DateTimeOffset.TryParse(value, out result);
    }
}
