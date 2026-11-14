/**
 * A room in the inspector: its name (its function's until named), number, function (the values other rooms have
 * offered first), department, floor finish and height; its own colour and
 * hatch, over its scheme's; where its outline comes from. Its areas are in
 * the quantities below.
 */

import { useEffect, useState } from 'react';
import { Palette, RefreshCw, SquareDashed, Tag, X } from 'lucide-react';
import { RoomIcon } from '../../icons';
import { activate } from '../../platform/modules';
import type { ToolDef } from '../../platform/module';
import { outlineFor, room, roomOutline } from './roomTools';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { Cell, LengthCell, NumberField } from '../../shell/cells';
import { HatchSwatch } from '../core/HatchSwatch';
import { useSchemes } from './rooms';
import { RoomFinishField } from './RoomFinishes';
import { RoomOpeningsField } from './RoomOpenings';
import { sameKind } from '../../shell/params';

/** A text field with suggestions (an HTML datalist): commits on blur or Enter. */
function Suggest({ value, options, onCommit, id, placeholder }: { value: string; options: string[]; onCommit: (v: string) => void; id: string; placeholder?: string }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return (
    <span className="cell-wrap">
      <input className="cell" list={id} value={text} placeholder={placeholder} onChange={(e) => setText(e.target.value)} onBlur={() => { if (text !== value) onCommit(text); }}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); if (e.key === 'Escape') setText(value); }} />
      <datalist id={id}>{options.map((o) => <option key={o} value={o} />)}</datalist>
    </span>
  );
}

/** The material a room's finish shows on its floor (its top layer), which its floor finish inherits while it has none of its own (forum topic 84). */
export function finishFloorOf(finish: NodeDto | undefined): string | null {
  const floor = (finish?.props.spec as { floor?: { material?: string }[] } | undefined)?.floor;
  return Array.isArray(floor) && typeof floor[0]?.material === 'string' ? floor[0].material : null;
}

export function RoomSection({ node }: { node: NodeDto }) {
  const [s] = useSchemes();
  const finish = useSession((x) => (typeof node.props.finishType === 'string' ? x.nodes.get(node.props.finishType) : undefined));
  const inherited = finishFloorOf(finish);
  const [hatches, setHatches] = useState<string[]>([]);
  useEffect(() => { void query<{ name: string }[]>('hatch.list').then((r) => r && setHatches(r.map((h) => h.name))); }, []);
  const str = (k: string) => (typeof node.props[k] === 'string' ? (node.props[k] as string) : '');
  // With several rooms selected, each takes it (topic #29), but its number, which is its own.
  const update = (k: string, v: unknown) => void execute('element.update', { ids: k === 'number' ? [node.id] : sameKind(node), attrs: { [k]: v === '' ? null : v } });
  const known = (k: string, extra: string[] = []) => [...new Set([...extra, ...(s?.values[k] ?? []).map((v) => v.value)])];
  const hatch = str('fillHatch');
  // A room found from its walls that nothing closes round any more keeps its last outline, and says so.
  const lost = useSession((x) => x.render.get(node.id)?.diagnostics.some((d) => d.startsWith('Nothing closes round')) ?? false);
  // A room imported without a shape: the next Room click (or outline) on its storey's plan gives it one.
  const none = !node.props.auto && !(Array.isArray(node.props.contour) && node.props.contour.length >= 3);
  const give = (tool: ToolDef) => { outlineFor.id = node.id; activate(tool); };
  return (
    <div className="room-section">
      <dl className="fields">
        <dt title="Empty: as its function">Name</dt>
        <dd><Cell value={node.name} onCommit={(v) => void execute('element.rename', { id: node.id, name: v.trim() || str('function').trim() || 'Room' })} /></dd>
        <dt>Number</dt><dd><Cell value={str('number')} onCommit={(v) => update('number', v.trim())} /></dd>
        <dt title="What the room is used for: a colour scheme by function colours it">Function</dt>
        <dd><Suggest id={`fn-${node.id}`} value={str('function')} options={known('function', s?.functions ?? [])} onCommit={(v) => update('function', v.trim())} /></dd>
        <dt>Department</dt>
        <dd><Suggest id={`dep-${node.id}`} value={str('department')} options={known('department')} onCommit={(v) => update('department', v.trim())} /></dd>
        <dt title="Empty: the floor of its finish (its top layer), as its tag and formulas read {finishFloor}">Floor finish</dt>
        <dd><Suggest id={`ff-${node.id}`} value={str('finishFloor')} placeholder={inherited ?? undefined} options={known('finishFloor', inherited ? [inherited] : [])} onCommit={(v) => update('finishFloor', v.trim())} /></dd>
        <dt title="What lines the room: its floor, walls and ceiling laid by Finishes → Finish room">Finish</dt>
        <dd><RoomFinishField node={node} /></dd>
        {typeof node.props.finishType === 'string' && (
          <>
            <dt title="Plans hatch the room's floor finish as its type says (a plan seen from below, its ceiling's)">Floor hatch</dt>
            <dd><input type="checkbox" data-field="room-finish-hatch" checked={node.props.finishHatch !== false} onChange={(e) => update('finishHatch', e.target.checked ? null : false)} /></dd>
          </>
        )}
        <dt title="Empty: the storey's height">Height</dt>
        <dd><LengthCell metres={typeof node.props.height === 'number' ? node.props.height : null} placeholder="the storey's" onCommit={(m) => update('height', m)} /></dd>
        <dt title="How far above its storey's level the room starts (a gallery, a raised floor)">Base offset</dt>
        <dd><LengthCell metres={typeof node.props.baseOffset === 'number' ? node.props.baseOffset : null} placeholder="0" onCommit={(m) => update('baseOffset', m)} /></dd>
        <dt title="The room's own colour, over its colour scheme's">Own colour</dt>
        <dd>
          <input type="color" value={str('fillColor') || '#ffffff'} onChange={(e) => update('fillColor', e.target.value)} />
          {str('fillColor') && <button type="button" className="icon mini" title="As its scheme says" onClick={() => update('fillColor', null)}><X size={10} /></button>}
        </dd>
        {/* The room's own opacity over its scheme entry's (forum #79): empty, its scheme's. */}
        <dt title="How much of the room's colour covers the plan under it, 0–100 %; empty: as its scheme says">Own opacity</dt>
        <dd><NumberField className="narrow" testId="room-fill-opacity" min={0} max={100} suffix="%" placeholder="as its scheme" value={typeof node.props.fillOpacity === 'number' ? node.props.fillOpacity : null}
          onCommit={(v) => update('fillOpacity', v)} /></dd>
        <dt title="The room's own hatch, over its colour scheme's">Own hatch</dt>
        <dd className="row">
          <select value={hatch} onChange={(e) => update('fillHatch', e.target.value)}>
            <option value="">as its scheme</option>
            {hatches.map((h) => <option key={h} value={h}>{h}</option>)}
          </select>
          {hatch && <NumberField className="narrow" min={0.1} title="Hatch scale" value={typeof node.props.hatchScale === 'number' ? node.props.hatchScale : 1} onCommit={(v) => { if (v != null) update('hatchScale', v); }} />}
          {hatch && <HatchSwatch hatch={hatch} scale={typeof node.props.hatchScale === 'number' ? node.props.hatchScale : 1} fill={str('fillColor') || null} size={24} />}
        </dd>
        <dt>Outline</dt>
        <dd className={lost || none ? 'small warn' : 'muted small'}>{node.props.auto ? (lost ? 'no longer enclosed: it keeps its last outline' : 'found from the walls round its point') : none ? 'none: its IFC file gave it no shape' : node.props.imported ? 'from its IFC file' : 'drawn'}</dd>
      </dl>
      <RoomOpeningsField node={node} />
      <div className="row">
        <button type="button" className="btn small" onClick={() => useUi.setState({ dialog: 'roomColors' })}><Palette size={12} />Room colours…</button>
        <button type="button" className="btn small" title="What the rooms' stamps show in plan" onClick={() => useUi.setState({ dialog: 'roomTag' })}><Tag size={12} />Room tag…</button>
        {none && <button type="button" className="btn small" title="Then click inside its walls on its storey's plan" onClick={() => give(room)}><RoomIcon size={12} />Find by a click</button>}
        {none && <button type="button" className="btn small" title="Then draw its corners on its storey's plan" onClick={() => give(roomOutline)}><SquareDashed size={12} />Draw outline</button>}
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
