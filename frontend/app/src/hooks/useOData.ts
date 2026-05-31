import { useState, useCallback } from "react";
import { ODataService } from "@jin-qu/odata";
import type { EntitySetInfo, EntitySchema, ODataQueryParams, DynamicEntity } from "@/types/odata";

// 自动生成的实体类映射（npm run generate-entities 从 $metadata 生成）
import { Products, Customers, Orders } from "@/types/generated/entities";

const API_BASE = "http://localhost:5000/odata";

/** 实体名 → 实体类（jinqu-odata 用它解析 /odata/{EntitySetName} 路由） */
const entityClassMap: Record<string, new () => object> = {
  Products, Customers, Orders,
};

const odataService = new ODataService(API_BASE);

// ====== 纯 fetch 回退（用于字符串 filter 查询） ======
async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error((err as { error?: string }).error || `HTTP ${res.status}`);
  }
  return res.json();
}

interface QueryResponse {
  value?: DynamicEntity[];
  "@odata.count"?: number;
  count?: number;
}

export function useOData() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [data, setData] = useState<DynamicEntity[]>([]);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [columns, setColumns] = useState<string[]>([]);
  const [entitySets, setEntitySets] = useState<EntitySetInfo[]>([]);

  const loadEntitySets = useCallback(async () => {
    try {
      const sets = await fetchJson<EntitySetInfo[]>(`${API_BASE}/admin/entity-sets`);
      setEntitySets(sets);
      return sets;
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load entity sets");
      return [];
    }
  }, []);

  const loadSchema = useCallback(async (entitySet: string): Promise<EntitySchema | null> => {
    try {
      return await fetchJson<EntitySchema>(`${API_BASE}/admin/schema/${entitySet}`);
    } catch { return null; }
  }, []);

  /**
   * 执行 OData 查询。
   * 
   * 字符串 filter 模式（UI QueryBuilder）：用 fetch + 拼接 URL
   * Lambda 模式（代码编写）：用 odataService.createQuery(Products).where(p => p.Price > 1000)
   * 
   * 两种模式共享同一个 ODataService 和实体类映射。
   */
  const executeQuery = useCallback(async (params: ODataQueryParams) => {
    setLoading(true);
    setError(null);

    try {
      const entityClass = entityClassMap[params.entitySet];
      if (!entityClass)
        throw new Error(`Unknown entity: "${params.entitySet}". Run "npm run generate-entities"`);

      const queryParts: string[] = [];
      if (params.filter) queryParts.push(`$filter=${encodeURIComponent(params.filter)}`);
      if (params.select) queryParts.push(`$select=${encodeURIComponent(params.select)}`);
      if (params.orderby) queryParts.push(`$orderby=${encodeURIComponent(params.orderby)}`);
      if (params.top != null) queryParts.push(`$top=${params.top}`);
      if (params.skip != null) queryParts.push(`$skip=${params.skip}`);
      if (params.count) queryParts.push(`$count=true`);

      const qs = queryParts.length > 0 ? `?${queryParts.join("&")}` : "";
      const url = `${API_BASE}/${params.entitySet}${qs}`;

      const result = await fetchJson<QueryResponse>(url);
      const items: DynamicEntity[] = Array.isArray(result) ? result : (result.value || []);

      setData(items);
      setTotalCount(result["@odata.count"] ?? result.count ?? null);
      if (items.length > 0) setColumns(Object.keys(items[0]));
      return items;
    } catch (e) {
      setError(e instanceof Error ? e.message : "Query execution failed");
      setData([]);
      return [];
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * 暴露 jinqu-odata 的 Lambda 查询接口，供代码编写时使用
   * 
   * @example
   *   const { lambdaQuery } = useOData();
   *   const books = await lambdaQuery(Products, q => q
   *     .where(p => p.Price > 1000)
   *     .orderBy(p => p.Price)
   *     .take(5)
   *   );
   */
  const lambdaQuery = useCallback(async <T extends object>(
    entityClass: new () => T,
    build: (q: ReturnType<typeof odataService.createQuery<T>>) => ReturnType<typeof odataService.createQuery<T>>
  ): Promise<T[]> => {
    setLoading(true);
    setError(null);
    try {
      let query = odataService.createQuery(entityClass);
      query = build(query);
      const result = await (query as any).toArrayAsync();
      return result as T[];
    } catch (e) {
      setError(e instanceof Error ? e.message : "Lambda query failed");
      return [];
    } finally {
      setLoading(false);
    }
  }, []);

  return {
    loading, error, data, totalCount, columns, entitySets,
    loadEntitySets, loadSchema, executeQuery, lambdaQuery, setError,
  };
}
