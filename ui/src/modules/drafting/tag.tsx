/**
 * Tags (report #64): the TAG command with what it shows chosen in the command
 * line's bar (a field, any parameter, a tag type) and its leader; a tag's
 * grips (its head, the leader's elbow and end; an elbow leader's knee, decision
 * 330); and its properties: what it shows, a template of its own, its type, the
 * leader on or off and its shape.
 */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, GripView, Vec2 } from '../../platform/module';
import { execute, query, useSession } from '../../engine/session';
import { cadHost, type Step } from '../../viewport/cad/session';
import { Cell } from '../../shell/cells';
import { pt } from '../../viewport/gripKit';
import { useUi } from '../../app/store';
import { isPart, partGrips } from '../../platform/labelParts';
import { formatLength, unitLabel } from '../../engine/unitsStore';
import { leaderShapeOf, rotationFor, snapKnee, snapTurn, tagLeaderShape, turnAngle } from './tagLeader';

/** What the TAG command puts down: the field it shows (a parameter's name too), a tag type (its template wins), a leader or none. */
interface TagPrefs { field: string; tagType: string; leader: boolean }
export const useTagPrefs = create<TagPrefs>(() => ({ field: 'mark', tagType: '', leader: true }));

/** The fields offered first: every element's, then the parameters most tagged (each found in the user's own or a standard set). */
export const QUICK_FIELDS: [string, string][] = [
  ['mark', 'Mark'], ['name', 'Name'], ['class', 'Class'], ['type', 'Type'],
  ['FireRating', 'Fire rating'], ['AcousticRating', 'Acoustic rating'], ['ThermalTransmittance', 'Thermal transmittance'],
];

interface TagTypeDto { id: string; name: string; template: string; shape?: string; classes?: string[]; leaderShape?: string }
/** The drafting standards' tag types a box can take, and the build-up tags (not a door's marker, not a room's stamp). */
export function useTagTypes(): TagTypeDto[] {
  const nodes = useSession((s) => s.nodes);
  const [types, setTypes] = useState<TagTypeDto[]>([]);
  useEffect(() => {
    let live = true;
    void query<{ tagTypes?: TagTypeDto[] }>('standards.get').then((b) => { if (live) setTypes((b?.tagTypes ?? []).filter((t) => (t.shape ?? 'box') === 'box' || t.shape === 'buildup')); });
    return () => { live = false; };
  }, [nodes]);
  return types;
}

/** A tag type of the standards by its id. */
async function tagTypeById(id: string): Promise<TagTypeDto | undefined> {
  const b = await query<{ tagTypes?: TagTypeDto[] }>('standards.get');
  return b?.tagTypes?.find((t) => t.id === id);
}

/** Whether a build-up tag type tags an element of class `cls`: one of its classes, or any when it names none. */
export function buildupTags(type: Pick<TagTypeDto, 'classes'>, cls: string | undefined): boolean {
  return !type.classes?.length || (!!cls && type.classes.includes(cls));
}

/** Asks for a parameter's name; null when none is given. */
function askField(): string | null {
  const t = window.prompt('The parameter to show: its name (Fire rating) or a standard one (Pset_WallCommon.FireRating)');
  return t?.trim().replace(/^\{|\}$/g, '').trim() || null;
}

/** The TAG command's bar: what the tag shows, its type, its leader (data-tag-* for the guide's pictures). */
export function TagOptions() {
  const { field, tagType, leader } = useTagPrefs();
  const types = useTagTypes();
  const known = QUICK_FIELDS.some(([k]) => k === field);
  return (
    <>
      <select className={`cad-layer${field !== 'mark' && !tagType ? ' on' : ''}`} value={tagType ? '' : field} data-tag-field title="What the tag shows: a field of the element, or any of its parameters"
        disabled={!!tagType} onChange={(e) => {
          if (e.target.value === '*') { const f = askField(); if (f) useTagPrefs.setState({ field: f }); return; }
          useTagPrefs.setState({ field: e.target.value });
        }}>
        {QUICK_FIELDS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
        {!known && !tagType && <option value={field} data-no-i18n>{field}</option>}
        <option value="*">Another parameter…</option>
        {tagType && <option value="">by its tag type</option>}
      </select>
      <select className={`cad-layer${tagType ? ' on' : ''}`} value={tagType} data-tag-type title="A tag type of the drafting standards: its template, its text style" onChange={(e) => useTagPrefs.setState({ tagType: e.target.value })}>
        <option value="">no tag type</option>
        {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
      </select>
      <button type="button" className={`cad-toggle${leader ? ' on' : ''}`} data-tag-leader title="A leader from the tag to the element" onClick={() => useTagPrefs.setState({ leader: !leader })}>LEADER</button>
    </>
  );
}

/**
 * TAG: click an element, then where the tag goes (E first: the leader's elbow, then the tag). What it shows is chosen in the
 * command line's bar or by a word: MARK, NAME, CLASS, TYPE, FIELD (any parameter by its name), LEADER (on or off).
 */
export function tagCommand(): Step {
  const words = (): Record<string, () => Step> => ({
    ...Object.fromEntries(['MARK', 'NAME', 'CLASS', 'TYPE'].map((k) => [k, () => { useTagPrefs.setState({ field: k.toLowerCase(), tagType: '' }); return pick(); }])),
    FIELD: () => ({
      prompt: 'The parameter to show (Fire rating, Pset_WallCommon.FireRating)', want: 'text',
      text: (s: string) => { const f = s.trim().replace(/^\{|\}$/g, '').trim(); if (f) useTagPrefs.setState({ field: f, tagType: '' }); return pick(); },
      enter: () => pick(),
    }),
    LEADER: () => { useTagPrefs.setState({ leader: !useTagPrefs.getState().leader }); return pick(); },
  });
  const shows = () => { const p = useTagPrefs.getState(); return p.tagType ? `type ${p.tagType}` : p.field; };
  const put = async (id: string, at: Vec2, elbow?: Vec2) => {
    const h = cadHost();
    if (!h) return;
    const p = useTagPrefs.getState();
    // A build-up tag type tags what has layers (its classes: walls, floors, roofs, ceilings); anything else gets the field.
    const type = p.tagType ? (await tagTypeById(p.tagType)) : undefined;
    const cls = useSession.getState().nodes.get(id)?.type;
    const typed = !!p.tagType && !(type?.shape === 'buildup' && !buildupTags(type, cls));
    await execute('drafting.addTag', {
      viewId: h.viewId, element: id, at: [at.x, at.y],
      ...(typed ? { tagType: p.tagType } : { field: p.field }),
      ...(p.leader ? {} : { leader: false }),
      ...(elbow ? { elbow: [elbow.x, elbow.y] } : {}),
    });
  };
  const pick = (): Step => ({
    prompt: `Click the element to tag [${shows()}: MARK/NAME/CLASS/TYPE/FIELD/LEADER ${useTagPrefs.getState().leader ? 'on' : 'off'}]`, want: 'select',
    keywords: words(),
    point: async (p) => {
      const id = cadHost()?.pickAny(p);
      if (!id || id.includes('#')) return pick();
      // An elbow leader (decision 331): the click after the element is the knee, the text beside it.
      const tagType = useTagPrefs.getState().tagType;
      if (useTagPrefs.getState().leader && leaderShapeOf(tagType ? await tagTypeById(tagType) : null) === 'elbow') {
        return {
          prompt: 'Where the leader bends (the text goes beside it)', want: 'point', base: p,
          preview: (c: Vec2) => {
            const w = Math.max(Math.abs(c.x - p.x), Math.abs(c.y - p.y)) * 0.6 || 1;
            const far = { x: c.x + (c.x >= p.x ? w : -w), y: c.y };
            return [{ type: 'line' as const, a: p, b: c }, { type: 'line' as const, a: c, b: far }];
          },
          point: async (at: Vec2) => { await put(id, at); return pick(); },
        };
      }
      const head = (elbow?: Vec2): Step => ({
        prompt: elbow ? 'Where the tag goes' : 'Where the tag goes [Elbow]', want: 'point', base: elbow ?? p,
        preview: (c) => (elbow ? [{ type: 'line', a: p, b: elbow }, { type: 'line', a: elbow, b: c }] : [{ type: 'line', a: p, b: c }]),
        ...(elbow ? {} : { keywords: { E: () => ({ prompt: 'The leader\'s elbow', want: 'point' as const, base: p, preview: (c: Vec2) => [{ type: 'line' as const, a: p, b: c }], point: (k: Vec2) => head(k) }) } }),
        point: async (at) => { await put(id, at, elbow); return pick(); },
      });
      return head();
    },
    enter: () => null,
  });
  return pick();
}

/**
 * A tag's grips, where the view drew it: its head (dragged: the tag moves, the leader's end stays), the leader's elbow
 * (dragged: it bends there; Ctrl/⌘-click: straight again) or, without one, the leader's middle (dragged: an elbow there),
 * and the leader's end (dragged: it points there; Ctrl/⌘-click: back on the element's middle).
 */
export function tagGrips(node: NodeDto, view?: GripView): Grip[] {
  if (node.props.predefinedType !== 'TAG') return [];
  const data = view?.mark?.data as { at?: unknown; elbow?: unknown; end?: unknown; from?: unknown; marker?: boolean; parts?: unknown[] } | undefined;
  // A door's or window's marker (decision 292): a grip for each of its parts. Several markers of one view selected take the same change.
  if (data?.marker) {
    const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
    const ids = markersAlike(node);
    return partGrips((data.parts ?? []).filter(isPart), (args) => ({ command: 'drafting.updateTag', args: { ids, ...args } })).map((g) => ({ ...g, view: viewId }));
  }
  const at = pt(data?.at) ?? pt(node.props.at);
  if (!at) return [];
  const id = node.id;
  if ((data as { shape?: string } | undefined)?.shape === 'elbow') return elbowGrips(node, data as ElbowData, at);
  const grips: Grip[] = [{
    key: 'at', kind: 'move', at, title: 'Drag: move the tag (the leader keeps pointing where it does)',
    step: (to: Vec2) => ({ command: 'drafting.updateTag', args: { id, at: [to.x, to.y] } }),
  }];
  const turn = turnGrip(node, data as { angle?: number; centre?: unknown; turn?: unknown; base?: number } | undefined);
  if (turn) grips.push(turn);
  const end = pt(data?.end), elbow = pt(data?.elbow), from = pt(data?.from);
  if (end) {
    grips.push({
      key: 'end', kind: 'end', at: end, title: 'Drag: the leader points here · Ctrl/⌘-click: back on the element\'s middle',
      step: (to: Vec2) => ({ command: 'drafting.updateTag', args: { id, end: [to.x, to.y] } }),
      remove: () => (node.props.end ? { command: 'drafting.updateTag', args: { id, end: null } } : null),
    });
    if (elbow) grips.push({
      key: 'elbow', kind: 'vertex', at: elbow, title: 'Drag: the leader bends here · Ctrl/⌘-click: straight again',
      step: (to: Vec2) => ({ command: 'drafting.updateTag', args: { id, elbow: [to.x, to.y] } }),
      remove: () => ({ command: 'drafting.updateTag', args: { id, elbow: null } }),
    });
    else if (from) grips.push({
      key: 'bend', kind: 'edgeMid', at: { x: (from.x + end.x) / 2, y: (from.y + end.y) / 2 }, title: 'Drag: bend the leader here',
      step: (to: Vec2) => ({ command: 'drafting.updateTag', args: { id, elbow: [to.x, to.y] } }),
    });
  }
  const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  return grips.map((g) => ({ ...g, view: viewId }));
}

interface ElbowData { knee?: unknown; head?: unknown; end?: unknown; side?: number }

/**
 * An elbow leader's grips (decision 331): the text (dragged: the tag moves, its knee with it, the leader's end stays), the
 * knee (dragged: kept to 30°, 45°, 60° or 90° from the end, Shift frees it; past the end the text goes to that side; with
 * leaders per layer the column of knees moves), and the leader's end as a straight leader's.
 */
function elbowGrips(node: NodeDto, data: ElbowData & { angle?: number; centre?: unknown; turn?: unknown; base?: number }, at: Vec2): Grip[] {
  const id = node.id;
  const knee = pt(data.knee) ?? at, head = pt(data.head) ?? at, end = pt(data.end);
  const grips: Grip[] = [{
    key: 'at', kind: 'move', at: head, title: 'Drag: move the tag with its knee (the leader keeps pointing where it does)',
    step: (to: Vec2) => ({ command: 'drafting.updateTag', args: { id, at: [knee.x + to.x - head.x, knee.y + to.y - head.y] } }),
  }];
  grips.push({
    key: 'knee', kind: 'vertex', at: knee, title: 'Drag: the leader bends here, at 30°, 45°, 60° or 90° (Shift: any angle); past the end the text goes to that side',
    ...(end ? { constrain: (to: Vec2, free: boolean) => snapKnee(end, to, free) } : {}),
    step: (to: Vec2) => ({ command: 'drafting.updateTag', args: { id, at: [to.x, to.y] } }),
  });
  if (end) grips.push({
    key: 'end', kind: 'end', at: end, title: 'Drag: the leader points here · Ctrl/⌘-click: back on the element\'s middle',
    step: (to: Vec2) => ({ command: 'drafting.updateTag', args: { id, end: [to.x, to.y] } }),
    remove: () => (node.props.end ? { command: 'drafting.updateTag', args: { id, end: null } } : null),
  });
  const turn = turnGrip(node, data as { angle?: number; centre?: unknown; turn?: unknown; base?: number });
  if (turn) grips.push(turn);
  const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  return grips.map((g) => ({ ...g, view: viewId }));
}

/**
 * A tag's turning grip (report #129): over its text, square to its baseline. Dragged, the text turns to follow the cursor, kept
 * to 15° steps (45° and 90° among them; Shift frees it), and from the element's direction when the tag is aligned to it;
 * Ctrl/⌘-click: upright again (or just aligned).
 */
function turnGrip(node: NodeDto, data: { angle?: number; centre?: unknown; turn?: unknown; base?: number } | undefined): Grip | null {
  const centre = pt(data?.centre), at = pt(data?.turn);
  if (!centre || !at) return null;
  const id = node.id;
  const base = typeof data?.base === 'number' ? data.base : undefined;
  return {
    key: 'turn', kind: 'rotate', at, title: 'Drag: turn the tag, in steps of 15° (Shift: any angle) · Ctrl/⌘-click: upright again',
    constrain: (to, free) => snapTurn(centre, to, free),
    step: (to) => ({ command: 'drafting.updateTag', args: { id, rotation: rotationFor(turnAngle(centre, to), base) } }),
    remove: () => (node.props.rotation ? { command: 'drafting.updateTag', args: { id, rotation: null } } : null),
  };
}

/** The selected markers of the same view as `node` (it first): what a part's grip changes. */
function markersAlike(node: NodeDto): string[] {
  const nodes = useSession.getState().nodes;
  const others = useUi.getState().selected.filter((id) => id !== node.id).filter((id) => {
    const n = nodes.get(id);
    const e = typeof n?.props.element === 'string' ? nodes.get(n.props.element) : undefined;
    return n?.props.predefinedType === 'TAG' && n.props.viewId === node.props.viewId && (e?.type === 'IfcDoor' || e?.type === 'IfcWindow');
  });
  return [node.id, ...others];
}

interface PartEdit { d?: number[]; rot?: number; ext?: number }
interface MarkerTypeDto { id: string; template?: string; inner?: string }

/** How many lines a template writes ("|" starts a new one). */
const lineCount = (t: string | undefined) => (t ? t.split('|').length : 0);

/**
 * A door's or window's marker part by part (decision 292): each text line's turn (typed; its grips in the plan move and
 * turn it), each end of its line's reach, each put back on its own or all at once.
 */
function MarkerParts({ node, tagType }: { node: NodeDto; tagType: string }) {
  const nodes = useSession((s) => s.nodes);
  const [type, setType] = useState<MarkerTypeDto | null>(null);
  useEffect(() => {
    let live = true;
    void query<{ tagTypes?: MarkerTypeDto[] }>('standards.get').then((b) => { if (live) setType(b?.tagTypes?.find((t) => t.id === tagType) ?? null); });
    return () => { live = false; };
  }, [tagType, nodes]);
  const parts = (node.props.parts ?? {}) as Record<string, PartEdit>;
  const set = (args: Record<string, unknown>) => void execute('drafting.updateTag', { id: node.id, ...args });
  const reset = (part: string) => <button type="button" className="btn mini" title="Back as by default" data-marker-reset={part} onClick={() => set({ part, reset: true })}>↺</button>;
  const num = (part: string, field: 'rot' | 'ext', value: number | undefined, title: string) => (
    <input className="cell num" key={`${part}${field}${value ?? ''}`} defaultValue={value == null ? '' : String(value)} placeholder="0" title={title} data-marker-part={`${part}.${field}`} style={{ width: 64 }}
      onBlur={(e) => {
        const t = e.target.value.trim().replace(',', '.');
        const n = t === '' ? null : Number(t);
        if (n !== null && !Number.isFinite(n)) return;
        if ((n ?? 0) !== (value ?? 0)) set({ part, [field]: n === 0 ? null : n });
      }} />
  );
  const texts: [string, string, number][] = [
    ...Array.from({ length: lineCount(type?.template) }, (_, i): [string, string, number] => [`o${i}`, 'Outer line', i + 1]),
    ...Array.from({ length: lineCount(type?.inner) }, (_, i): [string, string, number] => [`i${i}`, 'Inner line', i + 1]),
  ];
  const turnTitle = 'Its turn from the way the tag type lays it, in degrees; it is always drawn upright';
  const reachTitle = 'How much further this end of the line reaches, paper mm (negative: shorter)';
  return (
    <>
      <dt title={'Each text and each end of the marker\'s line has its grips in the plan: drag to move, turn or lengthen; Ctrl/⌘-click a grip puts that part back'}>Marker</dt>
      <dd><button type="button" className="btn mini" data-marker-reset-all disabled={!node.props.parts} onClick={() => set({ reset: true })}>Put the label back</button></dd>
      {texts.map(([key, label, n]) => (
        <span key={key} style={{ display: 'contents' }}>
          <dt><span>{label}</span><span data-no-i18n style={{ marginLeft: '0.35em' }}>{n}</span></dt>
          <dd style={{ display: 'flex', gap: 6, alignItems: 'center' }}>{num(key, 'rot', parts[key]?.rot, turnTitle)}<span className="muted">°</span>{parts[key] && reset(key)}</dd>
        </span>
      ))}
      {([['b', 'Outer end'], ['a', 'Inner end']] as const).map(([key, label]) => (
        <span key={key} style={{ display: 'contents' }}>
          <dt>{label}</dt>
          <dd style={{ display: 'flex', gap: 6, alignItems: 'center' }}>{num(key, 'ext', parts[key]?.ext, reachTitle)}<span className="muted">mm</span>{parts[key] && reset(key)}</dd>
        </span>
      ))}
    </>
  );
}

export interface FieldDto { field: string; set: string | null; name: string; group: string; value: string }
/** The groups of fields a tag offers: the element's own, then those it reads through its scopes (decision 550: {storey.code}, {project.number}). */
export const GROUPS: [string, string][] = [
  ['element', 'Element'], ['type', 'Its type'], ['standard', 'Standard properties'], ['imported', 'From the file'],
  ['storey', 'Its storey'], ['building', 'Its building'], ['site', 'Its site'], ['project', 'The project'], ['group', 'Its group'],
  ['assembly', 'Its assembly'], ['host', 'Its host'], ['room', 'Its room'], ['fromRoom', 'Room on the exterior side'], ['toRoom', 'Room on the interior side'],
];

/** What a tag of `element` can show, read again when the model changes. */
export function useTagFields(element: string | undefined): FieldDto[] {
  const nodes = useSession((s) => s.nodes);
  const [fields, setFields] = useState<FieldDto[]>([]);
  useEffect(() => {
    let live = true;
    if (element) void query<FieldDto[]>('drafting.tagFields', { element }).then((r) => { if (live) setFields(Array.isArray(r) ? r : []); });
    return () => { live = false; };
  }, [element, nodes]);
  return fields;
}

const Pair = ({ p }: { p: Vec2 }) => <span className="num">{formatLength(p.x)} · {formatLength(p.y)} {unitLabel()}</span>;

/** A tag in Properties: what it tags, what it shows (any of the element's parameters), a template of its own, its type, its leader. */
export function TagFields({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const element = typeof node.props.element === 'string' ? node.props.element : undefined;
  const target = element ? nodes.get(element) : undefined;
  const fields = useTagFields(target ? element : undefined);
  const types = useTagTypes();
  const tagType = typeof node.props.tagType === 'string' ? node.props.tagType : '';
  const field = String(node.props.field ?? 'mark');
  const template = typeof node.props.template === 'string' ? node.props.template : '';
  const leader = node.props.leader !== false;
  const rotation = Number(node.props.rotation ?? 0) || 0;
  const align = node.props.align === true;
  const at = pt(node.props.at);
  const set = (args: Record<string, unknown>) => void execute('drafting.updateTag', { id: node.id, ...args });
  const known = fields.some((f) => f.field === field);
  // An opening's marker (a door's or window's tag type) is drawn across its wall: no box, no leader.
  const marker = !!tagType && !types.some((t) => t.id === tagType);
  const type = types.find((t) => t.id === tagType);
  const typeShape = leaderShapeOf(type);
  const shape = tagLeaderShape(node.props, type);
  return (
    <dl className="fields" data-tag-fields>
      <dt>Tagged element</dt><dd><span className="num">{target ? `${target.name} (${target.type})` : '—'}</span></dd>
      {!marker && <>
        <dt title="A field of the element, a parameter of its own or of its type, a standard property (filled or not: the tag follows when it is)">Shows</dt>
        <dd><select className="cell" value={template || tagType ? '' : field} data-tag-shows onChange={(e) => {
          if (e.target.value === '*') { const f = askField(); if (f) set({ field: f }); return; }
          if (e.target.value) set({ field: e.target.value });
        }}>
          {(template || tagType) && <option value="">{template ? 'its own text' : 'its tag type'}</option>}
          {!known && !template && !tagType && <option value={field} data-no-i18n={QUICK_FIELDS.some(([k]) => k === field) ? undefined : true}>{QUICK_FIELDS.find(([k]) => k === field)?.[1] ?? field}</option>}
          {GROUPS.map(([g, label]) => {
            const list = fields.filter((f) => f.group === g);
            return list.length ? (
              <optgroup key={g} label={label}>
                {list.map((f) => <option key={f.field} value={f.field} data-no-i18n>{(QUICK_FIELDS.find(([k]) => k === f.field)?.[1] ?? (f.set ? `${f.set} · ${f.name}` : f.name)) + (f.value ? ` = ${f.value}` : '')}</option>)}
              </optgroup>
            ) : null;
          })}
          <option value="*">Another parameter…</option>
        </select></dd>
        <dt title="Its own text: {fields} read from the element, | starts a new line (empty: what Shows or its type says)">Text</dt>
        <dd><Cell value={template} placeholder="{mark}|{FireRating}" onCommit={(t) => set({ template: t })} testId="tag-template" /></dd>
        <dt title="A tag type of the drafting standards (Manage → Drafting standards → Tags): its template and text style">Tag type</dt>
        <dd><select className="cell" value={tagType} onChange={(e) => set({ tagType: e.target.value || null })}>
          <option value="">—</option>
          {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select></dd>
        <dt title="A line from the tag to the element; drag its grips to bend it or point it elsewhere">Leader</dt>
        <dd style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          <input type="checkbox" checked={leader} data-tag-leader-on onChange={(e) => set({ leader: e.target.checked })} />
          {!!node.props.elbow && shape === 'straight' && <button type="button" className="btn mini" onClick={() => set({ elbow: null })}>Straighten</button>}
          {!!node.props.end && <button type="button" className="btn mini" title="The leader points at the element's middle again" onClick={() => set({ end: null })}>On the element</button>}
        </dd>
        <dt title="The tag's text turned, in degrees from level (it is always drawn upright, never upside down); drag its turning grip in the plan, in steps of 15°">Rotation</dt>
        <dd style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          <input className="cell num" key={`rot${rotation}`} defaultValue={rotation ? String(rotation) : ''} placeholder="0" style={{ width: 64 }} data-tag-rotation
            onBlur={(e) => {
              const v = e.target.value.trim().replace(',', '.');
              const n = v === '' ? 0 : Number(v);
              if (Number.isFinite(n) && n !== rotation) set({ rotation: n === 0 ? null : n });
            }} />
          <span className="muted">°</span>
          <label style={{ display: 'flex', gap: 4, alignItems: 'center' }} title="Follow the direction of the element it tags (a wall, a beam, a long edge), turning with it; the rotation is added to it">
            <input type="checkbox" checked={align} data-tag-align onChange={(e) => set({ align: e.target.checked })} />
            <span>Align to element</span>
          </label>
        </dd>
        <dt title="Straight from a box round the text, or an elbow: an oblique line to a knee, then along under the text (as the tag type says, unless the tag says its own)">Leader shape</dt>
        <dd><select className="cell" data-tag-leader-shape value={typeof node.props.leaderShape === 'string' ? node.props.leaderShape : ''}
          onChange={(e) => set({ leaderShape: e.target.value || null })}>
          <option value="">{typeShape === 'straight' ? 'as its type (straight)' : 'as its type (elbow)'}</option>
          <option value="elbow">elbow, under the text</option>
          <option value="straight">straight, from a box</option>
        </select></dd>
      </>}
      {marker && <MarkerParts node={node} tagType={tagType} />}
      {at && !marker && <><dt>Position</dt><dd className="pair"><Pair p={at} /></dd></>}
    </dl>
  );
}
