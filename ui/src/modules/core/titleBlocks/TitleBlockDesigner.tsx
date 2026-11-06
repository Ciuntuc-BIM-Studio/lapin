/**
 * The title block designer (docs/plan-title-blocks.md, decision 340): the block's box in paper millimetres, drawn with lines,
 * rectangles, polylines, fills and hatches, texts, an image (a logo) and the revision table; live text inserted from the
 * fields panel (the project's, the sheet's, the system's), shown with its value on a sample sheet — a field with no value as
 * its token, greyed; an unknown one in red. Snaps to the items' ends, corners and middles, then to the grid; Shift keeps a
 * line straight. Select and drag to move, a handle to reshape, Delete, arrows to nudge, Ctrl+Z / Ctrl+Y, Ctrl+D to
 * duplicate, Esc. Save writes the template as one undo step.
 */

import { useCallback, useEffect, useMemo, useRef, useState, type PointerEvent as RPointerEvent } from 'react';
import {
  Braces, Image as ImageIcon, Minus, MousePointer2, PenLine, Redo2, Save, Square, Table2, Trash2, Type, Undo2, X, ZoomIn, ZoomOut,
} from 'lucide-react';
import { notify } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import { t as tr } from '../../../i18n';
import { prepareImage } from '../OwnImage';
import { getTitleBlock, titleBlockFields, updateTitleBlock, type TbFields } from './titleBlocks';
import {
  ANCHORS, bounds, dragHandle, handles, insertToken, itemAt, moveItem, newItem, round, snap, textRuns, type Pt, type TbItem, type TbTemplate, type TbType,
} from './titleBlockModel';

type Tool = 'select' | TbType;

const TOOLS: { id: Tool; label: string; icon: typeof Square; hint: string }[] = [
  { id: 'select', label: 'Select', icon: MousePointer2, hint: 'Click an item to change it, drag to move it, drag a handle to reshape it' },
  { id: 'line', label: 'Line', icon: Minus, hint: 'Click the start, then the end (Shift: straight)' },
  { id: 'rect', label: 'Rectangle', icon: Square, hint: 'Click one corner, then the opposite one' },
  { id: 'polyline', label: 'Polyline', icon: PenLine, hint: 'Click the points; double-click or Enter ends it, a click on the first point closes it' },
  { id: 'text', label: 'Text', icon: Type, hint: 'Click where the text starts' },
  { id: 'image', label: 'Image', icon: ImageIcon, hint: 'Click two corners, then choose a PNG or JPEG (a logo)' },
  { id: 'revisions', label: 'Revision table', icon: Table2, hint: 'Click two corners: the issues listed in rows, newest last' },
];

const GRIDS = [0, 0.5, 1, 2.5, 5];
const PAD = 8;

/** A number field committed on Enter or when left, only when it changed. */
function Num({ label, value, onCommit, step = 0.5, min }: { label: string; value: number | undefined; onCommit: (v: number) => void; step?: number; min?: number }) {
  return (
    <label className="tb-num">
      <span className="muted small">{label}</span>
      <input type="number" step={step} min={min} key={String(value)} defaultValue={value ?? ''} data-field={`tb-prop-${label.toLowerCase().replace(/\W+/g, '-')}`}
        onBlur={(e) => { const v = parseFloat(e.target.value); if (Number.isFinite(v) && v !== value) onCommit(round(v)); }}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
    </label>
  );
}

function Colour({ label, value, onChange, none }: { label: string; value: string | null | undefined; onChange: (v: string | null) => void; none?: boolean }) {
  return (
    <label className="tb-num">
      <span className="muted small">{label}</span>
      <span className="row">
        {none && <input type="checkbox" checked={!!value} onChange={(e) => onChange(e.target.checked ? value || '#d9d9d9' : null)} aria-label={label} />}
        <input type="color" value={value || '#000000'} disabled={none && !value} onChange={(e) => onChange(e.target.value)} />
      </span>
    </label>
  );
}

export function TitleBlockDesigner({ id, onClose }: { id: string; onClose: () => void }) {
  const nodes = useSession((s) => s.nodes);
  const [name, setName] = useState('');
  const [t, setT] = useState<TbTemplate | null>(null);
  const past = useRef<TbTemplate[]>([]);
  const future = useRef<TbTemplate[]>([]);
  const [dirty, setDirty] = useState(false);
  const [tool, setTool] = useState<Tool>('select');
  const [sel, setSel] = useState<number | null>(null);
  const [grid, setGrid] = useState(1);
  const [objectSnap, setObjectSnap] = useState(true);
  const [zoom, setZoom] = useState(1);
  const [fields, setFields] = useState<TbFields | null>(null);
  const [sample, setSample] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [pendingText, setPendingText] = useState<string | null>(null);
  // A shape being drawn: its points so far and where the pointer is; a drag of an item or a handle.
  const [draft, setDraft] = useState<{ pts: Pt[]; at: Pt | null } | null>(null);
  const drag = useRef<{ kind: 'move' | 'handle'; index: number; handle?: number; start: Pt; orig: TbItem; moved: boolean } | null>(null);
  const [hover, setHover] = useState<{ p: Pt; on: string } | null>(null);
  const svg = useRef<SVGSVGElement>(null);
  const textBox = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    void getTitleBlock(id).then((r) => { if (r) { setT(r.template); setName(r.name); } });
  }, [id]);
  const sheets = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinSheet'), [nodes]);
  const version = useSession((s) => s.version);
  useEffect(() => { void titleBlockFields(sample).then((r) => { if (r) { setFields(r); if (!sample && r.sheetId) setSample(r.sheetId); } }); }, [sample, version]); // eslint-disable-line react-hooks/exhaustive-deps
  const values = useMemo(() => new Map((fields?.fields ?? []).map((f) => [f.key, f.value ?? null] as [string, string | null])), [fields]);

  /** The template changed: kept for Undo unless `quiet` (a drag's every step). */
  const change = useCallback((next: TbTemplate, quiet = false) => {
    setT((cur) => {
      if (cur && !quiet) { past.current.push(cur); future.current = []; }
      return next;
    });
    setDirty(true);
  }, []);
  const setItem = (i: number, item: TbItem, quiet = false) => t && change({ ...t, items: t.items.map((x, k) => (k === i ? item : x)) }, quiet);
  const patch = (p: Partial<TbItem>) => { if (t && sel != null) setItem(sel, { ...t.items[sel], ...p } as TbItem); };
  const undo = () => { const prev = past.current.pop(); if (prev && t) { future.current.push(t); setT(prev); setSel(null); setDirty(true); } };
  const redo = () => { const next = future.current.pop(); if (next && t) { past.current.push(t); setT(next); setSel(null); setDirty(true); } };
  const add = (item: TbItem) => { if (!t) return; change({ ...t, items: [...t.items, item] }); setSel(t.items.length); };
  const remove = () => { if (!t || sel == null) return; change({ ...t, items: t.items.filter((_, k) => k !== sel) }); setSel(null); };

  /** A pointer's place on the block (mm, y up from its bottom-left) and the snapping tolerance (6 px). */
  const where = (e: { clientX: number; clientY: number }): { p: Pt; tol: number } | null => {
    const el = svg.current;
    if (!el || !t) return null;
    const m = el.getScreenCTM();
    if (!m) return null;
    const pt = el.createSVGPoint();
    pt.x = e.clientX; pt.y = e.clientY;
    const q = pt.matrixTransform(m.inverse());
    return { p: [q.x, t.h - q.y], tol: 6 / m.a };
  };
  const snapped = (p: Pt, tol: number, from?: Pt | null, ortho?: boolean, except?: number) =>
    snap(p, t?.items ?? [], { grid, tol, objects: objectSnap, from, ortho, except });

  const finishDraft = (pts: Pt[]) => {
    setDraft(null);
    if (!t) return;
    if (tool === 'polyline' && pts.length >= 2) {
      const closed = pts.length > 2 && Math.hypot(pts[0][0] - pts[pts.length - 1][0], pts[0][1] - pts[pts.length - 1][1]) < 1e-6;
      add({ type: 'polyline', pts: closed ? pts.slice(0, -1) : pts, closed, stroke: 0.25 });
    }
  };

  const chooseImage = (i: number) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/png,image/jpeg';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      try {
        const { mime, data } = await prepareImage(file);
        const r = await execute('image.add', { name: file.name, mime, data }) as { id?: string } | null;
        if (r?.id) setT((cur) => { if (!cur) return cur; past.current.push(cur); return { ...cur, items: cur.items.map((x, k) => (k === i && x.type === 'image' ? { ...x, imageId: r.id } : x)) }; });
        setDirty(true);
      } catch (err) { notify(`The image could not be read: ${err instanceof Error ? err.message : String(err)}`); }
    };
    input.click();
  };

  const onDown = (e: RPointerEvent<SVGSVGElement>) => {
    if (!t || e.button !== 0) return;
    const w = where(e);
    if (!w) return;
    if (tool === 'select') {
      // A handle of the selected item first, then the topmost item under the pointer.
      if (sel != null) {
        const hs = handles(t.items[sel]);
        const h = hs.findIndex((q) => Math.hypot(q[0] - w.p[0], q[1] - w.p[1]) <= w.tol);
        if (h >= 0) { drag.current = { kind: 'handle', index: sel, handle: h, start: w.p, orig: t.items[sel], moved: false }; svg.current?.setPointerCapture(e.pointerId); return; }
      }
      const i = itemAt(t.items, w.p, w.tol);
      setSel(i >= 0 ? i : null);
      if (i >= 0) { drag.current = { kind: 'move', index: i, start: w.p, orig: t.items[i], moved: false }; svg.current?.setPointerCapture(e.pointerId); }
      return;
    }
    const last = draft?.pts[draft.pts.length - 1] ?? null;
    const s = snapped(w.p, w.tol, last, e.shiftKey).p;
    if (tool === 'text') {
      add(newItem('text', s, pendingText ?? tr('Text')));
      setPendingText(null);
      setTool('select');
      setTimeout(() => { textBox.current?.focus(); textBox.current?.select(); }, 30);
      return;
    }
    if (tool === 'polyline') {
      const pts = [...(draft?.pts ?? [])];
      // A click on the first point closes it.
      if (pts.length > 2 && Math.hypot(pts[0][0] - s[0], pts[0][1] - s[1]) <= w.tol) { finishDraft([...pts, pts[0]]); return; }
      setDraft({ pts: [...pts, s], at: s });
      return;
    }
    if (!draft) { setDraft({ pts: [s], at: s }); return; }
    const a = draft.pts[0];
    setDraft(null);
    if (tool === 'line') { if (Math.hypot(s[0] - a[0], s[1] - a[1]) > 0.01) add({ type: 'line', a, b: s, w: 0.25 }); return; }
    const [x0, y0, x1, y1] = [Math.min(a[0], s[0]), Math.min(a[1], s[1]), Math.max(a[0], s[0]), Math.max(a[1], s[1])];
    if (x1 - x0 < 0.5 || y1 - y0 < 0.5) return;
    const box = { x: round(x0), y: round(y0), w: round(x1 - x0), h: round(y1 - y0) };
    if (tool === 'rect') add({ type: 'rect', ...box, stroke: 0.25 });
    else if (tool === 'revisions') add({ type: 'revisions', ...box, rowH: 5 });
    else if (tool === 'image') { add({ type: 'image', ...box }); chooseImage(t.items.length); }
    setTool('select');
  };

  const onMove = (e: RPointerEvent<SVGSVGElement>) => {
    if (!t) return;
    const w = where(e);
    if (!w) return;
    const d = drag.current;
    if (d) {
      if (!d.moved && Math.hypot(w.p[0] - d.start[0], w.p[1] - d.start[1]) < w.tol / 2) return;
      if (d.kind === 'handle') {
        const s = snapped(w.p, w.tol, null, false, d.index).p;
        setItem(d.index, dragHandle(d.orig, d.handle!, s), d.moved);
      } else {
        // The item's first key point goes where it snaps; the rest follows.
        const anchor = handles(d.orig)[0] ?? (d.orig.type === 'text' ? d.orig.at : [bounds(d.orig)[0], bounds(d.orig)[1]] as Pt);
        const target = snapped([anchor[0] + w.p[0] - d.start[0], anchor[1] + w.p[1] - d.start[1]], w.tol, e.shiftKey ? anchor : null, e.shiftKey, d.index).p;
        setItem(d.index, moveItem(d.orig, target[0] - anchor[0], target[1] - anchor[1]), d.moved);
      }
      d.moved = true;
      return;
    }
    if (tool === 'select') { setHover(null); return; }
    const last = draft?.pts[draft.pts.length - 1] ?? null;
    const s = snapped(w.p, w.tol, last, e.shiftKey);
    setHover(s);
    if (draft) setDraft({ ...draft, at: s.p });
  };

  const onUp = () => { drag.current = null; };

  // The keyboard while the designer is open (never while typing in a field).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const typing = (e.target as HTMLElement)?.closest('input, textarea, select');
      if ((e.ctrlKey || e.metaKey) && !typing && e.key.toLowerCase() === 'z') { e.preventDefault(); if (e.shiftKey) redo(); else undo(); return; }
      if ((e.ctrlKey || e.metaKey) && !typing && e.key.toLowerCase() === 'y') { e.preventDefault(); redo(); return; }
      if (typing) return;
      if (e.key === 'Escape') { e.stopPropagation(); if (draft) { if (tool === 'polyline' && draft.pts.length >= 2) finishDraft(draft.pts); else setDraft(null); } else if (tool !== 'select') setTool('select'); else setSel(null); return; }
      if (e.key === 'Enter' && draft && tool === 'polyline') { finishDraft(draft.pts); return; }
      if (!t || sel == null) return;
      if (e.key === 'Delete' || e.key === 'Backspace') { e.preventDefault(); remove(); return; }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'd') { e.preventDefault(); add(moveItem(t.items[sel], grid || 1, -(grid || 1))); return; }
      const step = (grid || 0.5) * (e.shiftKey ? 10 : 1);
      const delta = ({ ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, step], ArrowDown: [0, -step] } as Record<string, number[]>)[e.key];
      if (delta) { e.preventDefault(); setItem(sel, moveItem(t.items[sel], delta[0], delta[1])); }
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  });

  const save = async () => {
    if (!t) return;
    await updateTitleBlock(id, { name: name.trim() || undefined, template: t });
    setDirty(false);
    notify('Title block saved.');
  };
  const close = () => { if (!dirty || window.confirm(tr('Close the designer without saving your changes?'))) onClose(); };

  /** A field clicked: into the selected text, else a new text placed by the next click. */
  const pickField = (key: string) => {
    if (t && sel != null && t.items[sel].type === 'text') {
      const item = t.items[sel] as Extract<TbItem, { type: 'text' }>;
      setItem(sel, { ...item, text: insertToken(item.text, key) });
      return;
    }
    setPendingText(`{${key}}`);
    setTool('text');
  };

  if (!t) return <div className="dialog-backdrop"><div className="dialog"><p className="muted">Reading…</p></div></div>;
  const item = sel != null ? t.items[sel] : null;
  const vb = { x: -PAD, y: -PAD, w: t.w + 2 * PAD, h: t.h + 2 * PAD };
  const Y = (y: number) => t.h - y;
  const imageOf = (iid?: string) => { const n = iid ? nodes.get(iid) : undefined; return typeof n?.props.data === 'string' ? `data:${n.props.mime};base64,${n.props.data}` : null; };
  const groups: { id: string; label: string }[] = [{ id: 'project', label: 'Project' }, { id: 'sheet', label: 'Sheet' }, { id: 'system', label: 'System' }];
  const shownFields = (fields?.fields ?? []).filter((f) => !filter || `${f.label} ${f.key} ${f.value ?? ''}`.toLowerCase().includes(filter.toLowerCase()));

  const renderItem = (it: TbItem, i: number) => {
    const stroke = (w?: number) => Math.max(w ?? 0.25, 0.08);
    switch (it.type) {
      case 'line': return (it.w ?? 0.25) > 0 ? <line key={i} x1={it.a[0]} y1={Y(it.a[1])} x2={it.b[0]} y2={Y(it.b[1])} stroke={it.color ?? '#000'} strokeWidth={stroke(it.w)} strokeDasharray={it.dash?.join(' ')} strokeLinecap="round" /> : null;
      case 'rect': return <rect key={i} x={it.x} y={Y(it.y + it.h)} width={it.w} height={it.h} fill={it.fill ?? (it.hatch ? 'url(#tb-hatch)' : 'none')} stroke={(it.stroke ?? 0.25) > 0 ? it.color ?? '#000' : 'none'} strokeWidth={stroke(it.stroke)} />;
      case 'polyline': {
        const pts = it.pts.map((p) => `${p[0]},${Y(p[1])}`).join(' ');
        return it.closed ? <polygon key={i} points={pts} fill={it.fill ?? (it.hatch ? 'url(#tb-hatch)' : 'none')} stroke={(it.stroke ?? 0.25) > 0 ? it.color ?? '#000' : 'none'} strokeWidth={stroke(it.stroke)} />
          : <polyline key={i} points={pts} fill="none" stroke={it.color ?? '#000'} strokeWidth={stroke(it.stroke)} strokeLinejoin="round" />;
      }
      case 'text': {
        const style = fields?.textStyles.find((s) => s.id === it.style);
        const h = it.h ?? style?.height ?? 2.5;
        const runs = textRuns(it.text, values);
        return (
          <text key={i} x={it.at[0]} y={Y(it.at[1])} fontSize={h} fontWeight={(it.bold ?? style?.bold) ? 700 : 400} fontStyle={(it.italic ?? style?.italic) ? 'italic' : undefined}
            textAnchor={it.align ?? 'start'} dominantBaseline={it.valign === 'middle' ? 'central' : 'alphabetic'} fill={it.color ?? '#000'} fontFamily="Arial, Helvetica, sans-serif"
            transform={it.angle ? `rotate(${-it.angle} ${it.at[0]} ${Y(it.at[1])})` : undefined} data-no-i18n>
            {runs.map((r, k) => <tspan key={k} className={`tb-run-${r.kind}`}>{r.text}</tspan>)}
          </text>
        );
      }
      case 'image': {
        const href = imageOf(it.imageId);
        return href ? <image key={i} href={href} x={it.x} y={Y(it.y + it.h)} width={it.w} height={it.h} preserveAspectRatio="none" />
          : <g key={i}><rect x={it.x} y={Y(it.y + it.h)} width={it.w} height={it.h} fill="#f1f3f5" stroke="#9aa3ad" strokeWidth={0.2} strokeDasharray="1 1" /><text x={it.x + it.w / 2} y={Y(it.y + it.h / 2)} fontSize={2.2} textAnchor="middle" fill="#6b7280">{tr('Image')}</text></g>;
      }
      case 'revisions': {
        const rowH = it.rowH ?? 5;
        const rows = Math.max(0, Math.floor(it.h / rowH) - 1);
        const cols = [0, it.w * 0.12, it.w * 0.32, it.w * 0.47];
        const top = it.y + it.h;
        const labels = it.labels ?? {};
        return (
          <g key={i} stroke="#000" strokeWidth={stroke(it.stroke)}>
            <rect x={it.x} y={Y(top)} width={it.w} height={it.h} fill="none" />
            {Array.from({ length: rows }, (_, k) => <line key={k} x1={it.x} x2={it.x + it.w} y1={Y(top - (k + 1) * rowH)} y2={Y(top - (k + 1) * rowH)} />)}
            {cols.slice(1).map((c, k) => <line key={`c${k}`} x1={it.x + c} x2={it.x + c} y1={Y(it.y)} y2={Y(top)} />)}
            {[labels.revision ?? 'REV', labels.date ?? 'DATE', labels.status ?? 'STATUS', labels.description ?? 'DESCRIPTION'].map((l, k) =>
              <text key={`h${k}`} x={it.x + cols[k] + 1.2} y={Y(top - rowH + rowH * 0.3)} fontSize={Math.min(1.8, rowH * 0.5)} stroke="none" fill="#000" data-no-i18n>{l}</text>)}
            {rows > 0 && ['P01', '06.11.2026', 'S2', tr('For information')].map((l, k) =>
              <text key={`r${k}`} x={it.x + cols[k] + 1.2} y={Y(top - 2 * rowH + rowH * 0.3)} fontSize={Math.min(2.5, rowH * 0.5)} stroke="none" fill="#8a8f98">{l}</text>)}
          </g>
        );
      }
    }
  };

  const gridLines = () => {
    if (!grid) return null;
    const step = grid < 2.5 ? 5 : grid * 2;
    const o: JSX.Element[] = [];
    for (let x = 0; x <= t.w + 1e-9; x += step) o.push(<line key={`x${x}`} x1={x} x2={x} y1={0} y2={t.h} />);
    for (let y = 0; y <= t.h + 1e-9; y += step) o.push(<line key={`y${y}`} x1={0} x2={t.w} y1={Y(y)} y2={Y(y)} />);
    return <g className="tb-grid">{o}</g>;
  };

  const anchorMark = (() => {
    const [cx, cy] = [t.anchor.endsWith('Right') ? t.w : 0, t.anchor.startsWith('bottom') ? 0 : t.h];
    const sx = t.anchor.endsWith('Right') ? -1 : 1, sy = t.anchor.startsWith('bottom') ? 1 : -1;
    return <polygon className="tb-anchor" points={`${cx},${Y(cy)} ${cx + sx * 4},${Y(cy)} ${cx},${Y(cy + sy * 4)}`} />;
  })();

  const draftShape = (() => {
    if (!draft || !draft.at) return null;
    const a = draft.pts[0], b = draft.at;
    if (tool === 'line') return <line className="tb-draft" x1={a[0]} y1={Y(a[1])} x2={b[0]} y2={Y(b[1])} />;
    if (tool === 'polyline') return <polyline className="tb-draft" points={[...draft.pts, b].map((p) => `${p[0]},${Y(p[1])}`).join(' ')} fill="none" />;
    return <rect className="tb-draft" x={Math.min(a[0], b[0])} y={Y(Math.max(a[1], b[1]))} width={Math.abs(b[0] - a[0])} height={Math.abs(b[1] - a[1])} fill="none" />;
  })();

  const selection = (() => {
    if (!item) return null;
    const [x0, y0, x1, y1] = bounds(item);
    return (
      <g className="tb-selection">
        <rect x={x0 - 0.6} y={Y(y1 + 0.6)} width={x1 - x0 + 1.2} height={y1 - y0 + 1.2} fill="none" />
        {handles(item).map((h, k) => <rect key={k} className="tb-handle" x={h[0] - 0.8} y={Y(h[1]) - 0.8} width={1.6} height={1.6} />)}
      </g>
    );
  })();

  const textItem = item?.type === 'text' ? item : null;
  return (
    <div className="dialog-backdrop">
      <div className="dialog tb-designer" role="dialog" aria-label="Title block designer" data-dialog="title-block-designer" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Title block designer</span>
          <input className="tb-name" value={name} onChange={(e) => { setName(e.target.value); setDirty(true); }} aria-label="Name" data-field="tb-name" data-no-i18n />
          <span className="muted small" data-no-i18n>{`${t.w} × ${t.h} mm`}</span>
          <span className="grow" />
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="tb-body">
          <nav className="tb-tools" aria-label="Tools">
            {TOOLS.map((x) => (
              <button key={x.id} type="button" className={`icon tb-tool${tool === x.id ? ' active' : ''}`} title={`${tr(x.label)}: ${tr(x.hint)}`} data-field={`tb-tool-${x.id}`}
                onClick={() => { setTool(x.id); setDraft(null); setPendingText(null); }}><x.icon size={16} /></button>
            ))}
            <span className="tb-sep" />
            <button type="button" className="icon" title="Undo (Ctrl+Z)" onClick={undo} disabled={past.current.length === 0}><Undo2 size={15} /></button>
            <button type="button" className="icon" title="Redo (Ctrl+Y)" onClick={redo} disabled={future.current.length === 0}><Redo2 size={15} /></button>
            <button type="button" className="icon" title="Delete the selected item (Delete)" onClick={remove} disabled={sel == null} data-field="tb-delete-item"><Trash2 size={15} /></button>
            <span className="tb-sep" />
            <button type="button" className="icon" title="Zoom in" onClick={() => setZoom((z) => Math.min(6, z * 1.25))}><ZoomIn size={15} /></button>
            <button type="button" className="icon" title="Zoom out" onClick={() => setZoom((z) => Math.max(0.5, z / 1.25))}><ZoomOut size={15} /></button>
          </nav>
          <section className="tb-canvas-wrap">
            <div className="tb-statusbar small">
              <span className="muted">{tr(TOOLS.find((x) => x.id === tool)?.hint ?? '')}{pendingText ? ` · ${pendingText}` : ''}</span>
              <span className="grow" />
              <label className="row">Grid
                <select value={grid} onChange={(e) => setGrid(parseFloat(e.target.value))} data-field="tb-grid">
                  {GRIDS.map((g) => <option key={g} value={g}>{g === 0 ? tr('off') : `${g} mm`}</option>)}
                </select>
              </label>
              <label className="row"><input type="checkbox" checked={objectSnap} onChange={(e) => setObjectSnap(e.target.checked)} />Snap to items</label>
              {hover && <span className="muted" data-no-i18n>{`x ${hover.p[0].toFixed(2)}  y ${hover.p[1].toFixed(2)}`}</span>}
            </div>
            <div className="tb-canvas">
              <svg ref={svg} data-testid="tb-canvas" viewBox={`${vb.x} ${vb.y} ${vb.w} ${vb.h}`} style={{ width: `${zoom * 100}%` }} className={`tool-${tool}`}
                onPointerDown={onDown} onPointerMove={onMove} onPointerUp={onUp} onPointerLeave={() => setHover(null)}
                onDoubleClick={() => { if (tool === 'polyline' && draft) finishDraft(draft.pts.slice(0, -1).length >= 2 ? draft.pts.slice(0, -1) : draft.pts); }}>
                <defs>
                  <pattern id="tb-hatch" width="2" height="2" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><line x1="0" y1="0" x2="0" y2="2" stroke="#555" strokeWidth="0.15" /></pattern>
                </defs>
                <rect x={0} y={0} width={t.w} height={t.h} className="tb-paper" />
                {gridLines()}
                {t.items.map(renderItem)}
                {anchorMark}
                {selection}
                {draftShape}
                {hover && tool !== 'select' && <circle className={`tb-snap tb-snap-${hover.on}`} cx={hover.p[0]} cy={Y(hover.p[1])} r={0.7} />}
              </svg>
            </div>
          </section>
          <aside className="tb-side">
            {!item ? (
              <div className="tb-props" data-field="tb-template-props">
                <h4 className="eyebrow">Title block</h4>
                <div className="tb-grid2">
                  <Num label="Width" value={t.w} min={10} onCommit={(v) => change({ ...t, w: v })} />
                  <Num label="Height" value={t.h} min={10} onCommit={(v) => change({ ...t, h: v })} />
                </div>
                <label className="tb-num"><span className="muted small">Corner of the frame</span>
                  <select value={t.anchor} onChange={(e) => change({ ...t, anchor: e.target.value as TbTemplate['anchor'] })} data-field="tb-anchor">
                    {ANCHORS.map((a) => <option key={a} value={a}>{{ bottomRight: 'Bottom right', bottomLeft: 'Bottom left', topRight: 'Top right', topLeft: 'Top left' }[a]}</option>)}
                  </select>
                </label>
                <label className="row"><input type="checkbox" checked={t.frame.show} onChange={(e) => change({ ...t, frame: { ...t.frame, show: e.target.checked } })} />Draw the frame</label>
                <div className="tb-grid2">
                  {(['left', 'top', 'right', 'bottom'] as const).map((k) => <Num key={k} label={{ left: 'Left margin', top: 'Top margin', right: 'Right margin', bottom: 'Bottom margin' }[k]} value={t.frame[k]} min={0} onCommit={(v) => change({ ...t, frame: { ...t.frame, [k]: v } })} />)}
                </div>
                <label className="tb-num"><span className="muted small">Revision table</span>
                  <select value={t.revisions} onChange={(e) => change({ ...t, revisions: e.target.value as 'above' | 'none' })}>
                    <option value="above">Above the block (unless an item draws it)</option>
                    <option value="none">None (unless an item draws it)</option>
                  </select>
                </label>
                <p className="muted small">The block sits in the frame's corner on every paper, A4 to A0. Draw with the tools on the left; insert live fields from the list below.</p>
              </div>
            ) : (
              <div className="tb-props" data-field="tb-item-props">
                <h4 className="eyebrow">{tr(TOOLS.find((x) => x.id === item.type)?.label ?? item.type)}</h4>
                {item.type === 'line' && (
                  <div className="tb-grid2">
                    <Num label="x1" value={item.a[0]} onCommit={(v) => patch({ a: [v, item.a[1]] })} /><Num label="y1" value={item.a[1]} onCommit={(v) => patch({ a: [item.a[0], v] })} />
                    <Num label="x2" value={item.b[0]} onCommit={(v) => patch({ b: [v, item.b[1]] })} /><Num label="y2" value={item.b[1]} onCommit={(v) => patch({ b: [item.b[0], v] })} />
                    <Num label="Pen (mm)" value={item.w ?? 0.25} step={0.05} min={0} onCommit={(v) => patch({ w: v })} />
                    <Colour label="Colour" value={item.color ?? '#000000'} onChange={(c) => patch({ color: c ?? undefined })} />
                  </div>
                )}
                {(item.type === 'rect' || item.type === 'image' || item.type === 'revisions') && (
                  <div className="tb-grid2">
                    <Num label="x" value={item.x} onCommit={(v) => patch({ x: v })} /><Num label="y" value={item.y} onCommit={(v) => patch({ y: v })} />
                    <Num label="Width" value={item.w} min={0.5} onCommit={(v) => patch({ w: v })} /><Num label="Height" value={item.h} min={0.5} onCommit={(v) => patch({ h: v })} />
                  </div>
                )}
                {(item.type === 'rect' || item.type === 'polyline') && (
                  <>
                    <div className="tb-grid2">
                      <Num label="Pen (mm)" value={item.stroke ?? 0.25} step={0.05} min={0} onCommit={(v) => patch({ stroke: v })} />
                      <Colour label="Colour" value={item.color ?? '#000000'} onChange={(c) => patch({ color: c ?? undefined })} />
                      <Colour label="Fill" value={item.fill ?? null} none onChange={(c) => patch({ fill: c })} />
                    </div>
                    {item.type === 'polyline' && <label className="row"><input type="checkbox" checked={!!item.closed} onChange={(e) => patch({ closed: e.target.checked })} />Closed</label>}
                    <label className="tb-num"><span className="muted small">Hatch</span>
                      <select value={item.hatch?.pattern ?? ''} onChange={(e) => patch({ hatch: e.target.value ? { pattern: e.target.value, scale: item.hatch?.scale ?? 1 } : null })} data-field="tb-hatch">
                        <option value="">none</option>
                        {(fields?.patterns ?? []).map((p) => <option key={p} value={p} data-no-i18n>{p}</option>)}
                      </select>
                    </label>
                    {item.hatch && <Num label="Hatch scale" value={item.hatch.scale ?? 1} step={0.1} min={0.05} onCommit={(v) => patch({ hatch: { ...item.hatch!, scale: v } })} />}
                  </>
                )}
                {textItem && (
                  <>
                    <label className="tb-num"><span className="muted small">{'Text (fields as {project.name})'}</span>
                      <textarea ref={textBox} rows={2} key={`${sel}`} defaultValue={textItem.text} data-field="tb-text" data-no-i18n
                        onBlur={(e) => { if (e.target.value !== textItem.text) patch({ text: e.target.value }); }} />
                    </label>
                    <div className="tb-grid2">
                      <Num label="x" value={textItem.at[0]} onCommit={(v) => patch({ at: [v, textItem.at[1]] })} /><Num label="y" value={textItem.at[1]} onCommit={(v) => patch({ at: [textItem.at[0], v] })} />
                      <label className="tb-num"><span className="muted small">Text style</span>
                        <select value={textItem.style ?? ''} onChange={(e) => patch({ style: e.target.value || undefined, h: e.target.value ? undefined : textItem.h ?? 2.5 })} data-field="tb-text-style">
                          <option value="">its own height</option>
                          {(fields?.textStyles ?? []).map((s) => <option key={s.id} value={s.id} data-no-i18n>{`${s.name} (${s.height} mm)`}</option>)}
                        </select>
                      </label>
                      {!textItem.style && <Num label="Height (mm)" value={textItem.h ?? 2.5} step={0.1} min={0.5} onCommit={(v) => patch({ h: v })} />}
                      <label className="tb-num"><span className="muted small">Align</span>
                        <select value={textItem.align ?? 'start'} onChange={(e) => patch({ align: e.target.value as 'start' | 'middle' | 'end' })}>
                          <option value="start">Left</option><option value="middle">Centre</option><option value="end">Right</option>
                        </select>
                      </label>
                      <Num label="Angle" value={textItem.angle ?? 0} step={90} onCommit={(v) => patch({ angle: v })} />
                      <Num label="Fit to width" value={textItem.fit ?? 0} step={1} min={0} onCommit={(v) => patch({ fit: v > 0 ? v : undefined })} />
                      <Colour label="Colour" value={textItem.color ?? '#000000'} onChange={(c) => patch({ color: c ?? undefined })} />
                    </div>
                    <span className="row">
                      <label className="row"><input type="checkbox" checked={!!textItem.bold} onChange={(e) => patch({ bold: e.target.checked })} />Bold</label>
                      <label className="row"><input type="checkbox" checked={!!textItem.italic} onChange={(e) => patch({ italic: e.target.checked })} />Italic</label>
                    </span>
                  </>
                )}
                {item.type === 'image' && <button type="button" className="btn" onClick={() => chooseImage(sel!)}><ImageIcon size={13} />Choose image…</button>}
                {item.type === 'revisions' && (
                  <div className="tb-grid2">
                    <Num label="Row height" value={item.rowH ?? 5} min={2.5} onCommit={(v) => patch({ rowH: v })} />
                    {(['revision', 'date', 'status', 'description'] as const).map((k) => (
                      <label key={k} className="tb-num"><span className="muted small">{{ revision: 'Revision heading', date: 'Date heading', status: 'Status heading', description: 'Description heading' }[k]}</span>
                        <input defaultValue={item.labels?.[k] ?? ''} placeholder={{ revision: 'REV', date: 'DATE', status: 'STATUS', description: 'DESCRIPTION' }[k]} data-no-i18n
                          onBlur={(e) => patch({ labels: { ...(item.labels ?? {}), [k]: e.target.value } })} />
                      </label>
                    ))}
                  </div>
                )}
              </div>
            )}
            <div className="tb-fields" data-field="tb-fields">
              <h4 className="eyebrow"><Braces size={12} /> Fields</h4>
              <p className="muted small">{textItem ? 'A click adds the field to the selected text.' : 'A click places the field as a new text; with a text selected, it goes into it.'}</p>
              <div className="row">
                <input type="search" placeholder={tr('Find a field')} value={filter} onChange={(e) => setFilter(e.target.value)} data-field="tb-field-filter" />
                <select value={sample ?? ''} onChange={(e) => setSample(e.target.value || null)} aria-label="Sample sheet" title="The sheet whose values are shown" data-field="tb-sample">
                  {sheets.length === 0 && <option value="">a sample sheet</option>}
                  {sheets.map((s) => <option key={s.id} value={s.id} data-no-i18n>{`${(s.props.number as string) ?? ''} ${s.name}`.trim()}</option>)}
                </select>
              </div>
              {groups.map((g) => {
                const list = shownFields.filter((f) => f.group === g.id);
                if (list.length === 0) return null;
                return (
                  <section key={g.id}>
                    <h5 className="muted small">{g.label}</h5>
                    <ul className="tb-field-list">
                      {list.map((f) => (
                        <li key={f.key}>
                          <button type="button" onClick={() => pickField(f.key)} data-field={`tb-field-${f.key}`} title={`{${f.key}}`}>
                            <span>{f.label}</span>
                            <span className="muted small" data-no-i18n>{f.value || `{${f.key}}`}</span>
                          </button>
                        </li>
                      ))}
                    </ul>
                  </section>
                );
              })}
              <p className="muted small">Missing fields? Add the project's own in Manage → Project information….</p>
            </div>
          </aside>
        </div>
        <footer className="dialog-foot">
          <span className="muted small">{dirty ? tr('Not saved yet') : ''}</span>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Close</button>
          <button type="button" className="btn primary" onClick={() => void save()} data-field="tb-save"><Save size={13} />Save</button>
        </footer>
      </div>
    </div>
  );
}
