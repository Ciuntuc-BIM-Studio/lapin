/**
 * Ready-made materials (report #76), added to a project from Manage →
 * Materials → Library: metals (the dark grey aluminium of RAL 7016 window
 * frames, anodised, steel), glass (clear, blue, green, grey, frosted,
 * reflective), woods as planks, concrete, brick, plaster, paint, tiles, stone,
 * plastic and fabric. Each is the fields material.create takes: a colour, how
 * it renders, its drawn texture and how large a tile of it lies, and its IFC
 * category; its hatches come from its name as any material's do.
 */

import type { TextureKind } from '../../viewport/textures';

export interface Preset {
  name: string;
  group: PresetGroup;
  color: string;
  roughness: number;
  metalness?: number;
  transparency?: number;
  transmission?: number;
  ior?: number;
  texture?: TextureKind;
  textureSize?: [number, number];
  category?: 'Structure' | 'Finish' | 'Insulation' | 'Membrane' | 'Other';
}

export const PRESET_GROUPS = ['Metal', 'Glass', 'Wood', 'Concrete', 'Masonry', 'Plaster and paint', 'Tiles and stone', 'Plastic and fabric'] as const;
export type PresetGroup = (typeof PRESET_GROUPS)[number];

const GLASS = { transmission: 0.9, ior: 1.52 };

export const PRESETS: Preset[] = [
  // Metals: powder-coated aluminium is paint (dielectric), anodised and bare metals are metallic.
  { name: 'Aluminium RAL 7016 anthracite', group: 'Metal', color: '#383e42', roughness: 0.45, metalness: 0.1 },
  { name: 'Aluminium RAL 9016 white', group: 'Metal', color: '#f1f0ea', roughness: 0.45, metalness: 0.1 },
  { name: 'Aluminium RAL 9005 black', group: 'Metal', color: '#0e0e10', roughness: 0.45, metalness: 0.1 },
  { name: 'Aluminium anodised dark grey', group: 'Metal', color: '#4a4d50', roughness: 0.3, metalness: 1 },
  { name: 'Aluminium anodised natural', group: 'Metal', color: '#c8cacc', roughness: 0.3, metalness: 1 },
  { name: 'Steel brushed', group: 'Metal', color: '#b4b7ba', roughness: 0.3, metalness: 1, texture: 'metal', textureSize: [0.5, 0.5] },
  { name: 'Stainless steel polished', group: 'Metal', color: '#d0d3d6', roughness: 0.12, metalness: 1 },
  { name: 'Steel galvanised', group: 'Metal', color: '#a9adb1', roughness: 0.55, metalness: 1 },
  { name: 'Corten steel', group: 'Metal', color: '#8a4b2a', roughness: 0.85, metalness: 0.4 },
  { name: 'Copper', group: 'Metal', color: '#b87333', roughness: 0.35, metalness: 1 },
  { name: 'Zinc titanium', group: 'Metal', color: '#8c9093', roughness: 0.55, metalness: 1 },
  { name: 'Chrome', group: 'Metal', color: '#e5e7e9', roughness: 0.05, metalness: 1 },
  // Glass: what shows through (transparency), the light through its body (transmission), its index of refraction.
  { name: 'Glass clear', group: 'Glass', color: '#e8f1f2', roughness: 0.02, transparency: 0.8, ...GLASS },
  { name: 'Glass blue tinted', group: 'Glass', color: '#6f9fc8', roughness: 0.03, transparency: 0.6, ...GLASS },
  { name: 'Glass green tinted', group: 'Glass', color: '#8fbfa8', roughness: 0.03, transparency: 0.6, ...GLASS },
  { name: 'Glass grey tinted', group: 'Glass', color: '#5f6a70', roughness: 0.03, transparency: 0.5, ...GLASS },
  { name: 'Glass frosted', group: 'Glass', color: '#e6eef0', roughness: 0.45, transparency: 0.35, ...GLASS },
  { name: 'Glass reflective', group: 'Glass', color: '#3b5566', roughness: 0.04, metalness: 0.6, transparency: 0.3, ...GLASS },
  // Woods as planks: eight 150 mm boards to a 1.2 m tile.
  { name: 'Oak parquet', group: 'Wood', color: '#b98a5a', roughness: 0.45, texture: 'wood', textureSize: [1.2, 1.2], category: 'Finish' },
  { name: 'Laminate light oak', group: 'Wood', color: '#c9a57a', roughness: 0.4, texture: 'wood', textureSize: [1.2, 1.2], category: 'Finish' },
  { name: 'Walnut', group: 'Wood', color: '#6b4a33', roughness: 0.5, texture: 'wood', textureSize: [1.2, 1.2], category: 'Finish' },
  { name: 'Pine boards', group: 'Wood', color: '#d8b483', roughness: 0.7, texture: 'wood', textureSize: [1.6, 1.6] },
  { name: 'Larch cladding', group: 'Wood', color: '#b07a4a', roughness: 0.75, texture: 'wood', textureSize: [1.8, 1.8], category: 'Finish' },
  { name: 'Timber painted white', group: 'Wood', color: '#f3f1ea', roughness: 0.5 },
  // Concrete.
  { name: 'Concrete exposed', group: 'Concrete', color: '#a8a8a8', roughness: 0.9, texture: 'concrete', category: 'Structure' },
  { name: 'Concrete polished', group: 'Concrete', color: '#9b9b98', roughness: 0.35, texture: 'concrete', category: 'Finish' },
  { name: 'Concrete dark', group: 'Concrete', color: '#6e6e6c', roughness: 0.85, texture: 'concrete', category: 'Structure' },
  // Masonry.
  { name: 'Brick red', group: 'Masonry', color: '#b5654a', roughness: 0.9, texture: 'brick', category: 'Structure' },
  { name: 'Brick yellow', group: 'Masonry', color: '#d6b98a', roughness: 0.9, texture: 'brick', category: 'Structure' },
  { name: 'Brick dark clinker', group: 'Masonry', color: '#5a3a30', roughness: 0.75, texture: 'brick', category: 'Finish' },
  // Plaster and paint.
  { name: 'Plaster white', group: 'Plaster and paint', color: '#f2efe8', roughness: 0.9, texture: 'plaster', category: 'Finish' },
  { name: 'Render grey', group: 'Plaster and paint', color: '#c9c6c0', roughness: 0.9, texture: 'plaster', category: 'Finish' },
  { name: 'Paint white matt', group: 'Plaster and paint', color: '#f4f4f1', roughness: 0.8, category: 'Finish' },
  { name: 'Paint white satin', group: 'Plaster and paint', color: '#f4f4f1', roughness: 0.45, category: 'Finish' },
  { name: 'Paint anthracite', group: 'Plaster and paint', color: '#383e42', roughness: 0.6, category: 'Finish' },
  // Tiles and stone.
  { name: 'Ceramic tiles white', group: 'Tiles and stone', color: '#f0f0ee', roughness: 0.2, texture: 'tiles', textureSize: [0.6, 0.6], category: 'Finish' },
  { name: 'Porcelain tiles grey', group: 'Tiles and stone', color: '#8e8e8c', roughness: 0.3, texture: 'tiles', textureSize: [1.2, 1.2], category: 'Finish' },
  { name: 'Granite grey', group: 'Tiles and stone', color: '#8a8a88', roughness: 0.35, texture: 'stone', category: 'Finish' },
  { name: 'Marble white', group: 'Tiles and stone', color: '#ecebe6', roughness: 0.15, texture: 'stone', category: 'Finish' },
  { name: 'Limestone', group: 'Tiles and stone', color: '#d8cfbd', roughness: 0.8, texture: 'stone', category: 'Finish' },
  // Plastic and fabric.
  { name: 'PVC white', group: 'Plastic and fabric', color: '#f2f2f2', roughness: 0.4 },
  { name: 'PVC anthracite', group: 'Plastic and fabric', color: '#3b3f42', roughness: 0.4 },
  { name: 'Fabric grey', group: 'Plastic and fabric', color: '#7a7a7a', roughness: 0.97 },
  { name: 'Rubber black', group: 'Plastic and fabric', color: '#1d1d1d', roughness: 0.9 },
];

/** A preset as material.create's arguments (under its own name, or another when the project has that one). */
export function presetArgs(p: Preset, name = p.name): Record<string, unknown> {
  const a: Record<string, unknown> = { name, color: p.color, roughness: p.roughness };
  for (const k of ['metalness', 'transparency', 'transmission', 'ior', 'texture', 'textureSize', 'category'] as const) if (p[k] !== undefined) a[k] = p[k];
  return a;
}

/** A name the project does not have: the preset's, else with a number after it. */
export function freeName(name: string, taken: Iterable<string>): string {
  const lower = new Set([...taken].map((t) => t.toLowerCase()));
  if (!lower.has(name.toLowerCase())) return name;
  for (let i = 2; ; i++) if (!lower.has(`${name} ${i}`.toLowerCase())) return `${name} ${i}`;
}

/** The materials whose name has every word of the search, in any order (case and diacritics aside). */
export function matches(name: string, search: string): boolean {
  const f = (s: string) => s.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
  const n = f(name);
  return f(search).split(/\s+/).filter(Boolean).every((w) => n.includes(w));
}
