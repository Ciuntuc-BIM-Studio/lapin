/**
 * Match properties (decision 490; AutoCAD's MATCHPROP, Revit's Match Type): what one element carries, given to others.
 * What is carried, by group: its type (to an element of its class), its own fields (heights, offsets, materials, styles: those
 * the target's class has too), a door's or window's frame position and sill, the user's parameters, its phases, a drafted
 * shape's layer, colour and hatch, and its name when asked. Never where an element stands or its shape (start, end, at,
 * contour…), nor its identity (GlobalId, a room's number, a mark). Each target's steps go as one edit.batch: one undo step.
 */

import type { FieldDescriptor, NodeDto } from '../../engine/protocol';
import { fieldShown } from '../../engine/protocol';
import type { GripStep } from '../../platform/module';

export type MatchGroup = 'type' | 'fields' | 'params' | 'phase' | 'drafting' | 'name';

/** The groups the settings list, in order; `on`: chosen until the user says otherwise. */
export const MATCH_GROUPS: readonly { id: MatchGroup; label: string; on: boolean }[] = [
  { id: 'type', label: 'Type', on: true },
  { id: 'fields', label: 'Dimensions and settings', on: true },
  { id: 'params', label: 'Parameters', on: true },
  { id: 'phase', label: 'Phases', on: true },
  { id: 'drafting', label: 'Layer, colour and hatch', on: true },
  { id: 'name', label: 'Name', on: false },
];

/** The groups chosen, as remembered (a JSON list; anything else: the defaults). */
export function readGroups(raw: string | null): Set<MatchGroup> {
  const known = new Set(MATCH_GROUPS.map((g) => g.id));
  try {
    const list = raw ? JSON.parse(raw) as unknown : null;
    if (Array.isArray(list)) return new Set(list.filter((x): x is MatchGroup => known.has(x as MatchGroup)));
  } catch { /* the defaults */ }
  return new Set(MATCH_GROUPS.filter((g) => g.on).map((g) => g.id));
}

/**
 * Fields that place an element or shape it (pinned elements refuse the first ones: Pins.PlaceKeys), name it, or turn it, never
 * carried: what one wall's ends are is never another's.
 */
const NEVER = new Set([
  'x', 'y', 'z', 'start', 'end', 'at', 'a', 'b', 'rotation', 'contour', 'points', 'islands', 'shape', 'xf', 'hatch',
  'arc', 'startAngle', 'endAngle', 'along', 'station', 'from', 'to', 'length', 'angle', 'turn', 'flip', 'mirror', 'hand',
  'side', 'direction', 'spin', 'elevation', 'place', 'placement',
  'text', 'number', 'mark', 'description', 'source', 'image', 'seed', 'kind',
]);
const KINDS = new Set(['number', 'length', 'angle', 'text', 'bool', 'choice', 'color', 'material']);
const JOINERY = new Set(['IfcDoor', 'IfcWindow']);

/** What the steps need of the model and the product. */
export interface MatchContext {
  /** A class's fields (the engine's descriptors). */
  fieldsOf: (cls: string) => readonly FieldDescriptor[];
  /** The type an element is of, or null. */
  typeOf: (id: string) => NodeDto | null;
  /** The steps that give `ids` (of class `cls`) the type `typeId`, as its module does it; null when no module gives that class a type. */
  typeSteps: (cls: string, ids: string[], typeId: string) => GripStep[] | null;
  /** The sill of a door's or window's opening (null: none). */
  sillOf: (id: string) => number | null;
  /** Whether the product has the phases module. */
  phases: boolean;
}

const same = (a: unknown, b: unknown) => JSON.stringify(a ?? null) === JSON.stringify(b ?? null);
const isDraft = (n: NodeDto) => n.type === 'IfcAnnotation' && (!!n.props.shape || !!n.props.hatch);
/** An annotation's kind, so a text's style goes to texts and a line's to drafted shapes only. */
const annotationKind = (n: NodeDto) => `${String(n.props.predefinedType ?? '')}|${n.props.hatch ? 'hatch' : n.props.shape ? 'shape' : n.props.text != null ? 'text' : ''}`;

interface Param { name: string; kind: string; value: unknown; options?: string[]; pset?: string }
const paramsOf = (n: NodeDto) => (Array.isArray(n.props.params) ? n.props.params : []) as unknown as Param[];
const paramKey = (p: Param) => `${(p.pset ?? '').toLowerCase()}/${p.name.toLowerCase()}`;

/** The fields of `source` the target's class has too (the same key and kind), that it shows, and the target has otherwise. */
function fieldAttrs(source: NodeDto, target: NodeDto, ctx: MatchContext): Record<string, unknown> {
  if ((source.type === 'IfcAnnotation' || target.type === 'IfcAnnotation') && annotationKind(source) !== annotationKind(target)) return {};
  const theirs = ctx.fieldsOf(source.type);
  const attrs: Record<string, unknown> = {};
  for (const f of ctx.fieldsOf(target.type)) {
    const s = theirs.find((x) => x.key === f.key && x.kind === f.kind);
    if (!s || f.readOnly || s.readOnly || NEVER.has(f.key) || !KINDS.has(f.kind) || !fieldShown(s, source.props)) continue;
    const v = source.props[f.key] ?? null;
    if (!same(v, target.props[f.key])) attrs[f.key] = v;
  }
  return attrs;
}

/** The steps that give each of `targets` what `source` carries in `groups`: per target, in the order its module needs them. */
export function matchSteps(source: NodeDto, targets: readonly NodeDto[], groups: ReadonlySet<MatchGroup>, ctx: MatchContext): GripStep[] {
  const steps: GripStep[] = [];
  const type = groups.has('type') ? ctx.typeOf(source.id) : null;
  for (const t of targets) {
    if (t.id === source.id || !t.editable) continue;
    const ids = [t.id];
    if (type && t.type === source.type && ctx.typeOf(t.id)?.id !== type.id) steps.push(...(ctx.typeSteps(t.type, ids, type.id) ?? []));
    if (groups.has('fields')) {
      const attrs = fieldAttrs(source, t, ctx);
      if (Object.keys(attrs).length) steps.push({ command: 'element.update', args: { ids, attrs } });
      if (JOINERY.has(source.type) && JOINERY.has(t.type)) {
        // The frame's place across the wall (its own, else its type's) and the sill: where along the wall stays its own.
        const pos = source.props.position as Record<string, unknown> | null | undefined;
        if (!same(pos, t.props.position)) steps.push({ command: 'architecture.setJoineryPosition', args: pos ? { ids, ...pos } : { ids, clear: true } });
        const sill = ctx.sillOf(source.id), was = ctx.sillOf(t.id);
        if (sill != null && was != null && Math.abs(sill - was) > 1e-9) steps.push({ command: 'architecture.moveOpening', args: { ids, sill } });
      }
    }
    if (groups.has('params')) {
      const had = new Map(paramsOf(t).map((p) => [paramKey(p), p]));
      for (const p of paramsOf(source)) {
        // A standard set's property goes to its class only (Pset_DoorCommon is a door's).
        if (p.pset && t.type !== source.type) continue;
        const old = had.get(paramKey(p));
        if (old && same(old.value, p.value) && old.kind === p.kind) continue;
        steps.push({ command: 'element.setParam', args: { ids, name: p.name, kind: p.kind, value: p.value, ...(p.options ? { options: p.options } : {}), ...(p.pset ? { pset: p.pset } : {}) } });
      }
    }
    if (groups.has('phase') && ctx.phases && source.storeyId && t.storeyId && source.type !== 'IfcAnnotation' && t.type !== 'IfcAnnotation') {
      const created = source.props.phaseCreated ?? null, demolished = source.props.phaseDemolished ?? null;
      if (!same(created, t.props.phaseCreated) || !same(demolished, t.props.phaseDemolished)) steps.push({ command: 'phases.assign', args: { ids, created, demolished } });
    }
    if (groups.has('drafting') && isDraft(source) && isDraft(t)) {
      const args: Record<string, unknown> = { ids };
      const layer = typeof source.props.layer === 'string' ? source.props.layer : '0';
      if (layer !== (typeof t.props.layer === 'string' ? t.props.layer : '0')) args.layer = layer;
      if (!same(source.props.color, t.props.color)) args.color = source.props.color ?? null;
      const sh = source.props.hatch as Record<string, unknown> | undefined, th = t.props.hatch as Record<string, unknown> | undefined;
      if (sh && th) {
        const hatch = { pattern: sh.pattern ?? 'SOLID', scale: sh.scale ?? 1, angle: sh.angle ?? 0 };
        if (!same(hatch.pattern, th.pattern ?? 'SOLID') || !same(hatch.scale, th.scale ?? 1) || !same(hatch.angle, th.angle ?? 0)) args.hatch = hatch;
      }
      if (Object.keys(args).length > 1) steps.push({ command: 'drafting.updateDraft', args });
    }
    if (groups.has('name') && t.name !== source.name) steps.push({ command: 'element.rename', args: { id: t.id, name: source.name } });
  }
  return steps;
}
