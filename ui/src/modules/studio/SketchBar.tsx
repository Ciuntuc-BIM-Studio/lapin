/**
 * The bar over every sketch board (audit S11, docs/plan-sketch-canvas.md): whose sketch it is, how free it is (the constraint
 * chip), the Draft tools the board offers (the same commands as the Draft tab: LINE, TRIM, FILLET…), the constraint and dimension
 * tools when it takes them, Finish sketch and Cancel; over the 3D view also Look at plane and Open in 2D, flat Back to 3D. The same
 * bar for an outline's sketch (on its plan), a part's own sketch and Studio's sketch on a face (over the 3D view, on their plane).
 */

import type { ComponentType } from 'react';
import { ArrowRightToLine, Check, Circle, CopyPlus, CornerUpRight, Egg, Eraser, Grid3x3, Redo2, RectangleHorizontal, Scissors, Slash, Spline, Scan, Type, X, Box } from 'lucide-react';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { chipOf } from '../../viewport/cad/constraints';
import { cancel as cancelCommand, startCommand, useCad } from '../../viewport/cad/session';
import { useSketch } from '../../viewport/cad/sketchStore';
import { boardOf, type Board } from '../../viewport/sketch/board';
import { setWorkPlane, useWorkPlane } from '../../viewport/sketch/sketch3d';
import { DIMENSIONS, GEOMETRIC } from './constraintTools';

type Icon = ComponentType<{ size?: number | string }>;

/** The Draft commands' labels and icons, as the Draft tab has them. */
export const DRAFT_TOOLS: Record<string, { label: string; icon: Icon }> = {
  LINE: { label: 'Line', icon: Slash }, PLINE: { label: 'Polyline', icon: Spline }, RECTANGLE: { label: 'Rectangle', icon: RectangleHorizontal },
  CIRCLE: { label: 'Circle', icon: Circle }, ARC: { label: 'Arc', icon: Redo2 }, ELLIPSE: { label: 'Ellipse', icon: Egg },
  OFFSET: { label: 'Offset', icon: CopyPlus }, TRIM: { label: 'Trim', icon: Scissors }, EXTEND: { label: 'Extend', icon: ArrowRightToLine },
  FILLET: { label: 'Fillet', icon: CornerUpRight }, CHAMFER: { label: 'Chamfer', icon: CornerUpRight }, ERASE: { label: 'Erase', icon: Eraser },
  TEXT: { label: 'Text', icon: Type }, HATCH: { label: 'Hatch', icon: Grid3x3 },
};

const EDITS = new Set(['OFFSET', 'TRIM', 'EXTEND', 'FILLET', 'CHAMFER', 'ERASE']);

/** A Draft or constraint command from the bar: the one running ends first, the ribbon's tool is put down. */
const run = (command: string) => {
  if (useCad.getState().command) cancelCommand(true);
  useUi.setState({ tool: null });
  void startCommand(command);
};

export function SketchBar({ board, in3d, onLook }: { board: Board; in3d: boolean; onLook?: () => void }) {
  const command = useCad((s) => s.command);
  const status = useSketch((s) => s.views[board.viewId]?.status ?? null);
  const count = useSketch((s) => s.views[board.viewId]?.constraints.length ?? 0);
  const isPlane = useSession((s) => s.nodes.get(board.viewId)?.props.kind === 'plane');
  const chip = chipOf(status);
  const tools = board.tools.filter((c) => DRAFT_TOOLS[c]);
  const draw = tools.filter((c) => !EDITS.has(c)), edit = tools.filter((c) => EDITS.has(c));
  const button = (c: string, label: string, Icon: Icon | undefined, attr: Record<string, string>) => (
    <button key={c} type="button" className={`icon mini${command === c ? ' on' : ''}`} title={label} aria-pressed={command === c} {...attr} onClick={() => run(c)}>
      {Icon ? <Icon size={14} /> : <span>{label.slice(0, 2)}</span>}
    </button>
  );
  return (
    <div className={`sketch-bar${in3d ? ' in3d' : ''}`} role="toolbar" aria-label="Sketch tools" data-testid={`${board.kind}-sketch-bar`} data-sketch-bar={board.kind}
      {...(board.kind === 'face' ? { 'data-draft3d': 'sketch' } : {})}>
      <span className="sketch-bar-title"><span>{board.kind === 'face' ? 'Sketching on' : 'Sketch of'}</span> <span data-no-i18n>{board.title}</span></span>
      {board.constraints && (count > 0 || status) && (
        <span className={`sketch-chip inline ${chip.tone}`} role="status" data-testid="sketch-chip" data-tone={chip.tone}>
          <span className="sketch-chip-dot" /><span data-testid="sketch-chip-text">{chip.text}</span>
        </span>
      )}
      <span className="sketch-bar-group" data-group="draw">{draw.map((c) => button(c, DRAFT_TOOLS[c].label, DRAFT_TOOLS[c].icon, { 'data-sketch-tool': c }))}</span>
      <span className="sketch-bar-group" data-group="edit">{edit.map((c) => button(c, DRAFT_TOOLS[c].label, DRAFT_TOOLS[c].icon, { 'data-sketch-tool': c }))}</span>
      {board.constraints && (
        <span className="sketch-bar-group" data-group="constrain">
          {[...GEOMETRIC, ...DIMENSIONS].map((t) => button(t.command, t.label, t.icon as Icon | undefined, { 'data-sketch-constraint': t.kind }))}
        </span>
      )}
      {board.hint && !command && !in3d && <span className="muted sketch-bar-hint">{board.hint}</span>}
      <span className="sketch-bar-end">
        {in3d && onLook && <button type="button" className="btn small" data-action="look-at-plane" title="Look at plane: the view square to it" onClick={onLook}><Scan size={12} />Look at plane</button>}
        {in3d && isPlane && <button type="button" className="btn small" data-action="board-flat" title="Open the sketch's plane as a 2D view: the same sketch, the same tools" onClick={() => useUi.setState({ view: 'drawing', drawingViewId: board.viewId })}>Open in 2D</button>}
        {!in3d && isPlane && (board.kind === 'part' || board.kind === 'face') && (
          <button type="button" className="btn small" data-action="board-3d" title="Back to the 3D view, the sketch on its plane" onClick={() => {
            if (useWorkPlane.getState().plane?.planeId !== board.viewId) setWorkPlane({ planeId: board.viewId, name: useSession.getState().nodes.get(board.viewId)?.name ?? '' });
            useUi.setState({ view: '3d' });
          }}><Box size={12} />Back to 3D</button>
        )}
        <button type="button" className="btn primary small" data-action={`finish-${board.kind}`} title="Finish sketch: what is drawn is kept (one undo step)" onClick={() => { if (useCad.getState().command) cancelCommand(true); void board.finish(); }}><Check size={12} />Finish sketch</button>
        <button type="button" className="btn small" data-action={`cancel-${board.kind}`} title="Cancel: what this sketch drew is dropped" onClick={() => { if (useCad.getState().command) cancelCommand(true); void board.cancel(); }}><X size={12} />Cancel</button>
      </span>
    </div>
  );
}

/** Over a plan or a 2D view that is a board's: its bar. */
export function SketchBar2D() {
  const view = useUi((s) => s.view);
  const planViewId = useUi((s) => s.planViewId);
  const drawingViewId = useUi((s) => s.drawingViewId);
  const nodes = useSession((s) => s.nodes);
  const id = view === 'plan' ? planViewId : view === 'drawing' ? drawingViewId : null;
  const board = boardOf(id, nodes);
  if (!board) return null;
  const Panel = board.panel;
  return <><SketchBar board={board} in3d={false} />{Panel && <Panel key={board.viewId} board={board} />}</>;
}
