/**
 * Direct editing in a 2D view: the selection's reference lines (a wall's
 * axis, a slab's outline, a solid's sketch) and its grips.
 *
 * - One element selected: a dragged end moves alone; its neighbours stay.
 * - Several selected: grips at one point are one grip and move them all, as
 *   one undo step. Alt moves only the element the grip is shown for; Tab, over
 *   a shared grip, picks which one that is (each, then all), named in the hint.
 * - Ctrl/⌘-click on an edge's middle inserts a vertex; on a vertex, removes it.
 *
 * Plans show the grips of elements on their storey and of shapes drafted on
 * them; sections and elevations show the grips of shapes drafted on them.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import type { Grip, RefLine, Vec2 } from '../platform/module';
import { gripsOf, referencesOf } from '../platform/modules';
import { dropGrips, groupGrips, movers, runSteps, type GripGroup } from './grips';
import { formatAngle, formatLength, liveBegin, liveCursor, liveEnd, livePoint, useLive } from './liveInput';

type ToScreen = (p: Vec2) => { x: number; y: number };

export interface GripEditorOptions {
  /** The view's id as drafted shapes name it: a plan view, "plan:<storey>", a section. */
  viewIds: (string | null | undefined)[];
  mode: 'plan' | 'drawing';
  /** The plan's storey: elements on it show their grips. */
  storeyId?: string | null;
  toScreen: ToScreen;
  /** Where the cursor aims (snapped), measured from a base point. */
  aim: (clientX: number, clientY: number, from: Vec2) => Vec2;
  /** Pixels per model unit. */
  scale: number;
  /** A tool or a command is busy: no grips. */
  disabled: boolean;
  svg: React.RefObject<SVGSVGElement | null>;
}

interface Drag { group: GripGroup; members: Grip[]; at: Vec2; moved: boolean }

export function useGripEditor(o: GripEditorOptions) {
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const [hover, setHover] = useState<GripGroup | null>(null);
  const [pick, setPick] = useState<Record<string, number>>({});
  const [drag, setDrag] = useState<Drag | null>(null);
  const alt = useRef(false);
  const views = o.viewIds.filter(Boolean) as string[];
  const viewKey = views.join('|');

  // The selection's grips and lines that belong in this view.
  const { grips, refs } = useMemo(() => {
    if (o.disabled) return { grips: [] as Grip[], refs: [] as RefLine[] };
    const inView = (view: string | undefined, storey: string | null) =>
      view ? views.includes(view) : o.mode === 'plan' && (!o.storeyId || !storey || storey === o.storeyId);
    const gs: Grip[] = [];
    const rs: RefLine[] = [];
    for (const id of selected) {
      // A grid axis is selected as grid#tag: only that axis.
      const [nodeId, tag] = id.includes('#') ? [id.slice(0, id.indexOf('#')), id.slice(id.indexOf('#') + 1)] : [id, null];
      const n = nodes.get(nodeId);
      if (!n?.editable) continue;
      for (const g of gripsOf(n)) if ((!tag || g.key.startsWith(`${tag}:`)) && inView(g.view, n.storeyId)) gs.push(g);
      for (const r of referencesOf(n)) if ((!tag || !r.tag || r.tag === tag) && inView(r.view, n.storeyId)) rs.push(r);
    }
    return { grips: gs, refs: rs };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected, nodes, o.disabled, o.mode, o.storeyId, viewKey]);

  const groups = useMemo(() => groupGrips(grips, 3 / Math.max(o.scale, 1e-9)), [grips, o.scale]);
  const pickOf = (g: GripGroup) => pick[g.key] ?? g.members.length;

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
      const q = o.toScreen(g.at);
      const d = Math.hypot(q.x - p.x, q.y - p.y);
      if (d < bd) { bd = d; best = g; }
    }
    return best;
  }, [groups, local, o]);

  /** True when the press was on a grip (the view should do nothing else). */
  const onPointerDown = (e: React.PointerEvent): boolean => {
    if (e.button !== 0 || !groups.length) return false;
    const g = hit(e);
    if (!g) return false;
    const who = movers(g, pickOf(g), e.altKey);
    if (e.ctrlKey || e.metaKey) {
      const first = who[0];
      if (first.insert) void runSteps([first.insert()]);
      else if (first.remove) void runSteps([first.remove()]);
      return true;
    }
    (e.target as Element).setPointerCapture?.(e.pointerId);
    setDrag({ group: g, members: who, at: g.at, moved: false });
    // Its length and angle can be typed while dragging; Enter puts it there.
    liveBegin('grip', g.at, (p) => { void dropGrips(movers(g, pickOf(g), alt.current), p); setDrag(null); liveEnd('grip'); });
    return true;
  };
  /** True while dragging a grip. */
  const onPointerMove = (e: React.PointerEvent): boolean => {
    if (!drag) { setHover(hit(e)); return false; }
    const aimed = o.aim(e.clientX, e.clientY, drag.group.at);
    liveCursor(aimed);
    const at = livePoint({ ...useLive.getState(), cursor: aimed }) ?? aimed;
    const members = movers(drag.group, pickOf(drag.group), e.altKey);
    setDrag({ ...drag, members, at, moved: drag.moved || Math.hypot(at.x - drag.group.at.x, at.y - drag.group.at.y) > 1e-6 });
    return true;
  };
  const onPointerUp = (): boolean => {
    if (!drag) return false;
    if (drag.moved) void dropGrips(drag.members, drag.at);
    setDrag(null);
    liveEnd('grip');
    return true;
  };

  const nameOf = (owner?: string) => (owner ? nodes.get(owner)?.name ?? owner : '');
  const shape = (g: GripGroup, i: number) => {
    const q = o.toScreen(drag?.group === g ? drag.at : g.at);
    const kind = g.members[0].kind ?? 'end';
    const shared = g.members.length > 1;
    const cls = `plan-grip ${kind}${shared ? ' shared' : ''}${hover === g ? ' hot' : ''}`;
    const title = shared ? `${g.members.map((m) => nameOf(m.owner)).join(' + ')} · Tab picks one · Alt moves one` : kind === 'edgeMid' ? 'Drag the edge · Ctrl/⌘-click adds a vertex' : kind === 'vertex' ? 'Drag · Ctrl/⌘-click removes it' : 'Drag';
    const body = kind === 'edgeMid' ? <path d={`M${q.x - 4} ${q.y}L${q.x} ${q.y - 4}L${q.x + 4} ${q.y}L${q.x} ${q.y + 4}Z`} className={cls} />
      : kind === 'move' ? <path d={`M${q.x - 6} ${q.y}H${q.x + 6}M${q.x} ${q.y - 6}V${q.y + 6}`} className={`${cls} cross`} />
      : kind === 'rotate' || kind === 'radius' ? <circle cx={q.x} cy={q.y} r={4} className={cls} />
      : <rect x={q.x - 4} y={q.y - 4} width={8} height={8} className={cls} />;
    return <g key={`${g.key}:${i}`}>{body}<title>{title}</title></g>;
  };

  const render = (
    <g className="grip-layer">
      {refs.map((r, i) => {
        const ps = r.points.map(o.toScreen);
        if (ps.length < 2) return null;
        const d = `M${ps.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join('L')}${r.closed ? 'Z' : ''}`;
        return <path key={`ref${i}`} d={d} className={`ref-line ${r.kind}`} />;
      })}
      {groups.map(shape)}
      {drag?.moved && (() => {
        const a = o.toScreen(drag.group.at), b = o.toScreen(drag.at);
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
        const q = o.toScreen(hover.at);
        const who = movers(hover, pickOf(hover), alt.current);
        return <text x={q.x + 10} y={q.y + 16} className="grip-hint">{who.length > 1 ? `all ${who.length}` : nameOf(who[0].owner)} · Tab</text>;
      })()}
    </g>
  );

  return { render, onPointerDown, onPointerMove, onPointerUp, dragging: drag, hasGrips: groups.length > 0 };
}
