/**
 * The symbol editor (docs/plan-user-extensibility.md §3), built on the title block designer's canvas: a symbol drawn in millimetres
 * round its insertion point (the red cross; +x, the symbol's direction, the arrow), with lines, rectangles, polylines, circles,
 * arcs and texts; live text with fields ({view.scale}), attributes ({attr.no}) and formulas ({= attr.w * 2 | 0.00}); a preview by
 * the engine, turned and flipped, so the texts are seen staying readable. Scale: an annotation symbol is paper millimetres (it
 * stays the same size at any view scale), a model symbol real ones. Save writes the definition as one undo step.
 */

import { useCallback, useEffect, useMemo, useRef, useState, type PointerEvent as RPointerEvent } from 'react';
import { Braces, Circle as CircleIcon, FlipHorizontal2, FlipVertical2, Minus, MousePointer2, PenLine, Plus, Redo2, RotateCw, Save, Spline, Square, Trash2, Type, Undo2, X, ZoomIn, ZoomOut } from 'lucide-react';
import { t as tr } from '../../../i18n';
import { insertToken } from '../titleBlocks/titleBlockModel';
import { Colour, Num } from '../titleBlocks/TitleBlockDesigner';
import { pensToSvg } from '../documents/docModel';
import { evaluateText, previewSymbol, symbolFields, type SymFields } from './symbols';
import {
  CATEGORY_FALLBACK, attributeKey, defBounds, defaultValues, missingAttributes, newSymItem, previewImage, round, sweepDeg, symBounds, symDragHandle, symHandles, symItemAt, symMove, symSnap, turned,
  type Pt, type SymAttr, type SymDef, type SymItem, type SymPreview, type SymType,
} from './symbolModel';

type Tool = 'select' | SymType;

const TOOLS: { id: Tool; label: string; icon: typeof Square; hint: string }[] = [
  { id: 'select', label: 'Select', icon: MousePointer2, hint: 'Click an item to change it, drag to move it, drag a handle to reshape it' },
  { id: 'line', label: 'Line', icon: Minus, hint: 'Click the start, then the end (Shift: straight)' },
  { id: 'rect', label: 'Rectangle', icon: Square, hint: 'Click one corner, then the opposite one' },
  { id: 'polyline', label: 'Polyline', icon: PenLine, hint: 'Click the points; double-click or Enter ends it, a click on the first point closes it' },
  { id: 'circle', label: 'Circle', icon: CircleIcon, hint: 'Click the centre, then a point on the circle' },
  { id: 'arc', label: 'Arc', icon: Spline, hint: 'Click the centre, the start of the arc, then its end (counter-clockwise)' },
  { id: 'text', label: 'Text', icon: Type, hint: 'Click where the text starts' },
];

const GRIDS = [0, 0.25, 0.5, 1, 2.5, 5];

const CROSS = '#d63031';

export function SymbolEditor({ title, name: initialName, def: initialDef, categories, onSave, onClose }: {
  title?: string; name: string; def: SymDef; categories: string[];
  onSave: (name: string, def: SymDef) => Promise<boolean | void>; onClose: () => void;
}) {
  const [name, setName] = useState(initialName);
  const [d, setD] = useState<SymDef>(initialDef);
  const past = useRef<SymDef[]>([]);
  const future = useRef<SymDef[]>([]);
  const [dirty, setDirty] = useState(false);
  const [tool, setTool] = useState<Tool>('select');
  const [sel, setSel] = useState<number | null>(null);
  const [grid, setGrid] = useState(1);
  const [objectSnap, setObjectSnap] = useState(true);
  const [zoom, setZoom] = useState(1);
  const [fields, setFields] = useState<SymFields | null>(null);
  const [filter, setFilter] = useState('');
  const [pendingText, setPendingText] = useState<string | null>(null);
  const [draft, setDraft] = useState<{ pts: Pt[]; at: Pt | null } | null>(null);
  const drag = useRef<{ kind: 'move' | 'handle'; index: number; handle?: number; start: Pt; orig: SymItem; moved: boolean } | null>(null);
  const [hover, setHover] = useState<{ p: Pt; on: string } | null>(null);
  // The preview: turned and flipped as asked, made by the engine from the definition as it is now.
  const [view, setView] = useState({ flipX: false, flipY: false, angle: 0 });
  const [preview, setPreview] = useState<SymPreview | null>(null);
  const [tryText, setTryText] = useState('{= attr.w * 2 | 0.00} mm');
  const [tried, setTried] = useState<string | null>(null);
  const svg = useRef<SVGSVGElement>(null);
  const textBox = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    let live = true;
    void symbolFields({ def: d }).then((r) => { if (live && r) setFields(r); });
    return () => { live = false; };
  }, [d.attributes.map((a) => a.key).join('|')]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    let live = true;
    const h = setTimeout(() => { void previewSymbol({ def: d }, { values: defaultValues(d.attributes), ...view }).then((r) => { if (live) setPreview(r); }); }, 150);
    return () => { live = false; clearTimeout(h); };
  }, [d, view]);

  useEffect(() => {
    let live = true;
    void evaluateText(tryText, defaultValues(d.attributes)).then((r) => { if (live) setTried(r ? r.text + (r.unknown.length ? `  (${tr('unknown')}: ${r.unknown.join(', ')})` : '') : null); });
    return () => { live = false; };
  }, [tryText, d.attributes]);

  const change = useCallback((next: SymDef, quiet = false) => {
    setD((cur) => { if (!quiet) { past.current.push(cur); future.current = []; } return next; });
    setDirty(true);
  }, []);
  const setItem = (i: number, item: SymItem, quiet = false) => change({ ...d, items: d.items.map((x, k) => (k === i ? item : x)) }, quiet);
  const patch = (p: Record<string, unknown>) => { if (sel != null) setItem(sel, { ...d.items[sel], ...p } as SymItem); };
  const undo = () => { const prev = past.current.pop(); if (prev) { future.current.push(d); setD(prev); setSel(null); setDirty(true); } };
  const redo = () => { const next = future.current.pop(); if (next) { past.current.push(d); setD(next); setSel(null); setDirty(true); } };
  const add = (item: SymItem) => { change({ ...d, items: [...d.items, item] }); setSel(d.items.length); };
  const remove = () => { if (sel == null) return; change({ ...d, items: d.items.filter((_, k) => k !== sel) }); setSel(null); };

  // The canvas shows the symbol and some room round it, in steps so it does not jump at every drag.
  const [bx0, by0, bx1, by1] = defBounds(d.items);
  const ext = (() => {
    const lo = (v: number, d0: number) => Math.floor((Math.min(v, d0) - 8) / 10) * 10;
    const hi = (v: number, d0: number) => Math.ceil((Math.max(v, d0) + 8) / 10) * 10;
    return { x0: lo(bx0, -15), y0: lo(by0, -15), x1: hi(bx1, 15), y1: hi(by1, 15) };
  })();
  const W = ext.x1 - ext.x0, H = ext.y1 - ext.y0;

  const where = (e: { clientX: number; clientY: number }): { p: Pt; tol: number } | null => {
    const el = svg.current;
    const m = el?.getScreenCTM();
    if (!el || !m) return null;
    const pt = el.createSVGPoint();
    pt.x = e.clientX; pt.y = e.clientY;
    const q = pt.matrixTransform(m.inverse());
    return { p: [q.x, -q.y], tol: 6 / m.a };
  };
  const snapped = (p: Pt, tol: number, from?: Pt | null, ortho?: boolean, except?: number) => symSnap(p, d.items, { grid, tol, objects: objectSnap, from, ortho, except });

  const finishDraft = (pts: Pt[]) => {
    setDraft(null);
    if (tool === 'polyline' && pts.length >= 2) {
      const closed = pts.length > 2 && Math.hypot(pts[0][0] - pts[pts.length - 1][0], pts[0][1] - pts[pts.length - 1][1]) < 1e-6;
      add({ type: 'polyline', pts: closed ? pts.slice(0, -1) : pts, closed, stroke: 0.25 });
    }
  };

  const onDown = (e: RPointerEvent<SVGSVGElement>) => {
    if (e.button !== 0) return;
    const w = where(e);
    if (!w) return;
    if (tool === 'select') {
      if (sel != null) {
        const h = symHandles(d.items[sel]).findIndex((q) => Math.hypot(q[0] - w.p[0], q[1] - w.p[1]) <= w.tol);
        if (h >= 0) { drag.current = { kind: 'handle', index: sel, handle: h, start: w.p, orig: d.items[sel], moved: false }; svg.current?.setPointerCapture(e.pointerId); return; }
      }
      const i = symItemAt(d.items, w.p, w.tol);
      setSel(i >= 0 ? i : null);
      if (i >= 0) { drag.current = { kind: 'move', index: i, start: w.p, orig: d.items[i], moved: false }; svg.current?.setPointerCapture(e.pointerId); }
      return;
    }
    const last = draft?.pts[draft.pts.length - 1] ?? null;
    const s = snapped(w.p, w.tol, last, e.shiftKey).p;
    if (tool === 'text') {
      add(newSymItem('text', s, pendingText ?? tr('Text')));
      setPendingText(null);
      setTool('select');
      setTimeout(() => { textBox.current?.focus(); textBox.current?.select(); }, 30);
      return;
    }
    if (tool === 'polyline') {
      const pts = [...(draft?.pts ?? [])];
      if (pts.length > 2 && Math.hypot(pts[0][0] - s[0], pts[0][1] - s[1]) <= w.tol) { finishDraft([...pts, pts[0]]); return; }
      setDraft({ pts: [...pts, s], at: s });
      return;
    }
    if (!draft) { setDraft({ pts: [s], at: s }); return; }
    const a = draft.pts[0];
    if (tool === 'circle') {
      setDraft(null);
      const r = Math.hypot(s[0] - a[0], s[1] - a[1]);
      if (r > 0.1) add({ type: 'circle', c: a, r: round(r), stroke: 0.25 });
      setTool('select');
      return;
    }
    if (tool === 'arc') {
      if (draft.pts.length === 1) { if (Math.hypot(s[0] - a[0], s[1] - a[1]) > 0.1) setDraft({ pts: [a, s], at: s }); return; }
      const start = Math.atan2(draft.pts[1][1] - a[1], draft.pts[1][0] - a[0]) * 180 / Math.PI;
      const end = Math.atan2(s[1] - a[1], s[0] - a[0]) * 180 / Math.PI;
      setDraft(null);
      add({ type: 'arc', c: a, r: round(Math.hypot(draft.pts[1][0] - a[0], draft.pts[1][1] - a[1])), start: round(start), end: round(end), w: 0.25 });
      setTool('select');
      return;
    }
    setDraft(null);
    if (tool === 'line') { if (Math.hypot(s[0] - a[0], s[1] - a[1]) > 0.01) add({ type: 'line', a, b: s, w: 0.25 }); return; }
    const [x0, y0, x1, y1] = [Math.min(a[0], s[0]), Math.min(a[1], s[1]), Math.max(a[0], s[0]), Math.max(a[1], s[1])];
    if (x1 - x0 < 0.25 || y1 - y0 < 0.25) return;
    if (tool === 'rect') add({ type: 'rect', x: round(x0), y: round(y0), w: round(x1 - x0), h: round(y1 - y0), stroke: 0.25 });
    setTool('select');
  };

  const onMove = (e: RPointerEvent<SVGSVGElement>) => {
    const w = where(e);
    if (!w) return;
    const dr = drag.current;
    if (dr) {
      if (!dr.moved && Math.hypot(w.p[0] - dr.start[0], w.p[1] - dr.start[1]) < w.tol / 2) return;
      if (dr.kind === 'handle') {
        const s = snapped(w.p, w.tol, null, false, dr.index).p;
        setItem(dr.index, symDragHandle(dr.orig, dr.handle!, s), dr.moved);
      } else {
        const b = symBounds(dr.orig);
        const anchor: Pt = dr.orig.type === 'circle' || dr.orig.type === 'arc' ? dr.orig.c : dr.orig.type === 'text' ? dr.orig.at : [b[0], b[1]];
        const target = snapped([anchor[0] + w.p[0] - dr.start[0], anchor[1] + w.p[1] - dr.start[1]], w.tol, e.shiftKey ? anchor : null, e.shiftKey, dr.index).p;
        setItem(dr.index, symMove(dr.orig, target[0] - anchor[0], target[1] - anchor[1]), dr.moved);
      }
      dr.moved = true;
      return;
    }
    if (tool === 'select') { setHover({ p: w.p, on: 'none' }); return; }
    const last = draft?.pts[draft.pts.length - 1] ?? null;
    const s = snapped(w.p, w.tol, last, e.shiftKey);
    setHover(s);
    if (draft) setDraft({ ...draft, at: s.p });
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const typing = (e.target as HTMLElement)?.closest('input, textarea, select');
      if ((e.ctrlKey || e.metaKey) && !typing && e.key.toLowerCase() === 'z') { e.preventDefault(); if (e.shiftKey) redo(); else undo(); return; }
      if ((e.ctrlKey || e.metaKey) && !typing && e.key.toLowerCase() === 'y') { e.preventDefault(); redo(); return; }
      if (typing) return;
      if (e.key === 'Escape') { e.stopPropagation(); if (draft) { if (tool === 'polyline' && draft.pts.length >= 2) finishDraft(draft.pts); else setDraft(null); } else if (tool !== 'select') setTool('select'); else setSel(null); return; }
      if (e.key === 'Enter' && draft && tool === 'polyline') { finishDraft(draft.pts); return; }
      if (sel == null) return;
      if (e.key === 'Delete' || e.key === 'Backspace') { e.preventDefault(); remove(); return; }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'd') { e.preventDefault(); add(symMove(d.items[sel], grid || 1, -(grid || 1))); return; }
      const step = (grid || 0.25) * (e.shiftKey ? 10 : 1);
      const delta = ({ ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, step], ArrowDown: [0, -step] } as Record<string, number[]>)[e.key];
      if (delta) { e.preventDefault(); setItem(sel, symMove(d.items[sel], delta[0], delta[1])); }
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  });

  const save = async () => {
    // The attributes the texts read and the list does not have yet are added, so a placed symbol asks for them.
    const missing = missingAttributes(d);
    const attributes: SymAttr[] = [...d.attributes, ...missing.map((key) => ({ key, label: key, default: '' }))];
    const def: SymDef = { ...d, attributes, category: d.category.trim() || 'other' };
    const ok = await onSave(name.trim() || tr('Symbol'), def);
    if (ok === false) return;
    if (missing.length) setD(def);
    setDirty(false);
  };
  const close = () => { if (!dirty || window.confirm(tr('Close the editor without saving your changes?'))) onClose(); };

  const pickField = (key: string) => {
    if (sel != null && d.items[sel].type === 'text') {
      const item = d.items[sel] as Extract<SymItem, { type: 'text' }>;
      setItem(sel, { ...item, text: insertToken(item.text, key) });
      return;
    }
    setPendingText(`{${key}}`);
    setTool('text');
  };

  const setAttr = (i: number, p: Partial<SymAttr>) => change({ ...d, attributes: d.attributes.map((a, k) => (k === i ? { ...a, ...p } : a)) });
  const addAttr = () => {
    const taken = new Set(d.attributes.map((a) => a.key));
    let n = d.attributes.length + 1;
    while (taken.has(`attr${n}`)) n++;
    change({ ...d, attributes: [...d.attributes, { key: `attr${n}`, label: `Attribute ${n}`, default: '' }] });
  };

  const item = sel != null ? d.items[sel] : null;
  const textItem = item?.type === 'text' ? item : null;
  const Y = (y: number) => -y;
  const shownFields = (fields?.fields ?? []).filter((f) => !filter || `${f.label} ${f.key}`.toLowerCase().includes(filter.toLowerCase()));
  const groups = [...new Set(shownFields.map((f) => f.group))];
  const stroke = (w?: number) => Math.max(w ?? 0.25, 0.08);
  const fillOf = (f: string | null | undefined, hatch: unknown) => (f === 'byInstance' ? '#7d8a99' : f ?? (hatch ? 'url(#sy-hatch)' : 'none'));

  const renderItem = (it: SymItem, i: number) => {
    switch (it.type) {
      case 'line': return (it.w ?? 0.25) > 0 ? <line key={i} x1={it.a[0]} y1={Y(it.a[1])} x2={it.b[0]} y2={Y(it.b[1])} stroke={it.color ?? '#000'} strokeWidth={stroke(it.w)} strokeDasharray={it.dash?.join(' ')} strokeLinecap="round" /> : null;
      case 'rect': return <rect key={i} x={it.x} y={Y(it.y + it.h)} width={it.w} height={it.h} fill={fillOf(it.fill, it.hatch)} stroke={(it.stroke ?? 0.25) > 0 ? it.color ?? '#000' : 'none'} strokeWidth={stroke(it.stroke)} strokeDasharray={undefined} />;
      case 'polyline': {
        const pts = it.pts.map((p) => `${p[0]},${Y(p[1])}`).join(' ');
        return it.closed ? <polygon key={i} points={pts} fill={fillOf(it.fill, it.hatch)} stroke={(it.stroke ?? 0.25) > 0 ? it.color ?? '#000' : 'none'} strokeWidth={stroke(it.stroke)} />
          : <polyline key={i} points={pts} fill="none" stroke={it.color ?? '#000'} strokeWidth={stroke(it.stroke)} strokeLinejoin="round" />;
      }
      case 'circle': return <circle key={i} cx={it.c[0]} cy={Y(it.c[1])} r={it.r} fill={fillOf(it.fill, it.hatch)} stroke={(it.stroke ?? 0.25) > 0 ? it.color ?? '#000' : 'none'} strokeWidth={stroke(it.stroke)} strokeDasharray={it.dash?.join(' ')} />;
      case 'arc': return <path key={i} d={arcPath(it.c, it.r, it.start, it.end)} fill="none" stroke={it.color ?? '#000'} strokeWidth={stroke(it.w)} strokeDasharray={it.dash?.join(' ')} strokeLinecap="round" />;
      case 'text': {
        const h = it.h ?? 2.5;
        return (
          <text key={i} x={it.at[0]} y={Y(it.at[1])} fontSize={h} fontWeight={it.bold ? 700 : 400} fontStyle={it.italic ? 'italic' : undefined}
            textAnchor={it.align ?? 'start'} dominantBaseline={it.valign === 'middle' ? 'central' : 'alphabetic'} fill={it.color ?? '#000'} fontFamily="Arial, Helvetica, sans-serif"
            transform={it.angle ? `rotate(${-it.angle} ${it.at[0]} ${Y(it.at[1])})` : undefined} data-no-i18n>{it.text}</text>
        );
      }
    }
  };

  const gridLines = () => {
    if (!grid) return null;
    const step = grid < 2.5 ? 5 : grid * 2;
    const o: JSX.Element[] = [];
    for (let x = Math.ceil(ext.x0 / step) * step; x <= ext.x1; x += step) o.push(<line key={`x${x}`} x1={x} x2={x} y1={Y(ext.y0)} y2={Y(ext.y1)} />);
    for (let y = Math.ceil(ext.y0 / step) * step; y <= ext.y1; y += step) o.push(<line key={`y${y}`} x1={ext.x0} x2={ext.x1} y1={Y(y)} y2={Y(y)} />);
    return <g className="tb-grid">{o}</g>;
  };

  const draftShape = (() => {
    if (!draft || !draft.at) return null;
    const a = draft.pts[0], b = draft.at;
    if (tool === 'line') return <line className="tb-draft" x1={a[0]} y1={Y(a[1])} x2={b[0]} y2={Y(b[1])} />;
    if (tool === 'polyline') return <polyline className="tb-draft" points={[...draft.pts, b].map((p) => `${p[0]},${Y(p[1])}`).join(' ')} fill="none" />;
    if (tool === 'circle') return <circle className="tb-draft" cx={a[0]} cy={Y(a[1])} r={Math.hypot(b[0] - a[0], b[1] - a[1])} />;
    if (tool === 'arc') {
      if (draft.pts.length === 1) return <line className="tb-draft" x1={a[0]} y1={Y(a[1])} x2={b[0]} y2={Y(b[1])} />;
      const r = Math.hypot(draft.pts[1][0] - a[0], draft.pts[1][1] - a[1]);
      const s = Math.atan2(draft.pts[1][1] - a[1], draft.pts[1][0] - a[0]) * 180 / Math.PI;
      const en = Math.atan2(b[1] - a[1], b[0] - a[0]) * 180 / Math.PI;
      return <path className="tb-draft" d={arcPath(a, r, s, en)} />;
    }
    return <rect className="tb-draft" x={Math.min(a[0], b[0])} y={Y(Math.max(a[1], b[1]))} width={Math.abs(b[0] - a[0])} height={Math.abs(b[1] - a[1])} fill="none" />;
  })();

  const selection = (() => {
    if (!item) return null;
    const [x0, y0, x1, y1] = symBounds(item);
    return (
      <g className="tb-selection">
        <rect x={x0 - 0.6} y={Y(y1 + 0.6)} width={x1 - x0 + 1.2} height={y1 - y0 + 1.2} fill="none" />
        {symHandles(item).map((h, k) => <rect key={k} className="tb-handle" x={h[0] - 0.8} y={Y(h[1]) - 0.8} width={1.6} height={1.6} />)}
      </g>
    );
  })();

  const previewSvg = useMemo(() => (preview ? pensToSvg(previewImage(preview)) : ''), [preview]);
  const fillMode = (f: string | null | undefined) => (f === 'byInstance' ? 'instance' : f ? 'colour' : 'none');
  const fillControl = (it: Extract<SymItem, { type: 'rect' | 'polyline' | 'circle' }>) => (
    <div className="tb-grid2">
      <label className="tb-num"><span className="muted small">Fill</span>
        <select value={fillMode(it.fill)} data-field="sy-fill" onChange={(e) => patch({ fill: e.target.value === 'none' ? null : e.target.value === 'instance' ? 'byInstance' : (it.fill && it.fill !== 'byInstance' ? it.fill : '#d9d9d9') })}>
          <option value="none">none</option><option value="colour">a colour</option><option value="instance">the instance's colour</option>
        </select>
      </label>
      {fillMode(it.fill) === 'colour' && <Colour label="Fill" value={it.fill} onChange={(c) => patch({ fill: c })} />}
    </div>
  );

  return (
    <div className="dialog-backdrop">
      <div className="dialog tb-designer sy-editor" role="dialog" aria-label="Symbol editor" data-dialog="symbol-editor" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">{title ?? 'Symbol editor'}</span>
          <input className="tb-name" value={name} onChange={(e) => { setName(e.target.value); setDirty(true); }} aria-label="Name" data-field="sy-name" data-no-i18n />
          <span className="grow" />
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="tb-body">
          <nav className="tb-tools" aria-label="Tools">
            {TOOLS.map((x) => (
              <button key={x.id} type="button" className={`icon tb-tool${tool === x.id ? ' active' : ''}`} title={`${tr(x.label)}: ${tr(x.hint)}`} data-field={`sy-tool-${x.id}`}
                onClick={() => { setTool(x.id); setDraft(null); setPendingText(null); }}><x.icon size={16} /></button>
            ))}
            <span className="tb-sep" />
            <button type="button" className="icon" title="Undo (Ctrl+Z)" onClick={undo} disabled={past.current.length === 0}><Undo2 size={15} /></button>
            <button type="button" className="icon" title="Redo (Ctrl+Y)" onClick={redo} disabled={future.current.length === 0}><Redo2 size={15} /></button>
            <button type="button" className="icon" title="Delete the selected item (Delete)" onClick={remove} disabled={sel == null} data-field="sy-delete-item"><Trash2 size={15} /></button>
            <span className="tb-sep" />
            <button type="button" className="icon" title="Zoom in" onClick={() => setZoom((z) => Math.min(6, z * 1.25))}><ZoomIn size={15} /></button>
            <button type="button" className="icon" title="Zoom out" onClick={() => setZoom((z) => Math.max(0.5, z / 1.25))}><ZoomOut size={15} /></button>
          </nav>
          <section className="tb-canvas-wrap">
            <div className="tb-statusbar small">
              <span className="muted">{tr(TOOLS.find((x) => x.id === tool)?.hint ?? '')}{pendingText ? ` · ${pendingText}` : ''}</span>
              <span className="grow" />
              <label className="row">Grid
                <select value={grid} onChange={(e) => setGrid(parseFloat(e.target.value))} data-field="sy-grid">
                  {GRIDS.map((g) => <option key={g} value={g}>{g === 0 ? tr('off') : `${g} mm`}</option>)}
                </select>
              </label>
              <label className="row"><input type="checkbox" checked={objectSnap} onChange={(e) => setObjectSnap(e.target.checked)} />Snap to items</label>
              {hover && <span className="muted" data-no-i18n>{`x ${hover.p[0].toFixed(2)}  y ${hover.p[1].toFixed(2)}`}</span>}
            </div>
            <div className="tb-canvas">
              <svg ref={svg} data-testid="sy-canvas" viewBox={`${ext.x0} ${-ext.y1} ${W} ${H}`} style={{ width: `${zoom * 100}%` }} className={`tool-${tool}`}
                onPointerDown={onDown} onPointerMove={onMove} onPointerUp={() => { drag.current = null; }} onPointerLeave={() => setHover(null)}
                onDoubleClick={() => { if (tool === 'polyline' && draft) finishDraft(draft.pts.slice(0, -1).length >= 2 ? draft.pts.slice(0, -1) : draft.pts); }}>
                <defs>
                  <pattern id="sy-hatch" width="2" height="2" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><line x1="0" y1="0" x2="0" y2="2" stroke="#555" strokeWidth="0.15" /></pattern>
                </defs>
                <rect x={ext.x0} y={-ext.y1} width={W} height={H} className="tb-paper" />
                {gridLines()}
                {d.items.map(renderItem)}
                {/* The insertion point and the symbol's direction. */}
                <g stroke={CROSS} strokeWidth={0.2} fill={CROSS} data-testid="sy-origin">
                  <line x1={-2.5} y1={0} x2={2.5} y2={0} /><line x1={0} y1={-2.5} x2={0} y2={2.5} />
                  <line x1={2.5} y1={0} x2={7} y2={0} /><polygon points="7,-0.9 9,0 7,0.9" stroke="none" />
                  <circle cx={0} cy={0} r={0.7} fill="none" />
                </g>
                {selection}
                {draftShape}
                {hover && tool !== 'select' && <circle className={`tb-snap tb-snap-${hover.on}`} cx={hover.p[0]} cy={Y(hover.p[1])} r={0.7} />}
              </svg>
            </div>
          </section>
          <aside className="tb-side">
            {!item ? (
              <div className="tb-props" data-field="sy-props">
                <h4 className="eyebrow">Symbol</h4>
                <label className="tb-num"><span className="muted small">Symbol scale</span>
                  <select value={d.scale} onChange={(e) => change({ ...d, scale: e.target.value as SymDef['scale'] })} data-field="sy-scale">
                    <option value="annotation">Annotation: paper size, the same at any view scale</option>
                    <option value="model">Model: real size, millimetres on the ground</option>
                  </select>
                </label>
                <label className="tb-num"><span className="muted small">Category</span>
                  <input list="sy-categories" value={d.category} onChange={(e) => change({ ...d, category: e.target.value })} data-field="sy-category" data-no-i18n />
                  <datalist id="sy-categories">{(categories.length ? categories : CATEGORY_FALLBACK).map((c) => <option key={c} value={c} />)}</datalist>
                </label>
                <label className="tb-num"><span className="muted small">Description</span>
                  <textarea rows={2} defaultValue={d.description ?? ''} data-field="sy-description" data-no-i18n onBlur={(e) => { if (e.target.value !== (d.description ?? '')) change({ ...d, description: e.target.value || undefined }); }} />
                </label>
                <p className="muted small">The red cross is the insertion point and the arrow the symbol's direction: draw the symbol round it. Draw with the tools on the left; insert live fields from the list below.</p>
              </div>
            ) : (
              <div className="tb-props" data-field="sy-item-props">
                <h4 className="eyebrow">{tr(TOOLS.find((x) => x.id === item.type)?.label ?? item.type)}</h4>
                {item.type === 'line' && (
                  <div className="tb-grid2">
                    <Num label="x1" value={item.a[0]} onCommit={(v) => patch({ a: [v, item.a[1]] })} /><Num label="y1" value={item.a[1]} onCommit={(v) => patch({ a: [item.a[0], v] })} />
                    <Num label="x2" value={item.b[0]} onCommit={(v) => patch({ b: [v, item.b[1]] })} /><Num label="y2" value={item.b[1]} onCommit={(v) => patch({ b: [item.b[0], v] })} />
                    <Num label="Pen (mm)" value={item.w ?? 0.25} step={0.05} min={0} onCommit={(v) => patch({ w: v })} />
                    <Colour label="Colour" value={item.color ?? '#000000'} onChange={(c) => patch({ color: c ?? undefined })} />
                  </div>
                )}
                {item.type === 'rect' && (
                  <div className="tb-grid2">
                    <Num label="x" value={item.x} onCommit={(v) => patch({ x: v })} /><Num label="y" value={item.y} onCommit={(v) => patch({ y: v })} />
                    <Num label="Width" value={item.w} min={0.25} onCommit={(v) => patch({ w: v })} /><Num label="Height" value={item.h} min={0.25} onCommit={(v) => patch({ h: v })} />
                  </div>
                )}
                {item.type === 'circle' && (
                  <div className="tb-grid2">
                    <Num label="x" value={item.c[0]} onCommit={(v) => patch({ c: [v, item.c[1]] })} /><Num label="y" value={item.c[1]} onCommit={(v) => patch({ c: [item.c[0], v] })} />
                    <Num label="Radius" value={item.r} min={0.1} onCommit={(v) => patch({ r: v })} />
                  </div>
                )}
                {item.type === 'arc' && (
                  <div className="tb-grid2">
                    <Num label="x" value={item.c[0]} onCommit={(v) => patch({ c: [v, item.c[1]] })} /><Num label="y" value={item.c[1]} onCommit={(v) => patch({ c: [item.c[0], v] })} />
                    <Num label="Radius" value={item.r} min={0.1} onCommit={(v) => patch({ r: v })} />
                    <Num label="Start angle" value={item.start} step={15} onCommit={(v) => patch({ start: v })} />
                    <Num label="End angle" value={item.end} step={15} onCommit={(v) => patch({ end: v })} />
                    <Num label="Pen (mm)" value={item.w ?? 0.25} step={0.05} min={0} onCommit={(v) => patch({ w: v })} />
                    <Colour label="Colour" value={item.color ?? '#000000'} onChange={(c) => patch({ color: c ?? undefined })} />
                    <span className="muted small" data-no-i18n>{`${Math.round(sweepDeg(item.start, item.end) * 100) / 100}°`}</span>
                  </div>
                )}
                {(item.type === 'rect' || item.type === 'polyline' || item.type === 'circle') && (
                  <>
                    <div className="tb-grid2">
                      <Num label="Pen (mm)" value={item.stroke ?? 0.25} step={0.05} min={0} onCommit={(v) => patch({ stroke: v })} />
                      <Colour label="Colour" value={item.color ?? '#000000'} onChange={(c) => patch({ color: c ?? undefined })} />
                    </div>
                    {(item.type !== 'polyline' || item.closed) && fillControl(item)}
                    {item.type === 'polyline' && <label className="row"><input type="checkbox" checked={!!item.closed} onChange={(e) => patch({ closed: e.target.checked })} />Closed</label>}
                  </>
                )}
                {textItem && (
                  <>
                    <label className="tb-num"><span className="muted small">{'Text (fields as {view.scale}, attributes as {attr.no}, formulas as {= attr.w * 2 | 0.00})'}</span>
                      <textarea ref={textBox} rows={2} key={`${sel}`} defaultValue={textItem.text} data-field="sy-text" data-no-i18n
                        onBlur={(e) => { if (e.target.value !== textItem.text) patch({ text: e.target.value }); }} />
                    </label>
                    <div className="tb-grid2">
                      <Num label="x" value={textItem.at[0]} onCommit={(v) => patch({ at: [v, textItem.at[1]] })} /><Num label="y" value={textItem.at[1]} onCommit={(v) => patch({ at: [textItem.at[0], v] })} />
                      <Num label="Height (mm)" value={textItem.h ?? 2.5} step={0.1} min={0.5} onCommit={(v) => patch({ h: v })} />
                      <label className="tb-num"><span className="muted small">Align</span>
                        <select value={textItem.align ?? 'start'} onChange={(e) => patch({ align: e.target.value as 'start' | 'middle' | 'end' })}>
                          <option value="start">Left</option><option value="middle">Centre</option><option value="end">Right</option>
                        </select>
                      </label>
                      <label className="tb-num"><span className="muted small">Vertical</span>
                        <select value={textItem.valign ?? 'bottom'} onChange={(e) => patch({ valign: e.target.value })}>
                          <option value="bottom">Baseline</option><option value="middle">Middle</option>
                        </select>
                      </label>
                      <Num label="Angle" value={textItem.angle ?? 0} step={90} onCommit={(v) => patch({ angle: v })} />
                      <Colour label="Colour" value={textItem.color ?? '#000000'} onChange={(c) => patch({ color: c ?? undefined })} />
                    </div>
                    <span className="row">
                      <label className="row"><input type="checkbox" checked={!!textItem.bold} onChange={(e) => patch({ bold: e.target.checked })} />Bold</label>
                      <label className="row"><input type="checkbox" checked={!!textItem.italic} onChange={(e) => patch({ italic: e.target.checked })} />Italic</label>
                    </span>
                  </>
                )}
              </div>
            )}

            <div className="sy-attrs" data-field="sy-attributes">
              <h4 className="eyebrow">Attributes</h4>
              <p className="muted small">{'Values asked for when the symbol is placed, read in its texts as {attr.key}.'}</p>
              {d.attributes.length > 0 && (
                <table className="doc-info-params">
                  <thead><tr><th>Key</th><th>Label</th><th>Default</th><th /></tr></thead>
                  <tbody>
                    {d.attributes.map((a, i) => (
                      <tr key={i}>
                        <td><input defaultValue={a.key} data-field={`sy-attr-key-${i}`} data-no-i18n onBlur={(e) => { const k = attributeKey(e.target.value) || a.key; if (k !== a.key) setAttr(i, { key: k }); else e.target.value = a.key; }} /></td>
                        <td><input defaultValue={a.label} data-field={`sy-attr-label-${i}`} data-no-i18n onBlur={(e) => { if (e.target.value !== a.label) setAttr(i, { label: e.target.value }); }} /></td>
                        <td><input defaultValue={a.default} data-field={`sy-attr-default-${i}`} data-no-i18n onBlur={(e) => { if (e.target.value !== a.default) setAttr(i, { default: e.target.value }); }} /></td>
                        <td><button type="button" className="icon" title="Remove the attribute" onClick={() => change({ ...d, attributes: d.attributes.filter((_, k) => k !== i) })}><Trash2 size={13} /></button></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              <div className="row">
                <button type="button" className="btn" onClick={addAttr} data-field="sy-attr-add"><Plus size={13} />Add attribute</button>
                {missingAttributes(d).length > 0 && <span className="muted small" data-no-i18n>{missingAttributes(d).map((k) => `{attr.${k}}`).join(' ')}</span>}
              </div>
              {missingAttributes(d).length > 0 && <p className="muted small">Texts read attributes that are not listed: Save adds them.</p>}
            </div>

            <div className="sy-preview" data-field="sy-preview-panel">
              <h4 className="eyebrow">Preview, as the engine draws it</h4>
              <div className="row wrap">
                <button type="button" className={`btn${view.flipX ? ' primary' : ''}`} onClick={() => setView((v) => ({ ...v, flipX: !v.flipX }))} data-field="sy-flip-x" title="Left ↔ right across the symbol's own axis: the texts stay readable"><FlipHorizontal2 size={13} />Flip X</button>
                <button type="button" className={`btn${view.flipY ? ' primary' : ''}`} onClick={() => setView((v) => ({ ...v, flipY: !v.flipY }))} data-field="sy-flip-y" title="Up ↔ down across the symbol's own axis: the texts stay readable"><FlipVertical2 size={13} />Flip Y</button>
                <button type="button" className="btn" onClick={() => setView((v) => ({ ...v, angle: turned(v.angle) }))} data-field="sy-rotate" title="Turn the preview a quarter"><RotateCw size={13} />{`${view.angle}°`}</button>
              </div>
              <div className="tb-preview sy-preview-box" data-field="sy-preview" data-no-i18n dangerouslySetInnerHTML={{ __html: previewSvg }} />
            </div>

            <div className="tb-fields" data-field="sy-fields">
              <h4 className="eyebrow"><Braces size={12} /> Fields</h4>
              <p className="muted small">{textItem ? 'A click adds the field to the selected text.' : 'A click places the field as a new text; with a text selected, it goes into it.'}</p>
              <input type="search" placeholder={tr('Find a field')} value={filter} onChange={(e) => setFilter(e.target.value)} data-field="sy-field-filter" />
              {groups.map((g) => (
                <section key={g}>
                  <h5 className="muted small" data-no-i18n>{g}</h5>
                  <ul className="tb-field-list">
                    {shownFields.filter((f) => f.group === g).map((f) => (
                      <li key={f.key}><button type="button" onClick={() => pickField(f.key)} data-field={`sy-field-${f.key}`} title={`{${f.key}}`}><span data-no-i18n>{f.label}</span><span className="muted small" data-no-i18n>{`{${f.key}}`}</span></button></li>
                    ))}
                  </ul>
                </section>
              ))}
              <h5 className="muted small">Formulas</h5>
              <p className="muted small">{'A formula in a text reads {= expression | format}, for example {= attr.w * 2 | 0.00}; the format after the bar is a number pattern.'}</p>
              {fields && fields.functions.length > 0 && <p className="small" data-field="sy-functions" data-no-i18n>{fields.functions.join(', ')}</p>}
              <label className="tb-num"><span className="muted small">Try a text (with the attributes' defaults)</span>
                <input value={tryText} onChange={(e) => setTryText(e.target.value)} data-field="sy-try" data-no-i18n />
              </label>
              <p className="small" data-field="sy-tried" data-no-i18n>{tried ?? ''}</p>
            </div>
          </aside>
        </div>
        <footer className="dialog-foot">
          <span className="muted small">{dirty ? tr('Not saved yet') : ''}</span>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Close</button>
          <button type="button" className="btn primary" onClick={() => void save()} data-field="sy-save"><Save size={13} />Save</button>
        </footer>
      </div>
    </div>
  );
}

/** An arc's SVG path in the canvas (y flipped): counter-clockwise from start to end in the symbol's y-up frame. */
function arcPath(c: Pt, r: number, start: number, end: number): string {
  const P = (deg: number): [number, number] => [c[0] + Math.cos((deg * Math.PI) / 180) * r, -(c[1] + Math.sin((deg * Math.PI) / 180) * r)];
  const sweep = sweepDeg(start, end);
  const [sx, sy] = P(start);
  if (sweep >= 359.999) { const [mx, my] = P(start + 180); return `M${sx} ${sy}A${r} ${r} 0 1 0 ${mx} ${my}A${r} ${r} 0 1 0 ${sx} ${sy}`; }
  const [ex, ey] = P(end);
  return `M${sx} ${sy}A${r} ${r} 0 ${sweep > 180 ? 1 : 0} 0 ${ex} ${ey}`;
}
