using System.Text.Json;
using DynamicODataApi.Models;
using DynamicODataApi.Services;
using Microsoft.AspNetCore.OData;

var builder = WebApplication.CreateBuilder(args);

// 1. 加载数据源配置
var configPath = Path.Combine(AppContext.BaseDirectory, "datasources.json");
if (!File.Exists(configPath))
{
    // fallback: try relative path
    configPath = Path.Combine(Directory.GetCurrentDirectory(), "datasources.json");
}

var dataSourceRoot = File.Exists(configPath)
    ? JsonSerializer.Deserialize<DataSourceRoot>(File.ReadAllText(configPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    : new DataSourceRoot();

Console.WriteLine($"[Init] Loaded {dataSourceRoot?.DataSources.Count ?? 0} data source configs from {configPath}");

// 2. 注册服务（Singleton，因为数据在内存中）
var schemaService = new JsonSchemaInferenceService(
    builder.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>().CreateLogger<JsonSchemaInferenceService>());

var entityStore = new EntityDataStore(
    builder.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>().CreateLogger<EntityDataStore>());

// 3. 推断 Schema + 构建 EDM 模型
var schemas = new List<EntitySchema>();
if (dataSourceRoot?.DataSources != null)
{
    foreach (var ds in dataSourceRoot.DataSources)
    {
        try
        {
            var schema = schemaService.InferSchema(ds.FilePath, ds.EntitySetName, ds.IdProperty);
            schemas.Add(schema);

            entityStore.LoadDataSource(ds);

            Console.WriteLine($"[Schema] {ds.EntitySetName}: {schema.Properties.Count} properties, key={ds.IdProperty}");
            foreach (var p in schema.Properties)
                Console.WriteLine($"  - {p.Name} ({p.ODataTypeName}) {(p.IsKey ? "[KEY]" : "")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warn] Failed to load {ds.EntitySetName}: {ex.Message}");
        }
    }
}

var modelBuilder = new DynamicEdmModelBuilder(
    builder.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>().CreateLogger<DynamicEdmModelBuilder>());

var edmModel = modelBuilder.Build(schemas);

// 注册为 DI 服务
builder.Services.AddSingleton(entityStore);
builder.Services.AddSingleton(edmModel);

// 4. 配置 OData + Controllers
builder.Services.AddControllers()
    .AddOData(opt =>
    {
        opt.Select().Filter().OrderBy().Count().Expand().SetMaxTop(1000);
        opt.AddRouteComponents("odata", edmModel);
    });

// 5. CORS（允许前端跨域访问）
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("*");
    });
});

var app = builder.Build();

// 6. 中间件管道
app.UseCors();
app.UseRouting();
app.MapControllers();

Console.WriteLine($@"
═══════════════════════════════════════════
 Dynamic OData API is ready!
═══════════════════════════════════════════
 Available endpoints:

  GET /odata/{{entitySetName}}    - Query entities with OData options
    Examples:
    /odata/Products?$filter=Price gt 1000
    /odata/Products?$select=Name,Price&$orderby=Price desc&$top=5
    /odata/Products?$count=true
    /odata/Products/1          - Get single entity by ID

  GET /odata/admin/entity-sets  - List all available entity sets
  GET /odata/admin/schema/{{name}} - Get entity schema
  GET /odata/$metadata          - OData metadata document
═══════════════════════════════════════════
");

app.Run();
