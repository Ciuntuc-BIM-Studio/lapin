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
 * - Shift-drag on an outline's edge middle offsets the whole outline, every edge, live (decision 609; outlineOffset.ts):
 *   the distance out along the edge's normal, typed too; the engine's offset drawn as it would be, or why it cannot be.
 *
 * Plans show the grips of elements on their storey and of shapes drafted on
 * them; sections and elevations show the grips of shapes drafted on them, and
 * the model's elements selected there are dragged by their body (DrawingView,
 * decision 453).
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useUi } from '../app/store';
import { execute, useSession } from '../engine/session';
import type { NodeDto } from '../engine/protocol';
import type { Grip, GripView, OutlineOffsetGrip, RefLine, Vec2 } from '../platform/module';
import { offsetAll, offsetDistance, offsetOutline } from './outlineOffset';
import { outlinePoints } from './contour';
import { gripsOf, gripSnapped, referencesOf } from '../platform/modules';
import { throughInset, viaOf } from './insetMap';
import { dropGrips, groupGrips, movers, nearestGroup, runSteps, setHotRemove, useGripRev, type GripGroup } from './grips';
import { edgeOfGrip, nearestOnLine } from './gripKit';
import { askLength } from '../shell/AskBox';
import { openContextMenu, type MenuItem } from '../shell/ContextMenu';
import { autoholdOn, clickEnds, onAutoholdCancel, releaseEnds } from '../app/autohold';
import { formatAngle, formatLength, liveBegin, liveCursor, liveEnd, liveLock, livePoint, useLive } from './liveInput';
import { ownGuideOf } from './gripLock';
import { traceBegin, traceEnd } from './moveTrace';
import { frameArg, useCad } from './cad/session';
import type { Shape } from './cad/shapes';
import { t, useLang } from '../i18n';
import { faceLetters } from './faceMarks';
import { setGripDrag } from './gripDrag';
import { endPreview, previewDrag, viewHasConstraints } from './cad/sketchStore';

/** Where a model point shows on screen; `z` (the 3D view's) is the height it stands at. */
type ToScreen = (p: Vec2, z?: number) => { x: number; y: number };

/** How far out from its point a flip arrow or roof slope indicator is drawn, px. */
const FLIP_OUT = 22;
const SLOPE_IN = 22;

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
  aim: (clientX: number, clientY: number, from: Vec2, z?: number, along?: { from: Vec2; dir: Vec2; locked: boolean }) => Vec2;
  /** Where a point of the window is on the drawing, unsnapped and with no side effect (a right-click's "Add corner here"); else `aim` is asked. */
  raw?: (clientX: number, clientY: number) => Vec2;
  /** The height an element's grips stand at (3D); none in 2D. */
  zOf?: (owner: string | undefined) => number;
  /** Show every selected element's grips, whatever its storey (3D). */
  anyStorey?: boolean;
  /** Pixels per model unit. */
  scale: number;
  /** The view's drawn annotations: what an annotation's grips stand on (a dimension's points as drawn now). */
  marks?: NonNullable<GripView['mark']>[];
  /** A section's or an elevation's frame, kind and whether it is developed, lent to the grips (GripView). */
  frame?: GripView['frame']; viewKind?: string | null; developed?: boolean;
  /** A tool or a command is busy: no grips. */
  disabled: boolean;
  svg: React.RefObject<SVGSVGElement | null>;
}

/**
 * A grip being dragged; `copy`: a move grip dragged with Ctrl/⌘, which leaves the element and drops a copy; `offset`: an
 * edge middle Shift-dragged, the whole outlines of its members offset (decision 609).
 */
interface Drag { group: GripGroup; members: Grip[]; at: Vec2; moved: boolean; copy?: boolean; insert?: boolean; offset?: OutlineOffsetGrip[] }

/** An offset drag's preview: each outline as the engine would offset it (by its owner, for its height in 3D), or why it cannot be. */
interface OffsetPreview { distance: number; outlines: { owner?: string; pts: Vec2[] }[]; problem: string | null }

/**
 * A component's detail (forum #224) is kept on no view of the project's: in a view that draws it, its grips are those of the
 * shape as this view draws it, saved with the view's id so the engine takes the points to where it keeps them (docs/components.md).
 */
function drawnHere(n: NodeDto, mark: object | undefined, view: string | undefined): NodeDto {
  if (n.props.viewId !== 'component-detail' || !view) return n;
  const shape = (mark as { shapes?: Record<string, unknown>[] } | undefined)?.shapes?.find((s) => !s.hatch);
  return shape ? { ...n, props: { ...n.props, viewId: view, shape: shape as NodeDto["props"][string] } } : n;
}

/** What a drag's move reads of the pointer event (so a modifier key can redo the move without one). */
interface MoveLike { clientX: number; clientY: number; altKey: boolean; shiftKey: boolean; ctrlKey: boolean; metaKey: boolean }

export function useGripEditor(o: GripEditorOptions) {
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const letters = faceLetters(useLang((s) => s.lang));
  const [hover, setHover] = useState<GripGroup | null>(null);
  const [pick, setPick] = useState<Record<string, number>>({});
  const [drag, setDrag] = useState<Drag | null>(null);
  const [offsetShown, setOffsetShown] = useState<OffsetPreview | null>(null);
  const offsetAsk = useRef(0);
  const alt = useRef(false);
  const ctrl = useRef(false);
  /** The last pointer position of the drag, and what redoes its move (a modifier pressed or let go with the mouse still). */
  const lastMove = useRef<MoveLike | null>(null);
  const again = useRef<(() => void) | null>(null);
  const gripRev = useGripRev((s) => s.rev);
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
      const kept = nodes.get(nodeId);
      if (!kept?.editable) continue;
      const marks = o.marks?.filter((m) => m.id === kept.id || m.id.startsWith(`${kept.id}:`));
      const n = drawnHere(kept, marks?.find((m) => m.id === kept.id), views[0]);
      for (const g of gripsOf(n, { mark: marks?.find((m) => m.id === n.id), marks, view: views[0], frame: o.frame, kind: o.viewKind, developed: o.developed })) if ((!tag || g.key.startsWith(`${tag}:`)) && inView(g.view, n.storeyId)) gs.push(g);
      // Seen through an inset view (decision 576): the grips it has on its own view, where the inset shows them.
      for (const m of marks ?? []) {
        const via = m.id === n.id ? viaOf(m) : null;
        if (!via || !views.includes(via.host)) continue;
        const through = marks?.filter((x) => viaOf(x)?.inset === via.inset);
        for (const g of gripsOf(n, { mark: m, marks: through, view: via.source })) {
          if ((tag && !g.key.startsWith(`${tag}:`)) || g.view !== via.source) continue;
          const t = throughInset(g, via);
          if (t) gs.push({ ...t, key: `${t.key}@${via.inset}` });
        }
      }
      for (const r of referencesOf(n, { view: views[0] })) if ((!tag || !r.tag || r.tag === tag) && inView(r.view, n.storeyId)) rs.push({ ...r, owner: n.id });
    }
    return { grips: gs, refs: rs };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected, nodes, o.disabled, o.mode, o.storeyId, o.anyStorey, viewKey, o.marks, o.frame, o.viewKind, o.developed, gripRev]);

  const groups = useMemo(() => groupGrips(grips, 3 / Math.max(o.scale, 1e-9)), [grips, o.scale]);
  const pickOf = (g: GripGroup) => pick[g.key] ?? g.members.length;
  const zOf = (g: GripGroup) => o.zOf?.(g.members[0].owner);
  /** Where a group shows on screen: its point, a flip arrow or slope indicator offset along its direction. */
  const screenOf = (g: GripGroup, at: Vec2 = g.at) => {
    const z = zOf(g);
    const q = o.toScreen(at, z);
    const kind = g.members[0].kind;
    const d = g.members[0].dir;
    if ((kind !== 'flip' && kind !== 'slope') || !d) return { ...q, ux: 0, uy: 0 };
    const r = o.toScreen({ x: at.x + d.x * 0.01, y: at.y + d.y * 0.01 }, z);
    const l = Math.hypot(r.x - q.x, r.y - q.y) || 1;
    const ux = (r.x - q.x) / l, uy = (r.y - q.y) / l;
    const dist = kind === 'slope' ? SLOPE_IN : FLIP_OUT;
    return { x: q.x + ux * dist, y: q.y + uy * dist, ux, uy };
  };

  // A view that goes while a grip is dragged takes its trace with it.
  useEffect(() => () => { traceEnd('grip'); endPreview(); }, []);
  // Esc in autohold (issue 86): the grip goes back where it was, nothing is dropped.
  useEffect(() => onAutoholdCancel(() => { setDrag(null); setOffsetShown(null); offsetAsk.current++; endPreview(); traceEnd('grip'); liveEnd('grip'); }), []);
  // In 2D, the drag told to what follows it (a door's temporary dimensions, decision 511).
  useEffect(() => {
    if (o.zOf) return;
    setGripDrag(drag?.moved ? { owners: drag.members.map((m) => m.owner).filter((x): x is string => !!x), key: drag.members[0]?.key ?? '', at: drag.at } : null);
  }, [drag, o.zOf]);
  useEffect(() => () => setGripDrag(null), []);
  // Delete over a grip of a thing of its own removes that thing (decision 280).
  useEffect(() => { setHotRemove(hover?.members.length === 1 ? hover.members[0] : null); }, [hover]);
  useEffect(() => () => setHotRemove(null), []);

  // Tab over a shared grip: the next owner, then all.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Alt') alt.current = e.type === 'keydown';
      // Ctrl/⌘ while an end grip is dragged: the lock on its own line shows without the mouse moving (decision 865).
      if (e.key === 'Control' || e.key === 'Meta') { ctrl.current = e.type === 'keydown'; again.current?.(); }
      if (e.type !== 'keydown' || e.key !== 'Tab' || !hover || hover.members.length < 2) return;
      if ((e.target as HTMLElement)?.closest('input, select, textarea')) return;
      e.preventDefault();
      e.stopPropagation();
      if (e.repeat) return;
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
    return nearestGroup(groups, (g) => {
      const q = screenOf(g);
      return { d: Math.hypot(q.x - p.x, q.y - p.y), limit: g.members[0].kind === 'slope' ? 12 : 8 };
    }, 12);
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
    // A grip drag held by autohold (issue 86): a left click drops it where the pointer is (or where the typed values put it).
    if (e.button === 0 && drag && clickEnds(autoholdOn())) { finishDrag(); return true; }
    if (e.button !== 0 || !groups.length) return false;
    const g = hit(e);
    if (!g) return false;
    const who = movers(g, pickOf(g), e.altKey);
    // A flip arrow or roof slope indicator: a click.
    if (who[0].kind === 'flip' || who[0].kind === 'slope') { void runSteps([who[0].click?.()]); return true; }
    const copy = (e.ctrlKey || e.metaKey) && who[0].kind === 'move';
    const first = who[0];
    // Shift on an edge's middle: the whole outline offset, every edge (decision 609).
    if (e.shiftKey && !e.ctrlKey && !e.metaKey && first.offsetAll) {
      const members = who.filter((m) => m.offsetAll);
      const grips = members.map((m) => m.offsetAll!);
      (e.target as Element).setPointerCapture?.(e.pointerId);
      setDrag({ group: g, members, at: g.at, moved: false, offset: grips });
      liveBegin('grip', g.at, (p) => { void finishOffset(grips, offsetDistance(g.at, first.offsetAll!.normal, p)); setDrag(null); liveEnd('grip'); }, { delta: true });
      return true;
    }
    // Ctrl/⌘ on a grip with an insert-by-drag (a dimension's segment middle): a click inserts halfway, a drag where it is dropped.
    const insert = (e.ctrlKey || e.metaKey) && !copy && !!first.insertAt && !!first.insert;
    // Alt-click on a grip that takes its point out (a dimension's), alone in its group: it is taken out.
    if (e.altKey && !e.ctrlKey && !e.metaKey && first.altRemove && first.remove && g.members.length === 1) { void runSteps([first.remove()]); return true; }
    if ((e.ctrlKey || e.metaKey) && !copy && !insert) {
      if (first.extend) first.extend();
      else if (first.insert) void runSteps([first.insert()]);
      else if (first.remove) void runSteps([first.remove()]);
      return true;
    }
    (e.target as Element).setPointerCapture?.(e.pointerId);
    setDrag({ group: g, members: insert ? [first] : who, at: g.at, moved: false, copy, insert });
    // Its length and angle can be typed while dragging; Enter puts it there.
    // For radial/angular arc & circle grips with `center` (forum topic 293), measure radius and angle directly from the centre.
    const liveBase = first.center ?? g.at;
    liveBegin('grip', liveBase, (p) => { void dropGrips(movers(g, pickOf(g), alt.current), p); setDrag(null); endPreview(); liveEnd('grip'); traceEnd('grip'); }, { delta: !first.center });
    // In 2D the move's trace: the ghost of what the grip moves left where it is (a move grip carries it along).
    if (!o.zOf) traceBegin('grip', g.at, ghostOf(who), who[0].kind === 'move');
    return true;
  };
  /** "Fillet corner…" and "Chamfer corner…" on a corner that may be rounded or cut, the radius or distance typed (forum topic 97; a wall's joined end). */
  const cornerMenu = (c: NonNullable<Grip['corner']>): MenuItem[] => {
    const asked = (title: string, label: string, act: (v: number) => ReturnType<NonNullable<Grip['remove']>>) => async () => {
      setHover(null);
      const v = await askLength(title, label, 0.5);
      if (v != null && v > 0) void runSteps([act(v)]);
    };
    return [
      { label: 'Fillet corner…', disabled: !c.straight, onSelect: () => void asked('Fillet corner', 'Radius', c.fillet)() },
      { label: 'Chamfer corner…', disabled: !c.straight, onSelect: () => void asked('Chamfer corner', 'Distance', c.chamfer)() },
    ];
  };
  /** A right-click on a grip of a thing of its own, or one with a menu of its own: the menu. True when it opened. */
  const onContextMenu = (e: { clientX: number; clientY: number }): boolean => {
    const g = hit(e);
    const items = [
      ...(g?.members ?? []).filter((m) => m.remove && m.removeLabel)
        .map((m) => ({ label: m.removeLabel!, onSelect: () => { setHover(null); void runSteps([m.remove!()]); } })),
      ...(g?.members ?? []).flatMap((m) => m.menu ?? [])
        .map((x) => ({ label: x.label, onSelect: () => { setHover(null); void runSteps([x.run()]); } })),
      // A corner that is not an outline's vertex (those are cornerItems'): a wall's end joined to another wall.
      ...(g?.members ?? []).filter((m) => m.corner && !(m.kind === 'vertex' && m.remove && !m.removeLabel)).flatMap((m) => cornerMenu(m.corner!)),
    ];
    if (!items.length) return false;
    // An outline's corner whose grip has a menu of its own too (a terrain's outline point, T1): its corner's items first.
    const corner = g?.members.some((m) => m.kind === 'vertex' && m.remove && !m.removeLabel) ? cornerItems(e) : [];
    openContextMenu(e.clientX, e.clientY, [...corner, ...items]);
    return true;
  };
  /**
   * A right-click on a selected outline (a floor, an opening, a roof, a room…): "Remove corner" on one of its corners (refused
   * under three), else "Add corner here" when the cursor is near one of its edges, the corner put on the edge at the point
   * clicked (forum topic 97). An outline whose edges may be curved (a floor, a floor opening, a ceiling; stage 2) offers also
   * "Fillet corner…" and "Chamfer corner…" on a corner (the radius or distance typed), and "Make arc" or "Make straight" on an
   * edge. Each is one engine command, one undo step. For the menu the view opens.
   */
  const cornerItems = (e: { clientX: number; clientY: number }): MenuItem[] => {
    if (o.disabled || drag) return [];
    const g = hit(e);
    const run = (step: () => ReturnType<NonNullable<Grip['remove']>>) => () => { setHover(null); void runSteps([step()]); };
    const corner = g?.members.find((m) => m.kind === 'vertex' && m.remove && !m.removeLabel);
    if (corner) {
      const items: MenuItem[] = [{ label: 'Remove corner', disabled: !corner.remove!(), onSelect: run(corner.remove!) }];
      if (corner.corner) items.push(...cornerMenu(corner.corner));
      return items;
    }
    if (g) {
      // An edge's own grip (its diamond, or a curved edge's round grip): the edge's menu, as a right-click beside it gives.
      const edge = edgeOfGrip(g.members, grips);
      if (!edge) return [];
      const items: MenuItem[] = edge.insert ? [{ label: 'Add corner here', onSelect: run(edge.insert) }] : [];
      if (edge.bend) items.push({ label: 'Make arc', onSelect: run(edge.bend) });
      if (edge.straighten) items.push({ label: 'Make straight', onSelect: run(edge.straighten) });
      return items;
    }
    const p = o.raw ? o.raw(e.clientX, e.clientY) : o.aim(e.clientX, e.clientY, { x: 0, y: 0 });
    let best: { grip: Grip; dist: number } | null = null;
    for (const m of grips) {
      if (!m.edge || !m.addCornerAt) continue;
      const d = nearestOnLine(m.edge, p);
      if (d.t <= 1e-6 || d.t >= 1 - 1e-6) continue;
      if (!best || d.dist < best.dist) best = { grip: m, dist: d.dist };
    }
    if (!best || best.dist * o.scale > 10) return [];
    const at = best.grip;
    const items: MenuItem[] = [{ label: 'Add corner here', onSelect: run(() => at.addCornerAt!(p)) }];
    if (at.bend) items.push({ label: 'Make arc', onSelect: run(at.bend) });
    if (at.straighten) items.push({ label: 'Make straight', onSelect: run(at.straighten) });
    return items;
  };
  /** The outlines offset by `distance` as the engine would make them, shown while dragging (the last asked wins). */
  const previewOffset = (members: Grip[], distance: number) => {
    const ask = ++offsetAsk.current;
    void Promise.all(members.map((m) => offsetOutline(m.offsetAll!.outline, distance).then((r) => ({ owner: m.owner, r })))).then((rs) => {
      if (ask !== offsetAsk.current) return;
      const bad = rs.find((x) => 'problem' in x.r);
      setOffsetShown({ distance, problem: bad && 'problem' in bad.r ? bad.r.problem : null, outlines: rs.flatMap((x) => ('outline' in x.r ? [{ owner: x.owner, pts: outlinePoints(x.r.outline) }] : [])) });
    });
  };
  const finishOffset = async (grips: OutlineOffsetGrip[], distance: number) => {
    offsetAsk.current++;
    setOffsetShown(null);
    if (Math.abs(distance) > 1e-6) await offsetAll(grips, distance);
  };
  /** True while dragging a grip. */
  const onPointerMove = (e: MoveLike): boolean => {
    if (!drag) { setHover(hit(e as React.PointerEvent)); return false; }
    lastMove.current = { clientX: e.clientX, clientY: e.clientY, altKey: e.altKey, shiftKey: e.shiftKey, ctrlKey: e.ctrlKey, metaKey: e.metaKey };
    ctrl.current = e.ctrlKey || e.metaKey;
    if (drag.offset) {
      // Out along the edge's normal from its middle: the distance every edge goes (typed too).
      const n = drag.offset[0].normal, from = drag.group.at;
      const along = offsetDistance(from, n, o.aim(e.clientX, e.clientY, from, zOf(drag.group)));
      const cursor = { x: from.x + n.x * along, y: from.y + n.y * along };
      liveCursor(cursor);
      const distance = offsetDistance(from, n, livePoint({ ...useLive.getState(), cursor }) ?? cursor);
      const at = { x: from.x + n.x * distance, y: from.y + n.y * distance };
      const moved = drag.moved || Math.abs(distance) > 1e-6;
      setDrag({ ...drag, at, moved });
      if (moved && (!offsetShown || Math.abs(distance - offsetShown.distance) > 1e-9)) previewOffset(drag.members, distance);
      return true;
    }
    const members = movers(drag.group, pickOf(drag.group), e.altKey);
    // An end grip: Ctrl/⌘ keeps it on its element's own line, polar offers that line as a guide (decision 865).
    const own = ownGuideOf(members[0], { ctrl: ctrl.current, alt: e.altKey, copy: !!drag.copy || !!drag.insert });
    liveLock(own?.locked ? { from: own.from, dir: own.dir } : null, !!own);
    const aimed = o.aim(e.clientX, e.clientY, drag.group.at, zOf(drag.group), own ?? undefined);
    liveCursor(aimed);
    // A grip that keeps its own angles (a tag's knee): on them unless Shift frees it.
    const typed = livePoint({ ...useLive.getState(), cursor: aimed }) ?? aimed;
    // Not while Ctrl/⌘-dragging a new point out of the grip (`insertAt`): that point goes where it is dropped.
    const keep = members.length === 1 && !drag.insert ? members[0].constrain : undefined;
    const kept = keep ? keep(typed, e.shiftKey) : typed;
    // A module's own snap for a grip of an element it does not own (a generated block's grid), after the owner's.
    const at = members.length === 1 ? gripSnapped(members[0], kept, e.shiftKey) : kept;
    const moved = drag.moved || Math.hypot(at.x - drag.group.at.x, at.y - drag.group.at.y) > 1e-6;
    setDrag({ ...drag, members, at, moved });
    // A point of a constrained sketch: what the solver would make of it, shown live.
    const sk = members.length === 1 ? members[0].sketch : undefined;
    if (moved && sk && viewHasConstraints(sk.viewId)) void previewDrag(sk.viewId, sk.shape, sk.part, at);
    return true;
  };
  again.current = () => { if (drag && !drag.offset && lastMove.current) onPointerMove({ ...lastMove.current, ctrlKey: ctrl.current, metaKey: false }); };
  const onPointerUp = (): boolean => {
    if (!drag) return false;
    // Autohold: the button is let go, the drag is not.
    if (!releaseEnds(autoholdOn())) return true;
    return finishDrag();
  };
  /** The drag ends where it is: dropped, copied or offset, whichever it was (typed values held it by `liveCommit` already). */
  const finishDrag = (): boolean => {
    if (!drag) return false;
    if (drag.offset) {
      if (drag.moved) void finishOffset(drag.offset, offsetDistance(drag.group.at, drag.offset[0].normal, drag.at));
      else { offsetAsk.current++; setOffsetShown(null); }
    } else if (drag.insert) {
      const m = drag.members[0];
      void runSteps([drag.moved ? m.insertAt?.(drag.at) : m.insert?.()]);
    } else if (drag.moved && drag.copy) {
      // Ctrl/⌘ on a move grip: the elements stay, a copy goes where the grip was let go.
      const ids = [...new Set(drag.members.map((m) => m.owner).filter((x): x is string => !!x))];
      if (ids.length) void execute('element.transform', { ids, op: 'copy', by: [drag.at.x - drag.group.at.x, drag.at.y - drag.group.at.y], ...frameArg(drag.group.at) });
    } else if (drag.moved) void dropGrips(drag.members, drag.at);
    endPreview();
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
    const title = g.members[0].title ?? (shared ? `${g.members.map((m) => nameOf(m.owner)).join(' + ')} · Tab picks one · Alt moves one` : kind === 'edgeMid' ? 'Drag the edge · Ctrl/⌘-click: add a corner' : kind === 'vertex' ? 'Drag · Ctrl/⌘-click: remove this corner' : kind === 'end' && g.members[0].along ? 'Drag · hold Ctrl/⌘ while dragging to keep its angle' : kind === 'move' ? 'Drag to move · Ctrl/⌘-drag to copy' : 'Drag');
    // A flip arrow: a small arrowhead pointing the way it flips, its stem back towards the element.
    if (kind === 'flip') {
      const { ux, uy } = q, nx = -uy, ny = ux;
      const tip = { x: q.x + ux * 6, y: q.y + uy * 6 };
      const d = `M${tip.x} ${tip.y}L${q.x - ux * 2 + nx * 5} ${q.y - uy * 2 + ny * 5}L${q.x - ux * 2 - nx * 5} ${q.y - uy * 2 - ny * 5}Z`
        + `M${q.x - ux * 2} ${q.y - uy * 2}L${q.x - ux * 10} ${q.y - uy * 10}`;
      return <g key={`${g.key}:${i}`}><circle cx={q.x} cy={q.y} r={9} className="plan-grip-hit" /><path d={d} className={cls} /><title>{title}</title></g>;
    }
    // A roof edge's slope/gable indicator: eave shows an inward slope arrow + pitch; gable shows a parallel bar + G badge.
    if (kind === 'slope') {
      const { ux, uy } = q, nx = -uy, ny = ux;
      const slope = g.members[0].slope;
      const label = g.members[0].label ?? '';
      const overrideCls = slope?.override ? ' override' : '';
      if (slope?.kind === 'gable') {
        const bar = `M${q.x - nx * 8 - ux * 7} ${q.y - ny * 8 - uy * 7}L${q.x + nx * 8 - ux * 7} ${q.y + ny * 8 - uy * 7}`;
        return (
          <g key={`${g.key}:${i}`} data-grip={g.members[0].key} data-slope-kind="gable">
            <circle cx={q.x} cy={q.y} r={12} className="plan-grip-hit" />
            <path d={bar} className={`slope-gable-bar${overrideCls}${hover === g ? ' hot' : ''}`} />
            <circle cx={q.x} cy={q.y} r={8} className={`${cls} gable${overrideCls}`} />
            <text x={q.x} y={q.y} className="slope-label gable">{label || 'G'}</text>
            <title>{title}</title>
          </g>
        );
      }
      const tip = { x: q.x + ux * 7, y: q.y + uy * 7 };
      const d = `M${tip.x} ${tip.y}L${q.x + ux * 1 + nx * 4.5} ${q.y + uy * 1 + ny * 4.5}L${q.x + ux * 1 - nx * 4.5} ${q.y + uy * 1 - ny * 4.5}Z`
        + `M${q.x + ux * 1} ${q.y + uy * 1}L${q.x - ux * 9} ${q.y - uy * 9}`;
      return (
        <g key={`${g.key}:${i}`} data-grip={g.members[0].key} data-slope-kind="eave">
          <circle cx={q.x} cy={q.y} r={12} className="plan-grip-hit" />
          <path d={d} className={`${cls} eave${overrideCls}`} />
          {label && <text x={q.x + ux * 16} y={q.y + uy * 16} className="slope-label eave">{label}</text>}
          <title>{title}</title>
        </g>
      );
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
      {drag?.offset && offsetShown && (() => {
        const q = o.toScreen(drag.at, zOf(drag.group));
        return (
          <g className="plan-preview" data-testid="offset-preview">
            {offsetShown.outlines.map((x, i) => {
              const ps = x.pts.map((p) => o.toScreen(p, o.zOf?.(x.owner)));
              return <path key={i} d={`M${ps.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join('L')}Z`} className="ref-line outline offset-preview" />;
            })}
            <text x={q.x + 14} y={q.y - 14} className={`grip-hint${offsetShown.problem ? ' offset-problem' : ''}`} data-testid="offset-hint">
              {offsetShown.problem ?? `${t('Offset')} ${offsetShown.distance > 0 ? '+' : ''}${formatLength(offsetShown.distance)}`}
            </text>
          </g>
        );
      })()}
      {drag?.moved && o.zOf && !drag.offset && (() => {
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
      {drag?.moved && !drag.offset && !drag.insert && drag.members.length === 1 && drag.members[0].guide && (() => {
        // A guide the grip shows where it is now (a dimension line stacked on a parallel one, forum topic 236).
        const g = drag.members[0].guide!(drag.at);
        if (!g) return null;
        const z = zOf(drag.group), a = o.toScreen(g.a, z), b = o.toScreen(g.b, z), q = o.toScreen(drag.at, z);
        return (
          <g className="plan-preview" data-testid="grip-guide">
            <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} className="grip-guide" />
            {g.label && <text x={q.x + 12} y={q.y + 16} className="grip-hint">{g.label}</text>}
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

  return { render, onPointerDown, onPointerMove, onPointerUp, onContextMenu, cornerItems, onPointerLeave: () => { if (!drag) setHover(null); }, dragging: drag, hasGrips: groups.length > 0 };
}
