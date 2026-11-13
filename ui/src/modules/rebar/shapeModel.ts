/**
 * Bar shapes of the user's, the pure side (docs/plan-user-extensibility.md §6.3): a shape is a code the standard names (BS 8666's 21,
 * ISO 4066's 11, the office's), parameters (a key, a label, a default in metres, a least) and legs — each a length (a parameter's
 * key or a formula) and, but the last, a turn in degrees, counter-clockwise, to the next. The engine draws it, checks it and refuses
 * what does not read, saying why; this is how the editor changes a definition and fits its corners into a preview box.
 */

export interface ShapeParam { key: string; label: string; default: number; min?: number }
export interface ShapeLeg { length: string; turn?: string }
export interface ShapeHook { angle: number; length?: number }
export interface ShapeDef {
  code: string; label: string; standard?: string; params: ShapeParam[]; legs: ShapeLeg[];
  layer?: string; diameter?: number; hooks?: { start?: ShapeHook; end?: ShapeHook };
}
export interface BuiltinShape { code: string; label: string }
export interface UserShape { id: string; name: string; def: ShapeDef; uses: number }
export interface ShapeLibrary { builtin: BuiltinShape[]; shapes: UserShape[] }

export const MAX_LEGS = 24;

/** A new shape: an L with two parameters, as a start to change. */
export const newShape = (code = 'L1'): ShapeDef => ({
  code, label: 'L bar', params: [{ key: 'A', label: 'A', default: 0.6, min: 0.05 }, { key: 'B', label: 'B', default: 0.3, min: 0.05 }],
  legs: [{ length: 'A', turn: '90' }, { length: 'B' }],
});

/** The next free parameter key: A, B, C… (then A1, B1…). */
export function nextKey(params: ShapeParam[]): string {
  const used = new Set(params.map((p) => p.key.toLowerCase()));
  for (let round = 0; ; round++)
    for (let i = 0; i < 26; i++) { const k = String.fromCharCode(65 + i) + (round ? round : ''); if (!used.has(k.toLowerCase())) return k; }
}

export function addParam(def: ShapeDef): ShapeDef {
  const key = nextKey(def.params);
  return { ...def, params: [...def.params, { key, label: key, default: 0.3, min: 0.05 }] };
}

/** A parameter changed; a renamed key is renamed in the legs that name it exactly. */
export function setParam(def: ShapeDef, at: number, patch: Partial<ShapeParam>): ShapeDef {
  const was = def.params[at];
  if (!was) return def;
  const params = def.params.map((p, i) => (i === at ? { ...p, ...patch } : p));
  const rename = patch.key != null && patch.key !== was.key;
  const legs = rename ? def.legs.map((l) => ({ ...l, length: l.length.trim() === was.key ? patch.key! : l.length, ...(l.turn != null ? { turn: l.turn.trim() === was.key ? patch.key! : l.turn } : {}) })) : def.legs;
  return { ...def, params, legs };
}

/** A parameter removed (the legs that named it keep the name, and the engine says it no longer reads). */
export const removeParam = (def: ShapeDef, at: number): ShapeDef => ({ ...def, params: def.params.filter((_, i) => i !== at) });

/** A leg added at the end: the last leg gets a turn (90) and the new one is the last, with no turn. */
export function addLeg(def: ShapeDef): ShapeDef {
  if (def.legs.length >= MAX_LEGS) return def;
  const key = def.params[def.params.length - 1]?.key ?? '0.1';
  const legs = def.legs.map((l, i) => (i === def.legs.length - 1 ? { ...l, turn: l.turn ?? '90' } : l));
  return { ...def, legs: [...legs, { length: key }] };
}

/** A leg removed (one at least stays); the new last leg has no turn. */
export function removeLeg(def: ShapeDef, at: number): ShapeDef {
  if (def.legs.length <= 1) return def;
  const legs = def.legs.filter((_, i) => i !== at).map((l) => ({ ...l }));
  const last = legs[legs.length - 1];
  delete last.turn;
  return { ...def, legs };
}

export function setLeg(def: ShapeDef, at: number, patch: Partial<ShapeLeg>): ShapeDef {
  return { ...def, legs: def.legs.map((l, i) => (i === at ? { ...l, ...patch } : l)) };
}

/** The shape written as the engine reads it: the last leg without a turn, empty optional fields left out. */
export function cleanDef(def: ShapeDef): ShapeDef {
  const legs = def.legs.map((l, i) => (i === def.legs.length - 1 ? { length: l.length.trim() } : { length: l.length.trim(), turn: (l.turn ?? '0').trim() || '0' }));
  const o: ShapeDef = { code: def.code.trim(), label: def.label.trim() || `Shape ${def.code.trim()}`, params: def.params.map((p) => ({ key: p.key.trim(), label: p.label.trim() || p.key.trim(), default: p.default, ...(p.min != null ? { min: p.min } : {}) })), legs };
  if (def.standard?.trim()) o.standard = def.standard.trim();
  if (def.layer?.trim()) o.layer = def.layer.trim();
  if (def.diameter) o.diameter = def.diameter;
  const hooks = def.hooks;
  if (hooks && (hooks.start || hooks.end)) o.hooks = { ...(hooks.start ? { start: hooks.start } : {}), ...(hooks.end ? { end: hooks.end } : {}) };
  return o;
}

/** What is wrong with a definition before the engine is asked (it checks the same and says more). */
export function shapeProblem(def: ShapeDef): string | null {
  if (!def.code.trim()) return 'A bar shape needs its code (as the standard names it: 21, 11, L1…).';
  const keys = def.params.map((p) => p.key.trim().toLowerCase());
  if (def.params.some((p) => !/^[A-Za-z][A-Za-z0-9_]{0,11}$/.test(p.key.trim()))) return 'Each parameter has a key of letters and digits (A, B, R1).';
  if (new Set(keys).size !== keys.length) return 'Two parameters have the same key.';
  if (def.legs.length === 0) return 'A shape has a leg at least.';
  if (def.legs.some((l) => !l.length.trim())) return 'Each leg has a length: a parameter or a formula.';
  return null;
}

/** The corners as a polyline fitted into a box (SVG y down), with the scale used; null when there is nothing to draw. */
export function fitCorners(corners: number[][], width: number, height: number, pad = 10): { points: [number, number][]; scale: number } | null {
  if (corners.length < 2) return null;
  const xs = corners.map((c) => c[0]), ys = corners.map((c) => c[1]);
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const w = Math.max(x1 - x0, 1e-6), h = Math.max(y1 - y0, 1e-6);
  const scale = Math.min((width - 2 * pad) / w, (height - 2 * pad) / h);
  const ox = (width - w * scale) / 2, oy = (height - h * scale) / 2;
  return { points: corners.map((c) => [ox + (c[0] - x0) * scale, height - (oy + (c[1] - y0) * scale)] as [number, number]), scale };
}

/** The shapes a picker offers: Lapin's, then the project's by name. */
export const userShapeLabel = (s: UserShape): string => s.name;

/** The parameters of a shape in the form a bar section edits them (key, label, default, least in metres). */
export const paramsOf = (def: ShapeDef | undefined | null): { key: string; label: string; default: number; min: number }[] =>
  (def?.params ?? []).map((p) => ({ key: p.key, label: p.label || p.key, default: p.default, min: p.min ?? 0 }));
