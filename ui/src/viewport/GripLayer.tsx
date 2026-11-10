/**
 * Direct editing in a 2D view: the selection's reference lines (a wall's
 * axis, a slab's outline, a solid's sketch) and its grips.
 *
 * - One element selected: a dragged end moves alone; its neighbours stay.
 * - Several selected: grips at one point are one grip and move them all, as
 *   one undo step. Alt moves only the element the grip is shown for; Tab, over
 *   a shared grip, picks which one that is (each, then all), named in the hint.
 * - Ctrl/⌘-click on an edge's middle inserts a vertex; on a vertex, removes it.
 * - A grip of a thing of its own (a roof window on its roof): a right-click offers to remove it, and Delete over it does.
 *
 * Plans show the grips of elements on their storey and of shapes drafted on
 * them; sections and elevations show the grips of shapes drafted on them, and
 * the model's elements selected there are dragged by their body (DrawingView,
 * decision 453).
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useUi } from '../app/store';
import { execute, useSession } from '../engine/session';
import type { Grip, GripView, RefLine, Vec2 } from '../platform/module';
import { gripsOf, referencesOf } from '../platform/modules';
import { dropGrips, groupGrips, movers, runSteps, setHotRemove, type GripGroup } from './grips';
import { openContextMenu } from '../shell/ContextMenu';
import { formatAngle, formatLength, liveBegin, liveCursor, liveEnd, livePoint, useLive } from './liveInput';
import { traceBegin, traceEnd } from './moveTrace';
import { frameArg, useCad } from './cad/session';
import type { Shape } from './cad/shapes';
import { useLang } from '../i18n';
import { faceLetters } from './faceMarks';

/** Where a model point shows on screen; `z` (the 3D view's) is the height it stands at. */
type ToScreen = (p: Vec2, z?: number) => { x: number; y: number };

/** How far out from its point a flip arrow is drawn, px. */
const FLIP_OUT = 22;

/** A point a fraction of the way along a polyline (model), with the direction it runs there. */
function alongLine(pts: Vec2[], frac: number): { at: Vec2; dir: Vec2 } {
  const lens = pts.slice(1).map((p, i) => Math.hypot(p.x - pts[i].x, p.y - pts[i].y));
  let left = lens.reduce((a, b) => a + b, 0) * frac;
  for (let i = 0; i < lens.length; i++) {
    if (left <= lens[i] || i === lens.length - 1) {
      const k = lens[i] > 0 ? Math.min(1, left / lens[i]) : 0;
      const [a, b] = [pts[i], pts[i + 1]];
      return { at: { x: a.x + (b.x - a.x) * k, y: a.y + (b.y - a.y) * k }, dir: { x: (b.x - a.x) / (lens[i] || 1), y: (b.y - a.y) / (lens[i] || 1) } };
    }
    left -= lens[i];
  }
  return { at: pts[0], dir: { x: 1, y: 0 } };
}

export interface GripEditorOptions {
  /** The view's id as drafted shapes name it: a plan view, "plan:<storey>", a section. */
  viewIds: (string | null | undefined)[];
  mode: 'plan' | 'drawing';
  /** The plan's storey: elements on it show their grips. */
  storeyId?: string | null;
  toScreen: ToScreen;
  /** Where the cursor aims (snapped), measured from a base point (on the plane at height z, in 3D). */
  aim: (clientX: number, clientY: number, from: Vec2, z?: number) => Vec2;
  /** The height an element's grips stand at (3D); none in 2D. */
  zOf?: (owner: string | undefined) => number;
  /** Show every selected element's grips, whatever its storey (3D). */
  anyStorey?: boolean;
  /** Pixels per model unit. */
  scale: number;
  /** The view's drawn annotations: what an annotation's grips stand on (a dimension's points as drawn now). */
  marks?: NonNullable<GripView['mark']>[];
  /** A tool or a command is busy: no grips. */
  disabled: boolean;
  svg: React.RefObject<SVGSVGElement | null>;
}

/** A grip being dragged; `copy`: a move grip dragged with Ctrl/⌘, which leaves the element and drops a copy. */
interface Drag { group: GripGroup; members: Grip[]; at: Vec2; moved: boolean; copy?: boolean }

export function useGripEditor(o: GripEditorOptions) {
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const letters = faceLetters(useLang((s) => s.lang));
  const [hover, setHover] = useState<GripGroup | null>(null);
  const [pick, setPick] = useState<Record<string, number>>({});
  const [drag, setDrag] = useState<Drag | null>(null);
  const alt = useRef(false);
  const views = o.viewIds.filter(Boolean) as string[];
  const viewKey = views.join('|');

  // The selection's grips and lines that belong in this view.
  const { grips, refs } = useMemo(() => {
    if (o.disabled) return { grips: [] as Grip[], refs: [] as (RefLine & { owner: string })[] };
    const inView = (view: string | undefined, storey: string | null) =>
      view ? views.includes(view) : o.mode === 'plan' && (o.anyStorey || !o.storeyId || !storey || storey === o.storeyId);
    const gs: Grip[] = [];
    const rs: (RefLine & { owner: string })[] = [];
    for (const id of selected) {
      // A grid axis is selected as grid#tag: only that axis.
      const [nodeId, tag] = id.includes('#') ? [id.slice(0, id.indexOf('#')), id.slice(id.indexOf('#') + 1)] : [id, null];
      const n = nodes.get(nodeId);
      if (!n?.editable) continue;
      const marks = o.marks?.filter((m) => m.id === n.id || m.id.startsWith(`${n.id}:`));
      for (const g of gripsOf(n, { mark: marks?.find((m) => m.id === n.id), marks, view: views[0] })) if ((!tag || g.key.startsWith(`${tag}:`)) && inView(g.view, n.storeyId)) gs.push(g);
      for (const r of referencesOf(n)) if ((!tag || !r.tag || r.tag === tag) && inView(r.view, n.storeyId)) rs.push({ ...r, owner: n.id });
    }
    return { grips: gs, refs: rs };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected, nodes, o.disabled, o.mode, o.storeyId, o.anyStorey, viewKey, o.marks]);

  const groups = useMemo(() => groupGrips(grips, 3 / Math.max(o.scale, 1e-9)), [grips, o.scale]);
  const pickOf = (g: GripGroup) => pick[g.key] ?? g.members.length;
  const zOf = (g: GripGroup) => o.zOf?.(g.members[0].owner);
  /** Where a group shows on screen: its point, a flip arrow a little way out along its direction. */
  const screenOf = (g: GripGroup, at: Vec2 = g.at) => {
    const z = zOf(g);
    const q = o.toScreen(at, z);
    const d = g.members[0].dir;
    if (g.members[0].kind !== 'flip' || !d) return { ...q, ux: 0, uy: 0 };
    const r = o.toScreen({ x: at.x + d.x * 0.01, y: at.y + d.y * 0.01 }, z);
    const l = Math.hypot(r.x - q.x, r.y - q.y) || 1;
    const ux = (r.x - q.x) / l, uy = (r.y - q.y) / l;
    return { x: q.x + ux * FLIP_OUT, y: q.y + uy * FLIP_OUT, ux, uy };
  };

  // A view that goes while a grip is dragged takes its trace with it.
  useEffect(() => () => traceEnd('grip'), []);
  // Delete over a grip of a thing of its own removes that thing (decision 280).
  useEffect(() => { setHotRemove(hover?.members.length === 1 ? hover.members[0] : null); }, [hover]);
  useEffect(() => () => setHotRemove(null), []);

  // Tab over a shared grip: the next owner, then all.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Alt') alt.current = e.type === 'keydown';
      if (e.type !== 'keydown' || e.key !== 'Tab' || !hover || hover.members.length < 2) return;
      if ((e.target as HTMLElement)?.closest('input, select, textarea')) return;
      e.preventDefault();
      e.stopPropagation();
      setPick((p) => ({ ...p, [hover.key]: ((p[hover.key] ?? hover.members.length) + 1) % (hover.members.length + 1) }));
    };
    window.addEventListener('keydown', onKey, true);
    window.addEventListener('keyup', onKey, true);
    return () => { window.removeEventListener('keydown', onKey, true); window.removeEventListener('keyup', onKey, true); };
  }, [hover]);

  const local = useCallback((e: { clientX: number; clientY: number }) => {
    const r = o.svg.current!.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  }, [o.svg]);
  const hit = useCallback((e: { clientX: number; clientY: number }) => {
    const p = local(e);
    let best: GripGroup | null = null, bd = 8;
    for (const g of groups) {
      const q = screenOf(g);
      const d = Math.hypot(q.x - p.x, q.y - p.y);
      if (d < bd) { bd = d; best = g; }
    }
    return best;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groups, local, o]);

  /** What the grip's owners look like in this view (their outline, else their reference lines), where they are now. */
  const ghostOf = (who: Grip[]): Shape[] => {
    const owners = [...new Set(who.map((m) => m.owner).filter((x): x is string => !!x))];
    const host = useCad.getState().host;
    return owners.flatMap((id) => {
      const own = host?.outlinesOf?.(id);
      if (own?.length) return own;
      return refs.filter((r) => r.owner === id && r.points.length >= 2).map((r): Shape => ({ type: 'polyline', pts: r.points, closed: !!r.closed }));
    });
  };

  /** True when the press was on a grip (the view should do nothing else). */
  const onPointerDown = (e: React.PointerEvent): boolean => {
    if (e.button !== 0 || !groups.length) return false;
    const g = hit(e);
    if (!g) return false;
    const who = movers(g, pickOf(g), e.altKey);
    // A flip arrow: a click.
    if (who[0].kind === 'flip') { void runSteps([who[0].click?.()]); return true; }
    const copy = (e.ctrlKey || e.metaKey) && who[0].kind === 'move';
    if ((e.ctrlKey || e.metaKey) && !copy) {
      const first = who[0];
      if (first.insert) void runSteps([first.insert()]);
      else if (first.remove) void runSteps([first.remove()]);
      return true;
    }
    (e.target as Element).setPointerCapture?.(e.pointerId);
    setDrag({ group: g, members: who, at: g.at, moved: false, copy });
    // Its length and angle can be typed while dragging; Enter puts it there.
    liveBegin('grip', g.at, (p) => { void dropGrips(movers(g, pickOf(g), alt.current), p); setDrag(null); liveEnd('grip'); traceEnd('grip'); }, { delta: true });
    // In 2D the move's trace: the ghost of what the grip moves left where it is (a move grip carries it along).
    if (!o.zOf) traceBegin('grip', g.at, ghostOf(who), who[0].kind === 'move');
    return true;
  };
  /** A right-click on a grip of a thing of its own: a menu to remove it. True when it opened. */
  const onContextMenu = (e: { clientX: number; clientY: number }): boolean => {
    const g = hit(e);
    const items = (g?.members ?? []).filter((m) => m.remove && m.removeLabel)
      .map((m) => ({ label: m.removeLabel!, onSelect: () => { setHover(null); void runSteps([m.remove!()]); } }));
    if (!items.length) return false;
    openContextMenu(e.clientX, e.clientY, items);
    return true;
  };
  /** True while dragging a grip. */
  const onPointerMove = (e: React.PointerEvent): boolean => {
    if (!drag) { setHover(hit(e)); return false; }
    const aimed = o.aim(e.clientX, e.clientY, drag.group.at, zOf(drag.group));
    liveCursor(aimed);
    const members = movers(drag.group, pickOf(drag.group), e.altKey);
    // A grip that keeps its own angles (a tag's knee): on them unless Shift frees it.
    const typed = livePoint({ ...useLive.getState(), cursor: aimed }) ?? aimed;
    const keep = members.length === 1 ? members[0].constrain : undefined;
    const at = keep ? keep(typed, e.shiftKey) : typed;
    setDrag({ ...drag, members, at, moved: drag.moved || Math.hypot(at.x - drag.group.at.x, at.y - drag.group.at.y) > 1e-6 });
    return true;
  };
  const onPointerUp = (): boolean => {
    if (!drag) return false;
    if (drag.moved && drag.copy) {
      // Ctrl/⌘ on a move grip: the elements stay, a copy goes where the grip was let go.
      const ids = [...new Set(drag.members.map((m) => m.owner).filter((x): x is string => !!x))];
      if (ids.length) void execute('element.transform', { ids, op: 'copy', by: [drag.at.x - drag.group.at.x, drag.at.y - drag.group.at.y], ...frameArg(drag.group.at) });
    } else if (drag.moved) void dropGrips(drag.members, drag.at);
    setDrag(null);
    liveEnd('grip');
    traceEnd('grip');
    return true;
  };

  const nameOf = (owner?: string) => (owner ? nodes.get(owner)?.name ?? owner : '');
  const shape = (g: GripGroup, i: number) => {
    const q = screenOf(g, drag?.group === g ? drag.at : g.at);
    const kind = g.members[0].kind ?? 'end';
    const shared = g.members.length > 1;
    const cls = `plan-grip ${kind}${shared ? ' shared' : ''}${hover === g ? ' hot' : ''}`;
    const title = g.members[0].title ?? (shared ? `${g.members.map((m) => nameOf(m.owner)).join(' + ')} · Tab picks one · Alt moves one` : kind === 'edgeMid' ? 'Drag the edge · Ctrl/⌘-click adds a vertex' : kind === 'vertex' ? 'Drag · Ctrl/⌘-click removes it' : kind === 'move' ? 'Drag to move · Ctrl/⌘-drag to copy' : 'Drag');
    // A flip arrow: a small arrowhead pointing the way it flips, its stem back towards the element.
    if (kind === 'flip') {
      const { ux, uy } = q, nx = -uy, ny = ux;
      const tip = { x: q.x + ux * 6, y: q.y + uy * 6 };
      const d = `M${tip.x} ${tip.y}L${q.x - ux * 2 + nx * 5} ${q.y - uy * 2 + ny * 5}L${q.x - ux * 2 - nx * 5} ${q.y - uy * 2 - ny * 5}Z`
        + `M${q.x - ux * 2} ${q.y - uy * 2}L${q.x - ux * 10} ${q.y - uy * 10}`;
      return <g key={`${g.key}:${i}`}><circle cx={q.x} cy={q.y} r={9} className="plan-grip-hit" /><path d={d} className={cls} /><title>{title}</title></g>;
    }
    // A rebar's anchor: a small orange diamond standing the cover outside the bar, for the formwork's face.
    const body = kind === 'edgeMid' ? <path d={`M${q.x - 4} ${q.y}L${q.x} ${q.y - 4}L${q.x + 4} ${q.y}L${q.x} ${q.y + 4}Z`} className={cls} />
      : kind === 'anchor' ? <path d={`M${q.x - 5} ${q.y}L${q.x} ${q.y - 5}L${q.x + 5} ${q.y}L${q.x} ${q.y + 5}Z`} className={cls} />
      : kind === 'move' ? <path d={`M${q.x - 6} ${q.y}H${q.x + 6}M${q.x} ${q.y - 6}V${q.y + 6}`} className={`${cls} cross`} />
      : kind === 'rotate' || kind === 'radius' ? <circle cx={q.x} cy={q.y} r={4} className={cls} />
      : <rect x={q.x - 4} y={q.y - 4} width={8} height={8} className={cls} />;
    return <g key={`${g.key}:${i}`} data-grip={g.members[0].key}>{body}<title>{title}</title></g>;
  };

  const render = (
    <g className="grip-layer">
      {refs.map((r, i) => {
        const z = o.zOf?.(r.owner);
        const ps = r.points.map((p) => o.toScreen(p, z));
        if (ps.length < 2) return null;
        const d = `M${ps.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join('L')}${r.closed ? 'Z' : ''}`;
        if (!r.faces) return <path key={`ref${i}`} d={d} className={`ref-line ${r.kind}`} />;
        // A wall's axis (report #79): an arrowhead the way it was drawn, three quarters along (its middle has the move and
        // flip grips), and its faces' letters a quarter along, R on its right and L on its left, as the Wall types legend shows.
        const unit = (at: Vec2, v: Vec2) => {
          const a = o.toScreen(at, z), b = o.toScreen({ x: at.x + v.x * 0.01, y: at.y + v.y * 0.01 }, z);
          const l = Math.hypot(b.x - a.x, b.y - a.y) || 1;
          return { q: a, x: (b.x - a.x) / l, y: (b.y - a.y) / l };
        };
        const head = alongLine(r.points, 0.75), mid = alongLine(r.points, 0.25);
        const t = unit(head.at, head.dir), n = unit(mid.at, { x: -mid.dir.y, y: mid.dir.x });
        const tn = { x: -t.y, y: t.x };
        const arrow = `M${t.q.x + t.x * 7} ${t.q.y + t.y * 7}L${t.q.x - t.x * 4 + tn.x * 4.5} ${t.q.y - t.y * 4 + tn.y * 4.5}L${t.q.x - t.x * 4 - tn.x * 4.5} ${t.q.y - t.y * 4 - tn.y * 4.5}Z`;
        return (
          <g key={`ref${i}`} data-testid="wall-faces">
            <path d={d} className={`ref-line ${r.kind}`} />
            <path d={arrow} className="ref-arrow" />
            <text x={n.q.x - n.x * 14} y={n.q.y - n.y * 14} className="ref-face" data-face="right">{letters.right}</text>
            <text x={n.q.x + n.x * 14} y={n.q.y + n.y * 14} className="ref-face" data-face="left">{letters.left}</text>
          </g>
        );
      })}
      {groups.map(shape)}
      {drag?.moved && o.zOf && (() => {
        const a = o.toScreen(drag.group.at, zOf(drag.group)), b = o.toScreen(drag.at, zOf(drag.group));
        const dx = drag.at.x - drag.group.at.x, dy = drag.at.y - drag.group.at.y;
        const ang = ((Math.atan2(dy, dx) * 180) / Math.PI + 360) % 360;
        return (
          <g className="plan-preview">
            <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} className="plan-grip-drag" />
            <text x={b.x + 10} y={b.y - 10}>{formatLength(Math.hypot(dx, dy))} · {formatAngle(ang)}°</text>
          </g>
        );
      })()}
      {hover && hover.members.length > 1 && !drag && (() => {
        const q = o.toScreen(hover.at, zOf(hover));
        const who = movers(hover, pickOf(hover), alt.current);
        return <text x={q.x + 10} y={q.y + 16} className="grip-hint">{who.length > 1 ? `all ${who.length}` : nameOf(who[0].owner)} · Tab</text>;
      })()}
    </g>
  );

  return { render, onPointerDown, onPointerMove, onPointerUp, onContextMenu, onPointerLeave: () => { if (!drag) setHover(null); }, dragging: drag, hasGrips: groups.length > 0 };
}
