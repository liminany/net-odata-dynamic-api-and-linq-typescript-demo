# net-sample-cg 项目长期记忆

## 项目概述
动态 OData API 系统：JSON 数据源 → 自动 OData API → React 查询界面

## 技术栈
- 后端: ASP.NET Core 9 + Microsoft.AspNetCore.OData 9.0.0
- 前端: React + TypeScript + Vite + Tailwind CSS + shadcn/ui
- .NET SDK: 9.0.314 (安装于 %LOCALAPPDATA%\Microsoft\dotnet)

## 关键架构原则
- **零代码添加实体**: 在 datasources.json 中注册 JSON 文件即可，无需写 Controller
- **单一 Controller**: DynamicODataController 处理所有实体集的 OData 请求
- **动态 EDM**: 启动时根据 JSON Schema 推断结果动态构建 OData 模型
- **纯内存存储**: 数据从 JSON 加载到 EntityDataStore，重启后重新加载

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
- 后端: `backend\DynamicODataApi\build-and-run.ps1`
- 前端: `frontend\app\dev.ps1` (开发) 或 `npm run build` (生产)
