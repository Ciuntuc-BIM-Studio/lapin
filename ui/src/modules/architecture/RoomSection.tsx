/**
 * A room in the inspector: its number, function (the values other rooms have
 * offered first), department, floor finish and height; its own colour and
 * hatch, over its scheme's; where its outline comes from. Its areas are in
 * the quantities below.
 */

import { useEffect, useState } from 'react';
import { Palette, RefreshCw, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { Cell, LengthCell } from '../../shell/cells';
import { HatchSwatch } from '../core/HatchSwatch';
import { useSchemes } from './rooms';

/** A text field with suggestions (an HTML datalist): commits on blur or Enter. */
function Suggest({ value, options, onCommit, id }: { value: string; options: string[]; onCommit: (v: string) => void; id: string }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return (
    <span className="cell-wrap">
      <input className="cell" list={id} value={text} onChange={(e) => setText(e.target.value)} onBlur={() => { if (text !== value) onCommit(text); }}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); if (e.key === 'Escape') setText(value); }} />
      <datalist id={id}>{options.map((o) => <option key={o} value={o} />)}</datalist>
    </span>
  );
}

export function RoomSection({ node }: { node: NodeDto }) {
  const [s] = useSchemes();
  const [hatches, setHatches] = useState<string[]>([]);
  useEffect(() => { void query<{ name: string }[]>('hatch.list').then((r) => r && setHatches(r.map((h) => h.name))); }, []);
  const str = (k: string) => (typeof node.props[k] === 'string' ? (node.props[k] as string) : '');
  const update = (k: string, v: unknown) => void execute('element.update', { id: node.id, attrs: { [k]: v === '' ? null : v } });
  const known = (k: string, extra: string[] = []) => [...new Set([...extra, ...(s?.values[k] ?? []).map((v) => v.value)])];
  const hatch = str('fillHatch');
  // A room found from its walls that nothing closes round any more keeps its last outline, and says so.
  const lost = useSession((x) => x.render.get(node.id)?.diagnostics.some((d) => d.startsWith('Nothing closes round')) ?? false);
  return (
    <div className="room-section">
      <dl className="fields">
        <dt>Number</dt><dd><Cell value={str('number')} onCommit={(v) => update('number', v.trim())} /></dd>
        <dt title="What the room is used for: a colour scheme by function colours it">Function</dt>
        <dd><Suggest id={`fn-${node.id}`} value={str('function')} options={known('function', s?.functions ?? [])} onCommit={(v) => update('function', v.trim())} /></dd>
        <dt>Department</dt>
        <dd><Suggest id={`dep-${node.id}`} value={str('department')} options={known('department')} onCommit={(v) => update('department', v.trim())} /></dd>
        <dt>Floor finish</dt>
        <dd><Suggest id={`ff-${node.id}`} value={str('finishFloor')} options={known('finishFloor')} onCommit={(v) => update('finishFloor', v.trim())} /></dd>
        <dt title="Empty: the storey's height">Height</dt>
        <dd><LengthCell metres={typeof node.props.height === 'number' ? node.props.height : null} placeholder="the storey's" onCommit={(m) => update('height', m)} /></dd>
        <dt title="The room's own colour, over its colour scheme's">Own colour</dt>
        <dd>
          <input type="color" value={str('fillColor') || '#ffffff'} onChange={(e) => update('fillColor', e.target.value)} />
          {str('fillColor') && <button type="button" className="icon mini" title="As its scheme says" onClick={() => update('fillColor', null)}><X size={10} /></button>}
        </dd>
        <dt title="The room's own hatch, over its colour scheme's">Own hatch</dt>
        <dd className="row">
          <select value={hatch} onChange={(e) => update('fillHatch', e.target.value)}>
            <option value="">as its scheme</option>
            {hatches.map((h) => <option key={h}>{h}</option>)}
          </select>
          {hatch && <input className="num narrow" type="number" min={0.1} step={0.1} title="Hatch scale" value={typeof node.props.hatchScale === 'number' ? node.props.hatchScale : 1} onChange={(e) => { const v = Number(e.target.value); if (v > 0) update('hatchScale', v); }} />}
          {hatch && <HatchSwatch hatch={hatch} scale={typeof node.props.hatchScale === 'number' ? node.props.hatchScale : 1} fill={str('fillColor') || null} size={24} />}
        </dd>
        <dt>Outline</dt>
        <dd className={lost ? 'small warn' : 'muted small'}>{node.props.auto ? (lost ? 'no longer enclosed: it keeps its last outline' : 'found from the walls round its point') : 'drawn'}</dd>
      </dl>
      <div className="row">
        <button type="button" className="btn small" onClick={() => useUi.setState({ dialog: 'roomColors' })}><Palette size={12} />Room colours…</button>
        {Boolean(node.props.auto) && <button type="button" className="btn small" title="Store the outline the room finds now: what it keeps should its walls go" onClick={() => void execute('architecture.updateRooms', { ids: [node.id] })}><RefreshCw size={12} />Update outline</button>}
      </div>
    </div>
  );
}

/** A plan view's room colours in its properties: the scheme it fills its rooms with. */
export function RoomViewSection({ node }: { node: NodeDto }) {
  const [s] = useSchemes(node.id);
  if (node.props.kind !== 'plan' || !s) return null;
  return (
    <dl className="fields">
      <dt title="The colour scheme this plan fills its rooms with; none by default">Room colours</dt>
      <dd className="row">
        <select value={s.viewScheme ?? ''} onChange={(e) => void execute('architecture.viewColorScheme', { viewId: node.id, schemeId: e.target.value || null })}>
          <option value="">none</option>
          {s.schemes.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
        <button type="button" className="icon mini" title="Room colours…" onClick={() => useUi.setState({ dialog: 'roomColors' })}><Palette size={12} /></button>
      </dd>
    </dl>
  );
}
