/**
 * Reusable custom roof templates kept on this computer (localStorage), shared across all projects
 * alongside the engine's built-in catalogue styles and the project's own styles (`own:...`).
 * Templates capture the roof's assembly, shape, covering, drainage and framing fields, its own layers
 * and their extents, and the sections of all its timber — not its location (where its outline lies,
 * eaveOffset). Its edges (each one's rule and arc, decision 741) are kept beside, with the number of
 * corners they belong to: they go onto a roof with as many corners; on any other roof the template
 * sets the roof-wide fields only. A template saved before (no `version`) applies as it always did.
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
  'downpipeDiameter',
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
  // Since version 2 (decision 741): its own layers, their extents and the other members' sections.
  'layers',
  'layerExtents',
  'plateSection',
  'ridgeSection',
  'purlinSection',
  'collarSection',
  // Whether each of those is laid flat (forum round 26.11.2026, R1); a version 1 template leaves them as the roof has them.
  'plateFlat',
  'ridgeFlat',
  'purlinFlat',
  'collarFlat',
] as const;

/** The fields a template saved before version 2 did not keep: applying such a template leaves them as the roof has them. */
const SINCE_V2 = new Set<string>(['layers', 'layerExtents', 'plateSection', 'ridgeSection', 'purlinSection', 'collarSection', 'plateFlat', 'ridgeFlat', 'purlinFlat', 'collarFlat']);

/** The template format: 2 keeps layers, extents, sections and edges. */
export const ROOF_TEMPLATE_VERSION = 2;

/** A roof edge's rule as a template keeps it (as `edges[i]` on the roof). */
type TemplateEdge = { kind?: 'eave' | 'gable'; pitch?: number; overhang?: number; gutter?: boolean } | null;

/** The edges a template keeps: as many as the corners of the roof it was saved from, each one's rule and arc rise. */
export interface RoofTemplateOutline { corners: number; edges: TemplateEdge[]; arcs: number[] }

const FIELD_SET = new Set<string>(ROOF_TEMPLATE_FIELDS);

export interface UserRoofTemplate {
  id: string;
  label: string;
  shape: string;
  pitch: number;
  structure?: string;
  attrs: Record<string, unknown>;
  added?: string;
  /** 2 since layers, sections and edges were kept (decision 741); none before. */
  version?: number;
  /** The roof's edges, for a roof with as many corners. */
  outline?: RoofTemplateOutline;
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

function cleanEdge(e: unknown): TemplateEdge {
  if (!isPlainObject(e)) return null;
  const o: NonNullable<TemplateEdge> = {};
  if (e.kind === 'eave' || e.kind === 'gable') o.kind = e.kind;
  if (typeof e.pitch === 'number' && Number.isFinite(e.pitch)) o.pitch = e.pitch;
  if (typeof e.overhang === 'number' && Number.isFinite(e.overhang)) o.overhang = e.overhang;
  if (e.gutter === false) o.gutter = false;
  return Object.keys(o).length ? o : null;
}

/** A roof's edges as a template keeps them (its rules and arcs, one per corner); null when it has no corners. */
function outlineOf(corners: number, edges: unknown, arcs: unknown): RoofTemplateOutline | null {
  if (!Number.isInteger(corners) || corners < 3) return null;
  const es = Array.isArray(edges) ? edges : [];
  const rs = Array.isArray(arcs) ? arcs : [];
  return {
    corners,
    edges: Array.from({ length: corners }, (_, i) => cleanEdge(es[i])),
    arcs: Array.from({ length: corners }, (_, i) => (typeof rs[i] === 'number' && Number.isFinite(rs[i]) ? (rs[i] as number) : 0)),
  };
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
  const corners = contour?.length ?? (Array.isArray(sourceAttrs.contour) ? sourceAttrs.contour.length : 0);
  const outline = outlineOf(corners, sourceAttrs.edges, sourceAttrs.arcs);
  return {
    id,
    label: cleanLabel,
    shape,
    pitch,
    ...(structure ? { structure } : {}),
    attrs,
    added: new Date().toISOString().slice(0, 10),
    version: ROOF_TEMPLATE_VERSION,
    ...(outline ? { outline } : {}),
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
 * template to a roof in a single command (one undo step), clearing stale fields. Its edges (rules and
 * arcs) go onto a roof with as many corners as the one it was saved from; on any other roof the
 * per-edge rules are cleared, its arcs left as they are, and a layer's extent named by edge left out.
 */
export function applyRoofTemplateArgs(
  roofId: string,
  template: UserRoofTemplate,
  contour?: readonly [number, number][],
): { id: string; attrs: Record<string, unknown>; edges: TemplateEdge[]; arcs?: number[] } {
  const attrs: Record<string, unknown> = {};
  const v2 = (template.version ?? 1) >= 2;
  for (const key of ROOF_TEMPLATE_FIELDS) {
    // A template saved before version 2 never kept these: the roof keeps its own.
    if (!v2 && SINCE_V2.has(key)) continue;
    attrs[key] = key in template.attrs && template.attrs[key] !== undefined ? template.attrs[key] : null;
  }
  const same = !!template.outline && !!contour && contour.length === template.outline.corners;
  if (!same && Array.isArray(attrs.layerExtents)) {
    const kept = (attrs.layerExtents as unknown[]).filter((x) => !(isPlainObject(x) && x.edge === 'edge'));
    attrs.layerExtents = kept.length ? kept : null;
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
  if (same && template.outline) {
    return { id: roofId, attrs, edges: template.outline.edges.some((e) => e) ? [...template.outline.edges] : [], arcs: [...template.outline.arcs] };
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
  const o = isPlainObject(raw.outline) ? raw.outline : null;
  const outline = o ? outlineOf(Number(o.corners), o.edges, o.arcs) : null;
  const t = snapshotRoofTemplate(filteredAttrs, label, id);
  // A file from before version 2 applies as it did; one of version 2 brings its edges.
  const { outline: _none, version: _v, ...rest } = t;
  return {
    ...rest,
    ...(typeof raw.version === 'number' && raw.version >= 2 ? { version: raw.version } : {}),
    ...(outline ? { outline } : {}),
  };
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
