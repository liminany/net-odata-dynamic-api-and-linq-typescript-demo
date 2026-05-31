import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription } from "@/components/ui/alert";
import type { DynamicEntity } from "@/types/odata";
import { Database, AlertCircle } from "lucide-react";

interface Props {
  data: DynamicEntity[];
  columns: string[];
  totalCount: number | null;
  loading: boolean;
  error: string | null;
}

function formatCellValue(value: unknown): string {
  if (value === null || value === undefined) return "-";
  if (typeof value === "boolean") return value ? "true" : "false";
  if (typeof value === "number") {
    // format as currency if it looks like price
    if (Number.isFinite(value)) {
      const num = value as number;
      if (num % 1 !== 0 && num > 0) return num.toFixed(2);
      return num.toLocaleString();
    }
    return String(value);
  }
  if (typeof value === "string") {
    // truncate long strings
    if (value.length > 50) return value.slice(0, 47) + "...";
    return value;
  }
  if (typeof value === "object") {
    try {
      return JSON.stringify(value).slice(0, 50);
    } catch {
      return "[Object]";
    }
  }
  return String(value);
}

function getCellTypeClass(value: unknown): string {
  if (value === null || value === undefined) return "text-muted-foreground italic";
  if (typeof value === "boolean") return "";
  if (typeof value === "number") return "font-mono tabular-nums";
  return "";
}

function getTypeBadge(value: unknown): string {
  if (value === null) return "null";
  if (typeof value === "boolean") return "bool";
  if (typeof value === "number") return Number.isInteger(value as number) ? "int" : "float";
  if (typeof value === "string") return "str";
  if (Array.isArray(value)) return "arr";
  return "obj";
}

export function DataTable({ data, columns, totalCount, loading, error }: Props) {
  // Determine visible columns (if no data, use schema columns)
  const visibleColumns = columns.length > 0 ? columns : [];

  // Infer column types from first row
  const columnTypes: Record<string, string> = {};
  if (data.length > 0) {
    for (const col of visibleColumns) {
      columnTypes[col] = getTypeBadge(data[0][col]);
    }
  }

  if (error) {
    return (
      <Card>
        <CardContent className="p-6">
          <Alert variant="destructive">
            <AlertCircle className="w-4 h-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader className="py-3 px-4 flex flex-row items-center justify-between">
        <CardTitle className="text-sm flex items-center gap-2">
          <Database className="w-4 h-4" />
          查询结果
        </CardTitle>
        {totalCount != null && (
          <Badge variant="secondary" className="text-xs">
            共 {totalCount} 条
            {data.length < totalCount && ` (显示前 ${data.length} 条)`}
          </Badge>
        )}
        {totalCount == null && data.length > 0 && (
          <Badge variant="secondary" className="text-xs">{data.length} 条</Badge>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {loading ? (
          <div className="p-4 space-y-2">
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/2" />
            <Skeleton className="h-4 w-2/3" />
          </div>
        ) : data.length === 0 ? (
          <div className="p-8 text-center text-muted-foreground">
            <Database className="w-12 h-12 mx-auto mb-2 opacity-30" />
            <p className="text-sm">
              {visibleColumns.length === 0
                ? "选择一个实体集并执行查询"
                : "查询无结果"}
            </p>
          </div>
        ) : (
          <div className="overflow-auto max-h-[500px]">
            <Table>
              <TableHeader className="sticky top-0 bg-background z-10">
                <TableRow>
                  {visibleColumns.map((col) => (
                    <TableHead key={col} className="text-xs font-medium whitespace-nowrap">
                      <div className="flex items-center gap-1.5">
                        {col}
                        <Badge
                          variant="outline"
                          className="text-[9px] h-4 px-1 font-normal text-muted-foreground"
                        >
                          {columnTypes[col] || "?"}
                        </Badge>
                      </div>
                    </TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.map((row, i) => (
                  <TableRow key={i} className="hover:bg-accent/50 transition-colors">
                    {visibleColumns.map((col) => (
                      <TableCell
                        key={col}
                        className={`text-xs py-2 px-3 max-w-[200px] truncate ${getCellTypeClass(row[col])}`}
                        title={String(row[col] ?? "")}
                      >
                        {formatCellValue(row[col])}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
