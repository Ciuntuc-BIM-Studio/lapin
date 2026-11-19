/**
 * Angular temporary dimensions in plan (decision 738; the measuring in angleDims.ts), drawn as the linear ones are
 * (TempDimMarks: an arc at the meeting point, its value, its padlock):
 *
 * - While a wall, a beam, a line or a polyline segment is drawn (the live input's segment, a tool's or a CAD command's): the
 *   angle at its start to the previous segment, else to the wall or beam it starts on, else from the X axis. Read-only here;
 *   the box at the cursor types it (Tab to its ∡ field) and the direction follows.
 * - A straight wall or beam selected: the smaller angle at each end where it meets another wall or beam that is not parallel.
 *   A click on the value types it (degrees, sums allowed): Enter turns the selected element about the meeting point until the
 *   angle reads it (element.transform's rotate, one undo step, its joins worked out again); Escape leaves it. The padlock makes a
 *   permanent angular dimension of the plan held on both elements (drafting.addDimension, `holds`): it follows them, and its value
 *   typed turns the selected one again. It does not lock the angle: either element can still be turned.
 */

import { useEffect } from 'react';
import type { PlanOverlayProps, Vec2 } from '../platform/module';
import { useUi } from '../app/store';
import { execute, useSession } from '../engine/session';
import { hasModule } from '../platform/brand';
import { t } from '../i18n';
import { Marks, ROW_PX, type Shown } from './TempDimMarks';
import { useDimLines } from './LineTempDims';
import { useGripDrag } from './gripDrag';
import { formatAngle, liveMeasure, liveSetRef, parseAngle, useLive } from './liveInput';
import { elementAt, meetingAngles, rotationFor, segmentArc, toRad, wayAlong, X_REF, type Meeting } from './angleDims';

const fmt = (v: number) => `${formatAngle(v)}°`;
/** The arc's row, and how far out from the vertex (px) its rows start: clear of the walls' corner, room for the value. */
const ROW = 1;
const OUT = 48;

/** A screen point `out` px from `c` at the angle `a` (radians, in the model's plan). */
const polar = (toScreen: PlanOverlayProps['toScreen'], scale: number, c: Vec2, a: number, out: number) =>
  toScreen({ x: c.x + (Math.cos(a) * (out + OUT)) / scale, y: c.y + (Math.sin(a) * (out + OUT)) / scale });

/** Both: the drawn segment's angle and the selected element's. */
export function AngleDims(props: PlanOverlayProps) {
  return <><DrawnAngle {...props} /><MeetingAngles {...props} /></>;
}

/** The angle of the segment being drawn, at its start. */
function DrawnAngle({ toScreen, scale, view }: PlanOverlayProps) {
  const live = useLive();
  const shown = live.angles && (live.owner === 'tool' || live.owner === 'cad') && !!live.base && !live.delta && !live.measure && !live.rect && live.mode === 'polar';
  // The wall or beam the start lies on, when there is no previous segment: the reference the angle is measured from.
  const seek = shown && (!live.ref || live.ref.what === 'element');
  const lines = useDimLines(seek ? view.storeyId : null);
  const base = live.base;
  useEffect(() => {
    if (!seek || !lines || !base) return;
    liveSetRef(elementAt(base, lines));
  }, [seek, lines, base?.x, base?.y]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!shown || !base || !(scale > 0)) return null;
  const m = liveMeasure(live);
  if (!m || m.len * scale < 12) return null;
  const arc = segmentArc(live.ref ?? X_REF, base, m.end);
  if (!arc || arc.value < 0.05) return null;
  const item: Shown = {
    key: 'angle:drawn', row: ROW, a: arc.from, b: arc.to, steps: 24, value: arc.value, format: fmt, parse: parseAngle,
    pt: (a, out) => polar(toScreen, scale, base, a, out),
    data: { ref: live.ref?.what ?? 'x' }, commit: async () => null,
  };
  return (
    <g className="temp-dims angle-dims" data-testid="angle-dims-drawn" data-ref={live.ref?.what ?? 'x'}>
      <Marks items={[item]} live={false} />
    </g>
  );
}

/** The angles a straight wall or beam selected makes where it meets others. */
function MeetingAngles({ toScreen, scale, view }: PlanOverlayProps) {
  const tool = useUi((s) => s.tool);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const drag = useGripDrag((s) => s.now);
  const one = !tool && selected.length === 1 ? nodes.get(selected[0]) : undefined;
  const el = (one?.type === 'IfcWall' || one?.type === 'IfcBeam') && one.editable !== false && (!view.storeyId || !one.storeyId || one.storeyId === view.storeyId)
    && !(drag && drag.owners.includes(one.id)) ? one : undefined;
  const lines = useDimLines(el ? view.storeyId : null);
  if (!el || !lines || !(scale > 0)) return null;
  const host = lines.find((l) => l.id === el.id && !l.part);
  if (!host) return null;
  const meetings = meetingAngles(host, lines);
  if (!meetings.length) return null;
  const viewId = view.id ?? `plan:${view.storeyId}`;
  const items: Shown[] = meetings.map((m) => {
    const a0 = Math.atan2(m.otherRay.y, m.otherRay.x);
    return {
      key: m.key, row: ROW, a: a0, b: a0 + m.side * toRad(m.deg), steps: 24, value: m.deg, format: fmt, parse: parseAngle,
      pt: (a, out) => polar(toScreen, scale, m.at, a, out),
      data: { ref: 'angle', target: m.other.id, end: m.end },
      valueTitle: `${t('Click to type the angle: the element turns about the meeting point to match')} · ${m.other.name}`,
      commit: (v) => turn(el.id, m, v),
      permanent: hasModule('drafting') ? () => void permanentAngle(viewId, host.id, host.a, host.b, m, (ROW_PX[ROW] + OUT) / scale) : undefined,
    };
  });
  return (
    <g className="temp-dims angle-dims" data-testid="angle-dims">
      <Marks items={items} live />
    </g>
  );
}

/** The host turned about the meeting point for the angle to read `value` (one undo step). */
async function turn(id: string, m: Meeting, value: number): Promise<unknown> {
  if (!(value > 0 && value < 180)) { useSession.setState({ error: t('Type an angle above 0° and under 180°.') }); return null; }
  const angle = rotationFor(m, value);
  if (angle == null) return null;
  return execute('element.transform', { ids: [id], op: 'rotate', center: [m.at.x, m.at.y], angle });
}

/** The angle kept as a dimension of the plan, held on the other element (first) and the host (second, the one its value turns). */
function permanentAngle(viewId: string, hostId: string, ha: Vec2, hb: Vec2, m: Meeting, r: number) {
  const c = m.at;
  const on = (u: Vec2) => [c.x + u.x * r, c.y + u.y * r];
  const bis = { x: m.otherRay.x + m.hostRay.x, y: m.otherRay.y + m.hostRay.y };
  const bl = Math.hypot(bis.x, bis.y) || 1;
  return execute('drafting.addDimension', {
    viewId, dimKind: 'angular', c: [c.x, c.y], a: on(m.otherRay), b: on(m.hostRay), at: on({ x: bis.x / bl, y: bis.y / bl }),
    holds: [
      { element: m.other.id, way: wayAlong(m.otherRay, m.other.a, m.other.b) },
      { element: hostId, way: wayAlong(m.hostRay, ha, hb) },
    ],
  });
}
