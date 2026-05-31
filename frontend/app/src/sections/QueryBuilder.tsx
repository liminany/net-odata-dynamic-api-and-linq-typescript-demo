import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Checkbox } from "@/components/ui/checkbox";
import { Spinner } from "@/components/ui/spinner";
import type { ODataQueryParams, PropertySchema } from "@/types/odata";
import { Search, Play, RotateCcw, ChevronDown, ChevronUp } from "lucide-react";

interface Props {
  entitySet: string;
  schema: PropertySchema[] | null;
  loading: boolean;
  onExecute: (params: ODataQueryParams) => void;
}

// UI-friendly logical operators
const OPERATORS = [
  { label: "等于 ==", value: "eq", odata: "eq" },
  { label: "不等于 !=", value: "ne", odata: "ne" },
  { label: "大于 >", value: "gt", odata: "gt" },
  { label: "小于 <", value: "lt", odata: "lt" },
  { label: "大于等于 >=", value: "ge", odata: "ge" },
  { label: "小于等于 <=", value: "le", odata: "le" },
  { label: "包含 contains", value: "contains", odata: "contains" },
  { label: "开头 startswith", value: "startswith", odata: "startswith" },
];

export function QueryBuilder({ entitySet, schema, loading, onExecute }: Props) {
  const [expanded, setExpanded] = useState(true);

  // Filter builder state
  const [filterField, setFilterField] = useState<string>("");
  const [filterOp, setFilterOp] = useState<string>("eq");
  const [filterValue, setFilterValue] = useState<string>("");
  const [filters, setFilters] = useState<string[]>([]);

  // Select state
  const [selectedFields, setSelectedFields] = useState<Set<string>>(new Set());

  // OrderBy state
  const [orderField, setOrderField] = useState<string>("");
  const [orderDir, setOrderDir] = useState<"asc" | "desc">("desc");

  // Pagination
  const [top, setTop] = useState<string>("50");
  const [skip, setSkip] = useState<string>("0");
  const [includeCount, setIncludeCount] = useState(true);

  const addFilter = () => {
    if (!filterField || !filterValue) return;
    let expr: string;
    if (filterOp === "contains" || filterOp === "startswith") {
      expr = `${filterOp}(${filterField},'${filterValue}')`;
    } else {
      const quoted = isNaN(Number(filterValue)) ? `'${filterValue}'` : filterValue;
      expr = `${filterField} ${filterOp} ${quoted}`;
    }
    setFilters([...filters, expr]);
    setFilterValue("");
  };

  const removeFilter = (index: number) => {
    setFilters(filters.filter((_, i) => i !== index));
  };

  const toggleField = (field: string) => {
    const next = new Set(selectedFields);
    if (next.has(field)) next.delete(field);
    else next.add(field);
    setSelectedFields(next);
  };

  const buildAndExecute = () => {
    const params: ODataQueryParams = {
      entitySet,
      filter: filters.length > 0 ? filters.join(" and ") : undefined,
      select: selectedFields.size > 0 ? Array.from(selectedFields).join(",") : undefined,
      orderby: orderField ? `${orderField} ${orderDir}` : undefined,
      top: top ? parseInt(top) : undefined,
      skip: skip ? parseInt(skip) : undefined,
      count: includeCount,
    };
    onExecute(params);
  };

  const handleReset = () => {
    setFilters([]);
    setSelectedFields(new Set());
    setOrderField("");
    setTop("50");
    setSkip("0");
  };

  // Quick filter presets
  const quickFilters = schema
    ? [
        { label: "数字 > 1000", build: () => {
          const numField = schema.find(p => p.type.includes("Int") || p.type.includes("Double") || p.type.includes("Decimal"));
          return numField ? `${numField.name} gt 1000` : null;
        }},
        { label: "布尔 = true", build: () => {
          const boolField = schema.find(p => p.type.includes("Boolean"));
          return boolField ? `${boolField.name} eq true` : null;
        }},
      ].filter(q => q.build() !== null)
    : [];

  return (
    <Card>
      <CardHeader
        className="py-3 px-4 cursor-pointer hover:bg-accent/50 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm flex items-center gap-2">
            <Search className="w-4 h-4" />
            查询构建器 Query Builder
            {entitySet && <span className="text-muted-foreground font-normal">- {entitySet}</span>}
          </CardTitle>
          <Button variant="ghost" size="icon" className="h-6 w-6">
            {expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </Button>
        </div>
      </CardHeader>

      {expanded && (
        <CardContent className="px-4 pb-4 space-y-4">
          {/* Filter Section */}
          <div className="space-y-2">
            <Label className="text-xs font-medium flex items-center gap-2">
              $filter
              <span className="text-muted-foreground font-normal">
                (类似 LINQ .Where())
              </span>
            </Label>

            {/* Active filters */}
            {filters.length > 0 && (
              <div className="flex flex-wrap gap-1.5 mb-2">
                {filters.map((f, i) => (
                  <span
                    key={i}
                    className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-primary/10 text-primary text-xs cursor-pointer hover:bg-destructive/10 hover:text-destructive"
                    onClick={() => removeFilter(i)}
                    title="点击删除"
                  >
                    {f} ×
                  </span>
                ))}
              </div>
            )}

            {/* Quick filters */}
            {quickFilters.length > 0 && (
              <div className="flex gap-1 flex-wrap mb-1">
                {quickFilters.map((qf, i) => (
                  <Button
                    key={i}
                    variant="outline"
                    size="sm"
                    className="h-6 text-[11px] px-2"
                    onClick={() => {
                      const expr = qf.build();
                      if (expr) setFilters([...filters, expr]);
                    }}
                  >
                    {qf.label}
                  </Button>
                ))}
              </div>
            )}

            <div className="flex gap-2">
              <Select value={filterField} onValueChange={setFilterField}>
                <SelectTrigger className="h-8 text-xs w-[130px]">
                  <SelectValue placeholder="选择字段" />
                </SelectTrigger>
                <SelectContent>
                  {schema?.map((p) => (
                    <SelectItem key={p.name} value={p.name} className="text-xs">
                      {p.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <Select value={filterOp} onValueChange={setFilterOp}>
                <SelectTrigger className="h-8 text-xs w-[110px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {OPERATORS.map((op) => (
                    <SelectItem key={op.value} value={op.value} className="text-xs">
                      {op.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <Input
                className="h-8 text-xs flex-1"
                placeholder="值..."
                value={filterValue}
                onChange={(e) => setFilterValue(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && addFilter()}
              />

              <Button size="sm" className="h-8 text-xs" onClick={addFilter}>
                + 添加
              </Button>
            </div>
          </div>

          {/* Select & OrderBy Row */}
          <div className="grid grid-cols-2 gap-4">
            {/* $select */}
            <div className="space-y-1.5">
              <Label className="text-xs font-medium">
                $select <span className="text-muted-foreground font-normal">(.Select())</span>
              </Label>
              <div className="flex flex-wrap gap-1 max-h-[80px] overflow-y-auto">
                {schema?.map((p) => (
                  <Button
                    key={p.name}
                    variant={selectedFields.has(p.name) ? "default" : "outline"}
                    size="sm"
                    className="h-6 text-[11px] px-2"
                    onClick={() => toggleField(p.name)}
                  >
                    {p.name}
                  </Button>
                ))}
              </div>
            </div>

            {/* $orderby */}
            <div className="space-y-1.5">
              <Label className="text-xs font-medium">
                $orderby <span className="text-muted-foreground font-normal">(.OrderBy())</span>
              </Label>
              <div className="flex gap-2">
                <Select value={orderField} onValueChange={setOrderField}>
                  <SelectTrigger className="h-8 text-xs flex-1">
                    <SelectValue placeholder="排序字段" />
                  </SelectTrigger>
                  <SelectContent>
                    {schema?.map((p) => (
                      <SelectItem key={p.name} value={p.name} className="text-xs">
                        {p.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Select value={orderDir} onValueChange={(v) => setOrderDir(v as "asc" | "desc")}>
                  <SelectTrigger className="h-8 text-xs w-[80px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="asc" className="text-xs">asc ↑</SelectItem>
                    <SelectItem value="desc" className="text-xs">desc ↓</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          {/* Pagination */}
          <div className="grid grid-cols-3 gap-2">
            <div className="space-y-1">
              <Label className="text-xs">$top (.Take())</Label>
              <Input
                className="h-8 text-xs"
                type="number"
                value={top}
                onChange={(e) => setTop(e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">$skip (.Skip())</Label>
              <Input
                className="h-8 text-xs"
                type="number"
                value={skip}
                onChange={(e) => setSkip(e.target.value)}
              />
            </div>
            <div className="flex items-end pb-1">
              <div className="flex items-center gap-2">
                <Checkbox
                  id="count"
                  checked={includeCount}
                  onCheckedChange={(v) => setIncludeCount(!!v)}
                />
                <Label htmlFor="count" className="text-xs cursor-pointer">$count</Label>
              </div>
            </div>
          </div>

          {/* Actions */}
          <div className="flex gap-2 pt-1">
            <Button onClick={buildAndExecute} disabled={loading || !entitySet} className="flex-1 h-9">
              {loading ? <Spinner className="mr-2 w-4 h-4" /> : <Play className="mr-2 w-4 h-4" />}
              执行查询
            </Button>
            <Button variant="outline" onClick={handleReset} className="h-9">
              <RotateCcw className="mr-2 w-4 h-4" />
              重置
            </Button>
          </div>

          {/* Generated OData URL preview */}
          {entitySet && (
            <div className="text-[11px] text-muted-foreground font-mono bg-muted p-2 rounded-md break-all">
              GET /odata/{entitySet}
              {filters.length > 0 && <span className="text-primary">?$filter={filters.join(" and ")}</span>}
              {selectedFields.size > 0 && <span className="text-green-600">&amp;$select={Array.from(selectedFields).join(",")}</span>}
              {orderField && <span className="text-amber-600">&amp;$orderby={orderField} {orderDir}</span>}
              {top && <span>&amp;$top={top}</span>}
              {includeCount && <span>&amp;$count=true</span>}
            </div>
          )}
        </CardContent>
      )}
    </Card>
  );
}
