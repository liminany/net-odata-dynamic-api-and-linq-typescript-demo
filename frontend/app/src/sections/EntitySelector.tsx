import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { ScrollArea } from "@/components/ui/scroll-area";
import type { EntitySetInfo, PropertySchema } from "@/types/odata";

interface Props {
  entitySets: EntitySetInfo[];
  selectedEntity: string | null;
  schema: PropertySchema[] | null;
  schemaLoading: boolean;
  onSelect: (name: string) => void;
}

export function EntitySelector({ entitySets, selectedEntity, schema, schemaLoading, onSelect }: Props) {
  return (
    <div className="flex flex-col gap-4 h-full">
      <Card>
        <CardHeader className="py-3 px-4">
          <CardTitle className="text-sm">实体集 Entity Sets</CardTitle>
        </CardHeader>
        <CardContent className="p-2">
          <ScrollArea className="h-[240px]">
            <div className="flex flex-col gap-1 p-1">
              {entitySets.length === 0 && (
                <p className="text-sm text-muted-foreground p-3">暂无可用实体集</p>
              )}
              {entitySets.map((es) => (
                <Button
                  key={es.name}
                  variant={selectedEntity === es.name ? "default" : "ghost"}
                  className="justify-start h-auto py-2 px-3"
                  onClick={() => onSelect(es.name)}
                >
                  <div className="flex flex-col items-start gap-0.5">
                    <span className="text-sm font-medium">{es.name}</span>
                    <span className="text-xs text-muted-foreground">
                      {es.recordCount} records | key: {es.idProperty}
                    </span>
                  </div>
                </Button>
              ))}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>

      {selectedEntity && (
        <Card>
          <CardHeader className="py-3 px-4">
            <CardTitle className="text-sm">Schema</CardTitle>
          </CardHeader>
          <CardContent className="p-2">
            {schemaLoading ? (
              <div className="space-y-2 p-2">
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-4 w-1/2" />
                <Skeleton className="h-4 w-2/3" />
              </div>
            ) : schema ? (
              <ScrollArea className="h-[300px]">
                <div className="flex flex-col gap-1 p-1">
                  {schema.map((prop) => (
                    <div key={prop.name} className="flex items-center gap-2 px-2 py-1.5 rounded-md hover:bg-accent">
                      <span className="text-sm font-medium flex-1">{prop.name}</span>
                      <Badge variant="outline" className="text-[10px] h-5 px-1.5">
                        {prop.type.replace("Edm.", "")}
                      </Badge>
                      {prop.isKey && (
                        <Badge variant="secondary" className="text-[10px] h-5 px-1.5">KEY</Badge>
                      )}
                    </div>
                  ))}
                </div>
              </ScrollArea>
            ) : null}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
