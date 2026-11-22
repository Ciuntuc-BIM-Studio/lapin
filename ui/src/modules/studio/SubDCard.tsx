/** A SubD solid in the inspector (docs/plan-studio-freeform.md §8, F1c): its cage's counts, the level and deviation the body was made at, thickness and side, symmetry, and the way into the cage edit mode. */

import { useEffect, useState } from 'react';
import { Box } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { query, useSession } from '../../engine/session';
import { LengthField } from '../../shell/cells';
import { deviationMm, symmetryArgs, symmetryAxis, thicknessArgs, type Cage } from './subdModel';
import { applySet, enterCage, useSubD } from './subdStore';

export function SubDCard({ node }: { node: NodeDto }) {
  const version = useSession((s) => s.version);
  const editing = useSubD((s) => s.id === node.id);
  const live = useSubD((s) => (s.id === node.id ? s.cage : null));
  const [asked, setAsked] = useState<Cage | null>(null);
  const [problem, setProblem] = useState('');
  useEffect(() => { setAsked(null); setProblem(''); }, [node.id]);
  useEffect(() => {
    let on = true;
    void query<Cage>('studio.subdCage', { id: node.id }).then((c) => { if (on) setAsked(c); });
    return () => { on = false; };
  }, [node.id, version]);
  const cage = live ?? asked;
  if (!cage) return null;
  const open = !cage.closed;
  const set = async (args: Record<string, unknown>) => {
    setProblem('');
    const ok = await applySet(node.id, args);
    if (!ok) setProblem(useSession.getState().error ?? '');
  };
  const axis = symmetryAxis(cage);
  return (
    <dl className="fields" data-section="subd">
      <dt>Cage</dt>
      <dd>
        <button type="button" className="btn small" data-field="edit-cage" disabled={editing} title="Pick and move the cage's points, edges and faces in 3D (Tab)" onClick={() => enterCage(node.id)}><Box size={12} />Edit cage</button>
      </dd>
      <dt>Points</dt><dd data-subd-field="points">{cage.points.length}</dd>
      <dt>Faces</dt><dd data-subd-field="faces">{cage.faces.length}</dd>
      <dt>Surface</dt><dd>{cage.closed ? 'Closed' : 'Open'}</dd>
      <dt title="The subdivision level the body is made at: the chord rule stops it when it lies within the tolerance of the limit surface">Level</dt>
      <dd data-subd-field="level">{cage.level ?? '—'}</dd>
      <dt title="How far the body lies from the limit surface at that level">Deviation</dt>
      <dd data-subd-field="deviation">{deviationMm(cage.deviation)}</dd>
      <dt title={open ? 'The surface is offset into a solid' : 'Only an open surface takes a thickness: a closed cage is already a solid'}>Thickness</dt>
      <dd>
        <LengthField metres={cage.thickness} readOnly={!open} title={open ? 'The thickness of the open surface (0: a surface only)' : 'Only an open surface takes a thickness'}
          onCommit={(m) => { if (m != null) void set(thicknessArgs(m)); }} />
        {!open && <span className="muted small"> open surfaces only</span>}
      </dd>
      <dt title={open ? 'Which side of the surface the thickness goes' : 'Only an open surface takes a thickness'}>Side</dt>
      <dd>
        <select className="cell" disabled={!open} value={cage.side} data-field="subd-side" onChange={(e) => void set({ side: e.target.value })}>
          <option value="out">Out</option><option value="in">In</option><option value="centre">Centre</option>
        </select>
      </dd>
      <dt title="Edit one half: the other follows. A face that crosses the plane needs a loop or more segments first">Symmetry</dt>
      <dd>
        <select className="cell" value={axis} data-field="subd-symmetry" onChange={(e) => { const v = e.target.value; if (v !== 'plane') void set(symmetryArgs(v as 'none' | 'x' | 'y' | 'z')); }}>
          <option value="none">None</option><option value="x">x</option><option value="y">y</option><option value="z">z</option>
          {axis === 'plane' && <option value="plane">Plane</option>}
        </select>
      </dd>
      {(problem || cage.problem) && <><dt>Problems</dt><dd className="error-text small" role="alert" data-no-i18n>{problem || cage.problem}</dd></>}
    </dl>
  );
}
