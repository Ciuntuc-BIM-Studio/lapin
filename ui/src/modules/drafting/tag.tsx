/**
 * Tags (report #64): the TAG command with what it shows chosen in the command
 * line's bar (a field, any parameter, a tag type) and its leader; a tag's
 * grips (its head, the leader's elbow and end); and its properties: what it
 * shows, a template of its own, its type, the leader on or off.
 */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, GripView, Vec2 } from '../../platform/module';
import { execute, query, useSession } from '../../engine/session';
import { cadHost, type Step } from '../../viewport/cad/session';
import { Cell } from '../../shell/cells';
import { pt } from '../../viewport/gripKit';
import { formatLength, unitLabel } from '../../engine/unitsStore';

/** What the TAG command puts down: the field it shows (a parameter's name too), a tag type (its template wins), a leader or none. */
interface TagPrefs { field: string; tagType: string; leader: boolean }
export const useTagPrefs = create<TagPrefs>(() => ({ field: 'mark', tagType: '', leader: true }));

/** The fields offered first: every element's, then the parameters most tagged (each found in the user's own or a standard set). */
export const QUICK_FIELDS: [string, string][] = [
  ['mark', 'Mark'], ['name', 'Name'], ['class', 'Class'], ['type', 'Type'],
  ['FireRating', 'Fire rating'], ['AcousticRating', 'Acoustic rating'], ['ThermalTransmittance', 'Thermal transmittance'],
];

interface TagTypeDto { id: string; name: string; template: string; shape?: string }
/** The drafting standards' tag types a box can take (not a door's marker, not a room's stamp). */
export function useTagTypes(): TagTypeDto[] {
  const nodes = useSession((s) => s.nodes);
  const [types, setTypes] = useState<TagTypeDto[]>([]);
  useEffect(() => {
    let live = true;
    void query<{ tagTypes?: TagTypeDto[] }>('standards.get').then((b) => { if (live) setTypes((b?.tagTypes ?? []).filter((t) => (t.shape ?? 'box') === 'box')); });
    return () => { live = false; };
  }, [nodes]);
  return types;
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
    await execute('drafting.addTag', {
      viewId: h.viewId, element: id, at: [at.x, at.y],
      ...(p.tagType ? { tagType: p.tagType } : { field: p.field }),
      ...(p.leader ? {} : { leader: false }),
      ...(elbow ? { elbow: [elbow.x, elbow.y] } : {}),
    });
  };
  const pick = (): Step => ({
    prompt: `Click the element to tag [${shows()}: MARK/NAME/CLASS/TYPE/FIELD/LEADER ${useTagPrefs.getState().leader ? 'on' : 'off'}]`, want: 'select',
    keywords: words(),
    point: (p) => {
      const id = cadHost()?.pickAny(p);
      if (!id || id.includes('#')) return pick();
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
  const data = view?.mark?.data as { at?: unknown; elbow?: unknown; end?: unknown; from?: unknown } | undefined;
  const at = pt(data?.at) ?? pt(node.props.at);
  if (!at) return [];
  const id = node.id;
  const grips: Grip[] = [{
    key: 'at', kind: 'move', at, title: 'Drag: move the tag (the leader keeps pointing where it does)',
    step: (to: Vec2) => ({ command: 'drafting.updateTag', args: { id, at: [to.x, to.y] } }),
  }];
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

interface FieldDto { field: string; set: string | null; name: string; group: string; value: string }
const GROUPS: [string, string][] = [['element', 'Element'], ['type', 'Its type'], ['standard', 'Standard properties'], ['imported', 'From the file']];

/** What a tag of `element` can show, read again when the model changes. */
function useTagFields(element: string | undefined): FieldDto[] {
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
  const at = pt(node.props.at);
  const set = (args: Record<string, unknown>) => void execute('drafting.updateTag', { id: node.id, ...args });
  const known = fields.some((f) => f.field === field);
  // An opening's marker (a door's or window's tag type) is drawn across its wall: no box, no leader.
  const marker = !!tagType && !types.some((t) => t.id === tagType);
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
          {!!node.props.elbow && <button type="button" className="btn mini" onClick={() => set({ elbow: null })}>Straighten</button>}
          {!!node.props.end && <button type="button" className="btn mini" title="The leader points at the element's middle again" onClick={() => set({ end: null })}>On the element</button>}
        </dd>
      </>}
      {at && <><dt>Position</dt><dd className="pair"><Pair p={at} /></dd></>}
    </dl>
  );
}
