/**
 * A plan's view range (as in Revit): the cut plane, Top, Bottom and View
 * Depth, each on the plan's level, the level above or below, or unlimited,
 * with an offset. What lies above the cut draws dashed; below Bottom, down to
 * the depth, faint ("beyond"). The range's problems are shown as it is set.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { frontView } from '../../viewport/frontView';
import { LengthField } from '../../shell/cells';
import { PendingDialog } from './PendingDialog';

interface Edge { level: string; offset: number }
interface RangeDto { cutHeight: number; range: { top: Edge; bottom: Edge; depth: Edge }; custom: boolean; problems: string[]; resolved: { cut: number; top: number | null; bottom: number | null; depth: number | null } }

const LEVELS: [string, string][] = [['self', 'this level'], ['above', 'the level above'], ['below', 'the level below'], ['unlimited', 'unlimited']];
const PROBLEMS: Record<string, string> = {
  topBelowCut: 'Top is below the cut plane.',
  bottomAboveCut: 'Bottom is above the cut plane.',
  depthAboveBottom: 'View depth is above Bottom.',
};
const fmt = (v: number | null) => (v == null ? 'unlimited' : `${v >= 0 ? '+' : '−'}${Math.abs(v).toFixed(2)}`);

export function ViewRangeDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [id, setId] = useState<string | null>(null);
  const [r, setR] = useState<RangeDto | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    void (async () => {
      if (useUi.getState().view !== 'plan') { useUi.setState({ notice: 'The view range is a plan\'s: open a plan.' }); close(); return; }
      setId(await frontView());
    })();
  }, []);
  useEffect(() => {
    if (!id) return;
    let live = true;
    void query<RangeDto>('view.range', { id }).then((x) => { if (live) setR(x); });
    return () => { live = false; };
  }, [id, nodes]);
  if (!id || !r) return <PendingDialog title="View range" />;
  const set = (edge: 'top' | 'bottom' | 'depth', patch: Partial<Edge>) => void execute('view.setRange', { id, [edge]: { ...r.range[edge], ...patch } });
  const row = (edge: 'top' | 'bottom' | 'depth', label: string, at: number | null) => (
    <tr key={edge}>
      <td>{label}</td>
      <td><select value={r.range[edge].level} onChange={(e) => set(edge, { level: e.target.value })}>{LEVELS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
      <td><LengthField metres={r.range[edge].offset} readOnly={r.range[edge].level === 'unlimited'} onCommit={(m) => { if (m != null) set(edge, { offset: m }); }} /></td>
      <td className="muted small">{fmt(at)}</td>
    </tr>
  );
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="View range" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">View range · {nodes.get(id)?.name ?? 'Plan'}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <table className="layer-table">
            <thead><tr><th /><th>Level</th><th>Offset</th><th>At</th></tr></thead>
            <tbody>
              {row('top', 'Top', r.resolved.top)}
              <tr>
                <td>Cut plane</td><td>this level</td>
                <td><LengthField metres={r.cutHeight} onCommit={(m) => { if (m != null) void execute('view.setRange', { id, cutHeight: m }); }} /></td>
                <td className="muted small">{fmt(r.resolved.cut)}</td>
              </tr>
              {row('bottom', 'Bottom', r.resolved.bottom)}
              {row('depth', 'View depth', r.resolved.depth)}
            </tbody>
          </table>
          {r.problems.length > 0 && <ul className="error-text">{r.problems.map((p) => <li key={p}>{PROBLEMS[p] ?? p}</li>)}</ul>}
          {error && <p className="error-text" role="alert">{error}</p>}
          <label className="field-line"><span className="eyebrow">Seen from</span>
            <select value={nodes.get(id)?.props.look === 'up' ? 'up' : 'down'} onChange={(e) => void execute('view.setRange', { id, look: e.target.value })}>
              <option value="down">above (plan)</option><option value="up">below (ceiling plan)</option>
            </select>
          </label>
          {nodes.get(id)?.props.look === 'up'
            ? <p className="muted small">Seen from below: the cut, and above it up to Top drawn as seen (soffits, beams, formwork), lying the way the plan does.</p>
            : <p className="muted small">Above the cut, up to Top: dashed (overhead). From the cut down to Bottom: drawn. Below Bottom, down to the depth: faint (beyond).</p>}
        </div>
        <footer className="dialog-foot">
          {r.custom && <button type="button" className="btn" onClick={() => void execute('view.setRange', { id, reset: true })}>Back to the default</button>}
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
