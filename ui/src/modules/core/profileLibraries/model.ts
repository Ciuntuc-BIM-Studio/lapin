/**
 * Profile libraries (docs/plan-profile-libraries.md): what the engine's `SectionProfile` and `.lapinprofiles` look like
 * here, and the pure parts of the manager and the pickers — searching across libraries, families in their natural order,
 * a project's copy that its library has newer, the column mapping remembered for a table's layout, the values shown with
 * their units.
 */

export interface ProfileSummary { A?: number; mass?: number; checks?: number }
export interface ProfileJson {
  name: string; kind: string; family?: string | null; standard?: string | null; material?: string; category?: string | null; density?: number;
  h?: number; b?: number; tw?: number; tf?: number; r1?: number; r2?: number; lip?: number; slope?: number; b2?: number; tf2?: number;
  rings?: [number, number][][]; props?: Record<string, number>; source?: string | null;
  /** The engine's area and mass worked out, for lists (never read back). */
  summary?: ProfileSummary;
}
export interface LibraryJson { format?: string; version?: number; name: string; description?: string | null; material?: string; profiles: ProfileJson[] }

export type LibraryOrigin = 'builtin' | 'computer' | 'folder' | 'project';
/** A library as the manager lists it: where it lives, and (a folder's) its file and whether Lapin may write there. */
export interface LibraryEntry { key: string; origin: LibraryOrigin; name: string; library: LibraryJson; folder?: string; file?: string; writable?: boolean; added?: string }

/** A project's copy (`LapinSectionProfile`), as `profiles.project` gives it. */
export interface ProjectCopy { id: string; name: string; library?: string | null; source?: string | null; added?: string | null; updated?: string | null; profile: ProfileJson; types: string[] }
export interface LegacyType { typeId: string; typeName: string; class: string; section: Record<string, unknown>; builtin?: string | null }
export interface ProjectProfilesDto { copies: ProjectCopy[]; legacy: LegacyType[] }

export const KINDS = ['I', 'asymI', 'U', 'T', 'L', 'RHS', 'CHS', 'C', 'Z', 'SIGMA', 'rectangle', 'circle', 'ellipse', 'outline'] as const;
export const KIND_LABELS: Record<string, string> = {
  I: 'I and H', asymI: 'Asymmetric I', U: 'Channel (U)', T: 'Tee (T)', L: 'Angle (L)', RHS: 'Rectangular hollow (RHS, SHS)', CHS: 'Circular hollow (CHS)',
  C: 'Cold-formed C', Z: 'Cold-formed Z', SIGMA: 'Cold-formed Σ', rectangle: 'Rectangle', circle: 'Round', ellipse: 'Ellipse', outline: 'Outline',
};
export const MATERIALS = ['steel', 'stainless', 'aluminium', 'timber', 'concrete', 'pvc', 'other'] as const;
export const MATERIAL_LABELS: Record<string, string> = {
  steel: 'Steel', stainless: 'Stainless steel', aluminium: 'Aluminium', timber: 'Timber', concrete: 'Concrete', pvc: 'PVC', other: 'Other',
};

/** Each property's label and the unit it is shown in (its factor from SI). */
export const PROPS: { key: string; label: string; unit: string; factor: number }[] = [
  { key: 'A', label: 'Area A', unit: 'cm²', factor: 1e-4 },
  { key: 'mass', label: 'Mass', unit: 'kg/m', factor: 1 },
  { key: 'Iy', label: 'Iy', unit: 'cm⁴', factor: 1e-8 },
  { key: 'Iz', label: 'Iz', unit: 'cm⁴', factor: 1e-8 },
  { key: 'Wely', label: 'Wel,y', unit: 'cm³', factor: 1e-6 },
  { key: 'Welz', label: 'Wel,z', unit: 'cm³', factor: 1e-6 },
  { key: 'Wply', label: 'Wpl,y', unit: 'cm³', factor: 1e-6 },
  { key: 'Wplz', label: 'Wpl,z', unit: 'cm³', factor: 1e-6 },
  { key: 'iy', label: 'iy', unit: 'cm', factor: 1e-2 },
  { key: 'iz', label: 'iz', unit: 'cm', factor: 1e-2 },
  { key: 'It', label: 'It', unit: 'cm⁴', factor: 1e-8 },
  { key: 'Iw', label: 'Iw', unit: '×10³ cm⁶', factor: 1e-9 },
  { key: 'Iu', label: 'Iu', unit: 'cm⁴', factor: 1e-8 },
  { key: 'Iv', label: 'Iv', unit: 'cm⁴', factor: 1e-8 },
  { key: 'alpha', label: 'Principal axis α', unit: '°', factor: 1 },
  { key: 'cx', label: 'Centroid from the box centre, x', unit: 'mm', factor: 1e-3 },
  { key: 'cy', label: 'Centroid from the box centre, y', unit: 'mm', factor: 1e-3 },
  { key: 'paint', label: 'Paint area', unit: 'm²/m', factor: 1 },
];

/** A value in its shown unit, to about four significant figures. */
export function formatValue(v: number | undefined, factor: number): string {
  if (v == null || !Number.isFinite(v)) return '';
  const x = v / factor;
  const a = Math.abs(x);
  const digits = a >= 1000 ? 0 : a >= 100 ? 1 : a >= 10 ? 2 : a >= 1 ? 3 : 4;
  return x.toFixed(digits).replace(/\.?0+$/, (m) => (m.startsWith('.') ? '' : m));
}

/** Millimetres, a decimal only where needed (7.1, 10.7, 300). */
export const mm = (m: number | undefined): string => (m == null ? '' : `${Math.round(m * 1e5) / 100}`);

/** Lower case, no diacritics or punctuation, × as x: how names are searched. */
export const fold = (s: string): string => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/×/g, 'x').replace(/[\s.,_\-/]+/g, '');

/** Every word of the search in the profile's name, family, standard, kind or material. */
export function matches(p: ProfileJson, text: string): boolean {
  const words = text.trim().split(/\s+/).filter(Boolean).map(fold);
  if (!words.length) return true;
  const hay = fold(`${p.name} ${p.family ?? ''} ${p.standard ?? ''} ${p.kind} ${KIND_LABELS[p.kind] ?? ''} ${p.material ?? ''} ${p.category ?? ''}`);
  return words.every((w) => hay.includes(w));
}

export interface Found { entry: LibraryEntry; profile: ProfileJson }

/** The profiles of the libraries matching the search and the filters (a kind, a family, a material), at most `limit`. */
export function search(entries: LibraryEntry[], text: string, filters: { kind?: string; family?: string; material?: string; kinds?: readonly string[] } = {}, limit = 400): Found[] {
  const o: Found[] = [];
  for (const entry of entries) {
    for (const p of entry.library.profiles) {
      if (filters.kind && p.kind !== filters.kind) continue;
      if (filters.kinds && !filters.kinds.includes(p.kind)) continue;
      if (filters.family && (p.family ?? '') !== filters.family) continue;
      if (filters.material && (p.material ?? 'steel') !== filters.material) continue;
      if (!matches(p, text)) continue;
      o.push({ entry, profile: p });
      if (o.length >= limit) return o;
    }
  }
  return o;
}

/** Names compared as people read them: IPE 80 before IPE 100, L 50×50×5 before L 50×50×6. */
export const naturalCompare = (a: string, b: string): number => a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' });

/** A library's families in their natural order, each with how many profiles it has. */
export function families(profiles: ProfileJson[]): { family: string; count: number }[] {
  const m = new Map<string, number>();
  for (const p of profiles) m.set(p.family ?? '', (m.get(p.family ?? '') ?? 0) + 1);
  return [...m.entries()].map(([family, count]) => ({ family, count })).sort((a, b) => naturalCompare(a.family, b.family));
}

/** A profile's definition alone (no summary, no source): what tells two versions of it apart. */
export function definition(p: ProfileJson): string {
  const rest = { ...p } as Record<string, unknown>;
  for (const k of ['summary', 'source', 'library']) delete rest[k];
  return JSON.stringify(Object.keys(rest).sort().map((k) => [k, rest[k]]));
}

/** The profile of the same name in a library of the same name as the project's copy, when it differs from the copy: newer in the library. */
export function newerInLibrary(copy: ProjectCopy, entries: LibraryEntry[]): { entry: LibraryEntry; profile: ProfileJson } | null {
  if (!copy.library) return null;
  for (const entry of entries) {
    if (entry.origin === 'project' || entry.name !== copy.library) continue;
    const p = entry.library.profiles.find((x) => x.name.toLowerCase() === copy.name.toLowerCase());
    if (p && definition(p) !== definition(copy.profile)) return { entry, profile: p };
  }
  return null;
}

/** The mass a list shows: the file's, else the engine's worked out. */
export const massOf = (p: ProfileJson): number | undefined => p.props?.mass ?? p.summary?.mass;

// ─── A table's column mapping, remembered on this computer by its layout ─────────────

export interface ColumnChoice { index: number; unit: string; alternates?: number[] }
export interface TableChoice { map: Record<string, ColumnChoice | null>; kind?: string | null; material?: string }

const REMEMBERED = 'lapin.profileTableMappings';

export function rememberedChoice(layout: string): TableChoice | null {
  if (!layout) return null;
  try { return (JSON.parse(localStorage.getItem(REMEMBERED) ?? '{}') as Record<string, TableChoice>)[layout] ?? null; } catch { return null; }
}

export function rememberChoice(layout: string, choice: TableChoice): void {
  if (!layout) return;
  try {
    const all = JSON.parse(localStorage.getItem(REMEMBERED) ?? '{}') as Record<string, TableChoice>;
    all[layout] = choice;
    // The thirty most recent layouts are enough.
    const keys = Object.keys(all);
    for (const k of keys.slice(0, Math.max(0, keys.length - 30))) delete all[k];
    localStorage.setItem(REMEMBERED, JSON.stringify(all));
  } catch { /* the choice is made again next time */ }
}

/** The column a field is read from changed in the dialog: each column one field, a field taken by another column moved. */
export function setColumn(map: Record<string, ColumnChoice | null>, column: number, field: string | null, unit: string): Record<string, ColumnChoice | null> {
  const next: Record<string, ColumnChoice | null> = {};
  for (const [f, c] of Object.entries(map)) next[f] = c && c.index === column ? null : c;
  if (field) next[field] = { index: column, unit };
  return next;
}

export const fieldOf = (map: Record<string, ColumnChoice | null>, column: number): string | null =>
  Object.entries(map).find(([, c]) => c?.index === column)?.[0] ?? null;

// ─── A profile drawn ─────────────────────────────────────────────────────────

export type Ring = [number, number][];

/** The rings fitted in a box (px, y up), the scale (px a metre) and the box of the rings in metres. */
export function fit(rings: Ring[], w: number, h: number, pad: number): { k: number; x0: number; y0: number; minX: number; maxX: number; minY: number; maxY: number } {
  const pts = rings.flat();
  if (!pts.length) return { k: 1, x0: w / 2, y0: h / 2, minX: 0, maxX: 0, minY: 0, maxY: 0 };
  const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1]);
  const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
  const k = Math.min((w - 2 * pad) / Math.max(maxX - minX, 1e-6), (h - 2 * pad) / Math.max(maxY - minY, 1e-6));
  return { k, x0: w / 2 - ((minX + maxX) / 2) * k, y0: h / 2 + ((minY + maxY) / 2) * k, minX, maxX, minY, maxY };
}

/** An SVG path of the rings (even-odd: holes stay open). */
export function ringsPath(rings: Ring[], k: number, x0: number, y0: number): string {
  return rings.filter((r) => r.length >= 3).map((r) => `M${r.map(([x, y]) => `${(x0 + x * k).toFixed(2)},${(y0 - y * k).toFixed(2)}`).join('L')}Z`).join('');
}

/** The dimensions a profile's drawing writes beside it, by its kind (mm). */
export function dimensionsOf(p: ProfileJson): { key: string; label: string; value: number }[] {
  const o: { key: string; label: string; value: number }[] = [];
  const add = (key: string, label: string, v: number | undefined) => { if (v && v > 0) o.push({ key, label, value: v }); };
  if (p.kind === 'CHS' || p.kind === 'circle') add('b', 'D', p.b ?? p.h);
  else { add('h', 'h', p.h); add('b', 'b', p.b); }
  if (['I', 'asymI', 'U', 'T'].includes(p.kind)) { add('tw', 'tw', p.tw); add('tf', 'tf', p.tf); }
  else if (['L', 'RHS', 'CHS', 'C', 'Z', 'SIGMA'].includes(p.kind)) add('tw', 't', p.tw);
  add('r1', 'r1', p.r1); add('r2', 'r2', p.r2); add('lip', 'c', p.lip); add('b2', 'b2', p.b2); add('tf2', 'tf2', p.tf2);
  return o;
}
