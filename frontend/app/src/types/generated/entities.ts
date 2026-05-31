// 自动生成于 2026-05-31T03:41:56.686Z
// 来源: OData $metadata
// 生成命令: npm run generate-entities
// 请勿手动编辑此文件

import "reflect-metadata";
import { oDataResource } from "@jin-qu/odata";

/** OData entity set: Products */
@oDataResource("Products")
export class Products {
  Id!: number; // [Key]
  Category!: string | null;
  CreatedAt!: string;
  InStock!: boolean;
  Name!: string | null;
  Price!: number;
  Rating!: number;
  Tags!: string | null;
}

/** OData entity set: Customers */
@oDataResource("Customers")
export class Customers {
  Id!: number; // [Key]
  Balance!: number;
  City!: string | null;
  Email!: string | null;
  Level!: string | null;
  Name!: string | null;
  Orders!: number;
  RegisteredAt!: string;
}

/** OData entity set: Orders */
@oDataResource("Orders")
export class Orders {
  Id!: number; // [Key]
  CustomerId!: number;
  OrderDate!: string;
  PaymentMethod!: string | null;
  ProductIds!: string | null;
  Status!: string | null;
  TotalAmount!: number;
}
