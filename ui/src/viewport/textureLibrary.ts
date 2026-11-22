/**
 * The texture sets Lapin ships (docs/plan-render.md, S1): physically based maps (colour, normal, roughness, ambient
 * occlusion, height) from ambientCG, CC0, a few hundred kilobytes each, served beside the interface
 * (ui/public/textures/…) and listed with their hashes in texture-library.json (pnpm check:licenses). Chosen on a
 * material, a set's maps are fetched and copied into the project as its images in one step (material.textures), so
 * the project keeps them on any computer, in the browser, and in every export.
 */

import register from './texture-library.json';

/** The maps a set may have, as the register names them, and as material.textures takes them. */
export type MapName = 'color' | 'normal' | 'roughness' | 'ao' | 'height';

/** A shipped texture set. */
export interface TextureSet {
  id: string;
  source: string;
  name: string;
  /** What it is, for the drawn texture it stands for (brick, concrete, wood…). */
  kind: string;
  /** The tile it lies on: width and height on the model, metres. */
  size: [number, number];
  /** The factors its maps are read with: its roughness map multiplied by this, its metalness. */
  roughness: number;
  metalness: number;
  files: Partial<Record<MapName, { file: string; bytes: number; sha256: string }>>;
}

export const TEXTURE_SETS: readonly TextureSet[] = (register.sets as unknown as TextureSet[]);

/** A set's key on a material (its textureSet): source/id. */
export const setKey = (s: TextureSet): string => `${s.source}/${s.id}`;

/** The set a material names (its textureSet), if Lapin ships it. */
export const setOf = (key: unknown): TextureSet | undefined => (typeof key === 'string' ? TEXTURE_SETS.find((s) => setKey(s) === key) : undefined);

/** Where a set's map is served: under the interface's base (the desktop's /, the web build's /web/). */
export function mapUrl(s: TextureSet, map: MapName, base: string = import.meta.env?.BASE_URL ?? '/'): string | null {
  const f = s.files[map];
  if (!f) return null;
  return `${base.endsWith('/') ? base : base + '/'}${f.file.replace(/^ui\/public\//, '')}`;
}

/** The engine's name for a map: the height image is the material's bump map. */
const ENGINE_MAP: Record<MapName, string> = { color: 'color', normal: 'normal', roughness: 'roughness', ao: 'ao', height: 'bump' };

/** How many bytes a set's maps take in a project (each chosen set adds this once). */
export const setBytes = (s: TextureSet): number => Object.values(s.files).reduce((n, f) => n + (f?.bytes ?? 0), 0);

/**
 * The arguments of material.textures for a set: its maps (base64, each as the engine names it), its key, its tile and
 * its factors; `fetchBytes` reads a URL (the app's fetch, a test's stub).
 */
export async function setArgs(s: TextureSet, ids: string[], fetchBytes: (url: string) => Promise<Uint8Array>, base?: string): Promise<Record<string, unknown>> {
  const maps: Record<string, { name: string; mime: string; data: string }> = {};
  for (const map of Object.keys(s.files) as MapName[]) {
    const url = mapUrl(s, map, base);
    if (!url) continue;
    const bytes = await fetchBytes(url);
    maps[ENGINE_MAP[map]] = { name: `${s.id} ${map}.jpg`, mime: 'image/jpeg', data: toBase64(bytes) };
  }
  return { ...(ids.length === 1 ? { id: ids[0] } : { ids }), set: setKey(s), size: s.size, roughness: s.roughness, metalness: s.metalness, maps };
}

export function toBase64(bytes: Uint8Array): string {
  let bin = '';
  for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(bin);
}

/** The browser's way to read a shipped map. */
export async function fetchBytes(url: string): Promise<Uint8Array> {
  const r = await fetch(url);
  if (!r.ok) throw new Error(`${url}: ${r.status}`);
  return new Uint8Array(await r.arrayBuffer());
}
