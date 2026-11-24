/**
 * The diagram display (View → Display quality → Diagram): a soft architectural-diagram look for volume studies. Matte volumes
 * in one light colour (the white model, like a card model) or a muted pastel by category, thin light edges, the sun's soft
 * shadows and ambient occlusion on a light background. It is a look laid over the elements' own, the way X-ray is (xray.ts),
 * so the meshes and the instanced ones, picking and the selection's highlight go through the paths they already have, and
 * on screen only: drawings, sheets, exports and the model keep their colours.
 *
 * A colour already set for an element (a phase's, the 4D simulation's, Visibility/Graphics) wins over the palette, so a
 * site can be accented green, or one building highlighted. See-through things (glass) stay see-through: only the colour
 * changes. The pastel palette is the coded default; the user edits it in Preferences → Display (category → colour, reset).
 */

import * as THREE from 'three';
import { create } from 'zustand';
import type { Look } from '../platform/looks';

export type DiagramVariant = 'white' | 'pastel';

export const CATEGORIES = ['walls', 'floors', 'roofs', 'structure', 'openings', 'site', 'greenery', 'other'] as const;
export type DiagramCategory = (typeof CATEGORIES)[number];

/** What each category is called in Preferences. */
export const CATEGORY_LABELS: Record<DiagramCategory, string> = {
  walls: 'Walls', floors: 'Slabs and floors', roofs: 'Roofs', structure: 'Columns and beams', openings: 'Doors and windows',
  site: 'Terrain and site', greenery: 'Vegetation and furniture', other: 'Everything else',
};

/** The default pastel palette: muted, readable on a light background, one hue family a category. */
export const DEFAULT_PALETTE: Record<DiagramCategory, string> = {
  walls: '#e8d9c4', floors: '#cfd8dc', roofs: '#dcaa9c', structure: '#c9c3dd', openings: '#a9cfe0',
  site: '#dcd6c0', greenery: '#b5d6ac', other: '#e3e3e1',
};

/** The white model's one colour: a light warm grey, a card model's. */
export const WHITE_MODEL = '#ecebe8';
/** The view's background and edges in this display. */
export const DIAGRAM_BACKGROUND = 0xf4f4f2;
export const DIAGRAM_EDGES = 0xb4b8bd;

/** What the diagram colours, by class; what has no volume to colour (rooms, openings' voids, drafts, grids) is left alone. */
const SKIPPED = new Set(['IfcSpace', 'IfcOpeningElement', 'IfcAnnotation', 'IfcGrid', 'IfcVirtualElement']);
const OF_CLASS: Record<string, DiagramCategory> = {
  IfcWall: 'walls', IfcWallStandardCase: 'walls', IfcCurtainWall: 'walls', IfcPlate: 'walls',
  IfcSlab: 'floors', IfcCovering: 'floors', IfcStair: 'floors', IfcStairFlight: 'floors', IfcRamp: 'floors', IfcRampFlight: 'floors',
  IfcRoof: 'roofs',
  IfcColumn: 'structure', IfcBeam: 'structure', IfcMember: 'structure', IfcPile: 'structure', IfcFooting: 'structure',
  IfcDoor: 'openings', IfcWindow: 'openings',
  IfcSite: 'site', IfcCivilElement: 'site', IfcGeographicElement: 'site',
  IfcFurniture: 'greenery', IfcFurnishingElement: 'greenery',
};
const GREEN = /^(VEGETATION|TREE|SHRUB|PLANT|HEDGE|LAWN|GRASS)/i;

/** The category of an IFC class (and its predefined type: a geographic element is terrain, or vegetation); null: not coloured. */
export function categoryOf(type: string, predefinedType?: unknown): DiagramCategory | null {
  if (SKIPPED.has(type)) return null;
  if (type === 'IfcGeographicElement' && typeof predefinedType === 'string' && GREEN.test(predefinedType)) return 'greenery';
  return OF_CLASS[type] ?? 'other';
}

export interface DiagramSettings { variant: DiagramVariant; palette: Partial<Record<DiagramCategory, string>> }

const HEX = /^#[0-9a-f]{6}$/i;

/** The colour of a category in these settings: the white model's one, or the user's pastel, else the default. */
export function diagramColour(cat: DiagramCategory, s: DiagramSettings): string {
  if (s.variant === 'white') return WHITE_MODEL;
  return s.palette[cat] ?? DEFAULT_PALETTE[cat];
}

export interface DiagramItem { id: string; type: string; predefinedType?: unknown }

const painted = new WeakMap<Look, Map<string, Look>>();
const plain = new Map<string, Look>();

/** A look with a colour under it: the same object for the same input, so a view restyles only what changed. */
function coloured(l: Look | undefined, hex: string): Look {
  if (!l) { let p = plain.get(hex); if (!p) { p = { color: hex }; plain.set(hex, p); } return p; }
  let by = painted.get(l);
  if (!by) { by = new Map(); painted.set(l, by); }
  let p = by.get(hex);
  if (!p) { p = { ...l, color: hex }; by.set(hex, p); }
  return p;
}

/** The looks with the diagram's colours under them: an element's own colour (a look's) wins; a hidden one stays hidden. */
export function diagramLooks(looks: Record<string, Look | undefined>, items: Iterable<DiagramItem>, s: DiagramSettings): Record<string, Look | undefined> {
  const out: Record<string, Look | undefined> = { ...looks };
  for (const it of items) {
    const cat = categoryOf(it.type, it.predefinedType);
    if (!cat) continue;
    const own = looks[it.id];
    if (own?.hidden || own?.color) continue;
    out[it.id] = coloured(own, diagramColour(cat, s));
  }
  return out;
}

const KEY = 'lapin.diagram';

/** What was kept, anything unreadable at its default. */
export function readDiagram(stored: unknown): DiagramSettings {
  const o = (stored && typeof stored === 'object' ? stored : {}) as { variant?: unknown; palette?: unknown };
  const raw = (o.palette && typeof o.palette === 'object' ? o.palette : {}) as Record<string, unknown>;
  const palette: Partial<Record<DiagramCategory, string>> = {};
  for (const c of CATEGORIES) { const v = raw[c]; if (typeof v === 'string' && HEX.test(v) && v.toLowerCase() !== DEFAULT_PALETTE[c]) palette[c] = v.toLowerCase(); }
  return { variant: o.variant === 'pastel' ? 'pastel' : 'white', palette };
}

function read(): DiagramSettings {
  try { return readDiagram(JSON.parse(localStorage.getItem(KEY) ?? 'null')); } catch { return readDiagram(null); }
}

export const useDiagram = create<DiagramSettings>(() => read());

/** Changes the diagram's settings (the palette merged by category; a colour null goes back to the default) and keeps them on this computer. */
export function setDiagram(patch: { variant?: DiagramVariant; palette?: Partial<Record<DiagramCategory, string | null>> }): void {
  const now = useDiagram.getState();
  const palette: Record<string, unknown> = { ...now.palette };
  for (const [k, v] of Object.entries(patch.palette ?? {})) { if (v == null) delete palette[k]; else palette[k] = v; }
  useDiagram.setState(readDiagram({ variant: patch.variant ?? now.variant, palette }));
  try { localStorage.setItem(KEY, JSON.stringify(useDiagram.getState())); } catch { /* kept for this session */ }
}

/** The palette back to the default (the variant stays). */
export function resetDiagramPalette(): void {
  useDiagram.setState({ palette: {} });
  try { localStorage.setItem(KEY, JSON.stringify(useDiagram.getState())); } catch { /* kept for this session */ }
}

/** A texture's silhouette: white where its picture is opaque, the alpha kept, so a cut-out figure keeps its outline and takes the colour of its material. */
const silhouettes = new WeakMap<THREE.Texture, THREE.Texture>();
function silhouetteOf(t: THREE.Texture): THREE.Texture | null {
  let s = silhouettes.get(t);
  if (s) return s;
  const img = t.image as (CanvasImageSource & { width?: number; height?: number }) | undefined;
  if (!img || typeof document === 'undefined' || !img.width || !img.height) return null;
  const cv = document.createElement('canvas');
  cv.width = img.width; cv.height = img.height;
  const g = cv.getContext('2d');
  if (!g) return null;
  g.drawImage(img, 0, 0);
  g.globalCompositeOperation = 'source-in';
  g.fillStyle = '#fff';
  g.fillRect(0, 0, cv.width, cv.height);
  s = new THREE.CanvasTexture(cv);
  s.wrapS = t.wrapS; s.wrapT = t.wrapT; s.flipY = t.flipY; s.colorSpace = t.colorSpace;
  silhouettes.set(t, s);
  return s;
}

/**
 * No textures in the diagram display: every standard material loses its colour picture (a terrain's orthophoto, a material's
 * texture, an entourage figure's), so it takes its palette colour flat. A cut-out (alpha test, or see-through) keeps its
 * outline through the picture's silhouette. Off, every material gets its own picture back, exactly. Safe to call again as
 * the scene changes: it only touches materials not yet in the mode asked.
 */
export function flatTextures(root: THREE.Object3D, on: boolean): void {
  root.traverse((o) => {
    const mat = (o as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
    for (const m of Array.isArray(mat) ? mat : mat ? [mat] : []) {
      const std = m as THREE.MeshStandardMaterial;
      if (!std.isMeshStandardMaterial) continue;
      const own = std.userData.diagramMap as { map: THREE.Texture } | undefined;
      if (on && !own && std.map) {
        const cut = std.alphaTest > 0 || std.transparent;
        const flat = cut ? silhouetteOf(std.map) : null;
        std.userData.diagramMap = { map: std.map };
        std.map = flat;
        std.needsUpdate = true;
      } else if (!on && own) {
        std.map = own.map;
        delete std.userData.diagramMap;
        std.needsUpdate = true;
      }
    }
  });
}
