// OData response types for the Dynamic OData API

export interface EntitySetInfo {
  name: string;
  idProperty: string;
  recordCount: number;
}

export interface PropertySchema {
  name: string;
  type: string;
  isNullable: boolean;
  isKey: boolean;
}

export interface EntitySchema {
  entitySet: string;
  properties: PropertySchema[];
}

export interface ODataQueryParams {
  entitySet: string;
  filter?: string;
  select?: string;
  orderby?: string;
  top?: number;
  skip?: number;
  count?: boolean;
  expand?: string;
}

export interface ODataListResponse<T = Record<string, unknown>> {
  value: T[];
  count?: number;
}

export interface DynamicEntity extends Record<string, unknown> {
  [key: string]: unknown;
}
