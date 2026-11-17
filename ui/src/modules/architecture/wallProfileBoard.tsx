/**
 * A wall's profile (decision 372) and a curtain wall's boundary (#127) on the shared sketch canvas (audit S11 stage 2,
 * docs/plan-sketch-canvas.md §4): `studio.editWallProfile` opens a reference plane standing on the wall's axis, its x along the
 * wall from its start and its y up from its base, the top drawn there as one polyline and the wall's base and ends as construction
 * lines. Draft's tools, snaps, grips, constraints and dimensions work on it; the side form keeps the dialog's shapes to start from
 * (a gable, a slope, steps), its table of points in millimetres and Flat top. Finish sketch writes the top through the wall's own
 * command in one undo step (refused, saying why, the board kept open, when what is drawn is not one line from start to end);
 * Cancel drops it. A curved curtain wall keeps the dialog (the board stands on a straight wall), as does a product without Studio.
 */

import { useEffect, useState } from 'react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { hasModule } from '../../platform/brand';
import { NumberField } from '../../shell/cells';
import { frameBoard, registerBoard, rememberReturn, SKETCH_TOOLS, takeReturn, type Board } from '../../viewport/sketch/board';
import { profileShape } from './WallProfileDialog';

type Pt = [number, number];

interface WallProfileOf { id: string; curtain?: boolean; length: number; top: Pt[] }

/** What the board's drafts make now (`studio.profileBoard`). */
interface BoardTop { kind: 'wall'; length: number; top?: Pt[]; problem?: string }

/** Whether a wall's profile opens on the canvas: a product with Studio, a straight wall. */
export const opensOnBoard = (n: NodeDto): boolean => hasModule('studio') && !n.props.arc;

/** Opens the wall's (or curtain wall's) profile on its board; the dialog where the board cannot stand. */
export async function openWallProfile(id: string): Promise<void> {
  const n = useSession.getState().nodes.get(id);
  if (!n || !opensOnBoard(n)) { useUi.setState({ dialog: 'wallProfile', selection: id }); return; }
  const u = useUi.getState();
  const r = await execute('studio.editWallProfile', { id }) as { viewId?: string } | null;
  if (!r?.viewId) { const e = useSession.getState().error; if (e) notify(e); return; }
  rememberReturn(r.viewId, { view: u.view, planViewId: u.planViewId, drawingViewId: u.drawingViewId });
  useUi.setState({ view: 'drawing', drawingViewId: r.viewId, tool: null, selected: [], selection: null, dialog: null });
  const viewId = r.viewId;
  frameBoard(() => {
    const of = useSession.getState().nodes.get(viewId)?.props.wallProfileOf as WallProfileOf | undefined;
    // Room over the top for a gable or a step drawn higher.
    return of ? [0, 0, of.length, Math.max(...of.top.map((p) => p[1]), 1) * 1.3] : null;
  }, { panel: true });
}

function leave(viewId: string, wallId: string | undefined) {
  const back = takeReturn(viewId) ?? { view: 'plan', drawingViewId: null };
  useUi.setState({ ...back, ...(wallId && useSession.getState().nodes.has(wallId) ? { selected: [wallId], selection: wallId } : {}) });
}

async function finish(viewId: string, flat = false): Promise<void> {
  const of = useSession.getState().nodes.get(viewId)?.props.wallProfileOf as WallProfileOf | undefined;
  const r = await execute('studio.finishWallProfile', { viewId, ...(flat ? { flat: true } : {}) }) as { id?: string } | null;
  if (!r?.id) { const e = useSession.getState().error; if (e) notify(e); return; }
  notify(flat ? (of?.curtain ? 'The curtain wall is flat on top again.' : 'The wall is flat again.') : of?.curtain ? 'The curtain wall follows its boundary.' : 'The wall follows its profile.');
  leave(viewId, r.id);
}

async function cancel(viewId: string): Promise<void> {
  const of = useSession.getState().nodes.get(viewId)?.props.wallProfileOf as WallProfileOf | undefined;
  await execute('studio.cancelWallProfile', { viewId });
  if (!useSession.getState().error) leave(viewId, of?.id);
}

/** The side form: the shapes to start from, the top's points in millimetres, Flat top. */
function WallProfilePanel({ board }: { board: Board }) {
  const version = useSession((s) => s.version);
  const of = useSession((s) => s.nodes.get(board.viewId)?.props.wallProfileOf) as WallProfileOf | undefined;
  const [now, setNow] = useState<BoardTop | null>(null);
  const [peak, setPeak] = useState(() => String(Math.round(((of?.top?.[0]?.[1] ?? 3) + 1.5) * 1000)));
  useEffect(() => {
    let live = true;
    void query<BoardTop>('studio.profileBoard', { viewId: board.viewId }).then((r) => { if (live) setNow(r); });
    return () => { live = false; };
  }, [board.viewId, version]);
  if (!of) return null;
  const l = of.length, h = of.top?.[0]?.[1] ?? 3;
  const put = (pts: Pt[]) => void execute('studio.setBoardShapes', { viewId: board.viewId, shapes: [{ type: 'polyline', pts, closed: false }] });
  const shape = (kind: 'gable' | 'slope' | 'steps') => put(profileShape(kind, l, h, Number(peak) / 1000 || h + 1));
  const mm = (v: number) => Math.round(v * 1000);
  const top = now?.top;
  const setPoint = (i: number, p: Pt) => { if (top) put(top.map((q, j) => (j === i ? p : q))); };
  return (
    <div className="sketch-board-panel" data-testid="wall-profile-panel" data-i18n-ctx="wallProfile">
      <p className="muted small">Seen from its start to its end. The top is one line from the wall's start to its end; a step is two points one over the other.</p>
      <div className="row wrap" style={{ gap: 4 }}>
        <span className="eyebrow">Start from</span>
        <button type="button" className="btn small" data-action="profile-gable" onClick={() => shape('gable')}>Gable</button>
        <button type="button" className="btn small" data-action="profile-slope" onClick={() => shape('slope')}>Slope</button>
        <button type="button" className="btn small" data-action="profile-steps" onClick={() => shape('steps')}>Steps</button>
      </div>
      <div className="row" style={{ gap: 4 }}>
        <label className="small">to <input className="num mini" style={{ width: 64 }} data-field="profile-peak" value={peak} onChange={(e) => setPeak(e.target.value)} /> mm</label>
      </div>
      {now?.problem && <p className="small problem" data-testid="board-problem">{now.problem}</p>}
      {top && (
        <table className="grid small">
          <thead><tr><th>#</th><th>Along (mm)</th><th>Height (mm)</th></tr></thead>
          <tbody>
            {top.map(([u, v], i) => (
              <tr key={`${i}:${u}:${v}`}>
                <td data-no-i18n>{i + 1}</td>
                <td><NumberField className="mini" value={mm(u)} onCommit={(x) => { if (x != null) setPoint(i, [x / 1000, v]); }} /></td>
                <td><NumberField className="mini" value={mm(v)} onCommit={(x) => { if (x != null) setPoint(i, [u, x / 1000]); }} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="row" style={{ gap: 4 }}>
        <button type="button" className="btn small" data-action="profile-flat" title="Finish with a flat top: the profile taken away" onClick={() => void finish(board.viewId, true)}>Flat top</button>
      </div>
    </div>
  );
}

/** A wall's profile or a curtain wall's boundary: Finish writes the top through the wall's own command. */
function wallProfileBoard(viewId: string, nodes: ReadonlyMap<string, NodeDto>): Board | null {
  const of = nodes.get(viewId)?.props.wallProfileOf as WallProfileOf | undefined;
  if (!of || typeof of !== 'object' || typeof of.id !== 'string') return null;
  return {
    kind: 'wall-profile', viewId, title: nodes.get(of.id)?.name ?? of.id, hint: 'Draw the top with Draft, from the start to the end',
    tools: SKETCH_TOOLS, constraints: true,
    finish: () => finish(viewId), cancel: () => cancel(viewId), panel: WallProfilePanel,
  };
}

registerBoard(wallProfileBoard);
