/**
 * Channels, standalone kerbs and accessories along a line (site grading T6, docs/plan-site-grading.md; forum 230 §5): the
 * Channel and Kerb tools (drawn on a polyline, or along a line picked: a hardscape's edge, a path's side, a feature line…), the
 * Along a line tool for accessories, their sections in Properties and their grips. The pure parts are in channels.ts; the engine
 * does the work (civil.addChannel, civil.updateChannel, civil.addKerb, civil.updateKerb, element.array).
 */

import { create } from 'zustand';
import { Droplets, Lamp, Ruler } from 'lucide-react';
import type { Grip, ToolDef, GripView } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { activate } from '../../platform/modules';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { formatLength, levelUnit, lengthText, parseLength, parseLevel } from '../../engine/unitsStore';
import { MaterialPicker } from '../core/MaterialPicker';
import { arrayAlongLine } from '../core/arrayTool';
import { ProfileButton } from './ProfileButton';
import { CommitInput } from './FeatureLineSection';
import { Mm } from './SiteSections';
import { KerbSection } from './hardscapeTools';
import { ArcsToggle, lineGrips, risesOf } from './featureLineTools';
import { toggleOutlineArcs } from '../../viewport/outlineShape';
import { payloadOf } from './featureLines';
import { CHANNEL_KINDS, FOLLOW_TYPES, followArg, followsName, gradeShown, invertArgs, kindDefault, movedOutlet, readGrade, sizeFields, withOutlet, withoutOutlet, dropEnds, dropRows, nextDrop, type DropRow, type InvertMode, type SizeKey } from './channels';
import { KERB_SHAPES } from './hardscape';

const unreadLevel = () => notify('That elevation does not read: type a number in the project\'s unit.');
const unreadGrade = () => notify('That grade does not read: type 0.5 %, 1:200 or 0.005 (at most 50 %).');

// ─── The Channel tools ───────────────────────────────────────────────

/** What the Channel tools lay: its section (or a profile of the project's, `profile:id`), how its invert runs, and the sizes typed (empty: the section's own). */
export const useChannelTool = create<{ kind: string; invert: InvertMode; grade: string; startZ: string; width: string; depth: string }>(() => ({
  kind: 'u', invert: 'grade', grade: '0.5 %', startZ: '', width: '', depth: '',
}));

const useChannelProfiles = () => {
  const nodes = useSession((s) => s.nodes);
  return [...nodes.values()].filter((p) => p.type === 'LapinProfile' && p.props.use === 'channel');
};

function ChannelBar({ draw }: { draw: boolean }) {
  const s = useChannelTool();
  const profiles = useChannelProfiles();
  const set = useChannelTool.setState;
  return (
    <>
      {draw && <ArcsToggle />}
      <select className="cad-layer" value={s.kind} data-testid="channel-kind" title="The channel's section" onChange={(e) => set({ kind: e.target.value })}>
        {CHANNEL_KINDS.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
        {profiles.length > 0 && <optgroup label="Profiles">{profiles.map((p) => <option key={p.id} value={`profile:${p.id}`}>{p.name}</option>)}</optgroup>}
      </select>
      <select className={`cad-layer${s.invert === 'grade' ? ' on' : ''}`} value={s.invert} data-testid="channel-invert" title="Its bottom (invert) at a constant grade from a level, or at a depth under its top"
        onChange={(e) => set({ invert: e.target.value as InvertMode })}>
        <option value="grade">At a grade</option>
        <option value="depth">At a depth under its top</option>
      </select>
      {s.invert === 'grade' && <>
        <label className="cad-layer" title="Its fall from the start, 0.5 %, 1:200 or 0.005 (negative: it rises)">Grade{' '}
          <CommitInput value={s.grade} width="4.5em" label="Grade" testId="channel-grade" onCommit={(t) => set({ grade: t })} /></label>
        <label className="cad-layer" title="The level of its bottom at its start (empty: its top there less its depth)">Start level{' '}
          <CommitInput value={s.startZ} width="4.5em" label="Start level" testId="channel-start" placeholder="from the top" onCommit={(t) => set({ startZ: t })} /></label>
      </>}
      <label className="cad-layer" title="Its width (empty: the section's own)">Width{' '}
        <CommitInput value={s.width} width="4em" label="Width" testId="channel-width" onCommit={(t) => set({ width: t })} /></label>
      <label className="cad-layer" title="Its depth under its top (empty: the section's own)">Depth{' '}
        <CommitInput value={s.depth} width="4em" label="Depth" testId="channel-depth" onCommit={(t) => set({ depth: t })} /></label>
    </>
  );
}

/** The arguments the bar's answers make, or null (said) when one does not read. */
function channelArgs(): Record<string, unknown> | null {
  const s = useChannelTool.getState();
  const inv = invertArgs(s, parseLevel);
  if ('bad' in inv) { if (inv.bad === 'grade') unreadGrade(); else unreadLevel(); return null; }
  const o: Record<string, unknown> = { ...inv.args };
  if (s.kind.startsWith('profile:')) o.profileId = s.kind.slice(8); else o.kind = s.kind;
  const width = s.width.trim() ? parseLength(s.width) : undefined;
  if (width === null || (width !== undefined && !(width > 0))) { notify('That width does not read: type a length.'); return null; }
  if (width) o.width = width;
  const depth = s.depth.trim() ? parseLength(s.depth) : undefined;
  if (depth === null || (depth !== undefined && !(depth > 0))) { notify('That depth does not read: type a length.'); return null; }
  if (depth) o.depth = depth;
  return o;
}

async function addChannel(args: Record<string, unknown>): Promise<void> {
  const made = await execute('civil.addChannel', args) as { id?: string; notes?: string[] } | null;
  if (made?.id) { useUi.setState({ selected: [made.id] }); if (made.notes?.length) notify(made.notes[0]); }
}

/** A channel along a polyline (Enter finishes; Space: arcs), on the terrain or hardscape under it. */
export const channel: ToolDef = {
  id: 'civil.channel', label: 'Drainage channel', icon: Droplets, shortcut: 'RG',
  tooltip: 'A drainage channel along a line: a U or V channel, a slot or grated drain, a swale; its bottom at a grade to a gully, its top flush with what it lies in',
  gesture: { kind: 'polyline', arcs: true },
  options: () => <ChannelBar draw />,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }) => {
    if (points.length < 2) return;
    const a = channelArgs();
    if (!a) return;
    const rises = risesOf(points.length, arcs, false);
    await addChannel({ points: points.map((p) => [p.x, p.y]), ...(rises.some((r) => Math.abs(r) > 1e-6) ? { arcs: rises } : {}), ...a });
  },
};

/** A channel along a line picked: a hardscape's edge, a path's side, a feature line, a platform's outline, a kerb, another channel. */
export const channelAlong: ToolDef = {
  id: 'civil.channelAlong', label: 'Drainage channel along a line', icon: Droplets, shortcut: 'RJ',
  tooltip: 'Pick a hardscape (near the edge to run along), a path (the side clicked), a feature line, a platform or a kerb: a channel runs along it and follows it when it changes',
  gesture: { kind: 'pick', types: FOLLOW_TYPES, reach: 12 },
  options: () => <ChannelBar draw={false} />,
  complete: async ({ entity, points }) => {
    if (!entity) return;
    const a = channelArgs();
    if (!a) return;
    const at = points[points.length - 1] ?? { x: 0, y: 0 };
    await addChannel({ along: followArg(entity, at), ...a });
  },
};

// ─── The Kerb tools ──────────────────────────────────────────────────

type Follow = 'default' | 'line' | 'ground';

/** What the Kerb tools lay: the side of its line the kerb stands on, what gives its heights, its shape (or `profile:id`), its height. */
export const useKerbTool = create<{ side: 'right' | 'left'; follow: Follow; shape: string; height: string }>(() => ({ side: 'right', follow: 'default', shape: 'upright', height: '' }));

function KerbBar({ draw }: { draw: boolean }) {
  const s = useKerbTool();
  const nodes = useSession((x) => x.nodes);
  const profiles = [...nodes.values()].filter((p) => p.type === 'LapinProfile' && p.props.use === 'kerb');
  const set = useKerbTool.setState;
  return (
    <>
      {draw && <ArcsToggle />}
      <select className="cad-layer" value={s.shape} data-testid="kerb-tool-shape" title="The kerb's shape" onChange={(e) => set({ shape: e.target.value })}>
        {KERB_SHAPES.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
        {profiles.length > 0 && <optgroup label="Profiles">{profiles.map((p) => <option key={p.id} value={`profile:${p.id}`}>{p.name}</option>)}</optgroup>}
      </select>
      <select className="cad-layer" value={s.side} data-testid="kerb-tool-side" title="Which side of its line the kerb stands on, looking along it: away from the paving" onChange={(e) => set({ side: e.target.value as 'right' | 'left' })}>
        <option value="right">Stands on the right</option>
        <option value="left">Stands on the left</option>
      </select>
      <select className="cad-layer" value={s.follow} data-testid="kerb-tool-follow" title="Its heights: those of its line, or the ground (a hardscape's top where one lies under it)" onChange={(e) => set({ follow: e.target.value as Follow })}>
        <option value="default">Heights: automatic</option>
        <option value="line">Heights of its line</option>
        <option value="ground">Heights of the ground</option>
      </select>
      <label className="cad-layer" title="Its height above its line (empty: the shape's own)">Height{' '}
        <CommitInput value={s.height} width="4em" label="Height" testId="kerb-tool-height" onCommit={(t) => set({ height: t })} /></label>
    </>
  );
}

function kerbArgs(): Record<string, unknown> | null {
  const s = useKerbTool.getState();
  const o: Record<string, unknown> = { side: s.side };
  if (s.follow !== 'default') o.follow = s.follow;
  if (s.shape.startsWith('profile:')) o.profileId = s.shape.slice(8); else o.shape = s.shape;
  if (s.height.trim()) {
    const v = parseLength(s.height);
    if (v == null || !Number.isFinite(v) || v <= 0) { notify('That height does not read: type a length.'); return null; }
    o.height = v;
  }
  return o;
}

async function addKerb(args: Record<string, unknown>): Promise<void> {
  const made = await execute('civil.addKerb', args) as { id?: string } | null;
  if (made?.id) useUi.setState({ selected: [made.id] });
}

/** A kerb along a polyline (Enter finishes), on the ground under it; the paving is on the side it does not stand on. */
export const kerb: ToolDef = {
  id: 'civil.kerb', label: 'Kerb', icon: Ruler, shortcut: 'BK',
  tooltip: 'A kerb along a line, on its own: upright, bevelled or rounded, dropped where it is crossed; on the ground, or at the heights of the line',
  gesture: { kind: 'polyline', arcs: true },
  options: () => <KerbBar draw />,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }) => {
    if (points.length < 2) return;
    const a = kerbArgs();
    if (!a) return;
    const rises = risesOf(points.length, arcs, false);
    await addKerb({ points: points.map((p) => [p.x, p.y]), ...(rises.some((r) => Math.abs(r) > 1e-6) ? { arcs: rises } : {}), ...a });
  },
};

/** A kerb along a line picked, following it as it changes. */
export const kerbAlong: ToolDef = {
  id: 'civil.kerbAlong', label: 'Kerb along a line', icon: Ruler, shortcut: 'BJ',
  tooltip: 'Pick a hardscape (near the edge to run along), a path (the side clicked), a feature line or a platform: a kerb runs along it and follows it when it changes',
  gesture: { kind: 'pick', types: FOLLOW_TYPES, reach: 12 },
  options: () => <KerbBar draw={false} />,
  complete: async ({ entity, points }) => {
    if (!entity) return;
    const a = kerbArgs();
    if (!a) return;
    const at = points[points.length - 1] ?? { x: 0, y: 0 };
    await addKerb({ along: followArg(entity, at), ...a });
  },
};

// ─── Accessories along a line ────────────────────────────────────────

/** The selected accessories (posts, lamps, bollards, gullies, benches, trees) arrayed along a line, each on the ground there: the Array dialog, set to a path. */
export const alongLine: ToolDef = {
  id: 'civil.alongLine', label: 'Along a line', icon: Lamp, shortcut: 'AX',
  tooltip: 'Select posts, lamps, bollards, gullies, benches or trees first, then press: they are laid along a line (a hardscape\'s edge, a kerb, a channel, a path…) at a spacing, each standing on the ground',
  run: () => {
    if (!useUi.getState().selected.length) { notify('Select what to lay along a line first (a post, a lamp, a bollard, a tree), then Along a line.'); return; }
    void arrayAlongLine();
  },
};

// ─── Create similar ─────────────────────────────────────────────────

/** Create similar: the tool that made it, set as it was. */
export function similarChannel(n: NodeDto): ToolDef {
  const along = n.props.along != null;
  const grade = typeof n.props.grade === 'number' ? gradeShown(n.props.grade) : undefined;
  useChannelTool.setState({
    kind: typeof n.props.profileId === 'string' ? `profile:${n.props.profileId}` : typeof n.props.kind === 'string' ? n.props.kind : 'u',
    invert: n.props.invert === 'depth' ? 'depth' : 'grade', ...(grade ? { grade } : {}),
  });
  return along ? channelAlong : channel;
}

export function similarKerb(n: NodeDto): ToolDef | null {
  if (n.props.standalone !== true) return null;
  useKerbTool.setState({
    side: n.props.side === 'left' ? 'left' : 'right', follow: n.props.follow === 'line' || n.props.follow === 'ground' ? n.props.follow : 'default',
    shape: typeof n.props.profileId === 'string' ? `profile:${n.props.profileId}` : typeof n.props.shape === 'string' ? n.props.shape : 'upright',
  });
  return n.props.along != null ? kerbAlong : kerb;
}

export const activateTool = (t: ToolDef | null) => { if (t && useUi.getState().tool !== t.id) activate(t); };

// ─── Grips ───────────────────────────────────────────────────────────

/** A channel's own points in a plan: its corners, the middles of its segments, its arcs (a channel that follows a line has none: its line has them). */
export function channelGrips(n: NodeDto, view?: GripView): Grip[] {
  if (!view?.view) return [];
  return lineGrips(n, (l) => { const p = payloadOf(l); return { command: 'civil.updateChannel', args: { id: n.id, points: p.points, arcs: p.arcs } }; });
}

/** A standalone kerb's own points, as a channel's. */
export function kerbGrips(n: NodeDto, view?: GripView): Grip[] {
  if (!view?.view || n.props.standalone !== true) return [];
  return lineGrips(n, (l) => { const p = payloadOf(l); return { command: 'civil.updateKerb', args: { id: n.id, points: p.points, arcs: p.arcs, closed: p.closed } }; });
}

// ─── Properties ──────────────────────────────────────────────────────

const SIZE_LABELS: Record<SizeKey, string> = { width: 'Width', depth: 'Depth', wall: 'Wall', base: 'Base', grating: 'Grating thickness', slot: 'Slot width', neck: 'Slot neck height' };

const num = (n: NodeDto, k: string): number | null => (typeof n.props[k] === 'number' ? n.props[k] as number : null);
const level = (v: number | null) => (v == null ? '' : formatLength(v, levelUnit()));

/** The terrain a channel or a kerb cuts, or standing alone. */
function HostRows({ node, set }: { node: NodeDto; set: (args: Record<string, unknown>) => void }) {
  const nodes = useSession((s) => s.nodes);
  const terrain = typeof node.props.terrainId === 'string' ? nodes.get(node.props.terrainId) : undefined;
  const terrains = [...nodes.values()].filter((t) => t.type === 'IfcGeographicElement' && t.props.predefinedType === 'TERRAIN');
  return (
    <>
      <dt>Terrain</dt>
      <dd>{terrain ? <>cuts {terrain.name} <button type="button" className="link small" disabled={!node.editable} onClick={() => set({ standalone: true })}>standalone</button></>
        : <><span className="muted">standalone</span>{terrains.length > 0 && <> <button type="button" className="link small" disabled={!node.editable} onClick={() => set({ terrainId: terrains[0].id })}>cut into the terrain</button></>}</>}</dd>
    </>
  );
}

/** A channel in Properties: its section and sizes, its invert, its top, its outlets, its terrain, its numbers. */
export function ChannelSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const catalog = useSession((s) => s.info?.catalogs['civil.channelKinds']) as unknown;
  const profiles = useChannelProfiles();
  const set = (args: Record<string, unknown>) => void execute('civil.updateChannel', { id: node.id, ...args });
  const kind = typeof node.props.kind === 'string' ? node.props.kind : 'u';
  const profileId = typeof node.props.profileId === 'string' ? node.props.profileId : '';
  const invert: InvertMode = node.props.invert === 'grade' ? 'grade' : 'depth';
  const grade = num(node, 'grade') ?? 0;
  const length = num(node, 'length') ?? 0;
  const outlets = Array.isArray(node.props.outletStations) ? (node.props.outletStations as number[]) : [];
  const lows = Array.isArray(node.props.lowStations) ? (node.props.lowStations as number[]) : [];
  const material = typeof node.props.material === 'string' ? nodes.get(node.props.material)?.name ?? '' : '';
  const grating = typeof node.props.gratingMaterial === 'string' ? nodes.get(node.props.gratingMaterial)?.name ?? '' : '';
  const notes = Array.isArray(node.props.notes) ? (node.props.notes as string[]) : [];
  const follows = followsName(node, nodes);
  const own = node.props.outlets !== undefined;
  const shape = profileId ? `profile:${profileId}` : kind;
  const station = (s: number) => formatLength(s);
  return (
    <div className="layers" data-testid="channel-section">
      <dl className="fields">
        <ProfileButton id={node.id} />
        <dt>Section</dt>
        <dd><select value={shape} disabled={!node.editable} data-testid="channel-section-kind"
          onChange={(e) => set(e.target.value.startsWith('profile:') ? { profileId: e.target.value.slice(8) } : { profileId: '', kind: e.target.value })}>
          {CHANNEL_KINDS.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
          {profiles.length > 0 && <optgroup label="Profiles">{profiles.map((p) => <option key={p.id} value={`profile:${p.id}`}>{p.name}</option>)}</optgroup>}
        </select></dd>
        {sizeFields(profileId ? 'profile' : kind).map((k) => (
          <FragmentRow key={`${kind}${k}`} label={SIZE_LABELS[k]}><Mm value={node.props[k]} fallback={kindDefault(catalog, kind, k)} min={k === 'depth' ? 20 : k === 'slot' || k === 'width' ? 5 : 0} onCommit={(v) => set({ [k]: v })} /></FragmentRow>
        ))}
        <dt>Invert</dt>
        <dd><select value={invert} disabled={!node.editable} data-testid="channel-section-invert" onChange={(e) => set({ invert: e.target.value })}>
          <option value="grade">At a grade</option><option value="depth">At a depth under its top</option>
        </select></dd>
        {invert === 'grade' && <>
          <dt title="Positive: it falls from its start">Grade</dt>
          <dd><CommitInput value={gradeShown(grade)} width="5em" label="Grade" testId="channel-section-grade" disabled={!node.editable}
            onCommit={(t) => { const g = readGrade(t); if (g == null) { unreadGrade(); return; } set({ grade: g }); }} /></dd>
          <dt title="The level of its bottom at its start (empty: its top there less its depth)">Start level</dt>
          <dd><CommitInput value={level(num(node, 'startZ'))} placeholder={level(num(node, 'startInvert'))} width="6em" label="Start level" testId="channel-section-start" disabled={!node.editable}
            onCommit={(t) => { if (!t.trim()) { set({ startZ: null }); return; } const z = parseLevel(t); if (z == null || !Number.isFinite(z)) { unreadLevel(); return; } set({ startZ: z }); }} /></dd>
        </>}
        <dt title="What gives its top: the ground (or the hardscape it lies in), or the heights of the line it follows">Top</dt>
        <dd><select value={typeof node.props.top === 'string' ? node.props.top : 'auto'} disabled={!node.editable} data-testid="channel-section-top"
          onChange={(e) => set({ top: e.target.value === 'auto' ? null : e.target.value })}>
          <option value="auto">Automatic</option><option value="surface">On the ground</option>{follows && <option value="line">On the line it follows</option>}
        </select></dd>
        {follows && <><dt>Follows</dt><dd>{follows} <button type="button" className="link small" onClick={() => useUi.setState({ selected: [(node.props.along as { id: string }).id] })}>select</button></dd></>}
        <HostRows node={node} set={set} />
        <dt>Material</dt><dd><MaterialPicker value={material} onChange={(m) => { if (m && m !== material) set({ material: m }); }} /></dd>
        {kind === 'grated' || kind === 'slot' ? <><dt>Grating material</dt><dd><MaterialPicker value={grating} onChange={(m) => { if (m && m !== grating) set({ gratingMaterial: m }); }} /></dd></> : null}
      </dl>
      <p className="eyebrow" style={{ margin: '8px 0 2px' }}>Gullies</p>
      <table className="layer-edit small" style={{ width: '100%' }} data-testid="channel-outlets"><tbody>
        {outlets.map((s, k) => (
          <tr key={`${k}:${s}`}>
            <td className="muted">{k + 1}</td>
            <td><span className="muted">at</span> <CommitInput value={station(s)} width="5em" label="Station of the gully" testId={`channel-outlet${k}`} disabled={!node.editable}
              onCommit={(t) => { const v = parseLength(t); if (v == null || !Number.isFinite(v) || v < 0 || v > length) { notify('That station is not on the channel: type a distance along it.'); return; } set({ outlets: movedOutlet(outlets, k, v) }); }} /></td>
            <td>{node.editable && <button type="button" className="link small" data-testid={`channel-outlet-remove${k}`} onClick={() => set({ outlets: withoutOutlet(outlets, k) })}>remove</button>}</td>
          </tr>
        ))}
        {outlets.length === 0 && <tr><td className="muted small">none</td></tr>}
      </tbody></table>
      {node.editable && <div className="pad row-wrap">
        <button type="button" className="btn compact" data-testid="channel-outlet-add" title="A gully at the end of the channel" onClick={() => set({ outlets: withOutlet(outlets, length) })}>Add a gully</button>
        {own && <button type="button" className="btn compact" data-testid="channel-outlet-auto" title="One gully at its lowest point, as it is made" onClick={() => set({ outlets: null })}>At its lowest point</button>}
      </div>}
      {lows.length > 0 && <p className="muted small"><span>Low points:</span> <span className="num">{lows.map(station).join(', ')}</span>{outlets.length === 0 && <> <span>No gully there.</span></>}</p>}
      <dl className="fields">
        <dt>Length</dt><dd><span className="num">{lengthText(length)}</span></dd>
        <dt>Invert at start</dt><dd><span className="num">{level(num(node, 'startInvert'))}</span></dd>
        <dt>Invert at end</dt><dd><span className="num">{level(num(node, 'endInvert'))}</span></dd>
        <dt>Depth</dt><dd><span className="num">{num(node, 'minDepth') != null ? `${lengthText(num(node, 'minDepth')!)} – ${lengthText(num(node, 'maxDepth') ?? 0)}` : ''}</span></dd>
      </dl>
      {notes.map((t, i) => <p key={i} className="muted small" data-testid="channel-note">{t}</p>)}
    </div>
  );
}

function FragmentRow({ label, children }: { label: string; children: React.ReactNode }) {
  return <><dt>{label}</dt><dd>{children}</dd></>;
}

/** A standalone kerb in Properties: its shape and sizes, the side it stands on, its heights, its drops, its terrain. */
export function StandaloneKerbSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const set = (args: Record<string, unknown>) => void execute('civil.updateKerb', { id: node.id, ...args });
  const profiles = [...nodes.values()].filter((p) => p.type === 'LapinProfile' && p.props.use === 'kerb');
  const shape = typeof node.props.profileId === 'string' ? `profile:${node.props.profileId}` : typeof node.props.shape === 'string' ? node.props.shape : 'upright';
  const material = typeof node.props.material === 'string' ? nodes.get(node.props.material)?.name ?? '' : '';
  const length = num(node, 'length') ?? 0;
  const drops = dropRows(node.props.drops);
  const follows = followsName(node, nodes);
  const notes = Array.isArray(node.props.notes) ? (node.props.notes as string[]) : [];
  const saveDrops = (next: DropRow[]) => set({ drops: next });
  const editDrop = (k: number, patch: Partial<DropRow>) => {
    const d = { ...drops[k], ...patch };
    const ends = dropEnds(d.from, d.to, length);
    if (!ends || d.reveal < 0) { notify('A drop runs from one station to a further one along the kerb, its reveal 0 or more.'); return; }
    saveDrops(drops.map((x, i) => (i === k ? { ...ends, reveal: d.reveal } : x)));
  };
  const lenField = (v: number, label: string, testId: string, onCommit: (m: number) => void) => (
    <CommitInput value={formatLength(v)} width="5em" label={label} testId={testId} disabled={!node.editable}
      onCommit={(t) => { const m = parseLength(t); if (m == null || !Number.isFinite(m)) { notify('That length does not read.'); return; } onCommit(m); }} />
  );
  return (
    <div className="layers" data-testid="standalone-kerb-section">
      <dl className="fields">
        <dt>Shape</dt>
        <dd><select value={shape} disabled={!node.editable} data-testid="kerb-shape" onChange={(e) => set(e.target.value.startsWith('profile:') ? { profileId: e.target.value.slice(8) } : { profileId: '', shape: e.target.value })}>
          {KERB_SHAPES.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
          {profiles.length > 0 && <optgroup label="Profiles">{profiles.map((p) => <option key={p.id} value={`profile:${p.id}`}>{p.name}</option>)}</optgroup>}
        </select></dd>
        <dt title="Its height above its line">Height</dt><dd><Mm value={node.props.height} fallback={0.12} min={0} onCommit={(v) => set({ height: v })} /></dd>
        <dt>Width</dt><dd><Mm value={node.props.width} fallback={0.15} min={20} onCommit={(v) => set({ width: v })} /></dd>
        <dt title="How deep it goes below its line">Depth</dt><dd><Mm value={node.props.depth} fallback={0.25} min={0} onCommit={(v) => set({ depth: v })} /></dd>
        <dt title="Which side of its line the kerb stands on, looking along it: away from the paving">Stands on</dt>
        <dd><select value={node.props.side === 'left' ? 'left' : 'right'} disabled={!node.editable} data-testid="kerb-side" onChange={(e) => set({ side: e.target.value })}>
          <option value="right">The right of its line</option><option value="left">The left of its line</option>
        </select></dd>
        <dt title="Its heights: those of its line, or the ground (a hardscape's top where one lies under it)">Heights</dt>
        <dd><select value={node.props.follow === 'ground' ? 'ground' : 'line'} disabled={!node.editable} data-testid="kerb-follow" onChange={(e) => set({ follow: e.target.value })}>
          <option value="line">Those of its line</option><option value="ground">The ground's</option>
        </select></dd>
        <dt title="Raised over what it follows">Lift</dt><dd><Mm value={node.props.lift} fallback={0} onCommit={(v) => set({ lift: v })} /></dd>
        {follows && <><dt>Follows</dt><dd>{follows} <button type="button" className="link small" onClick={() => useUi.setState({ selected: [(node.props.along as { id: string }).id] })}>select</button></dd></>}
        <HostRows node={node} set={set} />
        <dt>Material</dt><dd><MaterialPicker value={material} onChange={(m) => { if (m && m !== material) set({ material: m }); }} /></dd>
        <dt>Length</dt><dd><span className="num">{lengthText(length)}</span></dd>
        <dt>Height of its line</dt><dd><span className="num">{level(num(node, 'startZ'))} – {level(num(node, 'endZ'))}</span></dd>
      </dl>
      <p className="eyebrow" style={{ margin: '8px 0 2px' }}>Drops</p>
      <table className="kerb-edges small" style={{ width: '100%' }} data-testid="kerb-drops">
        {drops.length > 0 && <thead><tr><th>From</th><th>To</th><th title="Its height over the paving where it is dropped (0 at a crossing)">Reveal</th><th /></tr></thead>}
        <tbody>
          {drops.map((d, k) => (
            <tr key={`${k}:${d.from}:${d.to}:${d.reveal}`}>
              <td>{lenField(d.from, 'Drop from', `kerb-drop-from${k}`, (m) => editDrop(k, { from: m }))}</td>
              <td>{lenField(d.to, 'Drop to', `kerb-drop-to${k}`, (m) => editDrop(k, { to: m }))}</td>
              <td>{lenField(d.reveal, 'Reveal', `kerb-drop-reveal${k}`, (m) => editDrop(k, { reveal: m }))}</td>
              <td>{node.editable && <button type="button" className="link small" data-testid={`kerb-drop-remove${k}`} onClick={() => saveDrops(drops.filter((_, i) => i !== k))}>remove</button>}</td>
            </tr>
          ))}
          {drops.length === 0 && <tr><td className="muted small">none</td></tr>}
        </tbody>
      </table>
      {node.editable && <div className="pad row-wrap"><button type="button" className="btn compact" data-testid="kerb-drop-add" disabled={nextDrop(drops, length) == null}
        onClick={() => { const d = nextDrop(drops, length); if (d) saveDrops([...drops, d]); }}>Add a drop</button></div>}
      {notes.map((t, i) => <p key={i} className="muted small">{t}</p>)}
    </div>
  );
}

/** A kerb selected: a standalone one has its own section; a hardscape's kerb shows its hardscape's. */
export function KerbOrStandaloneSection({ node }: { node: NodeDto }) {
  return node.props.standalone === true ? <StandaloneKerbSection node={node} /> : <KerbSection node={node} />;
}
