using DynamicODataApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Edm;

namespace DynamicODataApi.Controllers;

/// <summary>
/// 通用动态 OData 控制器 —— 只需一个 Controller 处理所有实体集。
/// 
/// 核心变化：实体现在是运行时生成的 CLR 类型（有真实属性），
/// [EnableQuery] 可以通过反射访问属性，自动处理 $filter/$select/$orderby/$top/$skip/$count。
/// 不再需要手动解析 OData 查询参数。
/// </summary>
[Route("odata")]
public class DynamicODataController : ODataController
{
    private readonly EntityDataStore _store;
    private readonly IEdmModel _edmModel;

    public DynamicODataController(EntityDataStore store, IEdmModel edmModel)
    {
        _store = store;
        _edmModel = edmModel;
    }

    /// <summary>
    /// 获取实体集列表 —— [EnableQuery] 自动处理 OData 查询选项。
    /// 关键：返回 IQueryable&lt;T&gt; (T=运行时生成的CLR类型)，[EnableQuery] 才能通过
    ///   IEnumerable&lt;T&gt; 接口找到正确的元素类型进行反射。
    /// GET /odata/Products?$filter=Price gt 1000&$select=Name,Price&$orderby=Price desc&$top=5&$count=true
    /// </summary>
    [EnableQuery(PageSize = 200, MaxTop = 1000)]
    [HttpGet("{entitySetName}")]
    public IActionResult GetEntities(string entitySetName)
    {
        if (!_store.GetEntitySetNames().Contains(entitySetName, StringComparer.OrdinalIgnoreCase))
            return NotFound(new { error = $"Entity set '{entitySetName}' not found" });

        // 用反射 Cast<T>() 返回 IQueryable<T>，而非 IQueryable<object>
        // 这样 [EnableQuery] 通过 IEnumerable<T> 找到正确的元素类型
        var queryable = _store.GetTypedQueryable(entitySetName);
        if (queryable == null)
            return NotFound();

        return Ok(queryable);
    }

    /// <summary>
    /// 按 ID 获取单个实体
    /// GET /odata/Products/1
    /// </summary>
    [EnableQuery]
    [HttpGet("{entitySetName}/{key}")]
    public IActionResult GetEntity(string entitySetName, string key)
    {
        if (!_store.GetEntitySetNames().Contains(entitySetName, StringComparer.OrdinalIgnoreCase))
            return NotFound(new { error = $"Entity set '{entitySetName}' not found" });

        var config = _store.GetConfig(entitySetName);
        if (config == null) return NotFound();

        var entity = _store.GetEntityById(entitySetName, config.IdProperty, key);
        if (entity == null)
            return NotFound(new { error = $"Entity with {config.IdProperty}={key} not found in {entitySetName}" });

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
            return new
            {
                name,
                idProperty = cfg?.IdProperty,
                recordCount = _store.GetCount(name),
                clrType = _store.GetEntityType(name)?.FullName
            };
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
}
