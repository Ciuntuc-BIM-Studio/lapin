/**
 * How the product this build was made for presents itself (products/<id>.json,
 * its "brand"): Lapin, or an edition of Lapin's modules under another name,
 * which says its own mark, accent, line and first hint. The interface is
 * written saying "Lapin"; the translator puts the brand's short name in its
 * place (<see cref="rebrand"/>), so the phrases and their translations stay one.
 */

import { product } from 'virtual:lapin-product';

export interface Brand {
  name: string; short: string;
  /** The letter of the brand's square mark, shown before its name; none: the name alone. */
  mark?: string;
  /** The accent colour, light and dark themes. */
  accent?: { light: string; dark: string };
  /** The line under the name on the start screen, and its first hint. */
  tagline?: string; hint?: string;
}
export interface ProductInfo { id: string; name: string; brand: Brand; modules: string[]; disciplines?: string[] | null }

export const productInfo: ProductInfo = product;
export const brand: Brand = product.brand;

/** Whether the product has a module (an engine or interface half named so). */
export const hasModule = (id: string): boolean => product.modules.includes(id);

/** "Lapin" the name (not Lapin's own class names, LapinTrack, Lapin_Stationing) said as the brand says it. */
export function rebrand(text: string): string {
  return rebrandAs(text, brand.short);
}

/** <see cref="rebrand"/> for a given short name. */
export function rebrandAs(text: string, short: string): string {
  return short === 'Lapin' ? text : text.replace(/\bLapin\b(?![_A-Za-z0-9])/g, short);
}

/** The line under the product's name on the start screen. */
export const tagline = (): string => brand.tagline ?? 'BIM authoring for architects and engineers';
