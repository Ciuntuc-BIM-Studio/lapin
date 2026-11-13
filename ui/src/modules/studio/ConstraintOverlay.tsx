/**
 * Sketch constraints drawn in a plan, a section or an elevation (stage C3 of decision 520): the sketch's shapes coloured by how
 * free they are (blue free, ink fixed, red in conflict), a glyph at each geometric constraint, each dimension drawn with its
 * value (driven ones in parentheses, a formula's on hover), and the live preview of a constrained drag. A glyph is clicked to
 * select it (Delete removes it); a dimension is double-clicked to edit its value in place, and dragged by its text or its line to
 * place it (previewed while dragged, `studio.updateConstraint {place}` on release; one undo step). A dimension not placed by the
 * user stands outside its shapes (`autoPlace`); a placed one selected offers "Automatic position" (a button beside it, or A) to take
 * it back there (`studio.updateConstraint {place: null}`). Asked of the engine again after each change of the model (`studio.constraints`,
 * `studio.sketchStatus`).
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import type { Vec2 } from '../../platform/module';
import { execute, useSession } from '../../engine/session';
import { lengthText, parseLength } from '../../engine/unitsStore';
import {
  constraintState, dimensionDraw, dimensionText, freePoints, glyphAnchor, GLYPHS, isDimension, parseDimensionInput, placeAt, pointOf, shapeStates, spreadGlyphs,
  type ConstraintDto, type DimensionPlace,
} from '../../viewport/cad/constraints';
import { useCad } from '../../viewport/cad/session';
import { fromJson, polylines, type Shape } from '../../viewport/cad/shapes';
import { refreshSketch, useSketch } from '../../viewport/cad/sketchStore';
import { constraintCommandOn } from './constraintTools';

interface Props {
  toScreen: (p: Vec2) => { x: number; y: number };
  /** An SVG point (pixels from the view's top left) as a view point: where a dragged dimension goes. */
  toWorld?: (x: number, y: number) => Vec2;
  scale: number;
  view: { id: string | null; storeyId?: string | null };
}

const pts = (line: Vec2[], toScreen: Props['toScreen']) => line.map((p) => { const q = toScreen(p); return `${q.x.toFixed(1)},${q.y.toFixed(1)}`; }).join(' ');

/** A glyph: a letter or a small drawing in a badge. */
function Glyph({ c, x, y, selected, state, hit }: { c: ConstraintDto; x: number; y: number; selected: boolean; state: string; hit: boolean }) {
  const g = GLYPHS[c.kind];
  if (!g) return null;
  const select = (e: React.PointerEvent) => {
    if (e.button !== 0) return;
    useSketch.setState((s) => ({ selected: e.shiftKey ? (s.selected.includes(c.id) ? s.selected.filter((i) => i !== c.id) : [...s.selected, c.id]) : [c.id] }));
  };
  return (
    <g className={`sk-glyph${selected ? ' selected' : ''} ${state}`} data-overlay-hit data-constraint={c.id} data-kind={c.kind} transform={`translate(${x} ${y})`} style={{ pointerEvents: hit ? 'all' : 'none' }} onPointerDown={select}>
      <circle r={9} className="sk-badge" />
      {g.path ? <path d={g.path} className="sk-icon" /> : <text className="sk-letter" textAnchor="middle" dominantBaseline="central">{g.text}</text>}
      <title>{`${g.label}${c.problem ? `: ${c.problem}` : ''}`}</title>
    </g>
  );
}

/** What a dimension is made of: the value as the engine knows it, in the project's unit. */
const textOf = (c: ConstraintDto) => dimensionText(c, lengthText);

/** The value typed in place: a number with its unit, or =formula. */
function EditField({ c, x, y, done }: { c: ConstraintDto; x: number; y: number; done: () => void }) {
  const initial = c.formula ? `=${c.formula}` : c.kind === 'angle' ? String(c.value ?? c.measured ?? '') : lengthText(c.value ?? c.measured ?? 0);
  const [text, setText] = useState(initial);
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => { input.current?.focus(); input.current?.select(); }, []);
  const commit = () => {
    const r = parseDimensionInput(text, c.kind, parseLength);
    if ('error' in r) { useSession.setState({ error: r.error }); return; }
    done();
    void execute('studio.updateConstraint', { id: c.id, ...('formula' in r ? { formula: r.formula } : { value: r.value }), driving: true });
  };
  return (
    <foreignObject x={x - 60} y={y - 13} width={120} height={26} data-overlay-hit style={{ pointerEvents: 'all' }}>
      <input ref={input} className="sk-edit" data-testid="constraint-edit" value={text} aria-label="Dimension value" onChange={(e) => setText(e.target.value)}
        onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') commit(); else if (e.key === 'Escape') done(); }} onBlur={done} />
    </foreignObject>
  );
}

/** A press that moves this many pixels is a drag (placing a dimension), not a click. */
const DRAG_PX = 4;

/** The sketch overlay of the view this is drawn in. */
export function ConstraintOverlay({ toScreen, toWorld, scale, view }: Props) {
  const viewId = view.id ?? (view.storeyId ? `plan:${view.storeyId}` : null);
  const nodes = useSession((s) => s.nodes);
  const version = useSession((s) => s.version);
  const sketch = useSketch((s) => (viewId ? s.views[viewId] : undefined));
  const hidden = useSketch((s) => (viewId ? !!s.hidden[viewId] : false));
  const { selected, editing, preview } = useSketch();
  const command = useCad((s) => s.command);
  const toolOn = !!command && constraintCommandOn();
  /** The dimension being dragged to a new place, previewed there until released. */
  const [drag, setDrag] = useState<{ id: string; place: DimensionPlace } | null>(null);

  // The engine's answer, asked again after each change of the model.
  useEffect(() => {
    if (!viewId) return;
    useSketch.setState({ shown: viewId });
    const t = setTimeout(() => void refreshSketch(viewId), 40);
    return () => clearTimeout(t);
  }, [viewId, version]);
  useEffect(() => () => { useSketch.setState((s) => (s.shown === viewId ? { shown: null } : s)); }, [viewId]);

  // Delete takes the selected constraints off; Escape lets go; a press elsewhere lets go too.
  useEffect(() => {
    if (!selected.length) return;
    const key = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement | null)?.closest?.('input, textarea, select')) return;
      if (e.key === 'Delete' || e.key === 'Backspace') {
        e.preventDefault(); e.stopImmediatePropagation();
        void execute('studio.removeConstraint', { ids: useSketch.getState().selected });
      } else if (e.key === 'Escape') useSketch.setState({ selected: [] });
      else if ((e.key === 'a' || e.key === 'A') && !e.ctrlKey && !e.metaKey && !e.altKey) {
        const list = (viewId ? useSketch.getState().views[viewId]?.constraints : undefined) ?? [];
        const placed = list.filter((c) => isDimension(c.kind) && c.place && useSketch.getState().selected.includes(c.id));
        if (!placed.length) return;
        e.preventDefault(); e.stopImmediatePropagation();
        for (const c of placed) void execute('studio.updateConstraint', { id: c.id, place: null });
      }
    };
    const press = (e: PointerEvent) => { if (!(e.target instanceof Element) || !e.target.closest('[data-constraint]')) useSketch.setState({ selected: [] }); };
    window.addEventListener('keydown', key, true);
    window.addEventListener('pointerdown', press, true);
    return () => { window.removeEventListener('keydown', key, true); window.removeEventListener('pointerdown', press, true); };
  }, [selected.length, viewId]);

  /** The view's drafted shapes, as the model has them now. */
  const drafts = useMemo(() => {
    const out: { id: string; shape: Shape }[] = [];
    if (!viewId) return out;
    for (const n of nodes.values()) {
      if (n.props.predefinedType !== 'DRAFT' || n.props.viewId !== viewId || !n.props.shape || n.props.hatch) continue;
      const s = fromJson(n.props.shape as Record<string, unknown>);
      if (s) out.push({ id: n.id, shape: s });
    }
    return out;
  }, [nodes, viewId]);

  const constraints = sketch?.constraints ?? [];
  const status = sketch?.status ?? null;
  const states = useMemo(() => shapeStates(drafts.map((d) => d.id), constraints, status), [drafts, constraints, status]);
  const anchors = useMemo(() => {
    const geo = constraints.filter((c) => !isDimension(c.kind));
    const at = geo.map((c) => glyphAnchor(c));
    return { geo, at };
  }, [constraints]);

  if (!viewId) return null;
  const active = constraints.length > 0 || toolOn;
  const dragShapes = preview && preview.viewId === viewId ? preview : null;
  if (!active && !dragShapes) return null;
  const showAll = active && !hidden;
  const hit = !toolOn;
  const off = 46 / scale;
  const byId = new Map(drafts.map((d) => [d.id, d.shape]));
  const shapesOf = (c: ConstraintDto) => [...new Set(c.refs.map((r) => r.shape))].map((id) => byId.get(id)).filter((x): x is Shape => !!x);

  /** A press on a dimension: selects it; moved past a few pixels, drags it to a new place, set on release. */
  const press = (c: ConstraintDto) => (e: React.PointerEvent) => {
    if (e.button !== 0) return;
    useSketch.setState((s) => ({ selected: e.shiftKey ? [...s.selected, c.id] : [c.id] }));
    const svg = (e.currentTarget as SVGElement).ownerSVGElement;
    if (!toWorld || !svg) return;
    // The release comes back here, not to the view underneath (no click there when the drag ends).
    try { (e.currentTarget as Element).setPointerCapture(e.pointerId); } catch { /* not every target captures */ }
    const x0 = e.clientX, y0 = e.clientY;
    let moved = false, last: DimensionPlace | null = null;
    const at = (ev: PointerEvent) => { const r = svg.getBoundingClientRect(); return toWorld(ev.clientX - r.left, ev.clientY - r.top); };
    const move = (ev: PointerEvent) => {
      if (!moved && Math.hypot(ev.clientX - x0, ev.clientY - y0) < DRAG_PX) return;
      moved = true;
      last = placeAt(c, at(ev), off);
      if (last) setDrag({ id: c.id, place: last });
    };
    const up = () => {
      window.removeEventListener('pointermove', move, true);
      window.removeEventListener('pointerup', up, true);
      if (moved && last) void execute('studio.updateConstraint', { id: c.id, place: last }).finally(() => setDrag(null));
      else setDrag(null);
    };
    window.addEventListener('pointermove', move, true);
    window.addEventListener('pointerup', up, true);
  };

  const spots = anchors.at.map((p) => (p ? toScreen(p) : { x: 0, y: 0 }));
  const spread = spreadGlyphs(spots);
  const dims = constraints.filter((c) => isDimension(c.kind));

  return (
    <g className="sketch-overlay" data-testid="sketch-overlay" data-view={viewId}>
      {showAll && status && drafts.map((d) => polylines(d.shape).map((line, i) => (
        <polyline key={`${d.id}:${i}`} className={`sk-shape ${states.get(d.id) ?? 'free'}`} points={pts(line, toScreen)} />
      )))}
      {showAll && freePoints(status).map((f) => {
        const at = drafts.find((d) => d.id === f.shape);
        const q = at ? pointOf(at.shape, f.part) : null;
        if (!q) return null;
        const s = toScreen(q);
        return <circle key={`${f.shape}:${f.part}`} className="sk-free-point" cx={s.x} cy={s.y} r={3.5} />;
      })}
      {showAll && dims.map((c) => {
        const d = dimensionDraw(c, off, { place: drag?.id === c.id ? drag.place : null, shapes: shapesOf(c), scale });
        if (!d) return null;
        const t = toScreen(d.text);
        const state = constraintState(c, status);
        const on = selected.includes(c.id);
        const cls = `sk-dim ${c.driving ? 'driving' : 'driven'} ${state}${on ? ' selected' : ''}${c.problem ? ' bad' : ''}`;
        const text = textOf(c);
        return (
          <g key={c.id} className={cls} data-constraint={c.id} data-kind={c.kind}>
            {d.lines.map((l, i) => <polyline key={i} className="sk-dim-line" points={pts(l, toScreen)} />)}
            {hit && d.lines.length === 3 && <polyline className="sk-dim-grab" data-overlay-hit points={pts(d.lines[2], toScreen)} onPointerDown={press(c)} />}
            {on && c.place && editing !== c.id && hit && (
              <g className="sk-dim-auto" data-overlay-hit data-testid="constraint-auto-place" style={{ pointerEvents: 'all', cursor: 'pointer' }} transform={`translate(${t.x + text.length * 3.6 + 22} ${t.y})`}
                onPointerDown={(e) => { e.stopPropagation(); void execute('studio.updateConstraint', { id: c.id, place: null }); }}>
                <circle r={9} className="sk-badge" />
                <text className="sk-letter" textAnchor="middle" dominantBaseline="central">A</text>
                <title>Automatic position</title>
              </g>
            )}
            {editing === c.id ? <EditField c={c} x={t.x} y={t.y} done={() => useSketch.setState({ editing: null })} /> : (
              <g data-overlay-hit style={{ pointerEvents: hit ? 'all' : 'none' }}
                onPointerDown={press(c)}
                onDoubleClick={() => useSketch.setState({ editing: c.id, selected: [c.id] })}>
                <rect className="sk-dim-hit" x={t.x - text.length * 3.6 - 4} y={t.y - 10} width={text.length * 7.2 + 8} height={20} rx={3} />
                <text className="sk-dim-text" x={t.x} y={t.y} textAnchor="middle" dominantBaseline="central" data-testid="constraint-dimension">{text}</text>
                <title>{c.problem ?? (c.place ? 'Automatic position: press A, or the button' : c.formula ? `=${c.formula}` : c.driving ? 'Double-click to change it; drag to place it' : 'Driven: it only reports; drag to place it')}</title>
              </g>
            )}
          </g>
        );
      })}
      {showAll && anchors.geo.map((c, i) => {
        const p = anchors.at[i];
        if (!p) return null;
        return <Glyph key={c.id} c={c} x={spots[i].x + spread[i].dx} y={spots[i].y + spread[i].dy} selected={selected.includes(c.id)} state={constraintState(c, status)} hit={hit} />;
      })}
      {dragShapes?.shapes.flatMap((d) => polylines(d.shape).map((line, i) => <polyline key={`${d.id}:${i}`} className={`cad-preview${dragShapes.ok ? '' : ' refused'}`} points={pts(line, toScreen)} />))}
      {dragShapes && !dragShapes.ok && dragShapes.reason && <text className="sk-reason" x={12} y={24}>{dragShapes.reason}</text>}
    </g>
  );
}
