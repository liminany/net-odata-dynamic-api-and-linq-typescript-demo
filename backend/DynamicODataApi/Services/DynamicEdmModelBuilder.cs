using DynamicODataApi.Models;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace DynamicODataApi.Services;

/// <summary>
/// 基于运行时生成的 CLR Type 构建 OData EDM 模型。
/// 
/// 与旧版的区别：
///   - 旧版用底层 EdmModel/EdmEntityType API，EDM 实体没有 CLR 类型注解
///   - 新版用 ODataConventionModelBuilder，从 CLR Type 自动发现属性、可空性、主键
///     → EDM 实体带上 CLR 类型注解 → [EnableQuery] 可以通过反射找到真实属性
/// </summary>
public class DynamicEdmModelBuilder(ILogger<DynamicEdmModelBuilder> logger)
{
    public IEdmModel Build(IEnumerable<EntitySchema> schemas)
    {
        var builder = new ODataConventionModelBuilder();
        builder.Namespace = "DynamicOData";

        foreach (var schema in schemas)
        {
            if (schema.ClrType == null)
            {
                logger.LogWarning("No CLR type for {EntitySet}, skipping", schema.EntitySetName);
                continue;
            }

            logger.LogInformation("Building EDM for {EntitySet} from CLR type {Type}",
                schema.EntitySetName, schema.ClrType.Name);

            // ODataConventionModelBuilder 从 CLR 类型自动发现属性、类型、主键
            var entityConfig = builder.AddEntityType(schema.ClrType);

            // 添加 EntitySet
            builder.AddEntitySet(schema.EntitySetName, entityConfig);
        }

        return builder.GetEdmModel();
    }
}
