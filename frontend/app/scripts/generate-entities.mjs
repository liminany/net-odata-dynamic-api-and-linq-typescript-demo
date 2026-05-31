/**
 * generate-entities.mjs
 * 
 * 从 OData $metadata XML 生成 TypeScript 实体类（带 @oDataResource 装饰器）
 * 
 * 用法: node scripts/generate-entities.mjs [backend-url]
 * 默认: http://localhost:5000/odata/$metadata
 * 
 * 输出: src/types/generated/entities.ts
 */

const DEFAULT_URL = "http://localhost:5000/odata/$metadata";
const OUTPUT_PATH = "src/types/generated/entities.ts";

// ====== OData type → TypeScript type 映射 ======
const TYPE_MAP = {
  "Edm.String": "string",
  "Edm.Int32": "number",
  "Edm.Int64": "number",
  "Edm.Double": "number",
  "Edm.Decimal": "number",
  "Edm.Boolean": "boolean",
  "Edm.DateTimeOffset": "string",
  "Edm.Guid": "string",
  "Edm.Binary": "string",
  "Edm.Byte": "number",
  "Edm.Int16": "number",
  "Edm.Single": "number",
};

function toTsType(edmType, nullable) {
  const base = TYPE_MAP[edmType] || "string";
  return nullable ? `${base} | null` : base;
}

async function fetchMetadata(url) {
  console.log(`Fetching $metadata from: ${url}`);
  const res = await fetch(url);
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
  return res.text();
}

function parseEntityTypes(xml) {
  // 简单 XML 解析（不依赖第三方库，仅解析 EntityType 和 Property）
  const entities = [];
  const entityRegex = /<EntityType\s+Name="(\w+)"[^>]*>([\s\S]*?)<\/EntityType>/g;
  const propRegex = /<Property\s+Name="(\w+)"\s+Type="([^"]+)"(?:\s+Nullable="(true|false)")?/g;
  const keyRegex = /<PropertyRef\s+Name="(\w+)"/g;

  let match;
  while ((match = entityRegex.exec(xml)) !== null) {
    const entityName = match[1];
    const body = match[2];

    // 提取主键
    const keys = [];
    let keyMatch;
    while ((keyMatch = keyRegex.exec(body)) !== null) {
      keys.push(keyMatch[1]);
    }
    keyRegex.lastIndex = 0;

    // 提取属性
    const properties = [];
    let propMatch;
    while ((propMatch = propRegex.exec(body)) !== null) {
      properties.push({
        name: propMatch[1],
        type: propMatch[2],
        nullable: propMatch[3] !== "false", // 默认 nullable，仅 Nullable="false" 时不可为空
      });
    }

    entities.push({ name: entityName, properties, keys });
  }

  return entities;
}

function generateCode(entities) {
  const lines = [];

  lines.push("// 自动生成于 " + new Date().toISOString());
  lines.push("// 来源: OData $metadata");
  lines.push("// 生成命令: npm run generate-entities");
  lines.push("// 请勿手动编辑此文件");
  lines.push("");
  lines.push('import "reflect-metadata";');
  lines.push('import { oDataResource } from "@jin-qu/odata";');
  lines.push("");

  for (const entity of entities) {
    const entitySetName = entity.name;

    lines.push(`/** OData entity set: ${entitySetName} */`);
    lines.push(`@oDataResource("${entitySetName}")`);
    lines.push(`export class ${entitySetName} {`);

    for (const prop of entity.properties) {
      const tsType = toTsType(prop.type, prop.nullable);
      const isKey = entity.keys.includes(prop.name);
      const comment = isKey ? " // [Key]" : "";
      lines.push(`  ${prop.name}!: ${tsType};${comment}`);
    }

    lines.push("}");
    lines.push("");
  }

  return lines.join("\n");
}

// ====== Main ======
async function main() {
  const url = process.argv[2] || DEFAULT_URL;

  console.log("=== OData Entity Generator ===\n");

  let xml;
  try {
    xml = await fetchMetadata(url);
  } catch (e) {
    console.error(`Failed to fetch metadata: ${e.message}`);
    console.error("Make sure the backend is running on the correct port.");
    process.exit(1);
  }

  const entities = parseEntityTypes(xml);
  console.log(`Found ${entities.length} entity types:`);
  for (const e of entities) {
    console.log(`  - ${e.name} (${e.properties.length} properties, keys: ${e.keys.join(", ")})`);
  }

  const code = generateCode(entities);
  
  const fs = await import("fs");
  fs.writeFileSync(OUTPUT_PATH, code, "utf-8");
  console.log(`\nGenerated: ${OUTPUT_PATH}`);
  console.log(`File size: ${code.length} bytes`);
}

main().catch(console.error);
