using System.Globalization;
using System.Text.RegularExpressions;
using DynamicODataApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Edm;

namespace DynamicODataApi.Controllers;

/// <summary>
/// 通用动态 OData 控制器 —— 手动解析 OData 查询参数，对 Dictionary 数据应用 LINQ 操作
/// 路由映射：GET /odata/{entitySetName}?$filter=...&$select=...&$orderby=...
/// </summary>
[Route("odata")]
public class DynamicODataController : ODataController
{
    private readonly EntityDataStore _store;
    private readonly IEdmModel _edmModel;
    private readonly ILogger<DynamicODataController> _logger;

    public DynamicODataController(EntityDataStore store, IEdmModel edmModel, ILogger<DynamicODataController> logger)
    {
        _store = store;
        _edmModel = edmModel;
        _logger = logger;
    }

    /// <summary>
    /// 获取实体集列表，支持 OData 查询选项
    /// GET /odata/Products?$filter=Price gt 1000&$select=Name,Price&$orderby=Price desc&$top=5&$count=true
    /// </summary>
    [HttpGet("{entitySetName}")]
    public IActionResult GetEntities(string entitySetName)
    {
        if (!_store.GetEntitySetNames().Contains(entitySetName, StringComparer.OrdinalIgnoreCase))
            return NotFound(new { error = $"Entity set '{entitySetName}' not found" });

        var data = _store.GetEntities(entitySetName);
        var query = HttpContext.Request.Query;

        try
        {
            // $filter
            var filterStr = query["$filter"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(filterStr))
            {
                data = ApplyFilter(data, filterStr);
            }

            // $orderby
            var orderbyStr = query["$orderby"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(orderbyStr))
            {
                data = ApplyOrderBy(data, orderbyStr);
            }

            // $top / $skip
            var skipStr = query["$skip"].FirstOrDefault();
            var topStr = query["$top"].FirstOrDefault();

            int? totalCount = null;
            if (query["$count"].FirstOrDefault() == "true")
            {
                totalCount = data.Count;
            }

            if (int.TryParse(skipStr, out var skip) && skip > 0)
                data = data.Skip(skip).ToList();
            if (int.TryParse(topStr, out var top) && top > 0)
                data = data.Take(top).ToList();

            // $select
            var selectStr = query["$select"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(selectStr))
            {
                data = ApplySelect(data, selectStr);
            }

            if (totalCount != null)
            {
                return Ok(new { value = data, count = totalCount });
            }

            return Ok(new { value = data });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Query error: {ex.Message}" });
        }
    }

    /// <summary>
    /// 按 ID 获取单个实体
    /// GET /odata/Products/1
    /// </summary>
    [HttpGet("{entitySetName}/{key}")]
    public IActionResult GetEntity(string entitySetName, string key)
    {
        if (!_store.GetEntitySetNames().Contains(entitySetName, StringComparer.OrdinalIgnoreCase))
            return NotFound(new { error = $"Entity set '{entitySetName}' not found" });

        var config = _store.GetConfig(entitySetName);
        if (config == null) return NotFound();

        var data = _store.GetEntities(entitySetName);
        var entity = FindByKey(data, config.IdProperty, key);

        if (entity == null)
            return NotFound(new { error = $"Entity with {config.IdProperty}={key} not found in {entitySetName}" });

        // $select on single entity
        var selectStr = HttpContext.Request.Query["$select"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(selectStr))
        {
            var fields = selectStr.Split(',').Select(f => f.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            entity = entity.Where(kv => fields.Contains(kv.Key))
                          .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        }

        return Ok(entity);
    }

    /// <summary>
    /// 获取可用实体集列表（管理端点）
    /// GET /odata/admin/entity-sets
    /// </summary>
    [HttpGet("admin/entity-sets")]
    public IActionResult ListEntitySets()
    {
        var sets = _store.GetEntitySetNames().Select(name =>
        {
            var cfg = _store.GetConfig(name);
            var count = _store.GetEntities(name).Count;
            return new { name, idProperty = cfg?.IdProperty, recordCount = count };
        });
        return Ok(sets);
    }

    /// <summary>
    /// 获取实体 schema（管理端点）
    /// GET /odata/admin/schema/{entitySetName}
    /// </summary>
    [HttpGet("admin/schema/{entitySetName}")]
    public IActionResult GetSchema(string entitySetName)
    {
        var entityType = _edmModel.SchemaElements
            .OfType<IEdmEntityType>()
            .FirstOrDefault(e => string.Equals(e.Name, entitySetName, StringComparison.OrdinalIgnoreCase));

        if (entityType == null)
            return NotFound(new { error = $"Schema not found for '{entitySetName}'" });

        var properties = entityType.DeclaredProperties.Select(p => new
        {
            name = p.Name,
            type = p.Type.FullName(),
            isNullable = p.Type.IsNullable,
            isKey = entityType.Key().Any(k => k.Name == p.Name)
        });

        return Ok(new { entitySet = entitySetName, properties });
    }

    // ============ Query Helpers ============

    private static List<Dictionary<string, object?>> ApplyFilter(
        List<Dictionary<string, object?>> data, string filter)
    {
        var expr = ParseFilterExpression(filter);
        return data.Where(item => EvaluateFilter(item, expr)).ToList();
    }

    private static List<Dictionary<string, object?>> ApplyOrderBy(
        List<Dictionary<string, object?>> data, string orderby)
    {
        // Parse "Field desc, Field2 asc"
        var clauses = orderby.Split(',').Select(c => c.Trim()).ToList();
        IOrderedEnumerable<Dictionary<string, object?>>? ordered = null;

        foreach (var clause in clauses)
        {
            var parts = clause.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var field = parts[0];
            var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

            if (ordered == null)
            {
                ordered = desc
                    ? data.OrderByDescending(item => GetComparable(item, field))
                    : data.OrderBy(item => GetComparable(item, field));
            }
            else
            {
                ordered = desc
                    ? ordered.ThenByDescending(item => GetComparable(item, field))
                    : ordered.ThenBy(item => GetComparable(item, field));
            }
        }

        return ordered?.ToList() ?? data;
    }

    private static List<Dictionary<string, object?>> ApplySelect(
        List<Dictionary<string, object?>> data, string select)
    {
        var fields = select.Split(',').Select(f => f.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return data.Select(item =>
            item.Where(kv => fields.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
        ).ToList();
    }

    // ============ Filter Parser ============

    private enum NodeType { Comparison, Logical, Function, Literal, Property }

    private record FilterNode(
        NodeType Type,
        string? Property = null,
        string? Operator = null,
        string? Value = null,
        string? FuncName = null,
        string? FuncArg = null,
        FilterNode? Left = null,
        FilterNode? Right = null);

    private static FilterNode ParseFilterExpression(string filter)
    {
        filter = filter.Trim();

        // Handle parenthesized groups
        if (filter.StartsWith('(') && filter.EndsWith(')'))
        {
            var inner = filter[1..^1];
            // Check for logical operators at the top level of parens
            var depth = 0;
            var opPos = -1;
            var opLen = 0;
            for (var i = 0; i < inner.Length; i++)
            {
                if (inner[i] == '(') depth++;
                else if (inner[i] == ')') depth--;
                else if (depth == 0)
                {
                    if (i + 4 <= inner.Length && inner.Substring(i, 4) == " and ")
                    { opPos = i; opLen = 4; break; }
                    if (i + 3 <= inner.Length && inner.Substring(i, 3) == " or ")
                    { opPos = i; opLen = 3; break; }
                }
            }
            if (opPos >= 0)
            {
                return new FilterNode(NodeType.Logical, Operator: inner.Substring(opPos, opLen).Trim(),
                    Left: ParseFilterExpression(inner[..opPos]),
                    Right: ParseFilterExpression(inner[(opPos + opLen)..]));
            }
            return ParseFilterExpression(inner);
        }

        // Find logical operators at top level
        var depth2 = 0;
        for (var i = 0; i < filter.Length; i++)
        {
            if (filter[i] == '(') depth2++;
            else if (filter[i] == ')') depth2--;
            else if (depth2 == 0)
            {
                if (i + 4 <= filter.Length && filter.Substring(i, 4) == " and ")
                {
                    return new FilterNode(NodeType.Logical, Operator: "and",
                        Left: ParseFilterExpression(filter[..i]),
                        Right: ParseFilterExpression(filter[(i + 5)..]));
                }
                if (i + 3 <= filter.Length && filter.Substring(i, 3) == " or ")
                {
                    return new FilterNode(NodeType.Logical, Operator: "or",
                        Left: ParseFilterExpression(filter[..i]),
                        Right: ParseFilterExpression(filter[(i + 4)..]));
                }
            }
        }

        // Function: contains(prop, 'val') or startswith(prop, 'val')
        var funcMatch = Regex.Match(filter, @"^(contains|startswith|endswith)\((\w+),\s*'([^']*)'\s*\)$");
        if (funcMatch.Success)
        {
            return new FilterNode(NodeType.Function, FuncName: funcMatch.Groups[1].Value,
                Property: funcMatch.Groups[2].Value, FuncArg: funcMatch.Groups[3].Value);
        }

        // Comparison: prop op value
        var compMatch = Regex.Match(filter, @"^(\w+)\s+(eq|ne|gt|lt|ge|le)\s+(.+)$");
        if (compMatch.Success)
        {
            var val = compMatch.Groups[3].Value.Trim();
            // Remove quotes around string values
            if (val.StartsWith('\'') && val.EndsWith('\''))
                val = val[1..^1];
            return new FilterNode(NodeType.Comparison, Property: compMatch.Groups[1].Value,
                Operator: compMatch.Groups[2].Value, Value: val);
        }

        throw new InvalidOperationException($"Unable to parse filter expression: {filter}");
    }

    private static bool EvaluateFilter(Dictionary<string, object?> item, FilterNode node)
    {
        return node.Type switch
        {
            NodeType.Logical => node.Operator == "and"
                ? EvaluateFilter(item, node.Left!) && EvaluateFilter(item, node.Right!)
                : EvaluateFilter(item, node.Left!) || EvaluateFilter(item, node.Right!),

            NodeType.Comparison => EvaluateComparison(item, node.Property!, node.Operator!, node.Value!),

            NodeType.Function => node.FuncName switch
            {
                "contains" => EvaluateContains(item, node.Property!, node.FuncArg!),
                "startswith" => EvaluateStartsWith(item, node.Property!, node.FuncArg!),
                "endswith" => EvaluateEndsWith(item, node.Property!, node.FuncArg!),
                _ => throw new InvalidOperationException($"Unknown function: {node.FuncName}")
            },

            _ => throw new InvalidOperationException($"Unknown node type: {node.Type}")
        };
    }

    private static bool EvaluateComparison(Dictionary<string, object?> item, string property, string op, string value)
    {
        if (!item.TryGetValue(property, out var itemValue) || itemValue == null)
            return op == "ne"; // null != value is true, null == value is false

        var itemStr = itemValue.ToString()!;
        var valStr = value;

        // Try numeric comparison
        if (IsNumeric(itemValue) && double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var valNum))
        {
            var itemNum = Convert.ToDouble(itemValue, CultureInfo.InvariantCulture);
            return op switch
            {
                "eq" => Math.Abs(itemNum - valNum) < 0.0001,
                "ne" => Math.Abs(itemNum - valNum) > 0.0001,
                "gt" => itemNum > valNum,
                "lt" => itemNum < valNum,
                "ge" => itemNum >= valNum,
                "le" => itemNum <= valNum,
                _ => throw new InvalidOperationException($"Unknown operator: {op}")
            };
        }

        // Boolean comparison
        if (itemValue is bool boolVal && bool.TryParse(valStr, out var valBool))
        {
            return op switch
            {
                "eq" => boolVal == valBool,
                "ne" => boolVal != valBool,
                _ => throw new InvalidOperationException($"Boolean comparison only supports eq/ne, got: {op}")
            };
        }

        // String comparison
        var cmp = string.Compare(itemStr, valStr, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "eq" => cmp == 0,
            "ne" => cmp != 0,
            "gt" => cmp > 0,
            "lt" => cmp < 0,
            "ge" => cmp >= 0,
            "le" => cmp <= 0,
            _ => throw new InvalidOperationException($"Unknown operator: {op}")
        };
    }

    private static bool EvaluateContains(Dictionary<string, object?> item, string property, string value)
    {
        return item.TryGetValue(property, out var itemValue) && itemValue != null &&
               itemValue.ToString()!.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateStartsWith(Dictionary<string, object?> item, string property, string value)
    {
        return item.TryGetValue(property, out var itemValue) && itemValue != null &&
               itemValue.ToString()!.StartsWith(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateEndsWith(Dictionary<string, object?> item, string property, string value)
    {
        return item.TryGetValue(property, out var itemValue) && itemValue != null &&
               itemValue.ToString()!.EndsWith(value, StringComparison.OrdinalIgnoreCase);
    }

    private static IComparable GetComparable(Dictionary<string, object?> item, string field)
    {
        if (!item.TryGetValue(field, out var val) || val == null)
            return "";

        return val switch
        {
            int i => i,
            long l => l,
            double d => d,
            decimal m => m,
            DateTimeOffset dto => dto,
            bool b => b ? 1 : 0,
            _ => val.ToString() ?? ""
        };
    }

    private static bool IsNumeric(object? value) =>
        value is int or long or double or decimal or float or short or byte;

    private static Dictionary<string, object?>? FindByKey(
        List<Dictionary<string, object?>> data, string idProperty, string keyValue)
    {
        foreach (var item in data)
        {
            if (item.TryGetValue(idProperty, out var val) && val != null)
            {
                if (val.ToString() == keyValue) return item;
            }
        }
        return null;
    }
}
