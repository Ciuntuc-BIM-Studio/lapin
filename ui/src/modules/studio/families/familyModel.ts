/**
 * Families of the user's own (docs/plan-user-extensibility.md §5.6): the pure side of the editor. A family's definition is
 * `{ifcClass, params, bodies}` as the engine keeps it (metres and degrees); a body's corners, depth, offset and condition are
 * numbers or formulas of the parameters. These helpers edit a definition without mutating it, read what a cell holds, and
 * project the engine's preview edges into a small isometric wireframe.
 */

export type ParamKind = 'length' | 'number' | 'angle' | 'bool';
export const PARAM_KINDS: ParamKind[] = ['length', 'number', 'angle', 'bool'];
export const PARAM_KIND_LABELS: Record<ParamKind, string> = { length: 'Length', number: 'Number', angle: 'Angle', bool: 'Yes/no' };
export type Plane = 'xy' | 'xz' | 'yz';
export const PLANES: Plane[] = ['xy', 'xz', 'yz'];
export const PLANE_LABELS: Record<Plane, string> = { xy: 'Lying (xy)', xz: 'Standing, front (xz)', yz: 'Standing, side (yz)' };

/** A number, or a formula of the family's parameters. */
export type Cell = number | string;

export interface FParam { key: string; label: string; kind: ParamKind; default: number }
export interface FBody {
  name: string; plane: Plane; points: [Cell, Cell][]; holes?: [Cell, Cell][][]; depth: Cell; offset?: [Cell, Cell, Cell]; when?: string; material?: string;
}
export interface FamilyDef { ifcClass: string; description?: string; planSymbol?: string; params: FParam[]; bodies: FBody[] }
export interface FamilyDto { id: string; name: string; def: FamilyDef; uses: number }

/** The IFC classes a family is exported as (a choice; the engine takes any Ifc class). */
export const IFC_CLASSES = [
  'IfcBuildingElementProxy', 'IfcFurnishingElement', 'IfcColumn', 'IfcBeam', 'IfcMember', 'IfcPlate', 'IfcSlab', 'IfcWall', 'IfcCovering', 'IfcRailing', 'IfcStair', 'IfcFooting',
  'IfcFlowTerminal', 'IfcFlowSegment', 'IfcFlowFitting', 'IfcDistributionElement', 'IfcSanitaryTerminal', 'IfcLightFixture', 'IfcElectricAppliance', 'IfcTransportElement',
];

/** The class name without its prefix, spaced: IfcFlowTerminal → Flow terminal. */
export const className = (c: string): string => c.replace(/^Ifc/, '').replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, (x) => x.toUpperCase()).replace(/ ([A-Z])/g, (_, x: string) => ` ${x.toLowerCase()}`);

// ------------------------------------------------------------ cells

const round = (n: number) => Math.round(n * 1e6) / 1e6;
const NUMBER = /^[-+]?(\d+([.,]\d*)?|[.,]\d+)(e[-+]?\d+)?$/i;

/** A cell as written in the table. */
export const cellText = (c: Cell | undefined): string => (c === undefined ? '' : typeof c === 'number' ? String(round(c)) : c);

/** What a cell's text means: a number when it is one (a comma is a decimal point), else the formula as written; empty is 0. */
export function parseCell(text: string): Cell {
  const t = text.trim();
  if (!t) return 0;
  if (NUMBER.test(t)) return round(Number(t.replace(',', '.')));
  return t;
}

export const isFormula = (c: Cell | undefined): boolean => typeof c === 'string';

/** A parameter's value as typed: a number, a yes/no for a bool; null when it is empty (the default) or not a number. */
export function parseValue(text: string, kind: ParamKind, length?: (t: string) => number | null): number | null | 'bad' {
  const t = text.trim();
  if (!t) return null;
  if (kind === 'bool') return /^(1|yes|true|da)$/i.test(t) ? 1 : /^(0|no|false|nu)$/i.test(t) ? 0 : 'bad';
  if (kind === 'length' && length) { const m = length(t); return m == null ? 'bad' : round(m); }
  return NUMBER.test(t) ? round(Number(t.replace(',', '.'))) : 'bad';
}

/** The values typed for a preview or an object: only those that are numbers; keys of the family only. */
export function valuesOf(def: FamilyDef, typed: Record<string, string>, length?: (t: string) => number | null): Record<string, number> {
  const out: Record<string, number> = {};
  for (const p of def.params) {
    const v = parseValue(typed[p.key] ?? '', p.kind, length);
    if (typeof v === 'number') out[p.key] = v;
  }
  return out;
}

// ------------------------------------------------------------ editing

const insert = <T,>(list: T[], at: number, v: T): T[] => [...list.slice(0, at), v, ...list.slice(at)];
const replace = <T,>(list: T[], at: number, f: (x: T) => T): T[] => list.map((x, i) => (i === at ? f(x) : x));
const drop = <T,>(list: T[], at: number): T[] => list.filter((_, i) => i !== at);

/** A new family: a box W × H × D, its parameters the three sizes. */
export function blankFamily(): FamilyDef {
  return {
    ifcClass: 'IfcBuildingElementProxy',
    params: [
      { key: 'W', label: 'Width', kind: 'length', default: 1 },
      { key: 'H', label: 'Depth', kind: 'length', default: 0.5 },
      { key: 'D', label: 'Height', kind: 'length', default: 0.8 },
    ],
    bodies: [{ name: 'Body', plane: 'xy', points: [[0, 0], ['W', 0], ['W', 'H'], [0, 'H']], depth: 'D' }],
  };
}

/** A name not in the list: base, then base 2, base 3… */
export function freeKey(taken: string[], base: string): string {
  const have = new Set(taken.map((k) => k.toLowerCase()));
  if (!have.has(base.toLowerCase())) return base;
  for (let i = 2; ; i++) if (!have.has(`${base}${i}`.toLowerCase())) return `${base}${i}`;
}

export function addParam(def: FamilyDef): FamilyDef {
  const key = freeKey(def.params.map((p) => p.key), 'P');
  return { ...def, params: [...def.params, { key, label: key, kind: 'length', default: 1 }] };
}
export const setParam = (def: FamilyDef, at: number, patch: Partial<FParam>): FamilyDef => ({ ...def, params: replace(def.params, at, (p) => ({ ...p, ...patch })) });
export const removeParam = (def: FamilyDef, at: number): FamilyDef => ({ ...def, params: drop(def.params, at) });

/** A key is letters and digits (and _), starting with a letter, at most 24 long, and not another parameter's. */
export function keyProblem(def: FamilyDef, at: number): string | null {
  const k = def.params[at]?.key ?? '';
  if (!k || k.length > 24 || !/^[A-Za-z][A-Za-z0-9_]*$/.test(k)) return 'A key is letters and digits, starting with a letter (W, Height, HasShelf).';
  if (def.params.some((p, i) => i !== at && p.key.toLowerCase() === k.toLowerCase())) return 'Two parameters have this key.';
  return null;
}

/** A parameter's default, as its kind allows (a yes/no is 0 or 1). */
export const defaultFor = (kind: ParamKind, v: number): number => (kind === 'bool' ? (v ? 1 : 0) : v);

export function addBody(def: FamilyDef): FamilyDef {
  const name = freeKey(def.bodies.map((b) => b.name), 'Body');
  return { ...def, bodies: [...def.bodies, { name, plane: 'xy', points: [[0, 0], [0.5, 0], [0.5, 0.5], [0, 0.5]], depth: 0.5 }] };
}
export const setBody = (def: FamilyDef, b: number, patch: Partial<FBody>): FamilyDef => ({ ...def, bodies: replace(def.bodies, b, (x) => ({ ...x, ...patch })) });
export const removeBody = (def: FamilyDef, b: number): FamilyDef => ({ ...def, bodies: drop(def.bodies, b) });

/** A body's condition, an offset cell or its material may be left empty: the key is then left out. */
export function setBodyOptional(def: FamilyDef, b: number, key: 'when' | 'material', text: string): FamilyDef {
  return { ...def, bodies: replace(def.bodies, b, (x) => { const { [key]: _drop, ...rest } = x; void _drop; return text.trim() ? { ...rest, [key]: text.trim() } : rest; }) };
}
export function setOffset(def: FamilyDef, b: number, axis: 0 | 1 | 2, text: string): FamilyDef {
  return { ...def, bodies: replace(def.bodies, b, (x) => {
    const off: [Cell, Cell, Cell] = [...(x.offset ?? [0, 0, 0])] as [Cell, Cell, Cell];
    off[axis] = parseCell(text);
    const { offset: _o, ...rest } = x; void _o;
    return off.every((c) => c === 0) ? rest : { ...rest, offset: off };
  }) };
}

export function addPoint(def: FamilyDef, b: number, after?: number): FamilyDef {
  return { ...def, bodies: replace(def.bodies, b, (x) => {
    const at = after == null ? x.points.length : after + 1;
    const a = x.points[at - 1] ?? [0, 0];
    const c = x.points[at % Math.max(x.points.length, 1)] ?? a;
    // Between its neighbours when both are numbers, else a copy of the one before.
    const mid = (i: 0 | 1): Cell => (typeof a[i] === 'number' && typeof c[i] === 'number' ? round(((a[i] as number) + (c[i] as number)) / 2) : a[i]);
    return { ...x, points: insert(x.points, at, [mid(0), mid(1)] as [Cell, Cell]) };
  }) };
}
export const removePoint = (def: FamilyDef, b: number, p: number): FamilyDef => ({ ...def, bodies: replace(def.bodies, b, (x) => ({ ...x, points: drop(x.points, p) })) });
export function setPoint(def: FamilyDef, b: number, p: number, axis: 0 | 1, text: string): FamilyDef {
  return { ...def, bodies: replace(def.bodies, b, (x) => ({ ...x, points: replace(x.points, p, (pt) => (axis === 0 ? [parseCell(text), pt[1]] : [pt[0], parseCell(text)]) as [Cell, Cell]) })) };
}

/**
 * A formula with every use of the name `oldKey` written `newKey`. Names are read as the engine's evaluator reads them (letters,
 * digits, _ and dots, starting with a letter or _; case does not matter), so only a whole name is replaced: not a part of
 * `W2`, `Width`, `attr.W` or `W_x`, not a function (`max(` is followed by a bracket), and not a number (`2e3`).
 */
export function renameInFormula(text: string, oldKey: string, newKey: string): string {
  const old = oldKey.toLowerCase();
  let out = '';
  let i = 0;
  while (i < text.length) {
    const c = text[i];
    if (/\d/.test(c) || (c === '.' && /\d/.test(text[i + 1] ?? ''))) {
      // A number, with its exponent: 2e3, 1.5E-2.
      let j = i;
      while (j < text.length && /[\d.]/.test(text[j])) j++;
      if (/[eE]/.test(text[j] ?? '') && (/\d/.test(text[j + 1] ?? '') || /[-+]/.test(text[j + 1] ?? ''))) { j += 2; while (j < text.length && /\d/.test(text[j])) j++; }
      out += text.slice(i, j); i = j;
    } else if (/[\p{L}_]/u.test(c)) {
      let j = i;
      while (j < text.length && /[\p{L}\p{N}_.]/u.test(text[j])) j++;
      const word = text.slice(i, j);
      const call = /^\s*\(/.test(text.slice(j));
      out += !call && word.toLowerCase() === old ? newKey : word; i = j;
    } else { out += c; i++; }
  }
  return out;
}

/** A cell with the name renamed (a number is left as it is). */
const renameCell = (c: Cell, o: string, n: string): Cell => (typeof c === 'string' ? renameInFormula(c, o, n) : c);

/**
 * The definition with the parameter `oldKey` called `newKey`: the parameter's key, and every formula that names it (corners,
 * holes, depth, offset, condition). Unchanged when the keys are the same, when no parameter has `oldKey`, or when another
 * parameter already has `newKey` (case apart).
 */
export function renameKey(def: FamilyDef, oldKey: string, newKey: string): FamilyDef {
  if (oldKey === newKey || !def.params.some((p) => p.key === oldKey)) return def;
  if (def.params.some((p) => p.key !== oldKey && p.key.toLowerCase() === newKey.toLowerCase())) return def;
  const r = (c: Cell) => renameCell(c, oldKey, newKey);
  const pt = (p: [Cell, Cell]): [Cell, Cell] => [r(p[0]), r(p[1])];
  return {
    ...def,
    params: def.params.map((p) => (p.key === oldKey ? { ...p, key: newKey } : p)),
    bodies: def.bodies.map((b) => ({
      ...b,
      points: b.points.map(pt),
      ...(b.holes ? { holes: b.holes.map((h) => h.map(pt)) } : {}),
      depth: r(b.depth),
      ...(b.offset ? { offset: b.offset.map(r) as [Cell, Cell, Cell] } : {}),
      ...(b.when !== undefined ? { when: renameInFormula(b.when, oldKey, newKey) } : {}),
    })),
  };
}

/** The names a formula may use: the parameters' keys. */
export const formulaNames = (def: FamilyDef): string[] => def.params.map((p) => p.key).filter(Boolean);

/** What would make the family refused before the engine is asked: a body needs three corners, and the family one body. */
export function quickProblem(def: FamilyDef): string | null {
  if (def.bodies.length === 0) return 'A family has one body at least.';
  const b = def.bodies.find((x) => x.points.length < 3);
  return b ? `${b.name || 'A body'}: an outline needs three corners at least.` : null;
}

/** A definition as the engine gives it, made safe to edit (a missing list is empty, a missing class the default). */
export function normaliseFamily(d: Partial<FamilyDef> | null | undefined): FamilyDef {
  return {
    ifcClass: d?.ifcClass || 'IfcBuildingElementProxy',
    ...(d?.description ? { description: d.description } : {}), ...(d?.planSymbol ? { planSymbol: d.planSymbol } : {}),
    params: (d?.params ?? []).map((p) => ({ key: p.key, label: p.label || p.key, kind: p.kind ?? 'length', default: Number(p.default) || 0 })),
    bodies: (d?.bodies ?? []).map((b) => ({ ...b, name: b.name ?? '', plane: b.plane ?? 'xy', points: (b.points ?? []) as [Cell, Cell][], depth: b.depth ?? 0 })),
  };
}

export const sameDef = (a: FamilyDef, b: FamilyDef): boolean => JSON.stringify(a) === JSON.stringify(b);

// ------------------------------------------------------------ the wireframe

export interface PreviewBody { name: string; edges: number[][]; volume: number }
export interface Wire { width: number; height: number; lines: [number, number, number, number][] }

/** A point of the model turned (yaw about the vertical) and tipped (pitch): the isometric-like view the preview shows; y up on screen. */
export function isoPoint(x: number, y: number, z: number, yaw = Math.PI / 6, pitch = Math.PI / 6): [number, number] {
  const rx = x * Math.cos(yaw) - y * Math.sin(yaw);
  const ry = x * Math.sin(yaw) + y * Math.cos(yaw);
  return [rx, z * Math.cos(pitch) + ry * Math.sin(pitch)];
}

/** The edges of the bodies projected, each one once, fitted into width × height with a margin (aspect kept; SVG y down). */
export function wireframe(bodies: PreviewBody[], width = 320, height = 220, margin = 10): Wire {
  const seen = new Set<string>();
  const raw: [number, number, number, number][] = [];
  for (const b of bodies) {
    for (const e of b.edges) {
      if (e.length < 6) continue;
      const [x1, y1, z1, x2, y2, z2] = e;
      const key = [[x1, y1, z1], [x2, y2, z2]].map((p) => p.map((v) => v.toFixed(5)).join(',')).sort().join('|');
      if (seen.has(key)) continue;
      seen.add(key);
      const a = isoPoint(x1, y1, z1), c = isoPoint(x2, y2, z2);
      raw.push([a[0], a[1], c[0], c[1]]);
    }
  }
  if (raw.length === 0) return { width, height, lines: [] };
  const xs = raw.flatMap((l) => [l[0], l[2]]), ys = raw.flatMap((l) => [l[1], l[3]]);
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const k = Math.min((width - 2 * margin) / Math.max(x1 - x0, 1e-9), (height - 2 * margin) / Math.max(y1 - y0, 1e-9));
  const ox = (width - (x1 - x0) * k) / 2, oy = (height - (y1 - y0) * k) / 2;
  const f = (x: number, y: number): [number, number] => [round2(ox + (x - x0) * k), round2(height - (oy + (y - y0) * k))];
  return { width, height, lines: raw.map((l) => [...f(l[0], l[1]), ...f(l[2], l[3])] as [number, number, number, number]) };
}
const round2 = (n: number) => Math.round(n * 100) / 100;

/** The bodies' size: the box they fill, in metres (x, y, z). */
export function sizeOf(bodies: PreviewBody[]): [number, number, number] | null {
  const pts = bodies.flatMap((b) => b.edges.flatMap((e) => [[e[0], e[1], e[2]], [e[3], e[4], e[5]]]));
  if (!pts.length) return null;
  const span = (i: number) => Math.max(...pts.map((p) => p[i])) - Math.min(...pts.map((p) => p[i]));
  return [span(0), span(1), span(2)];
}

// ------------------------------------------------------------ from a sketch

interface SketchNode { type: string; props: Record<string, unknown> }

/** Whether a node is drafting that is a closed outline: a closed polyline (a rectangle is one) or a circle. */
export function isClosedOutline(n: SketchNode | undefined): boolean {
  if (!n || n.type !== 'IfcAnnotation' || typeof n.props.viewId !== 'string' || n.props.predefinedType !== 'DRAFT') return false;
  const s = n.props.shape as { type?: string; closed?: boolean } | undefined;
  return s?.type === 'circle' || (s?.type === 'polyline' && !!s.closed);
}

/** The closed outlines among the ids, and the view of the first (the family is made from one view's drafting). */
export function sketchSources(ids: string[], nodes: Map<string, SketchNode>): { viewId: string; ids: string[] } | null {
  const mine = ids.filter((id) => isClosedOutline(nodes.get(id)));
  if (!mine.length) return null;
  const viewId = nodes.get(mine[0])!.props.viewId as string;
  return { viewId, ids: mine.filter((id) => nodes.get(id)!.props.viewId === viewId) };
}

// ------------------------------------------------------------ the ghost in a plan

/** The bodies' edges seen from above, in the family's own frame: x, y only, each one once, those that stand on a point left out. */
export function planEdges(bodies: PreviewBody[]): [number, number, number, number][] {
  const seen = new Set<string>();
  const out: [number, number, number, number][] = [];
  for (const b of bodies) {
    for (const e of b.edges) {
      if (e.length < 4) continue;
      const [x1, y1, x2, y2] = e.length >= 6 ? [e[0], e[1], e[3], e[4]] : [e[0], e[1], e[2], e[3]];
      if (Math.hypot(x2 - x1, y2 - y1) < 1e-6) continue;
      const a = `${x1.toFixed(5)},${y1.toFixed(5)}`, c = `${x2.toFixed(5)},${y2.toFixed(5)}`;
      const key = a < c ? `${a}|${c}` : `${c}|${a}`;
      if (seen.has(key)) continue;
      seen.add(key);
      out.push([round(x1), round(y1), round(x2), round(y2)]);
    }
  }
  return out;
}

/** The plan edges standing at `at`: x flipped when mirrored, then turned `rotation` degrees (anticlockwise, y up), then moved. One segment each, [a, b]. */
export function placedEdges(edges: [number, number, number, number][], at: { x: number; y: number }, rotation = 0, mirror = false): [{ x: number; y: number }, { x: number; y: number }][] {
  const a = (rotation * Math.PI) / 180, cos = Math.cos(a), sin = Math.sin(a);
  const P = (x: number, y: number) => { const fx = mirror ? -x : x; return { x: at.x + fx * cos - y * sin, y: at.y + fx * sin + y * cos }; };
  return edges.map((e) => [P(e[0], e[1]), P(e[2], e[3])]);
}
