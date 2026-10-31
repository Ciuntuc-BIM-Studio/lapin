// Types of product.mjs, for the UI build (vite.config.ts).
export interface ResolvedProduct {
  /** How the product presents itself (products/<id>.json "brand"; Lapin's own when it says none). */
  brand: { name: string; short: string; identifier: string; mark?: string; accent?: { light: string; dark: string }; tagline?: string; hint?: string; icons?: string };
  /** The ribbon's tabs placed otherwise: tab id → order. */
  tabs?: Record<string, number>;
  /** What a new project starts with, when the product says. */
  storeys?: { name: string; elevation: number; height: number }[];
  /** The disciplines the browser groups views by, when the product says. */
  disciplines?: string[];
  id: string;
  name: string;
  modules: string[];
  required: string[];
  engine: string[];
  ui: string[];
}
export function resolveProduct(root: string, productId: string): ResolvedProduct;
export function productIds(root: string): string[];
