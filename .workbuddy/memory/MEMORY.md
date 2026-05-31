# net-sample-cg 项目长期记忆

## 项目概述
动态 OData API 系统：JSON 数据源 → 自动 OData API → React 查询界面

## 技术栈
- 后端: ASP.NET Core 9 + Microsoft.AspNetCore.OData 9.0.0
- 前端: React + TypeScript + Vite + Tailwind CSS + shadcn/ui + odata-query 8.1.0
- .NET SDK: 9.0.314 (安装于 %LOCALAPPDATA%\Microsoft\dotnet)

## 关键架构原则
- **零代码添加实体**: 在 datasources.json 中注册 JSON 文件即可，无需写 Controller
- **单一 Controller**: DynamicODataController 处理所有实体集的 OData 请求
- **运行时类型生成**: DynamicTypeBuilder 用 Reflection.Emit 生成有真实 CLR 属性的实体类
- **运行时 EDM 构建**: DynamicEdmModelBuilder 基于 CLR 类型用 ODataConventionModelBuilder 构建 EDM
- **[EnableQuery] 原生支持**: 返回 IQueryable<T> 让 OData 框架自动处理查询
- **纯内存存储**: 数据从 JSON 加载为强类型实体对象，重启后重新加载

## V2 核心服务
| 服务 | 职责 |
|---|---|
| JsonSchemaInferenceService | JSON → 属性名/类型推断 |
| DynamicTypeBuilder | Schema → 运行时 CLR 类型 (Reflection.Emit) |
| DynamicEdmModelBuilder | CLR 类型 → EDM 模型 (ODataConventionModelBuilder) |
| EntityDataStore | 强类型实体存储 + IQueryable<T> 查询 |

## 新增数据源步骤
1. 在 Data/ 下放置 JSON 数组文件
2. 在 datasources.json 的 dataSources 数组中添加配置项 (entitySetName, filePath, idProperty)
3. 重启后端

## 端口
- 后端 API: http://localhost:5000
- 前端开发: http://localhost:5173
- OData 端点: /odata/{entitySetName}
- OData 元数据: /odata/$metadata

## 构建脚本
- 后端: `dotnet build --no-restore` (本地 DLL 引用，无需 NuGet restore)
- 前端: `npm run build` (生产) 或 `npm run dev` (开发)
- 一键启动: `start.cmd`

## NuGet 本地包
为避免沙箱 NuGet 问题，5 个 OData 包通过本地 DLL 引用：
- Microsoft.AspNetCore.OData 9.0.0
- Microsoft.OData.Core 8.0.2
- Microsoft.OData.Edm 8.0.2
- Microsoft.OData.ModelBuilder 2.0.0
- Microsoft.Spatial 8.0.2
位置: `backend/packages/`
