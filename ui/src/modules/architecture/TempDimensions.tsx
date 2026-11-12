/**
 * A door's, a window's or a bare wall opening's temporary dimensions (a tester's report, decision 511; openingDims.ts):
 * in plan, with one selected, as it is dragged along its wall, and while one is being placed with the Door or Window
 * tool, along a straight wall or round a curved one (measured along the face they are drawn on); in a section or an
 * elevation that sees its wall face-on, along the wall over the opening and up its side (its sill and head heights).
 *
 * - The value: a click types it (the project's unit, sums allowed); Enter moves the opening so that the dimension reads
 *   it, the reference staying put (architecture.moveOpening: along the wall, or its sill; one undo step); Escape leaves it.
 * - The opening's end (a small dot): a click switches it between the opening's edge and its centre.
 * - The reference's end: a click takes the next thing on it (a wall's near face, far face, core faces, axis; the other
 *   opening's near edge, centre, far edge).
 * - The padlock: the dimension made a permanent one of the view (drafting.addDimension; on an arc, an arc length).
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import type { DrawingOverlayProps, PlanOverlayProps, Vec2 } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { relatingOf } from '../../engine/model';
import { formatLength, parseLength } from '../../engine/unitsStore';
import { usePlacing } from '../../platform/placement';
import { hasModule } from '../../platform/brand';
import { useGripDrag } from '../../viewport/gripDrag';
import { markOwner } from '../../viewport/planPick';
import { arcOf } from '../../viewport/arc';
import { t } from '../../i18n';
import { jambTo } from './wallOpeningGrips';
import {
  alongFor, alongOf, arcDimension, at, chooseSide, faceOn, faceScale, sillFor, tempDims, toView, verticalDims,
  type DimPick, type OpeningRefs, type Side, type TempDim,
} from './openingDims';

/** How far out from the wall's face (or the opening's head or side) the dimension lines run, px: the nearest wall's first, a neighbouring opening's further out. */
const ROW_PX = [22, 42];

/** One dimension as drawn: from `a` (its reference) to `b` (the opening's end), `pt(t, out)` the screen point at t, `out` px off the measured line. */
interface Shown {
  key: string;
  row: 0 | 1;
  a: number;
  b: number;
  pt: (t: number, out: number) => Vec2;
  /** Pieces the line is drawn in (an arc's many). */
  steps: number;
  value: number;
  data: Record<string, string>;
  refTitle?: string;
  cycleRef?: () => void;
  cycleOwn?: () => void;
  commit: (value: number) => Promise<unknown>;
  permanent?: () => void;
}

/** The dimensions, their ends and values, an input for the one being typed. */
function Marks({ items, live }: { items: Shown[]; live: boolean }) {
  const [editing, setEditing] = useState<{ key: string; text: string } | null>(null);
  const hit = live ? { 'data-overlay-hit': true, style: { pointerEvents: 'all' as const, cursor: 'pointer' } } : {};
  const commit = async (d: Shown, text: string) => {
    const v = parseLength(text);
    if (v == null || !(v >= 0)) { useSession.setState({ error: 'Type a distance of zero or more.' }); return; }
    setEditing(null);
    await d.commit(v);
  };
  return (
    <>
      {items.map((d) => {
        const line = ROW_PX[d.row];
        const A = d.pt(d.a, line), B = d.pt(d.b, line);
        const pts = Array.from({ length: d.steps + 1 }, (_, i) => d.pt(d.a + ((d.b - d.a) * i) / d.steps, line));
        const mt = (d.a + d.b) / 2, eps = Math.abs(d.b - d.a) / 50 || 1e-3;
        const p0 = d.pt(mt - eps, line), p1 = d.pt(mt + eps, line), pm = d.pt(mt, line);
        const ul = Math.hypot(p1.x - p0.x, p1.y - p0.y) || 1;
        const u = { x: (p1.x - p0.x) / ul, y: (p1.y - p0.y) / ul };
        const po = d.pt(mt, line + 10);
        const ol = Math.hypot(po.x - pm.x, po.y - pm.y) || 1;
        const n = { x: (po.x - pm.x) / ol, y: (po.y - pm.y) / ol };
        const witness = (s: number) => { const p = d.pt(s, 3), q = d.pt(s, line + 4); return `M${p.x} ${p.y}L${q.x} ${q.y}`; };
        // A tick across each end, slanting the same way along the line there.
        const tick = (s: number, p: Vec2) => {
          const q = d.pt(s + eps, line), l = Math.hypot(q.x - p.x, q.y - p.y) || 1, v = { x: (q.x - p.x) / l, y: (q.y - p.y) / l };
          const w = d.pt(s, line + 10), wl = Math.hypot(w.x - p.x, w.y - p.y) || 1, m = { x: (w.x - p.x) / wl, y: (w.y - p.y) / wl };
          return `M${p.x - (v.x + m.x) * 3.5} ${p.y - (v.y + m.y) * 3.5}L${p.x + (v.x + m.x) * 3.5} ${p.y + (v.y + m.y) * 3.5}`;
        };
        const mid = { x: pm.x + n.x * 8, y: pm.y + n.y * 8 };
        let angle = (Math.atan2(u.y, u.x) * 180) / Math.PI;
        if (angle > 90) angle -= 180; else if (angle < -90) angle += 180;
        const value = formatLength(d.value);
        const half = value.length * 3.4 + 4;
        // The padlock after the value, along the line the way the text reads.
        const rad = (angle * Math.PI) / 180;
        const lock = { x: mid.x + Math.cos(rad) * (half + 9), y: mid.y + Math.sin(rad) * (half + 9) };
        const editingThis = editing?.key === d.key;
        return (
          <g key={d.key} className={`temp-dim row${d.row}`} data-dim={d.key} {...Object.fromEntries(Object.entries(d.data).map(([k, v]) => [`data-${k}`, v]))} data-value={d.value.toFixed(4)}>
            <path className="temp-dim-line" d={`${witness(d.a)}${witness(d.b)}M${pts.map((p) => `${p.x} ${p.y}`).join('L')}${tick(d.a, A)}${tick(d.b, B)}`} />
            {live && d.cycleRef && (
              <g {...hit} data-end="ref" onClick={d.cycleRef}>
                <circle cx={A.x} cy={A.y} r={8} className="temp-dim-hit" /><circle cx={A.x} cy={A.y} r={3} className="temp-dim-end" />
                <title>{d.refTitle}</title>
              </g>
            )}
            {live && d.cycleOwn && (
              <g {...hit} data-end="own" onClick={d.cycleOwn}>
                <circle cx={B.x} cy={B.y} r={8} className="temp-dim-hit" /><circle cx={B.x} cy={B.y} r={3} className="temp-dim-end own" />
                <title>{t('Click: measure from the opening\'s edge or its centre')}</title>
              </g>
            )}
            {!editingThis && (
              <g {...hit} data-value-hit onClick={live ? () => setEditing({ key: d.key, text: value }) : undefined}>
                <rect x={mid.x - half} y={mid.y - 8} width={half * 2} height={16} className="temp-dim-hit" transform={`rotate(${angle} ${mid.x} ${mid.y})`} />
                <text x={mid.x} y={mid.y} className="temp-dim-text" transform={`rotate(${angle} ${mid.x} ${mid.y})`}>{value}</text>
                {live && <title>{t('Click to type the distance: the opening moves to match')}</title>}
              </g>
            )}
            {live && !editingThis && d.permanent && (
              <g {...hit} data-lock onClick={d.permanent} transform={`translate(${lock.x} ${lock.y})`}>
                <rect x={-7} y={-7} width={14} height={14} className="temp-dim-hit" />
                <path className="temp-dim-lock" d="M-3.5 -0.5V-3a3.5 3.5 0 0 1 7 0V-0.5M-4.5 -0.5H4.5V5H-4.5Z" />
                <title>{t('Make this dimension permanent')}</title>
              </g>
            )}
            {editingThis && (
              <foreignObject x={mid.x - 45} y={mid.y - 12} width={90} height={24} data-overlay-hit style={{ pointerEvents: 'all', overflow: 'visible' }}>
                <input className="temp-dim-input" data-field="temp-dim" autoFocus value={editing.text}
                  onFocus={(e) => e.currentTarget.select()}
                  onChange={(e) => setEditing({ key: d.key, text: e.target.value })}
                  onBlur={() => setEditing(null)}
                  onKeyDown={(e) => {
                    e.stopPropagation();
                    if (e.key === 'Enter') { e.preventDefault(); void commit(d, editing.text); }
                    else if (e.key === 'Escape') { e.preventDefault(); setEditing(null); }
                  }} />
              </foreignObject>
            )}
          </g>
        );
      })}
    </>
  );
}

/** The engine's references for an opening (or a wall a door is being placed on), asked again whenever the model changes; the last answer kept until the next. */
function useRefs(ask: { id: string } | { wallId: string } | null): OpeningRefs | null {
  const nodes = useSession((s) => s.nodes);
  const [refs, setRefs] = useState<OpeningRefs | null>(null);
  const asked = useRef(0);
  const key = ask ? JSON.stringify(ask) : '';
  useEffect(() => {
    if (!ask) { setRefs(null); return; }
    const n = ++asked.current;
    void query<OpeningRefs>('architecture.openingReferences', ask).then((r) => { if (n === asked.current) setRefs(r); });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, nodes]);
  return refs;
}

/** The one door, window or wall opening selected, its opening and its wall; null otherwise (or while a tool is on). */
function useSelectedOpening(): { id: string; owner: string; wallId: string; storeyId: string | null } | null {
  const tool = useUi((s) => s.tool);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  return useMemo(() => {
    if (tool || selected.length !== 1) return null;
    const s = useSession.getState();
    const n = nodes.get(selected[0]);
    if (!n?.editable) return null;
    const opening = n.type === 'IfcDoor' || n.type === 'IfcWindow' ? relatingOf(s, n.id, 'IfcRelFillsElement') : n.type === 'IfcOpeningElement' ? n : null;
    const wall = opening ? relatingOf(s, opening.id, 'IfcRelVoidsElement') : null;
    if (!opening || wall?.type !== 'IfcWall') return null;
    return { id: opening.id, owner: n.id, wallId: wall.id, storeyId: wall.storeyId ?? null };
  }, [tool, selected, nodes]);
}

/** Per opening, what the user chose for its dimensions (their references' options, their own ends); forgotten for another. */
function usePicks(who: string): [Record<string, DimPick>, (key: string, p: DimPick) => void] {
  const [picks, setPicks] = useState<{ who: string; p: Record<string, DimPick> }>({ who, p: {} });
  const now = picks.who === who ? picks.p : {};
  return [now, (key, p) => setPicks({ who, p: { ...now, [key]: { ...now[key], ...p } } })];
}

/** The along-the-wall dimensions as `Shown`s: `pt(s, out)` places them; `permanent` makes one a dimension of the view. */
function alongShown(refs: OpeningRefs, side: Side, dims: TempDim[], width: number, live: boolean, pick: (key: string, p: DimPick) => void,
  pt: (s: number, out: number) => Vec2, steps: number, permanent?: (d: TempDim) => void): Shown[] {
  const k = faceScale(refs, side);
  return dims.map((d) => ({
    key: d.key, row: d.row, a: d.refS, b: d.ownS, pt, steps, value: d.value,
    data: { ref: d.target.kind, what: d.target.options[d.opt].what, own: d.own },
    refTitle: `${t(d.target.kind === 'opening' ? 'Click: the other opening\'s near edge, centre or far edge' : 'Click: the next reference (near face, far face, core faces, axis)')} · ${d.target.name}`,
    cycleRef: live ? () => pick(d.key, { opt: d.opt + 1 }) : undefined,
    cycleOwn: live ? () => pick(d.key, { own: d.own === 'edge' ? 'centre' : 'edge' }) : undefined,
    commit: (v) => execute('architecture.moveOpening', { id: refs.opening!.id, along: alongFor(d, v, width, k) }),
    permanent: permanent && hasModule('drafting') ? () => permanent(d) : undefined,
  }));
}

/** In plan: the opening selected (or dragged, or being placed). */
export function OpeningDims({ toScreen, scale, view }: PlanOverlayProps) {
  const selectedOpening = useSelectedOpening();
  const tool = useUi((s) => s.tool);
  const placing = usePlacing((s) => s.req);
  const nodes = useSession((s) => s.nodes);
  const drag = useGripDrag((s) => s.now);
  const place = tool && placing && tool === `architecture.${placing.kind}` && placing.host.type === 'IfcWall' && placing.host.storeyId === view.storeyId ? placing : null;
  const sel = selectedOpening && (!view.storeyId || !selectedOpening.storeyId || selectedOpening.storeyId === view.storeyId) ? selectedOpening : null;
  const refs = useRefs(place ? { wallId: place.host.id } : sel ? { id: sel.id } : null);
  const [picks, pick] = usePicks(sel?.id ?? '');

  const wallId = place ? place.host.id : sel?.wallId;
  if (!wallId || !refs || !refs.faces || refs.wallId !== wallId || !(scale > 0)) return null;
  if (sel && !place && refs.opening?.id !== sel.id) return null;
  const length = refs.length ?? 0;

  // Where the opening is now: as the model has it, as a grip being dragged has it, or where the tool would put it.
  let along: number, width: number;
  if (place) {
    width = place.width;
    along = Math.min(Math.max(place.along, Math.min(width / 2, length / 2)), Math.max(length - width / 2, length / 2));
  } else {
    along = refs.opening!.along;
    width = refs.opening!.width;
    if (drag && sel && drag.owners.includes(sel.owner)) {
      const c = arcOf(nodes.get(sel.wallId)?.props ?? {});
      if (drag.key === 'along') along = Math.max(0, Math.min(length, alongOf(refs, drag.at)));
      else if (c && (drag.key === 'jamb+' || drag.key === 'jamb-')) {
        const r = jambTo(c, along, width, drag.key === 'jamb+' ? 1 : -1, drag.at);
        if (r) ({ along, width } = r);
      }
    }
  }
  const live = !place && !drag;
  const side = chooseSide(refs);
  const dims = tempDims(refs, side, along, width, picks);
  if (!dims.length) return null;
  const face = refs.faces[side];
  const sgn = side === 'left' ? 1 : -1;
  const pt = (s: number, out: number) => toScreen(at(refs, s, face + (sgn * out) / scale));
  const viewId = view.id ?? `plan:${view.storeyId}`;
  // Made permanent: an aligned dimension between the two points on the face; on an arc, the arc's length between them.
  const permanent = (d: TempDim) => {
    const mid = at(refs, (d.refS + d.ownS) / 2, face + (sgn * ROW_PX[d.row]) / scale);
    if (refs.curved) {
      const a = arcDimension(refs, side, d.refS, d.ownS);
      if (a) void execute('drafting.addDimension', { viewId, dimKind: 'arc', ...a, at: [mid.x, mid.y] });
      return;
    }
    const a = at(refs, d.refS, face), b = at(refs, d.ownS, face);
    void execute('drafting.addDimension', { viewId, a: [a.x, a.y], b: [b.x, b.y], at: [mid.x, mid.y] });
  };
  const items = alongShown(refs, side, dims, width, live, pick, pt, refs.curved ? 24 : 1, permanent);
  return (
    <g className="temp-dims" data-testid="temp-dims" data-side={side} data-curved={refs.curved ? 'true' : undefined}>
      <Marks items={items} live={live} />
    </g>
  );
}

/** In a section or an elevation that sees the selected opening's wall face-on: along the wall over it, and its heights up its side. */
export function OpeningDimsInView({ toScreen, scale, view, drawing }: DrawingOverlayProps) {
  const sel = useSelectedOpening();
  const nodes = useSession((s) => s.nodes);
  // Only where the view draws it.
  const shown = !!sel && !!drawing?.marks?.some((m) => { const o = markOwner(m.id, nodes); return o?.owner.id === sel.owner || o?.pick === sel.owner; });
  const refs = useRefs(sel && shown && drawing?.frame && !drawing.developed ? { id: sel.id } : null);
  const [picks, pick] = usePicks(sel?.id ?? '');
  if (!sel || !refs || !drawing?.frame || drawing.developed || refs.opening?.id !== sel.id || !refs.faces || !(scale > 0)) return null;
  const f = drawing.frame;
  const side = faceOn(refs, f);
  const o = refs.opening;
  if (!side || o.top == null || o.bottom == null) return null;
  const face = refs.faces[side];
  const viewId = view.id;
  const inView = (s: number, z: number) => toView(f, at(refs, s, face), z);
  // Along the wall: over the opening's head, `out` px up.
  const pt = (s: number, out: number) => toScreen(inView(s, o.top! + out / scale));
  const permanentAlong = viewId ? (d: TempDim) => {
    const [a, b] = [inView(d.refS, o.top!), inView(d.ownS, o.top!)];
    const mid = inView((d.refS + d.ownS) / 2, o.top! + ROW_PX[d.row] / scale);
    void execute('drafting.addDimension', { viewId, a: [a.x, a.y], b: [b.x, b.y], at: [mid.x, mid.y] });
  } : undefined;
  const along = alongShown(refs, side, tempDims(refs, side, o.along, o.width, picks), o.width, true, pick, pt, 1, permanentAlong);
  // Up its side: beside the edge further right in the view, from the storey's level to its bottom and to its top.
  const edges = [o.along - o.width / 2, o.along + o.width / 2];
  const xs = edges.map((s) => inView(s, o.bottom!).x);
  const edge = edges[xs[0] > xs[1] ? 0 : 1], x = Math.max(...xs);
  const up = (z: number, out: number) => toScreen({ x: x + out / scale, y: inView(edge, z).y });
  const heights: Shown[] = verticalDims(refs).map((d, i) => ({
    key: d.key, row: i as 0 | 1, a: d.from, b: d.to, pt: up, steps: 1, value: d.value, data: { ref: 'level', what: d.key === 'v:sill' ? 'sill' : 'head' },
    commit: (v) => execute('architecture.moveOpening', { id: sel.owner, sill: sillFor(refs, d, v) }),
    permanent: viewId && hasModule('drafting') ? () => {
      const [a, b] = [{ x, y: inView(edge, d.from).y }, { x, y: inView(edge, d.to).y }];
      void execute('drafting.addDimension', { viewId, a: [a.x, a.y], b: [b.x, b.y], at: [x + ROW_PX[i] / scale, (a.y + b.y) / 2] });
    } : undefined,
  }));
  return (
    <g className="temp-dims" data-testid="temp-dims" data-side={side} data-view="section">
      <Marks items={[...along, ...heights]} live />
    </g>
  );
}
