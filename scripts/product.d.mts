// Types of product.mjs, for the UI build (vite.config.ts).
export interface ResolvedProduct {
  id: string;
  name: string;
  modules: string[];
  required: string[];
  engine: string[];
  ui: string[];
}
export function resolveProduct(root: string, productId: string): ResolvedProduct;
export function productIds(root: string): string[];
