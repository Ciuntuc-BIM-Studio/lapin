/**
 * Smart Context Pack for local Ollama models (including small 7B–14B models like gemma4:12b)
 * (docs/plan-python-notebook.md):
 *
 * Instead of dumping the 197 KB command catalogue or relying solely on multi-round tool calls,
 * this module proactively builds a tight, high-signal context pack (~2,000–3,000 tokens):
 *   1. Bilingual RO + EN BIM term expansion → pre-retrieves top 10 exact `lapin.*` signatures
 *   2. Curated mini-cookbook (16 verified `lapinbim` recipes) → injects top 2–3 few-shot examples
 *   3. Live BIM model summary (storeys, classes, types, and exact `props` of selected elements)
 *   4. Live Python kernel variables (from `script.inspect`) + code cells above
 */

import { useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { ident, snake, type Catalogue } from './logic';

export interface KernelVar {
  name: string;
  type: string;
  len?: number;
  preview: string;
}

export interface Recipe {
  id: string;
  title: string;
  tags: string[];
  code: string;
}

/** Bilingual Romanian (diacritic-stripped) & English BIM synonyms → English catalogue search terms. */
const SYNONYMS: Record<string, string[]> = {
  perete: ['wall', 'architecture'],
  pereti: ['wall', 'architecture'],
  zid: ['wall'],
  stalp: ['column', 'structure'],
  stalpi: ['column', 'structure'],
  coloana: ['column', 'structure'],
  coloane: ['column', 'structure'],
  grinda: ['beam', 'structure'],
  grinzi: ['beam', 'structure'],
  planseu: ['slab', 'floor', 'architecture'],
  plansee: ['slab', 'floor'],
  placa: ['slab', 'floor', 'plate'],
  placi: ['slab', 'floor'],
  podea: ['slab', 'floor'],
  usa: ['door', 'architecture'],
  usi: ['door', 'architecture'],
  fereastra: ['window', 'architecture'],
  ferestre: ['window', 'architecture'],
  acoperis: ['roof', 'architecture'],
  acoperisuri: ['roof'],
  sarpanta: ['roof', 'wood', 'timber', 'rafter'],
  camera: ['room', 'space'],
  camere: ['room', 'space'],
  incapere: ['room', 'space'],
  incaperi: ['room', 'space'],
  spatiu: ['room', 'space'],
  scara: ['stair'],
  scari: ['stair'],
  fundatie: ['footing', 'foundation', 'structure'],
  fundatii: ['footing', 'foundation'],
  balcon: ['balcony'],
  balcoane: ['balcony'],
  tavan: ['ceiling'],
  tavane: ['ceiling'],
  balustrada: ['railing'],
  balustrade: ['railing'],
  nivel: ['storey', 'level'],
  niveluri: ['storey', 'level'],
  etaj: ['storey', 'level'],
  etaje: ['storey', 'level'],
  parter: ['storey', 'ground'],
  armatura: ['rebar'],
  armaturi: ['rebar'],
  otel: ['steel', 'member'],
  lemn: ['wood', 'timber'],
  instalatii: ['mep', 'pipe', 'duct'],
  teava: ['pipe', 'mep'],
  conducta: ['duct', 'mep'],
  mobilier: ['furniture'],
  lift: ['elevator'],
  teren: ['terrain', 'site'],
  cantitati: ['quantities', 'volume', 'area', 'length'],
  volum: ['volume', 'quantities'],
  arie: ['area', 'quantities'],
  suprafata: ['area', 'quantities'],
  lungime: ['length', 'quantities'],
  parametru: ['param', 'pset', 'setparam'],
  parametri: ['param', 'pset', 'setparam'],
  proprietate: ['param', 'pset', 'update'],
  proprietati: ['param', 'pset', 'update'],
  selectie: ['selection', 'selected'],
  selectat: ['selection', 'selected'],
  selectate: ['selection', 'selected'],
  sterge: ['delete', 'remove'],
  elimina: ['delete', 'remove'],
  muta: ['move', 'translate'],
  roteste: ['rotate'],
  copiaza: ['copy'],
  corp: ['body', 'mesh', 'create_body'],
  geometrie: ['body', 'mesh', 'geometry'],
  zabrele: ['truss', 'body', 'mesh'],
  ferma: ['truss', 'body', 'mesh'],
  analiza: ['analysis', 'load'],
  incarcare: ['load', 'analysis'],
  incarcari: ['load', 'analysis'],
  tabel: ['schedule', 'csv'],
  extras: ['schedule', 'quantities'],
  curtain: ['curtain', 'wall'],
  cortina: ['curtain', 'wall'],
  ax: ['grid', 'axis'],
  axe: ['grid', 'axis'],
  grila: ['grid', 'column', 'range'],
};

/** Strip Romanian/Latin diacritics and lowercase for robust keyword matching. */
export function normalizeText(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase();
}

/** Expand a user prompt (RO or EN) into weighted English BIM search tokens. */
export function expandQueryTokens(text: string): string[] {
  const raw = normalizeText(text).split(/[^a-z0-9_]+/).filter((t) => t.length >= 2);
  const out = new Set<string>(raw);
  for (const tok of raw) {
    const mapped = SYNONYMS[tok];
    if (mapped) {
      for (const m of mapped) out.add(m);
    }
  }
  return [...out];
}

/** Curated, verified `lapinbim` cookbook recipes for few-shot grounding of small models. */
export const RECIPES: readonly Recipe[] = [
  {
    id: 'ground-storey',
    title: 'Find a storey by elevation or name',
    tags: ['storey', 'level', 'ground', 'model'],
    code: `storey = next((s for s in lapin.model.storeys if s.elevation == 0), lapin.model.storeys[0])
storey_id = storey.id`,
  },
  {
    id: 'perimeter-walls',
    title: 'Create perimeter walls around a rectangle',
    tags: ['wall', 'architecture', 'rectangle', 'perimeter', 'batch'],
    code: `storey_id = next((s.id for s in lapin.model.storeys if s.elevation == 0), lapin.model.storeys[0].id)
w, d, h = 10.0, 6.0, 2.8
pts = [(0, 0), (w, 0), (w, d), (0, d)]
with lapin.batch("Perimeter walls"):
    for (x1, y1), (x2, y2) in zip(pts, pts[1:] + pts[:1]):
        lapin.architecture.add_wall(storey_id=storey_id, x1=x1, y1=y1, x2=x2, y2=y2, height=h)`,
  },
  {
    id: 'column-grid',
    title: 'Place structural columns on an X/Y grid',
    tags: ['column', 'structure', 'grid', 'range', 'batch'],
    code: `storey_id = next((s.id for s in lapin.model.storeys if s.elevation == 0), lapin.model.storeys[0].id)
nx, ny, dx, dy = 4, 3, 5.0, 4.0
with lapin.batch("Column grid"):
    for i in range(nx):
        for j in range(ny):
            lapin.structure.add_column(storey_id=storey_id, x=i * dx, y=j * dy)`,
  },
  {
    id: 'slab-outline',
    title: 'Create a floor slab from a polygon',
    tags: ['slab', 'floor', 'architecture', 'points'],
    code: `storey_id = next((s.id for s in lapin.model.storeys if s.elevation == 0), lapin.model.storeys[0].id)
pts = [[0, 0], [10, 0], [10, 6], [0, 6]]
lapin.architecture.add_slab(storey_id=storey_id, points=pts)`,
  },
  {
    id: 'door-on-wall',
    title: 'Add a door or window on an existing wall',
    tags: ['door', 'window', 'wall', 'architecture'],
    code: `wall = lapin.model.walls[0]
lapin.architecture.add_door(wall_id=wall.id, along=2.0, width=0.9, height=2.1)
lapin.architecture.add_window(wall_id=wall.id, along=4.5, width=1.4, height=1.2, sill=0.9)`,
  },
  {
    id: 'beam-between-columns',
    title: 'Connect columns with structural beams',
    tags: ['beam', 'column', 'structure'],
    code: `storey_id = next((s.id for s in lapin.model.storeys if s.elevation == 0), lapin.model.storeys[0].id)
lapin.structure.add_beam(storey_id=storey_id, x1=0, y1=0, x2=5, y2=0, z=3.0)`,
  },
  {
    id: 'read-quantities',
    title: 'Compute total length, area or volume of elements',
    tags: ['quantities', 'volume', 'area', 'length', 'wall', 'slab', 'report'],
    code: `walls = lapin.model.walls
total_vol = sum(w.quantity("NetVolume", 0.0) for w in walls)
total_area = sum(w.quantity("NetSideArea", 0.0) for w in walls)
print(f"{len(walls)} walls: area={total_area:.2f} m2, volume={total_vol:.2f} m3")`,
  },
  {
    id: 'filter-and-update',
    title: 'Filter elements and update attributes or parameters',
    tags: ['update', 'param', 'pset', 'filter', 'wall', 'door'],
    code: `with lapin.batch("Update fire rating"):
    for wall in lapin.model.walls:
        if (wall.attrs.get("height") or 0) >= 2.8:
            wall.set_param("FireRating", "EI60", kind="text", pset="Pset_WallCommon")`,
  },
  {
    id: 'work-on-selection',
    title: 'Operate on the currently selected elements',
    tags: ['selection', 'selected', 'update', 'delete', 'param'],
    code: `ids = lapin.selection()
selected_elems = [lapin.model[i] for i in ids]
for e in selected_elems:
    print(e.id, e.type, e.name, e.attrs)`,
  },
  {
    id: 'rooms-spaces',
    title: 'Inspect rooms (IfcSpace) and their bounding boxes',
    tags: ['room', 'space', 'bbox', 'measure'],
    code: `rooms = lapin.model.of("IfcSpace")
for r in rooms:
    box = lapin.measure_value(f"element.{r.id}.bbox")
    print(r.name, box)`,
  },
  {
    id: 'roof-contour',
    title: 'Create a roof over a rectangular contour',
    tags: ['roof', 'architecture', 'contour', 'points'],
    code: `storey_id = lapin.model.storeys[-1].id
pts = [[0, 0], [10, 0], [10, 6], [0, 6]]
lapin.architecture.add_roof(storey_id=storey_id, points=pts)`,
  },
  {
    id: 'custom-body-mesh',
    title: 'Create a custom 3D IFC element from vertices and triangles',
    tags: ['body', 'mesh', 'truss', 'geometry', 'member'],
    code: `gid = lapin.global_id("custom:prism:1")
mesh = {"positions": [0,0,0, 2,0,0, 2,1,0, 0,1,0, 0,0,3, 2,0,3, 2,1,3, 0,1,3],
        "indices": [0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7]}
lapin.create_body("IfcMember", mesh, storey=lapin.model.storeys[0].id, name="Custom Prism", global_id=gid)`,
  },
  {
    id: 'delete-filtered',
    title: 'Delete elements matching a condition in one batch',
    tags: ['delete', 'remove', 'filter', 'batch'],
    code: `to_remove = [e.id for e in lapin.model.of("IfcColumn") if e.storey == lapin.model.storeys[0].id]
if to_remove:
    lapin.execute("element.delete", {"ids": to_remove})`,
  },
  {
    id: 'schedule-query',
    title: 'Query schedules or classes in the model',
    tags: ['schedule', 'csv', 'query', 'report'],
    code: `for cls in sorted(lapin.model.classes):
    print(cls, len(lapin.model.of(cls)))`,
  },
  {
    id: 'interactive-dialog',
    title: 'Ask the user for parameters with a native dialog',
    tags: ['ask', 'dialog', 'ui', 'tool', 'input'],
    code: `vals = lapin.ui.ask("Parameters", fields={"height": 2.8, "count": 5})
if vals:
    print(vals["height"], vals["count"])`,
  },
];

/** Format a single engine command/query as a Python call signature. */
export function formatPythonSignature(name: string, args?: { name: string; type?: string }[]): string {
  const [ns, ...rest] = name.split('.');
  const fn = rest.length ? ident(snake(rest.join('_'))) : ident(snake(ns));
  const pyName = rest.length ? `lapin.${ident(snake(ns))}.${fn}` : `lapin.${fn}`;
  const sig = (args ?? []).map((a) => `${ident(snake(a.name))}${a.type ? `: ${a.type}` : ''}`).join(', ');
  return `${pyName}(${sig})`;
}

/** Proactively select top-K matching `session.catalogue` commands/queries for the prompt. */
export function matchCatalogue(catalogue: Catalogue | null, prompt: string, limit = 10): string[] {
  if (!catalogue) return [];
  const tokens = expandQueryTokens(prompt);
  if (!tokens.length) return [];

  const score = (name: string, label?: string, mod?: string): number => {
    const lowerName = name.toLowerCase();
    const hay = `${lowerName} ${(label ?? '').toLowerCase()} ${(mod ?? '').toLowerCase()}`;
    let s = 0;
    for (const t of tokens) {
      if (lowerName.includes(t)) s += 3;
      else if (hay.includes(t)) s += 1;
    }
    return s;
  };

  return [
    ...catalogue.commands.map((c) => ({ name: c.name, args: c.args, s: score(c.name, c.label, c.module) })),
    ...catalogue.queries.map((q) => ({ name: q.name, args: q.args, s: score(q.name, undefined, q.module) })),
  ]
    .filter((x) => x.s > 0)
    .sort((a, b) => b.s - a.s || a.name.localeCompare(b.name))
    .slice(0, limit)
    .map((x) => formatPythonSignature(x.name, x.args));
}

/** Select the top 2–3 cookbook recipes relevant to the prompt. */
export function matchRecipes(prompt: string, limit = 3): Recipe[] {
  const tokens = new Set(expandQueryTokens(prompt));
  if (!tokens.size) return RECIPES.slice(0, Math.min(2, limit));

  const scored = RECIPES.map((r) => {
    let s = 0;
    for (const tag of r.tags) if (tokens.has(tag)) s += 2;
    const titleTokens = normalizeText(r.title).split(/[^a-z0-9]+/);
    for (const tt of titleTokens) if (tokens.has(tt)) s += 1;
    return { r, s };
  })
    .filter((x) => x.s > 0)
    .sort((a, b) => b.s - a.s);

  if (scored.length === 0) return RECIPES.slice(0, Math.min(2, limit));
  return scored.slice(0, limit).map((x) => x.r);
}

/** Compact live summary of the open BIM model + exact attributes of selected elements. */
export function buildModelSnapshot(): string {
  const nodes = useSession.getState().nodes;
  const storeys: { id: string; name: string; elevation: unknown }[] = [];
  const counts: Record<string, number> = {};
  for (const n of nodes.values()) {
    counts[n.type] = (counts[n.type] ?? 0) + 1;
    if (n.type === 'IfcBuildingStorey') {
      storeys.push({ id: n.id, name: n.name, elevation: n.props.elevation ?? 0 });
    }
  }
  const ui = useUi.getState();
  const selIds = ui.selected.length ? ui.selected : ui.selection ? [ui.selection] : [];
  const selectedSample = selIds.slice(0, 4).map((id) => {
    const n = nodes.get(id);
    if (!n) return { id };
    const compactProps: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(n.props ?? {}).slice(0, 10)) {
      if (typeof v === 'string' || typeof v === 'number' || typeof v === 'boolean') compactProps[k] = v;
    }
    return { id: n.id, type: n.type, name: n.name, props: compactProps };
  });

  const lines: string[] = [];
  if (storeys.length) {
    lines.push(`Storeys: ${storeys.map((s) => `${s.name} (id="${s.id}", z=${s.elevation}m)`).join(', ')}`);
  }
  const topClasses = Object.entries(counts)
    .filter(([k]) => !k.startsWith('Lapin') && k !== 'IfcProject' && k !== 'IfcSite' && k !== 'IfcBuilding')
    .sort((a, b) => b[1] - a[1])
    .slice(0, 12)
    .map(([k, v]) => `${k}:${v}`);
  if (topClasses.length) lines.push(`Elements: ${topClasses.join(', ')}`);
  if (selIds.length) {
    lines.push(`Selection (${selIds.length}): ${JSON.stringify(selectedSample)}`);
  }
  return lines.join('\n');
}

/** Format live kernel variables from `script.inspect` into a compact block. */
export function formatKernelVars(vars: readonly KernelVar[]): string {
  if (!vars.length) return '';
  return vars
    .slice(0, 25)
    .map((v) => `${v.name}: ${v.type}${v.len !== undefined ? ` (len=${v.len})` : ''} = ${v.preview}`)
    .join('\n');
}

export interface ContextPackOptions {
  prompt: string;
  catalogue: Catalogue | null;
  contextAbove?: string;
  kernelVars?: readonly KernelVar[];
  maxChars?: number;
}

/**
 * Assemble a tight, high-signal context pack (~2,000–3,000 tokens) tailored for small local models.
 */
export function buildContextPack(opts: ContextPackOptions): string {
  const maxChars = opts.maxChars ?? 10_000;
  const sections: string[] = [];

  const sigs = matchCatalogue(opts.catalogue, opts.prompt, 10);
  if (sigs.length) {
    sections.push(`Relevant lapin commands (exact Python signatures):\n${sigs.map((s) => `- ${s}`).join('\n')}`);
  }

  const recipes = matchRecipes(opts.prompt, 3);
  if (recipes.length) {
    sections.push(
      `Verified lapinbim examples:\n${recipes.map((r) => `# ${r.title}\n${r.code}`).join('\n\n')}`,
    );
  }

  const model = buildModelSnapshot();
  if (model) {
    sections.push(`Open BIM model:\n${model}`);
  }

  const varsBlock = formatKernelVars(opts.kernelVars ?? []);
  if (varsBlock) {
    sections.push(`Live Python kernel variables in memory:\n${varsBlock}`);
  }

  if (opts.contextAbove?.trim()) {
    const trimmed = opts.contextAbove.trim();
    const capped = trimmed.length > 2500 ? trimmed.slice(trimmed.length - 2500) : trimmed;
    sections.push(`Code in notebook cells above:\n\`\`\`python\n${capped}\n\`\`\``);
  }

  const joined = sections.join('\n\n');
  return joined.length > maxChars ? joined.slice(0, maxChars) : joined;
}
