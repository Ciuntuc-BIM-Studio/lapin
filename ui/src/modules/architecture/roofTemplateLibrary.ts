/**
 * Reusable custom roof templates kept on this computer (localStorage), shared across all projects
 * alongside the engine's built-in catalogue styles and the project's own styles (`own:...`).
 * Templates capture the roof's assembly, shape, covering, drainage and framing fields — not its
 * location (outline, eaveOffset, or per-edge overrides).
 */

const LIBRARY_KEY = 'lapin.roofTemplates';
export const ROOF_TEMPLATE_PREFIX = 'tpl:';
export const MAX_ROOF_TEMPLATES_CHARS = 500_000;

/**
 * Roof fields captured by a user template (all valid writable `IfcRoof` fields in `RoofDraft`,
 * excluding location-specific `eaveOffset` and `cutPriority`).
 */
export const ROOF_TEMPLATE_FIELDS = [
  'shape',
  'pitch',
  'overhang',
  'gableOverhang',
  'ridgeDirection',
  'shedFlip',
  'upperPitch',
  'mansardInset',
  'covering',
  'coveringMaterial',
  'coveringThickness',
  'deckMaterial',
  'deckThickness',
  'gutterProfile',
  'gutterMaterial',
  'gutterColor',
  'gutterWidth',
  'gutterOffset',
  'gutterDrop',
  'gutterFall',
  'gutterEnds',
  'downpipes',
  'snowGuards',
  'structure',
  'rafterSection',
  'rafterSpacing',
  'purlins',
  'webSection',
  'timberGrade',
  'collarHeight',
  'rafterTail',
  'rafterSeat',
] as const;

const FIELD_SET = new Set<string>(ROOF_TEMPLATE_FIELDS);

export interface UserRoofTemplate {
  id: string;
  label: string;
  shape: string;
  pitch: number;
  structure?: string;
  attrs: Record<string, unknown>;
  added?: string;
}

const listeners = new Set<() => void>();

/** Subscribe to changes in the user roof template library. */
export function subscribeRoofTemplates(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

function notifyListeners(): void {
  for (const listener of listeners) listener();
}

export function isUserRoofTemplateId(id: string | undefined | null): id is string {
  return typeof id === 'string' && id.startsWith(ROOF_TEMPLATE_PREFIX);
}

function isPlainObject(v: unknown): v is Record<string, unknown> {
  return !!v && typeof v === 'object' && !Array.isArray(v);
}

function isValidTemplate(v: unknown): v is UserRoofTemplate {
  if (!isPlainObject(v)) return false;
  return typeof v.id === 'string' && v.id.length > 0 && typeof v.label === 'string' && v.label.trim().length > 0 && isPlainObject(v.attrs);
}

/** Read all user roof templates stored on this computer. */
export function libraryRoofTemplates(): UserRoofTemplate[] {
  try {
    const raw = JSON.parse(localStorage.getItem(LIBRARY_KEY) ?? '[]') as unknown;
    return Array.isArray(raw) ? raw.filter(isValidTemplate) : [];
  } catch {
    return [];
  }
}

export function libraryRoofTemplate(id: string): UserRoofTemplate | null {
  return libraryRoofTemplates().find((t) => t.id === id) ?? null;
}

function store(all: UserRoofTemplate[]): boolean {
  const text = JSON.stringify(all);
  if (text.length > MAX_ROOF_TEMPLATES_CHARS) return false;
  try {
    localStorage.setItem(LIBRARY_KEY, text);
    notifyListeners();
    return true;
  } catch {
    return false;
  }
}

function slugId(label: string, taken: Iterable<string>): string {
  const used = new Set(taken);
  const stem = label
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
    .replace(/-+$/, '') || 'roof';
  const base = `${ROOF_TEMPLATE_PREFIX}${stem}`;
  if (!used.has(base)) return base;
  for (let i = 2; ; i++) {
    const candidate = `${base}-${i}`;
    if (!used.has(candidate)) return candidate;
  }
}

/** Perpendicular span of a 4-corner barrel roof contour (shorter pair of opposite edges). */
function barrelSpan(contour?: readonly [number, number][]): number | null {
  if (!contour || contour.length !== 4) return null;
  const edgeLen = (i: number) => {
    const a = contour[i];
    const b = contour[(i + 1) % 4];
    return Math.hypot(b[0] - a[0], b[1] - a[1]);
  };
  const d0 = edgeLen(0);
  const d1 = edgeLen(1);
  const span = Math.min(d0, d1);
  return span > 1e-6 ? span : null;
}

/** Build a clean template snapshot from a roof's attributes. */
export function snapshotRoofTemplate(
  sourceAttrs: Record<string, unknown>,
  label: string,
  id: string,
  contour?: readonly [number, number][],
): UserRoofTemplate {
  const cleanLabel = label.trim();
  const attrs: Record<string, unknown> = {};
  for (const key of ROOF_TEMPLATE_FIELDS) {
    const val = sourceAttrs[key];
    if (val !== undefined && val !== null) attrs[key] = val;
  }
  const shape = typeof attrs.shape === 'string' && attrs.shape ? attrs.shape : 'hip';
  if (shape === 'barrel') {
    const rise = typeof sourceAttrs.rise === 'number' ? sourceAttrs.rise : undefined;
    const span = barrelSpan(contour);
    if (rise !== undefined && span !== null) {
      attrs.riseShare = Math.round((rise / span) * 10000) / 10000;
    } else if (typeof sourceAttrs.riseShare === 'number') {
      attrs.riseShare = sourceAttrs.riseShare;
    } else if (rise !== undefined) {
      attrs.rise = rise;
    }
  }
  const rawPitch = Number(attrs.pitch);
  const pitch = Number.isFinite(rawPitch) && rawPitch > 0 ? rawPitch : 30;
  const structure = typeof attrs.structure === 'string' && attrs.structure ? attrs.structure : undefined;
  return {
    id,
    label: cleanLabel,
    shape,
    pitch,
    ...(structure ? { structure } : {}),
    attrs,
    added: new Date().toISOString().slice(0, 10),
  };
}

export type SaveRoofTemplateResult =
  | { status: 'kept'; template: UserRoofTemplate }
  | { status: 'exists'; existing: UserRoofTemplate }
  | { status: 'full' | 'invalid' };

/**
 * Save a roof configuration as a reusable user template on this computer.
 * Matches existing templates by case-insensitive label (or explicit `id`), returning `'exists'`
 * unless `overwrite` is true.
 */
export function saveRoofTemplate(
  label: string,
  sourceAttrs: Record<string, unknown>,
  options: { id?: string; overwrite?: boolean; contour?: readonly [number, number][] } = {},
): SaveRoofTemplateResult {
  const trimmed = label.trim();
  if (!trimmed) return { status: 'invalid' };

  const all = libraryRoofTemplates();
  const existing = options.id
    ? all.find((t) => t.id === options.id)
    : all.find((t) => t.label.toLowerCase() === trimmed.toLowerCase());

  if (existing && !options.overwrite) {
    return { status: 'exists', existing };
  }

  const id = existing?.id ?? slugId(trimmed, all.map((t) => t.id));
  const nextTemplate = snapshotRoofTemplate(sourceAttrs, trimmed, id, options.contour);
  const next = existing
    ? all.map((t) => (t.id === id ? nextTemplate : t))
    : [...all, nextTemplate];

  return store(next) ? { status: 'kept', template: nextTemplate } : { status: 'full' };
}

/** Rename a user roof template in place. */
export function renameRoofTemplate(id: string, label: string): boolean {
  const trimmed = label.trim();
  if (!trimmed) return false;
  const all = libraryRoofTemplates();
  const idx = all.findIndex((t) => t.id === id);
  if (idx < 0) return false;
  all[idx] = { ...all[idx], label: trimmed };
  return store(all);
}

/** Remove a user roof template from this computer. */
export function forgetRoofTemplate(id: string): void {
  const all = libraryRoofTemplates();
  const next = all.filter((t) => t.id !== id);
  if (next.length !== all.length) store(next);
}

/**
 * Build the `architecture.updateRoof` / `architecture.roofPreview` argument payload to apply a user
 * template to a roof in a single command (one undo step), clearing stale fields and per-edge rules.
 */
export function applyRoofTemplateArgs(
  roofId: string,
  template: UserRoofTemplate,
  contour?: readonly [number, number][],
): { id: string; attrs: Record<string, unknown>; edges: [] } {
  const attrs: Record<string, unknown> = {};
  for (const key of ROOF_TEMPLATE_FIELDS) {
    attrs[key] = key in template.attrs && template.attrs[key] !== undefined ? template.attrs[key] : null;
  }
  if (template.shape === 'barrel') {
    const share = typeof template.attrs.riseShare === 'number' ? template.attrs.riseShare : undefined;
    const span = barrelSpan(contour);
    if (share !== undefined && span !== null) {
      attrs.rise = Math.round(span * share * 1000) / 1000;
    } else if (typeof template.attrs.rise === 'number') {
      attrs.rise = template.attrs.rise;
    } else {
      attrs.rise = null;
    }
  } else {
    attrs.rise = null;
  }
  return { id: roofId, attrs, edges: [] };
}

/** Serialize one or more user roof templates as a portable JSON file. */
export function exportRoofTemplatesJson(templates: UserRoofTemplate[]): string {
  return JSON.stringify({ format: 'lapin-roof-templates', version: 1, templates }, null, 2) + '\n';
}

function sanitizeImportedTemplate(raw: unknown): UserRoofTemplate | null {
  if (!isPlainObject(raw)) return null;
  const label = typeof raw.label === 'string' ? raw.label.trim() : '';
  if (!label || !isPlainObject(raw.attrs)) return null;

  const filteredAttrs: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(raw.attrs)) {
    if ((FIELD_SET.has(k) || k === 'riseShare' || k === 'rise') && v !== undefined && v !== null) {
      filteredAttrs[k] = v;
    }
  }
  const id = typeof raw.id === 'string' && raw.id.startsWith(ROOF_TEMPLATE_PREFIX) ? raw.id : '';
  return snapshotRoofTemplate(filteredAttrs, label, id);
}

/** Parse and validate an exported roof template JSON payload (single template, wrapper object, or array). */
export function parseRoofTemplatesJson(raw: unknown): UserRoofTemplate[] {
  if (Array.isArray(raw)) {
    return raw.map(sanitizeImportedTemplate).filter((x): x is UserRoofTemplate => x !== null);
  }
  if (isPlainObject(raw) && Array.isArray(raw.templates)) {
    return raw.templates.map(sanitizeImportedTemplate).filter((x): x is UserRoofTemplate => x !== null);
  }
  const single = sanitizeImportedTemplate(raw);
  return single ? [single] : [];
}

/** Import parsed user roof templates into the computer's library. */
export function importRoofTemplates(
  incoming: UserRoofTemplate[],
  overwrite = false,
): { added: number; updated: number; skipped: number; full: boolean } {
  const all = libraryRoofTemplates();
  let added = 0;
  let updated = 0;
  let skipped = 0;

  for (const item of incoming) {
    const byId = item.id ? all.findIndex((t) => t.id === item.id) : -1;
    const byLabel = all.findIndex((t) => t.label.toLowerCase() === item.label.toLowerCase());
    const idx = byId >= 0 ? byId : byLabel;

    if (idx >= 0) {
      if (!overwrite) {
        skipped++;
        continue;
      }
      all[idx] = { ...item, id: all[idx].id, added: new Date().toISOString().slice(0, 10) };
      updated++;
    } else {
      const id = item.id && !all.some((t) => t.id === item.id) ? item.id : slugId(item.label, all.map((t) => t.id));
      all.push({ ...item, id, added: new Date().toISOString().slice(0, 10) });
      added++;
    }
  }

  if (added === 0 && updated === 0) {
    return { added, updated, skipped, full: false };
  }
  if (!store(all)) {
    return { added: 0, updated: 0, skipped: incoming.length, full: true };
  }
  return { added, updated, skipped, full: false };
}
