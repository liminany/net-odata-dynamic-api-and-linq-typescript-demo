# AI 交互记录

---

## 2026-05-31

### 1. NuGet 缓存目录配置
- **工具**: CodeBuddy (replace_in_file)
- **原始需求**: 帮我把net项目nuget包的全局和用户级级别的存储缓存目录改为D:\Admin\Documents
- **操作**: 修改 `backend/DynamicODataApi/NuGet.config`，将 `globalPackagesFolder` 从 `..\.nuget` 改为 `D:\Admin\Documents`，并新增 `http-cache` 配置项指向 `D:\Admin\Documents`

---
