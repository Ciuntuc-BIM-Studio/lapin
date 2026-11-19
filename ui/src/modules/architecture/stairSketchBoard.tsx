/**
 * The stair by sketch (users asked for Revit's "stair by sketch", 19.11.2026) on the shared sketch canvas: `studio.editStairSketch`
 * opens a plan of the storey of its own, where the stair is drawn with Draft's tools as lines of three roles: Boundary (its two
 * sides, green), Riser (each riser, from one side to the other; none: spread as the rules ask) and Path (which way it climbs,
 * from its start). The side form picks the role new lines take, turns the way it climbs round and shows, as it is drawn, what the
 * sketch makes (the risers drawn against those the height asks, the riser and the tread, what is wrong). Finish sketch makes the
 * stair, or draws it again, in one undo step (refused, saying why, the board kept open); Cancel drops it. A product without Studio
 * places stairs by their shapes only.
 */

import { useEffect, useState } from 'react';
import { notify, useUi } from '../../app/store';
import { activeStorey, execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { lengthText } from '../../engine/unitsStore';
import { hasModule } from '../../platform/brand';
import { useCad } from '../../viewport/cad/session';
import { registerBoard, rememberReturn, SKETCH_TOOLS, takeReturn, type Board } from '../../viewport/sketch/board';

/** Whose sketch a board's view is: a stair's id, or a new stair's storey and type. */
type StairSketchOf = string | { storeyId: string; typeId?: string };

/** What the board's lines make now (`studio.stairSketchCheck`). */
interface SketchCheck { ok: boolean; error?: string; risers?: number; needed?: number; riserHeight?: number; tread?: number; width?: number; height?: number; drawn?: boolean; landings?: number; rulesOk?: boolean }

/** The roles a stair's sketch draws with, as Revit's: the sides, the risers, the path. */
export const STAIR_SKETCH_ROLES = [
  { role: 'boundary', label: 'Boundary', title: 'The stair\'s two sides: lines and arcs end to end, one line each side' },
  { role: 'riser', label: 'Riser', title: 'A riser, from one side to the other; none drawn: the rules\' risers spread evenly' },
  { role: 'path', label: 'Path', title: 'Which way the stair climbs: from the path\'s start' },
] as const;

/** Whether stairs can be drawn by sketch here: a product with Studio (its board). */
export const sketchStairs = (): boolean => hasModule('studio');

async function open(args: Record<string, unknown>, stairId?: string): Promise<void> {
  const u = useUi.getState();
  const r = await execute('studio.editStairSketch', args) as { viewId?: string; ids?: string[] } | null;
  if (!r?.viewId) { const e = useSession.getState().error; if (e) notify(e); return; }
  rememberReturn(r.viewId, { view: u.view, planViewId: u.planViewId, ...(stairId ? { selected: [stairId], selection: stairId } : {}) });
  useUi.setState({ view: 'plan', planViewId: r.viewId, tool: null, selected: [], selection: null, dialog: null });
  // The sides first, as in Revit: lines drawn now are the stair's boundary.
  useCad.setState({ role: r.ids?.length ? 'riser' : 'boundary' });
  notify(r.ids?.length
    ? 'Draw the stair again: Boundary for its sides, Riser for each riser, Path for the way it climbs; then Finish sketch.'
    : 'Draw the stair\'s two sides as Boundary lines, its risers as Riser lines (or none: spread by the rules), then Finish sketch.');
}

/** A new stair drawn by sketch on the storey shown, of the stair type picked. */
export async function newStairSketch(): Promise<void> {
  if (!sketchStairs()) { notify('Drawing a stair by sketch needs Studio, not in this edition.'); return; }
  const storeyId = activeStorey()?.id;
  if (!storeyId) { notify('Open a plan to draw a stair.'); return; }
  const typeId = useUi.getState().stairTypeId ?? undefined;
  await open({ storeyId, ...(typeId ? { typeId } : {}) });
}

/** A stair's sketch opened again (a stair of its type's shape: an empty board over its place, the stair shown). */
export async function editStairSketch(id: string): Promise<void> {
  if (!sketchStairs()) { notify('Drawing a stair by sketch needs Studio, not in this edition.'); return; }
  await open({ id }, id);
}

function leave(viewId: string, select?: string) {
  const back = takeReturn(viewId) ?? { view: 'plan', planViewId: null };
  useCad.setState({ role: '' });
  useUi.setState({ ...back, ...(select && useSession.getState().nodes.has(select) ? { selected: [select], selection: select } : {}) });
}

async function finish(viewId: string): Promise<void> {
  const r = await execute('studio.finishStairSketch', { viewId }) as { id?: string } | null;
  if (!r?.id) { const e = useSession.getState().error; if (e) notify(e); return; }
  leave(viewId, r.id);
}

async function cancel(viewId: string): Promise<void> {
  const of = useSession.getState().nodes.get(viewId)?.props.stairSketchOf as StairSketchOf | undefined;
  await execute('studio.cancelStairSketch', { viewId });
  if (!useSession.getState().error) leave(viewId, typeof of === 'string' ? of : undefined);
}

/** The side form: the role new lines take, the way it climbs, what the sketch makes so far. */
function StairSketchPanel({ board }: { board: Board }) {
  const version = useSession((s) => s.version);
  const flip = useSession((s) => !!s.nodes.get(board.viewId)?.props.stairSketchFlip);
  const role = useCad((s) => s.role);
  const [now, setNow] = useState<SketchCheck | null>(null);
  useEffect(() => {
    let live = true;
    void query<SketchCheck>('studio.stairSketchCheck', { viewId: board.viewId }).then((r) => { if (live) setNow(r); });
    return () => { live = false; };
  }, [board.viewId, version]);
  return (
    <div className="sketch-board-panel" data-testid="stair-sketch-panel" data-i18n-ctx="stairSketch">
      <div className="row wrap" style={{ gap: 4 }}>
        <span className="eyebrow">Draw</span>
        {STAIR_SKETCH_ROLES.map((r) => (
          <button key={r.role} type="button" className={`btn small${role === r.role ? ' on' : ''}`} data-action={`stair-sketch-${r.role}`} title={r.title}
            aria-pressed={role === r.role} onClick={() => useCad.setState({ role: r.role })}>{r.label}</button>
        ))}
      </div>
      <p className="muted small">Boundary: the two sides (a line with no role counts as one). Riser: from one side to the other. A wide gap between two risers is a landing.</p>
      <label className="row small">
        <input type="checkbox" data-field="stair-sketch-flip" checked={flip}
          onChange={(e) => void execute('studio.flipStairSketch', { viewId: board.viewId, flip: e.target.checked })} />
        Climbs the other way
      </label>
      {now && !now.ok && now.error && <p className="small problem" data-testid="board-problem">{now.error}</p>}
      {now?.ok && (
        <dl className="fields small" data-testid="stair-sketch-numbers">
          <dt>Risers</dt>
          <dd><span className="num">{now.risers}</span> <span className="muted small">{now.drawn ? `drawn; the height asks ${now.needed}` : 'spread by the rules'}</span></dd>
          <dt>Riser</dt><dd><span className="num">{lengthText(now.riserHeight ?? 0)}</span></dd>
          <dt>Tread</dt><dd><span className="num">{lengthText(now.tread ?? 0)}</span></dd>
          <dt>Width</dt><dd><span className="num">{lengthText(now.width ?? 0)}</span></dd>
          {!!now.landings && <><dt>Landings</dt><dd><span className="num">{now.landings}</span></dd></>}
        </dl>
      )}
      {now?.ok && now.rulesOk === false && <p className="small muted">Some numbers are outside the type's rules: the stair's Checks say which.</p>}
    </div>
  );
}

/** A stair's sketch: Finish makes the stair (or draws it again). */
function stairSketchBoard(viewId: string, nodes: ReadonlyMap<string, NodeDto>): Board | null {
  const of = nodes.get(viewId)?.props.stairSketchOf as StairSketchOf | undefined;
  if (of == null) return null;
  return {
    kind: 'stair-sketch', viewId, title: typeof of === 'string' ? nodes.get(of)?.name ?? of : 'New stair',
    hint: 'Draw the sides, the risers and the path with Draft',
    tools: SKETCH_TOOLS.filter((c) => c !== 'ELLIPSE' && c !== 'CIRCLE'), constraints: true,
    finish: () => finish(viewId), cancel: () => cancel(viewId), panel: StairSketchPanel,
  };
}

registerBoard(stairSketchBoard);
