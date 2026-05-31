using System.Text.Json;
using DynamicODataApi.Models;
using DynamicODataApi.Services;
using Microsoft.AspNetCore.OData;

var builder = WebApplication.CreateBuilder(args);

// ====== 阶段 1: 加载数据源配置 ======
var configPath = Path.Combine(AppContext.BaseDirectory, "datasources.json");
if (!File.Exists(configPath))
    configPath = Path.Combine(Directory.GetCurrentDirectory(), "datasources.json");

var dataSourceRoot = File.Exists(configPath)
    ? JsonSerializer.Deserialize<DataSourceRoot>(File.ReadAllText(configPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    : new DataSourceRoot();

Console.WriteLine($"[Init] Loaded {dataSourceRoot?.DataSources.Count ?? 0} data source configs");

// ====== 阶段 2: 推断 JSON Schema ======
var logFactory = LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Information));
var schemaService = new JsonSchemaInferenceService(
    logFactory.CreateLogger<JsonSchemaInferenceService>());

var schemas = new List<EntitySchema>();
if (dataSourceRoot?.DataSources != null)
{
    foreach (var ds in dataSourceRoot.DataSources)
    {
        try
        {
            var schema = schemaService.InferSchema(ds.FilePath, ds.EntitySetName, ds.IdProperty);
            schemas.Add(schema);
            Console.WriteLine($"[Schema] {ds.EntitySetName}: {schema.Properties.Count} properties, key={ds.IdProperty}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warn] Failed to infer schema for {ds.EntitySetName}: {ex.Message}");
        }
    }
}

// ====== 阶段 3: 运行时生成 CLR 类型 (Reflection.Emit) ======
var typeBuilder = new DynamicTypeBuilder(logFactory.CreateLogger<DynamicTypeBuilder>());
foreach (var schema in schemas)
{
    typeBuilder.GetOrCreateType(schema);
    Console.WriteLine($"[Type] {schema.EntitySetName} → {schema.ClrType!.FullName}");
}

// ====== 阶段 4: 基于 CLR 类型构建 EDM 模型 ======
var edmBuilder = new DynamicEdmModelBuilder(logFactory.CreateLogger<DynamicEdmModelBuilder>());
var edmModel = edmBuilder.Build(schemas);
Console.WriteLine($"[EDM] Model built with {schemas.Count} entity types");

// ====== 阶段 5: 加载数据（使用 CLR 类型实例化实体） ======
var entityStore = new EntityDataStore(logFactory.CreateLogger<EntityDataStore>());
if (dataSourceRoot?.DataSources != null)
{
    foreach (var ds in dataSourceRoot.DataSources)
    {
        var schema = schemas.FirstOrDefault(s =>
            string.Equals(s.EntitySetName, ds.EntitySetName, StringComparison.OrdinalIgnoreCase));
        if (schema?.ClrType == null) continue;

        try
        {
            entityStore.LoadDataSource(ds, schema.ClrType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warn] Failed to load {ds.EntitySetName}: {ex.Message}");
        }
    }
}

// ====== 阶段 6: 注册依赖注入 ======
builder.Services.AddSingleton(entityStore);
builder.Services.AddSingleton(edmModel);

builder.Services.AddControllers()
    .AddOData(opt =>
    {
        opt.Select().Filter().OrderBy().Count().Expand().SetMaxTop(1000);
        opt.AddRouteComponents("odata", edmModel);
    });

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("*"));
});

var app = builder.Build();

app.UseCors();
app.UseRouting();
app.MapControllers();

Console.WriteLine($@"
═══════════════════════════════════════════
 Dynamic OData API is ready!
═══════════════════════════════════════════
 Entities: {string.Join(", ", entityStore.GetEntitySetNames())}
 
  GET /odata/{{entitySetName}}?$filter=...&$select=...&$orderby=...
  GET /odata/$metadata
  GET /odata/admin/entity-sets
═══════════════════════════════════════════
");

app.Run();
