import { useState, useCallback } from "react";
import buildODataQuery from "odata-query";
import type { EntitySetInfo, EntitySchema, DynamicEntity, ODataQueryParams } from "@/types/odata";

const API_BASE = "http://localhost:5000";

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
  const [generatedUrl, setGeneratedUrl] = useState<string>("");

  const loadEntitySets = useCallback(async () => {
    try {
      const sets = await fetchJson<EntitySetInfo[]>(`${API_BASE}/odata/admin/entity-sets`);
      setEntitySets(sets);
      return sets;
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load entity sets");
      return [];
    }
  }, []);

  const loadSchema = useCallback(async (entitySet: string): Promise<EntitySchema | null> => {
    try {
      return await fetchJson<EntitySchema>(`${API_BASE}/odata/admin/schema/${entitySet}`);
    } catch {
      return null;
    }
  }, []);

  /**
   * 使用 odata-query 库构建 OData URL。
   * 提供 LINQ/Lambda 式查询接口：
   * 
   *   buildQuery('Products', {
   *     filter: { Price: { gt: 1000 } },
   *     select: ['Name', 'Price'],
   *     orderBy: [['Price', 'desc']],
   *     top: 5,
   *     count: true
   *   })
   *   → /odata/Products?$filter=Price gt 1000&$select=Name,Price&$orderby=Price desc&$top=5&$count=true
   */
  const executeQuery = useCallback(async (params: ODataQueryParams) => {
    setLoading(true);
    setError(null);

    try {
      // 用 odata-query 库构建查询字符串（LINQ/Lambda 式）
      const queryOpts: Record<string, unknown> = {};

      if (params.filter) {
        // 字符串 filter 原样传递
        queryOpts.filter = params.filter;
      }
      if (params.select) {
        queryOpts.select = params.select.split(",").map(s => s.trim());
      }
      if (params.orderby) {
        queryOpts.orderBy = params.orderby;
      }
      if (params.top != null) queryOpts.top = params.top;
      if (params.skip != null) queryOpts.skip = params.skip;
      if (params.count) queryOpts.count = true;

      const queryStr = buildODataQuery(queryOpts);
      const url = `${API_BASE}/odata/${params.entitySet}${queryStr}`;
      setGeneratedUrl(url);

      const result = await fetchJson<QueryResponse>(url);
      
      // 兼容 OData 标准格式 { value: [...] } 和直接数组格式 [...]
      const items: DynamicEntity[] = Array.isArray(result) 
        ? result 
        : (result.value || []);

      setData(items);
      
      // 读取 @odata.count（标准）或 count（简化）
      const cnt = result["@odata.count"] ?? result.count ?? null;
      setTotalCount(cnt);

      if (items.length > 0) {
        setColumns(Object.keys(items[0]));
      }

      return items;
    } catch (e) {
      setError(e instanceof Error ? e.message : "Query execution failed");
      setData([]);
      return [];
    } finally {
      setLoading(false);
    }
  }, []);

  return {
    loading,
    error,
    data,
    totalCount,
    columns,
    entitySets,
    generatedUrl,
    loadEntitySets,
    loadSchema,
    executeQuery,
    setError,
  };
}
