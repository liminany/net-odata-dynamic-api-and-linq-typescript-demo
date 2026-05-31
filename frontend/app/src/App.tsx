import { useState, useEffect, useCallback } from "react";
import { EntitySelector } from "@/sections/EntitySelector";
import { QueryBuilder } from "@/sections/QueryBuilder";
import { DataTable } from "@/sections/DataTable";
import { LambdaPlayground } from "@/sections/LambdaPlayground";
import { useOData } from "@/hooks/useOData";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Separator } from "@/components/ui/separator";
import type { PropertySchema, ODataQueryParams } from "@/types/odata";
import { Database, RefreshCw, Code2, Layers } from "lucide-react";

export default function App() {
  const {
    loading,
    error,
    data,
    totalCount,
    columns,
    entitySets,
    loadEntitySets,
    loadSchema,
    executeQuery,
  } = useOData();

  const [selectedEntity, setSelectedEntity] = useState<string | null>(null);
  const [schema, setSchema] = useState<PropertySchema[] | null>(null);
  const [schemaLoading, setSchemaLoading] = useState(false);
  const [lastQuery, setLastQuery] = useState<ODataQueryParams | null>(null);

  // On mount, load entity sets
  useEffect(() => {
    loadEntitySets();
  }, [loadEntitySets]);

  // When entity is selected, load its schema
  const handleEntitySelect = useCallback(async (name: string) => {
    setSelectedEntity(name);
    setSchemaLoading(true);
    const result = await loadSchema(name);
    if (result) {
      setSchema(result.properties);
    }
    setSchemaLoading(false);
  }, [loadSchema]);

  const handleExecuteQuery = useCallback((params: ODataQueryParams) => {
    setLastQuery(params);
    executeQuery(params);
  }, [executeQuery]);

  const handleRefresh = useCallback(() => {
    loadEntitySets();
    if (selectedEntity) {
      handleEntitySelect(selectedEntity);
    }
    if (lastQuery) {
      executeQuery(lastQuery);
    }
  }, [loadEntitySets, selectedEntity, handleEntitySelect, lastQuery, executeQuery]);

  return (
    <div className="min-h-screen bg-background">
      {/* Header */}
      <header className="border-b bg-card">
        <div className="flex items-center justify-between px-6 py-3">
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-lg bg-primary flex items-center justify-center">
                <Database className="w-4 h-4 text-primary-foreground" />
              </div>
              <div>
                <h1 className="text-sm font-semibold">Dynamic OData Explorer</h1>
                <p className="text-xs text-muted-foreground">
                  动态 JSON 数据源 OData 查询工具
                </p>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-3">
            {entitySets.length > 0 && (
              <Badge variant="outline" className="text-xs gap-1">
                <Layers className="w-3 h-3" />
                {entitySets.length} entity sets
              </Badge>
            )}
            <Button variant="outline" size="sm" onClick={handleRefresh} className="h-8 text-xs gap-1">
              <RefreshCw className="w-3 h-3" />
              刷新
            </Button>
            <Button variant="ghost" size="sm" className="h-8 text-xs gap-1" asChild>
              <a href="http://localhost:5000/odata/$metadata" target="_blank" rel="noopener noreferrer">
                <Code2 className="w-3 h-3" />
                $metadata
              </a>
            </Button>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <div className="flex h-[calc(100vh-57px)]">
        {/* Left Sidebar */}
        <aside className="w-72 border-r bg-card p-3 overflow-hidden">
          <EntitySelector
            entitySets={entitySets}
            selectedEntity={selectedEntity}
            schema={schema}
            schemaLoading={schemaLoading}
            onSelect={handleEntitySelect}
          />
        </aside>

        {/* Right Content */}
        <main className="flex-1 flex flex-col overflow-hidden">
          {/* Tabs for views */}
          <div className="flex-1 overflow-auto">
            <Tabs defaultValue="query" className="w-full h-full flex flex-col">
              <div className="border-b px-4 py-2 bg-card">
                <TabsList className="h-8">
                  <TabsTrigger value="query" className="text-xs h-7">查询 Query</TabsTrigger>
                  <TabsTrigger value="lambda" className="text-xs h-7">Lambda 演示</TabsTrigger>
                </TabsList>
              </div>

              <TabsContent value="query" className="flex-1 overflow-auto p-4 space-y-4 mt-0">
                <QueryBuilder
                  entitySet={selectedEntity || ""}
                  schema={schema}
                  loading={loading}
                  onExecute={handleExecuteQuery}
                />
                <Separator />
                <DataTable
                  data={data}
                  columns={columns}
                  totalCount={totalCount}
                  loading={loading}
                  error={error}
                />
              </TabsContent>

              <TabsContent value="lambda" className="flex-1 overflow-auto p-4 mt-0">
                <LambdaPlayground />
              </TabsContent>
            </Tabs>
          </div>
        </main>
      </div>
    </div>
  );
}
