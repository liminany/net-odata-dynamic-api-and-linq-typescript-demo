import { useState, useCallback } from "react";
import type { EntitySetInfo, EntitySchema, DynamicEntity, ODataQueryParams, ODataListResponse } from "@/types/odata";

const API_BASE = "http://localhost:5000";

// Simple fetch wrapper
async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error((err as { error?: string }).error || `HTTP ${res.status}`);
  }
  return res.json();
}

export function useOData() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [data, setData] = useState<DynamicEntity[]>([]);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [columns, setColumns] = useState<string[]>([]);
  const [entitySets, setEntitySets] = useState<EntitySetInfo[]>([]);

  // Load available entity sets
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

  // Load schema for an entity set
  const loadSchema = useCallback(async (entitySet: string): Promise<EntitySchema | null> => {
    try {
      return await fetchJson<EntitySchema>(`${API_BASE}/odata/admin/schema/${entitySet}`);
    } catch {
      return null;
    }
  }, []);

  // Execute OData query
  const executeQuery = useCallback(async (params: ODataQueryParams) => {
    setLoading(true);
    setError(null);

    try {
      const queryParts: string[] = [];

      if (params.filter) queryParts.push(`$filter=${encodeURIComponent(params.filter)}`);
      if (params.select) queryParts.push(`$select=${encodeURIComponent(params.select)}`);
      if (params.orderby) queryParts.push(`$orderby=${encodeURIComponent(params.orderby)}`);
      if (params.top != null) queryParts.push(`$top=${params.top}`);
      if (params.skip != null) queryParts.push(`$skip=${params.skip}`);
      if (params.count) queryParts.push(`$count=true`);
      if (params.expand) queryParts.push(`$expand=${encodeURIComponent(params.expand)}`);

      const queryStr = queryParts.length > 0 ? `?${queryParts.join("&")}` : "";
      const url = `${API_BASE}/odata/${params.entitySet}${queryStr}`;

      const result = await fetchJson<ODataListResponse>(url);
      const items = result.value || [];

      setData(items);
      setTotalCount(result.count ?? null);

      // Infer columns from first item
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

  // Build query params helper — for programmatic query construction
  const buildQuery = useCallback((params: ODataQueryParams): ODataQueryParams => params, []);

  return {
    loading,
    error,
    data,
    totalCount,
    columns,
    entitySets,
    loadEntitySets,
    loadSchema,
    executeQuery,
    buildQuery,
    setError,
  };
}
