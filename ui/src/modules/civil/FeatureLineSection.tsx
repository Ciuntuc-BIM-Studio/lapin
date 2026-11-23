/**
 * A feature line in the inspector (site grading T2): its terrain, the contour's elevation, whether it lies on the ground, the
 * tolerance within which survey points are left out of the triangulation, and a table of its corners: each corner's elevation (its
 * own plainly, one taken from the ground or a grade greyed) and, between corners, the grade (typed as 2 %, 1:50, -1,5 %).
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { notify } from '../../app/store';
import { formatLength, levelUnit, parseLength, parseLevel } from '../../engine/unitsStore';
import { formatGrade, lineOf, readGrade } from './featureLines';

/** A text field that commits on Enter or blur and takes the value back when the node changes under it. */
function CommitInput({ value, onCommit, className, label, testId, disabled, width = '6em', placeholder }: { placeholder?: string; value: string; onCommit: (text: string) => void; className?: string; label: string; testId?: string; disabled?: boolean; width?: string }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const commit = () => { if (text !== value) onCommit(text); };
  return <input type="text" className={className} value={text} disabled={disabled} aria-label={label} placeholder={placeholder} data-testid={testId} style={{ width, flex: 'none' }}
    onChange={(e) => setText(e.target.value)} onBlur={commit} onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); commit(); } else if (e.key === 'Escape') setText(value); }} />;
}

const unreadElevation = () => notify('That elevation does not read: type a number in the project\'s unit.');

export function FeatureLineSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const l = lineOf(node.props);
  const [keep, setKeep] = useState<'start' | 'end'>('start');
  const terrain = typeof node.props.terrain === 'string' ? nodes.get(node.props.terrain) : undefined;
  const elevation = typeof node.props.elevation === 'number' ? node.props.elevation : null;
  const drape = node.props.drape === true;
  const tolerance = typeof node.props.tolerance === 'number' ? node.props.tolerance : 0.05;
  const lengths = Array.isArray(node.props.segmentLengths) ? node.props.segmentLengths as number[] : [];
  const editable = node.editable;
  const update = (args: Record<string, unknown>) => void execute('civil.updateFeatureLine', { id: node.id, ...args });
  const unit = levelUnit();

  const setHeight = (i: number, text: string) => {
    const z = parseLevel(text);
    if (z == null || !Number.isFinite(z)) { unreadElevation(); return; }
    void execute('civil.setFeatureLinePoints', { id: node.id, indices: [i], z });
  };
  const setGrade = (k: number, text: string) => {
    const g = readGrade(text);
    if (g === undefined) { notify('That grade does not read: type 2 %, 1:50 or -1,5 %.'); return; }
    void execute('civil.setFeatureLineGrade', { id: node.id, segment: k, grade: g, keep });
  };
  const gradeOf = (k: number): { text: string; own: boolean } => {
    const own = l.grades[k];
    if (own != null) return { text: formatGrade(own), own: true };
    const j = (k + 1) % l.pts.length;
    const run = lengths[k];
    const [z0, z1] = [l.resolved[k], l.resolved[j]];
    return z0 != null && z1 != null && run > 1e-9 ? { text: formatGrade((z1 - z0) / run), own: false } : { text: '', own: false };
  };

  const rows: JSX.Element[] = [];
  l.pts.forEach((_, i) => {
    const z = l.resolved[i];
    rows.push(
      <tr key={`c${i}`} data-testid="feature-line-corner">
        <td className="muted small">{i + 1}</td>
        <td>
          <CommitInput value={z == null ? '' : formatLength(z, unit)} label={`Elevation of corner ${i + 1}`} testId={`feature-line-z${i}`} disabled={!editable}
            className={l.own[i] ? '' : 'muted'} onCommit={(t) => setHeight(i, t)} />
        </td>
        <td>{editable && l.own[i] && <button type="button" className="link small" title="Take this corner's height from the ground again" onClick={() => void execute('civil.setFeatureLinePoints', { id: node.id, indices: [i], ground: true })}>to ground</button>}</td>
      </tr>,
    );
    if (i < l.arcs.length) {
      const { text, own } = gradeOf(i);
      // A contour's segments are level; a closed line's closing segment has no grade of its own (the engine refuses it).
      const locked = elevation != null || (l.closed && i === l.pts.length - 1);
      rows.push(
        <tr key={`g${i}`} data-testid="feature-line-grade">
          <td />
          <td colSpan={2} className="small">
            <span className="muted">↓ </span>
            <CommitInput value={text} label={`Grade between corners ${i + 1} and ${(i + 1) % l.pts.length + 1}`} testId={`feature-line-grade${i}`} disabled={!editable || locked} className={own ? '' : 'muted'} width="5.5em" onCommit={(t) => setGrade(i, t)} />
            {lengths[i] != null && <span className="muted"> {formatLength(lengths[i], unit)} {unit === 'ft-in' || unit === 'in' ? '' : unit}</span>}
          </td>
        </tr>,
      );
    }
  });

  return (
    <div className="layers" data-testid="feature-line-section">
      <dl className="fields">
        <dt>Terrain</dt>
        <dd>{terrain ? terrain.name : <span className="muted">—</span>}</dd>
        <dt>Contour elevation</dt>
        <dd>
          <CommitInput value={elevation == null ? '' : formatLength(elevation, unit)} label="Elevation of the contour" testId="feature-line-elevation" disabled={!editable} width="6em" placeholder="—"
            onCommit={(t) => { if (t.trim() === '') { update({ elevation: null }); return; } const z = parseLevel(t); if (z == null || !Number.isFinite(z)) unreadElevation(); else update({ elevation: z }); }} />
          <div className="muted small">{elevation != null ? 'Every corner at this one elevation. Empty: a line with heights of its own.' : 'Type an elevation to make it a contour.'}</div>
        </dd>
        <dt>On the ground</dt>
        <dd><input type="checkbox" checked={drape} disabled={!editable || elevation != null} data-testid="feature-line-drape" onChange={(e) => update({ drape: e.target.checked })} /></dd>
        <dt>Closed line</dt>
        <dd><input type="checkbox" checked={l.closed} disabled={!editable || l.pts.length < 3} data-testid="feature-line-closed-prop" onChange={(e) => update({ closed: e.target.checked })} /></dd>
        <dt>Tolerance</dt>
        <dd>
          <CommitInput value={formatLength(tolerance, 'm')} label="Tolerance" testId="feature-line-tolerance" disabled={!editable} width="5em"
            onCommit={(t) => { const v = parseLength(t, 'm'); if (v == null || !(v > 0)) notify('That length does not read: type a number in metres.'); else update({ tolerance: v }); }} /> <span className="muted">m</span>
          <div className="muted small">Survey points this near the line are left out of the terrain.</div>
        </dd>
        <dt>A typed grade keeps</dt>
        <dd>
          <select value={keep} aria-label="Which end a typed grade keeps" onChange={(e) => setKeep(e.target.value as 'start' | 'end')} data-testid="feature-line-keep">
            <option value="start">its start</option>
            <option value="end">its end</option>
          </select>
        </dd>
      </dl>
      <table data-testid="feature-line-corners">
        <tbody>{rows}</tbody>
      </table>
    </div>
  );
}
