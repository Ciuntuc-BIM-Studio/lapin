/**
 * The remarks of a sheet drawn over it in the sheet view (docs/plan-cde.md,
 * stage 3): each one's outline in the review colour at its place on the
 * paper, a marker with the author's initials, the text in a tooltip; a click
 * puts it in front, and the card beside the paper shows its text, who and
 * when, the replies, and lets it be resolved, closed, reopened or deleted.
 * An overlay for the screen alone: the sheet prints without it.
 */

import { Check, RotateCcw, Trash2, X, XCircle } from 'lucide-react';
import { notify } from '../../../../app/store';
import { useSession } from '../../../../engine/session';
import { deleteRemarks, initials, KIND_LABELS, remarkShape, remarksOf, setRemarkStatus, useRemarkUi, type Remark } from './remarks';

const when = (iso: string | null) => { if (!iso) return ''; const d = new Date(iso); return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }); };

/** The outlines and markers, in the sheet's SVG; `toScreen` maps paper mm to pixels, `k` is pixels per mm. */
export function RemarksOverlay({ sheetId, toScreen, k }: { sheetId: string; toScreen: (p: { x: number; y: number }) => { x: number; y: number }; k: number }) {
  const nodes = useSession((s) => s.nodes);
  const highlight = useRemarkUi((s) => s.highlight);
  const remarks = remarksOf(nodes, sheetId).filter((r) => r.status !== 'closed');
  if (remarks.length === 0) return null;
  const pt = (p: number[]) => { const q = toScreen({ x: p[0], y: p[1] }); return `${q.x.toFixed(1)},${q.y.toFixed(1)}`; };
  return (
    <g className="sheet-remarks" data-testid="sheet-remarks">
      {remarks.map((r) => {
        const s = remarkShape(r);
        const m = toScreen({ x: s.marker[0], y: s.marker[1] });
        const on = highlight === r.id;
        const title = `${r.author ?? '—'}${r.date ? ' · ' + when(r.date) : ''}\n${r.text || KIND_LABELS[r.kind] || r.kind}`;
        return (
          <g key={r.id} className={`remark ${r.kind}${r.status === 'resolved' ? ' resolved' : ''}${on ? ' on' : ''}`}
            onPointerDown={(e) => { e.stopPropagation(); useRemarkUi.setState({ highlight: on ? null : r.id }); }}>
            <title>{title}</title>
            {s.box && !s.ellipse && (() => { const a = toScreen({ x: s.box[0], y: s.box[1] }); return <rect x={a.x} y={a.y} width={(s.box[2] - s.box[0]) * k} height={(s.box[3] - s.box[1]) * k} />; })()}
            {s.box && s.ellipse && (() => { const c = toScreen({ x: (s.box[0] + s.box[2]) / 2, y: (s.box[1] + s.box[3]) / 2 }); return <ellipse cx={c.x} cy={c.y} rx={((s.box[2] - s.box[0]) / 2) * k} ry={((s.box[3] - s.box[1]) / 2) * k} />; })()}
            {s.lines.map((line, i) => (s.closed
              ? <polygon key={i} points={line.map(pt).join(' ')} />
              : <polyline key={i} points={line.map(pt).join(' ')} />))}
            <g className="remark-marker" transform={`translate(${m.x} ${m.y})`}>
              <circle r={on ? 11 : 9} />
              <text>{initials(r.author)}</text>
            </g>
          </g>
        );
      })}
    </g>
  );
}

/** The card of the remark in front: beside the paper, with its thread and its status. */
export function RemarkCard({ sheetId }: { sheetId: string }) {
  const nodes = useSession((s) => s.nodes);
  const highlight = useRemarkUi((s) => s.highlight);
  const r = highlight ? remarksOf(nodes, sheetId).find((x) => x.id === highlight) : undefined;
  if (!r) return null;
  const close = () => useRemarkUi.setState({ highlight: null });
  return (
    <div className={`remark-card ${r.status}`} data-testid="remark-card">
      <header className="row">
        <span className="remark-initials" title={r.author ?? ''}>{initials(r.author)}</span>
        <span className="grow"><b>{r.author ?? '—'}</b><span className="muted small"> · <span>{KIND_LABELS[r.kind] ?? r.kind}</span>{r.date ? ` · ${when(r.date)}` : ''}</span></span>
        <button type="button" className="icon mini" title="Close" onClick={close}><X size={13} /></button>
      </header>
      {r.subject && <div className="muted small">{r.subject}</div>}
      <div className="remark-text">{r.text || <span className="muted">(no text)</span>}</div>
      {r.replies.map((p, i) => (
        <div key={i} className="remark-reply"><b>{p.author ?? '—'}</b><span className="muted small">{p.date ? ` · ${when(p.date)}` : ''}</span><div className="remark-text">{p.text}</div></div>
      ))}
      <div className="muted small">{r.source.file}{r.source.revision ? ` · ${r.source.revision}` : ''} · <span>{r.status}</span></div>
      <RemarkActions remark={r} onDeleted={close} />
    </div>
  );
}

/** Resolve, close, reopen and delete, as the card and the list offer them. */
export function RemarkActions({ remark, onDeleted }: { remark: Remark; onDeleted?: () => void }) {
  const set = async (status: Remark['status']) => { try { await setRemarkStatus([remark.id], status); } catch (e) { notify(e instanceof Error ? e.message : String(e)); } };
  const del = async () => {
    if (!window.confirm('Delete this remark? Reading the PDF again would bring it back.')) return;
    try { await deleteRemarks([remark.id]); onDeleted?.(); } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  return (
    <div className="row remark-actions">
      {remark.status !== 'resolved' && <button type="button" className="btn small" title="Mark the remark as dealt with" onClick={() => void set('resolved')}><Check size={12} /> Resolve</button>}
      {remark.status !== 'closed' && <button type="button" className="btn small" title="Close the remark: it leaves the sheet's overlay" onClick={() => void set('closed')}><XCircle size={12} /> Close remark</button>}
      {remark.status !== 'open' && <button type="button" className="btn small" title="Open the remark again" onClick={() => void set('open')}><RotateCcw size={12} /> Reopen</button>}
      <span className="grow" />
      <button type="button" className="icon mini" title="Delete the remark" onClick={() => void del()}><Trash2 size={12} /></button>
    </div>
  );
}
