using DynamicODataApi.Models;
using Microsoft.OData.Edm;

namespace DynamicODataApi.Services;

/// <summary>
/// 根据推断的 JSON Schema 动态构建 OData EDM 模型
/// 使用底层 EdmModel API，不依赖 CLR 类型——真正做到零代码添加实体
/// </summary>
public class DynamicEdmModelBuilder(ILogger<DynamicEdmModelBuilder> logger)
{
    public IEdmModel Build(IEnumerable<EntitySchema> schemas)
    {
        var model = new EdmModel();

        // 创建默认容器 (EntityContainer)
        var container = new EdmEntityContainer("DynamicOData", "Container");
        model.AddElement(container);

        foreach (var schema in schemas)
        {
            logger.LogInformation("Building EDM entity type for {EntitySet} with {Count} properties",
                schema.EntitySetName, schema.Properties.Count);

            // 创建 EdmEntityType
            var entityType = new EdmEntityType("DynamicOData", schema.EntitySetName);
            model.AddElement(entityType);

            // 添加属性
            foreach (var prop in schema.Properties)
            {
                var typeKind = GetEdmTypeKind(prop);
                var isNullable = prop.IsNullable && !prop.IsKey;

                var edmProp = entityType.AddStructuralProperty(
                    prop.Name,
                    typeKind,
                    isNullable
                );

                // 设置主键
                if (prop.IsKey)
                {
                    entityType.AddKeys(edmProp);
                }
            }

            // 注册到 EntityContainer
            container.AddEntitySet(schema.EntitySetName, entityType);
        }

        return model;
    }

    private static EdmPrimitiveTypeKind GetEdmTypeKind(PropertySchema prop)
    {
        return prop.ClrType switch
        {
            not null when prop.ClrType == typeof(int) => EdmPrimitiveTypeKind.Int32,
            not null when prop.ClrType == typeof(long) => EdmPrimitiveTypeKind.Int64,
            not null when prop.ClrType == typeof(double) => EdmPrimitiveTypeKind.Double,
            not null when prop.ClrType == typeof(decimal) => EdmPrimitiveTypeKind.Decimal,
            not null when prop.ClrType == typeof(bool) => EdmPrimitiveTypeKind.Boolean,
            not null when prop.ClrType == typeof(DateTimeOffset) => EdmPrimitiveTypeKind.DateTimeOffset,
            not null when prop.ClrType == typeof(DateTime) => EdmPrimitiveTypeKind.DateTimeOffset,
            not null when prop.ClrType == typeof(Guid) => EdmPrimitiveTypeKind.Guid,
            _ => EdmPrimitiveTypeKind.String
        };
    }
}
