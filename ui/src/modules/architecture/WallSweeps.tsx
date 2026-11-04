/**
 * Wall sweeps and reveals (decision 297, a user's request), the Architecture tab's Sweeps group: Wall sweep (a cornice,
 * a band, a plinth, a sill standing out of the face) and Wall reveal (a groove taken out of the wall), each put on the face
 * picked (in 3D at the height picked, in plan at the height typed) or on every wall selected; their profile and height in
 * the command line's bar; the sweep in the inspector (its profile with its preview, its material, the face), and on a wall
 * the sweeps it carries. A sweep of the wall's type (decision 298) says so in the inspector: "From the type", or
 * the fields the wall made its own, with Back to the type and Switch off on this wall; the wall's list offers the type's
 * sweeps switched off on it again. The Wall sweeps schedule adds them all up.
 */

import { useMemo } from 'react';
import { create } from 'zustand';
import { FlipHorizontal2, MousePointer2, RotateCcw, Table2, EyeOff, Eye } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import type { ToolDef } from '../../platform/module';
import { notify, select, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { relatedOf, relatingOf } from '../../engine/model';
import { LengthField } from '../../shell/cells';
import { WallRevealIcon, WallSweepIcon } from '../../icons';
import { MaterialPicker } from '../core/MaterialPicker';
import { ProfilePicker, ProfilePreview } from '../core/ProfilesDialog';
import type { ProfileJson } from '../core/profiles';

type Mode = 'add' | 'cut';
interface SweepPrefs { sweepProfile: string | null; revealProfile: string | null; height: number; fromTop: boolean }

/** What the next sweep or reveal is made with, for this session. */
export const useSweepPrefs = create<SweepPrefs>(() => ({ sweepProfile: null, revealProfile: null, height: 0.3, fromTop: false }));

const profileArg = (mode: Mode) => {
  const id = mode === 'cut' ? useSweepPrefs.getState().revealProfile : useSweepPrefs.getState().sweepProfile;
  return id && useSession.getState().nodes.get(id) ? { profileId: id } : {};
};

/** The bar of the sweep tools: the profile, the height (over the wall's base, or under its top). */
function SweepOptions({ mode }: { mode: Mode }) {
  const prefs = useSweepPrefs();
  const value = mode === 'cut' ? prefs.revealProfile : prefs.sweepProfile;
  return (
    <span className="row" style={{ gap: 6 }}>
      <ProfilePicker use={mode === 'cut' ? 'reveal' : 'sweep'} value={value} testId="sweep-profile"
        onPick={(id) => useSweepPrefs.setState(mode === 'cut' ? { revealProfile: id } : { sweepProfile: id })} />
      <span className="eyebrow">Height</span>
      <LengthField testId="sweep-height" metres={prefs.height} title="In plan: the height over the wall's base (or under its top); in 3D the height clicked" onCommit={(m) => { if (m != null) useSweepPrefs.setState({ height: m }); }} />
      <label className="check"><input type="checkbox" checked={prefs.fromTop} onChange={(e) => useSweepPrefs.setState({ fromTop: e.target.checked })} /><span>from the top</span></label>
    </span>
  );
}

const SweepBar = () => <SweepOptions mode="add" />;
const RevealBar = () => <SweepOptions mode="cut" />;

function onFace(mode: Mode): ToolDef['complete'] {
  return async ({ entity, face, points }, ctx) => {
    if (!entity || entity.type !== 'IfcWall') { notify('Pick a wall\'s face.'); return; }
    const p = face?.point ?? (points[0] ? [points[0].x, points[0].y] : null);
    if (!p) return;
    const { height, fromTop } = useSweepPrefs.getState();
    // In 3D the point has its height; in plan the height typed.
    const in3d = !!face?.normal && p.length > 2;
    await ctx.execute('architecture.addWallSweep', { wallId: entity.id, mode, point: p, ...(in3d ? {} : { height }), fromTop, ...profileArg(mode) });
  };
}

/** A sweep on the wall face picked: a cornice, a band, a plinth, a sill. */
export const wallSweep: ToolDef = {
  id: 'architecture.wallSweep', label: 'Wall sweep', icon: WallSweepIcon, shortcut: 'WB', options: SweepBar,
  gesture: { kind: 'face', types: ['IfcWall'] }, complete: onFace('add'),
};

/** A reveal in the wall face picked: a groove taken out of the wall's layers. */
export const wallReveal: ToolDef = {
  id: 'architecture.wallReveal', label: 'Wall reveal', icon: WallRevealIcon, shortcut: 'RV', options: RevealBar,
  gesture: { kind: 'face', types: ['IfcWall'] }, complete: onFace('cut'),
};

/** The same on every wall selected, on its exterior face (its right as drawn); Flip side in Properties turns one. */
function onSelected(mode: Mode) {
  const walls = useUi.getState().selected.filter((id) => useSession.getState().nodes.get(id)?.type === 'IfcWall');
  if (!walls.length) { notify('Select the walls first, then this.'); return; }
  const { height, fromTop } = useSweepPrefs.getState();
  void execute('architecture.addWallSweep', { wallIds: walls, mode, side: 'right', height, fromTop, ...profileArg(mode) })
    .then((r) => { if (r) notify(mode === 'cut' ? 'Reveal added on the exterior face of the walls selected.' : 'Sweep added on the exterior face of the walls selected.'); });
}

export const sweepSelected: ToolDef = { id: 'architecture.sweepSelected', label: 'Sweep on selected walls', icon: WallSweepIcon, run: () => onSelected('add') };
export const revealSelected: ToolDef = { id: 'architecture.revealSelected', label: 'Reveal on selected walls', icon: WallRevealIcon, run: () => onSelected('cut') };

/** A sweep or reveal in the inspector: its profile (with its preview), its material, its face; its height and reach are the class's own fields. */
export function WallSweepSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const wall = relatingOf(model, node.id, 'IfcRelVoidsElement');
  const mode: Mode = node.props.mode === 'cut' ? 'cut' : 'add';
  const shape = node.props.profile as unknown as ProfileJson | undefined;
  const material = typeof node.props.material === 'string' ? nodes.get(node.props.material)?.name ?? '' : '';
  const set = (args: Record<string, unknown>) => void execute('architecture.updateWallSweep', { id: node.id, ...args });
  return (
    <div className="layers">
      <div className="row" style={{ gap: 8, alignItems: 'flex-start' }}>
        <ProfilePreview shape={shape} size={64} />
        <div className="grow">
          <label className="type-pick"><span className="eyebrow">Profile</span>
            <ProfilePicker use={mode === 'cut' ? 'reveal' : 'sweep'} value={typeof node.props.profileId === 'string' ? node.props.profileId : null} onPick={(profileId) => set({ profileId })} /></label>
          <p className="small muted">{mode === 'cut' ? 'A reveal: taken out of the wall.' : 'A sweep: added to the wall\'s face.'} {node.props.side === 'right' ? 'Right face (exterior).' : 'Left face (interior).'}</p>
        </div>
      </div>
      {mode === 'add' && <label className="field-line"><span className="eyebrow">Material</span><MaterialPicker value={material} onChange={(name) => set({ material: name })} /></label>}
      {typeof node.props.typeSweep === 'string' && <TypeSweepState node={node} />}
      <div className="row" style={{ gap: 6 }}>
        <button type="button" className="btn small" disabled={!node.editable} onClick={() => set({ side: 'flip' })}><FlipHorizontal2 size={13} /> Flip side</button>
        {wall && <button type="button" className="btn small" onClick={() => select(wall.id)}><MousePointer2 size={13} /> Select its wall</button>}
      </div>
    </div>
  );
}

/** The labels of the fields a wall makes its own, as the inspector names them. */
const OWN_LABELS: Record<string, string> = {
  mode: 'kind', side: 'face', height: 'height', fromTop: 'from the top', offset: 'offset', gaps: 'openings', flip: 'flip', setbackStart: 'set-back', setbackEnd: 'set-back',
  returns: 'returns', returnsAtOpenings: 'returns at openings', material: 'material', profile: 'profile', start: 'start', end: 'end',
};

/** A wall type's sweep: whether it is as the type has it or the wall's own in some fields, and the way back. */
function TypeSweepState({ node }: { node: NodeDto }) {
  const own = Array.isArray(node.props.own) ? (node.props.own as string[]) : [];
  const fields = [...new Set(own.map((k) => OWN_LABELS[k] ?? k))];
  return (
    <div className="row" style={{ gap: 6, flexWrap: 'wrap' }} data-testid="type-sweep-state">
      {own.length === 0
        ? <span className="chip">From the type</span>
        : <span className="chip on" title="These fields are this wall's own: the type's changes leave them"><span>Own:</span> {fields.map((f, i) => <span key={f}>{i > 0 ? ', ' : ''}<span>{f}</span></span>)}</span>}
      {own.length > 0 && <button type="button" className="btn small" onClick={() => void execute('architecture.updateWallSweep', { id: node.id, reset: true })}><RotateCcw size={13} /> Back to the type</button>}
      <button type="button" className="btn small" title="The type's sweep is left off this wall (as deleting it does); the wall's Sweeps and reveals switch it on again"
        onClick={() => void execute('architecture.updateWallSweep', { id: node.id, off: true })}><EyeOff size={13} /> Switch off on this wall</button>
    </div>
  );
}

const sweepsOf = (id: string) => relatedOf(useSession.getState(), id, 'IfcRelVoidsElement').filter((n) => n.type === 'LapinWallSweep');

/** On a wall: the sweeps and reveals it carries, each to select. */
export function WallSweepsOnWall({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const list = useMemo(() => relatedOf({ nodes, relations }, node.id, 'IfcRelVoidsElement').filter((n) => n.type === 'LapinWallSweep'), [nodes, relations, node.id]);
  const off = Array.isArray(node.props.sweepsOff) ? node.props.sweepsOff.length : 0;
  return (
    <>
      <ul className="plain-list">
        {list.map((s) => (
          <li key={s.id}><button type="button" className="link small" onClick={() => select(s.id)}><span data-no-i18n>{s.name}</span></button>
            <span className="muted small"> · {s.props.side === 'right' ? 'exterior' : 'interior'}{typeof s.props.typeSweep === 'string' ? ' · from the type' : ''}</span></li>
        ))}
      </ul>
      {off > 0 && (
        <button type="button" className="btn small" disabled={!node.editable} data-testid="type-sweeps-on" onClick={() => void execute('architecture.restoreWallSweeps', { wallId: node.id })}>
          <Eye size={13} /> <span>Switch the type's sweeps on again</span> <span className="muted" data-no-i18n>({off})</span>
        </button>
      )}
    </>
  );
}

export const hasSweeps = (node: NodeDto) => sweepsOf(node.id).length > 0 || (Array.isArray(node.props.sweepsOff) && node.props.sweepsOff.length > 0);

/** The wall sweeps schedule: every sweep and reveal by profile, kind and material, their count, pieces, length and volume. */
export const wallSweepSchedule: ToolDef = {
  id: 'architecture.wallSweepSchedule', label: 'Wall sweeps schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'wallSweeps' }).then((r) => { if (r) notify('Wall sweeps schedule made: it is in the project browser, under Schedules.'); }),
};
