namespace DynamicODataApi.Models;

public class DataSourceConfig
{
    public string EntitySetName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string IdProperty { get; set; } = "Id";
}

public class DataSourceRoot
{
    public List<DataSourceConfig> DataSources { get; set; } = new();
}

public class PropertySchema
{
    public string Name { get; set; } = string.Empty;
    public Type ClrType { get; set; } = typeof(string);
    public bool IsNullable { get; set; } = true;
    public bool IsKey { get; set; }

    public string ODataTypeName => ClrType switch
    {
        not null when ClrType == typeof(int) => "Edm.Int32",
        not null when ClrType == typeof(long) => "Edm.Int64",
        not null when ClrType == typeof(double) => "Edm.Double",
        not null when ClrType == typeof(decimal) => "Edm.Decimal",
        not null when ClrType == typeof(bool) => "Edm.Boolean",
        not null when ClrType == typeof(DateTime) => "Edm.DateTimeOffset",
        not null when ClrType == typeof(DateTimeOffset) => "Edm.DateTimeOffset",
        not null when ClrType == typeof(Guid) => "Edm.Guid",
        _ => "Edm.String"
    };
}

public class EntitySchema
{
    public string EntitySetName { get; set; } = string.Empty;
    public string IdProperty { get; set; } = "Id";
    public List<PropertySchema> Properties { get; set; } = new();
}
