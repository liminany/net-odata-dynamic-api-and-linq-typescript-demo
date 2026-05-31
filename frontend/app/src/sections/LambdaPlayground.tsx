import { useState } from "react";
import { ODataService } from "@jin-qu/odata";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Spinner } from "@/components/ui/spinner";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Products, Customers, Orders } from "@/types/generated/entities";
import type { DynamicEntity } from "@/types/odata";
import { Code2, Play, Copy, Check } from "lucide-react";

const API_BASE = "http://localhost:5000/odata";
const service = new ODataService(API_BASE);

// 预设：展示代码 + 真实可执行的 jinqu-odata Lambda 查询
const PRESETS = [
  {
    label: "价格 > 5000 的产品",
    code: `service.createQuery(Products)
  .where(p => p.Price > 5000)
  .orderBy(p => p.Price)
  .take(5)
  .toArrayAsync()`,
    desc: "C#: Products.Where(p => p.Price > 5000).Take(5)",
    execute: async () => {
      const q = service.createQuery(Products) as any;
      return q.where((p: any) => p.Price > 5000)
              .orderBy((p: any) => p.Price)
              .take(5).toArrayAsync();
    },
  },
  {
    label: "包含 Pro 的产品",
    code: `service.createQuery(Products)
  .where(p => p.Name.includes("Pro"))
  .take(10)
  .toArrayAsync()`,
    desc: 'C#: Products.Where(p => p.Name.Contains("Pro"))',
    execute: async () => {
      const q = service.createQuery(Products) as any;
      return q.where((p: any) => p.Name.includes("Pro"))
              .take(10).toArrayAsync();
    },
  },
  {
    label: "VIP 客户",
    code: `service.createQuery(Customers)
  .where(c => c.Level == "VIP")
  .orderBy(c => c.Balance)
  .take(5)
  .toArrayAsync()`,
    desc: 'C#: Customers.Where(c => c.Level == "VIP")',
    execute: async () => {
      const q = service.createQuery(Customers) as any;
      return q.where((c: any) => c.Level == "VIP")
              .orderBy((c: any) => c.Balance).take(5).toArrayAsync();
    },
  },
  {
    label: "已完成订单",
    code: `service.createQuery(Orders)
  .where(o => o.Status == "Completed")
  .take(3)
  .toArrayAsync()`,
    desc: 'C#: Orders.Where(o => o.Status == "Completed")',
    execute: async () => {
      const q = service.createQuery(Orders) as any;
      return q.where((o: any) => o.Status == "Completed")
              .take(3).toArrayAsync();
    },
  },
];

export function LambdaPlayground() {
  const [activePreset, setActivePreset] = useState(0);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<DynamicEntity[] | null>(null);
  const [copied, setCopied] = useState(false);

  const runPreset = async (index: number) => {
    setActivePreset(index);
    setLoading(true);
    try {
      const items = await PRESETS[index].execute();
      setResult(items as DynamicEntity[]);
    } catch (e) {
      console.error(e);
      setResult(null);
    } finally {
      setLoading(false);
    }
  };

  const copyCode = (code: string) => {
    navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Card>
      <CardHeader className="py-3 px-4">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-sm flex items-center gap-2">
              <Code2 className="w-4 h-4" />
              Lambda 查询演示（jinqu-odata）
            </CardTitle>
            <CardDescription className="text-xs mt-1">
              真正的 C# LINQ Lambda 语法：.where(p =&gt; p.Price &gt; 1000)
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="px-4 pb-4 space-y-3">
        <div className="flex flex-wrap gap-2">
          {PRESETS.map((preset, i) => (
            <Button key={i} variant={activePreset === i ? "default" : "outline"}
              size="sm" className="h-7 text-xs"
              onClick={() => runPreset(i)} disabled={loading}>
              {preset.label}
            </Button>
          ))}
        </div>

        {PRESETS[activePreset] && (
          <div className="relative">
            <div className="bg-muted rounded-md p-3 font-mono text-[11px] leading-relaxed overflow-x-auto">
              <pre className="whitespace-pre">{PRESETS[activePreset].code}</pre>
            </div>
            <div className="flex items-center justify-between mt-2">
              <Badge variant="secondary" className="text-[10px]">
                {PRESETS[activePreset].desc}
              </Badge>
              <div className="flex gap-1">
                <Button variant="ghost" size="sm" className="h-6 text-xs gap-1"
                  onClick={() => copyCode(PRESETS[activePreset].code)}>
                  {copied ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
                  {copied ? "已复制" : "复制"}
                </Button>
                <Button size="sm" className="h-6 text-xs gap-1"
                  onClick={() => runPreset(activePreset)} disabled={loading}>
                  {loading ? <Spinner className="w-3 h-3" /> : <Play className="w-3 h-3" />}
                  运行
                </Button>
              </div>
            </div>
          </div>
        )}

        {result && result.length > 0 && (
          <div>
            <Badge variant="secondary" className="text-xs mb-2">{result.length} 条结果</Badge>
            <ScrollArea className="h-[200px] border rounded-md">
              <pre className="p-3 text-[11px] font-mono leading-relaxed">
                {JSON.stringify(result, null, 2)}
              </pre>
            </ScrollArea>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
