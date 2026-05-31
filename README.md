# Dynamic OData API - 基于 JSON 数据源的零代码 OData API

## 项目概述

**核心价值：** 提供 JSON 数据文件 → 自动暴露为 OData API，无需为每个业务对象编写 Controller。前端可通过 OData 查询语法（类似 LINQ/Lambda）动态搜索数据。

```
datasources.json 配置 → 自动推断 Schema → 动态构建 EDM 模型 → 通用 OData Controller → React 查询界面
```

## 项目结构

```
net-sample-cg/
├── backend/
│   └── DynamicODataApi/          # ASP.NET Core 9 + OData
│       ├── Program.cs            # 启动入口，加载配置+动态构建EDM
│       ├── datasources.json      # ← 在这里新增数据源
│       ├── Data/                 # JSON 数据文件
│       │   ├── products.json     #   示例：商品数据
│       │   ├── customers.json    #   示例：客户数据
│       │   └── orders.json       #   示例：订单数据
│       ├── Services/
│       │   ├── JsonSchemaInferenceService.cs  # JSON → Schema 推断
│       │   ├── DynamicEdmModelBuilder.cs      # Schema → EDM 模型
│       │   └── EntityDataStore.cs             # 内存数据存储
│       ├── Controllers/
│       │   └── DynamicODataController.cs      # 唯一的通用 Controller
│       ├── Models/               # 数据模型
│       └── build-and-run.ps1     # 构建启动脚本
│
└── frontend/
    └── app/                      # React + shadcn/ui
        ├── src/
        │   ├── sections/
        │   │   ├── EntitySelector.tsx   # 实体选择器
        │   │   ├── QueryBuilder.tsx     # OData 查询构建器
        │   │   └── DataTable.tsx        # 动态数据表格
        │   ├── hooks/useOData.ts        # OData API Hook
        │   └── types/odata.ts           # 类型定义
        ├── dev.ps1               # 前端开发启动
        └── dist/                 # 前端构建产物
```

## 快速启动

### 前置要求
- .NET SDK 9.0+ (安装: `winget install Microsoft.DotNet.SDK.9`)
- Node.js 20+ (安装: `winget install OpenJS.NodeJS`)

### 1. 启动后端

```powershell
cd backend\DynamicODataApi
.\build-and-run.ps1
```

后端运行在: **http://localhost:5000**

### 2. 启动前端

```powershell
cd frontend\app
.\dev.ps1
```

前端运行在: **http://localhost:5173**

## 新增数据源（3 步完成）

### 第 1 步：准备 JSON 数据文件

在 `backend/DynamicODataApi/Data/` 下创建 JSON 文件：

```json
[
  {"Id": 1, "Name": "...", "Price": 99.9, "Category": "...", ...},
  ...
]
```

### 第 2 步：在 `datasources.json` 中注册

```json
{
  "dataSources": [
    { "entitySetName": "YourEntities", "filePath": "Data/yourfile.json", "idProperty": "Id" }
  ]
}
```

### 第 3 步：重启后端

API 自动暴露 `/odata/YourEntities`，支持完整 OData 查询。

## OData 查询示例

```bash
# 获取所有记录
GET http://localhost:5000/odata/Products

# 过滤条件：价格 > 1000
GET http://localhost:5000/odata/Products?$filter=Price gt 1000

# 选择字段 + 排序 + 分页
GET http://localhost:5000/odata/Products?$select=Name,Price&$orderby=Price desc&$top=5

# 获取总数
GET http://localhost:5000/odata/Products?$count=true

# 字符串模糊搜索
GET http://localhost:5000/odata/Products?$filter=contains(Name,'Pro')

# 组合条件
GET http://localhost:5000/odata/Products?$filter=Price gt 1000 and Category eq 'Laptop'

# 获取单条
GET http://localhost:5000/odata/Products/1

# 查看元数据
GET http://localhost:5000/odata/$metadata

# 管理端点
GET http://localhost:5000/odata/admin/entity-sets
GET http://localhost:5000/odata/admin/schema/Products
```

## OData → LINQ 对照表

| OData 查询参数 | LINQ 方法 | 说明 |
|---|---|---|
| `$filter=Price gt 1000` | `.Where(x => x.Price > 1000)` | 过滤 |
| `$select=Name,Price` | `.Select(x => new { x.Name, x.Price })` | 投影 |
| `$orderby=Price desc` | `.OrderByDescending(x => x.Price)` | 排序 |
| `$top=10` | `.Take(10)` | 取前 N 条 |
| `$skip=20` | `.Skip(20)` | 跳过 N 条 |
| `$count=true` | `.Count()` | 返回总数 |
| `$expand=Orders` | `.Include(x => x.Orders)` | 展开导航属性 |

## 前端查询构建器说明

前端提供直观的 OData 查询构造界面：

1. **左侧面板** - 选择实体集，查看 Schema
2. **查询构建器** - 可视化构造 $filter / $select / $orderby / 分页
3. **数据表格** - 动态渲染查询结果，自动适配列类型
4. **URL 预览** - 实时显示生成的 OData 查询 URL

## 注意事项

- 后端数据完全在内存中，重启后重新加载 JSON 文件
- 大文件（>10MB）建议分片或使用数据库
- 当前版本不支持数据写入（只读查询）
- 如需持久化编辑，可扩展 `EntityDataStore` 添加写回文件逻辑
