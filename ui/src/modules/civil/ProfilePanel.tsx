/**
 * The profile editor (site grading T3, docs/plan-site-grading.md): a panel docked under the plan, for any line (a feature line, a
 * section line over the terrain, a hardscape's edge, a path, wall or ditch). The long profile is a graph of the line's heights
 * along its stations, the existing ground dashed, the design ground thin and the line's own profile heavy, its points dragged or
 * typed; the cross sections show the ground across the line at chosen stations, their points dragged the same way. The pure
 * parts (the frame, hit tests, drag previews, the commands) are in profile.ts; the engine does the work.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { ChartSpline, FileOutput, Lock, LockOpen, Plus, Trash2, X } from 'lucide-react';
import { execute, query, useSession } from '../../engine/session';
import { notify, useUi } from '../../app/store';
import { t } from '../../i18n';
import { formatLength, levelUnit, parseLength, parseLevel } from '../../engine/unitsStore';
import { paperForNew } from '../../shell/browserFind';
import { CommitInput } from './FeatureLineSection';
import { PartBoundary } from '../../shell/PartBoundary';
import { closeProfile, useProfile } from './profileStore';
import {
  capabilities, dragPoint, gradeOfSegment, gradeText, heightAtPx, heightRange, hitPoint, hitSegment, makeFrame, niceStep, planCrossMove, planCrossPoint, planCurve,
  planDelete, planGrade, planInsert, planLock, planMove, polylineOf, segmentsOf, seriesPath, readProfile, stationAtPx, stationName, sx, sy, ticks, valueAt, withGround,
  type CrossData, type Frame, type Planned, type ProfileData, type ProfilePoint,
} from './profile';

const EXISTING = '#7d7d7d', DESIGN = '#3f8a4a', LINE = '#c8372d';
const EXAGGERATIONS = [1, 2, 5, 10];

/** The size of an element, followed as it is resized. */
function useBox(): [React.RefObject<HTMLDivElement>, { w: number; h: number }] {
  const ref = useRef<HTMLDivElement>(null);
  const [box, setBox] = useState({ w: 600, h: 220 });
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const read = () => setBox({ w: Math.max(200, Math.floor(el.clientWidth)), h: Math.max(120, Math.floor(el.clientHeight)) });
    read();
    const ro = new ResizeObserver(read);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  return [ref, box];
}

/** An edit sent to the engine, its refusal shown in the panel. */
async function run(p: Planned | { command: string; args: Record<string, unknown> } | null): Promise<boolean> {
  if (!p) return false;
  const call = 'call' in p ? p.call : 'refused' in p ? null : p;
  if (!call) { useProfile.setState({ message: t((p as { refused: string }).refused) }); return false; }
  await execute(call.command, call.args);
  const err = useSession.getState().error;
  useProfile.setState({ message: err });
  return !err;
}

const round = (v: number, k: number): number => Math.round(v * k) / k;
const level = (z: number): string => formatLength(z, levelUnit());

// ---------------------------------------------------------------------------------------------------------------- long profile

interface Dragging { index: number; x0: number; y0: number; moved: boolean; z: number; station: number }

function LongGraph({ d, w, h }: { d: ProfileData; w: number; h: number }) {
  const ex = useProfile((s) => s.exaggeration);
  const hover = useProfile((s) => s.hover);
  const station = useProfile((s) => s.station);
  const point = useProfile((s) => s.point);
  const segment = useProfile((s) => s.segment);
  const [drag, setDrag] = useState<Dragging | null>(null);
  const svg = useRef<SVGSVGElement>(null);
  const end = d.start + d.length;
  const [z0, z1] = useMemo(() => heightRange([d.existing, d.design, d.line], d.points.map((p) => p.z)), [d]);
  const fr: Frame = useMemo(() => makeFrame({ s0: d.start, s1: end, z0, z1, width: w, height: h, exaggeration: ex }), [d.start, end, z0, z1, w, h, ex]);
  const caps = capabilities(d);
  const shown = drag?.moved ? dragPoint(d.points, drag.index, { z: drag.z, station: drag.station }, end, d.closed) ?? d.points : d.points;
  const line = drag?.moved ? polylineOf(shown) : d.line;
  const segs = useMemo(() => segmentsOf(shown, d.closed, end), [shown, d.closed, end]);
  const at = (e: React.PointerEvent | React.MouseEvent): { x: number; y: number } => { const r = svg.current!.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
  const clampS = (s: number) => Math.min(end, Math.max(d.start, s));

  const sStep = niceStep(end - d.start, Math.max(2, (w - 70) / 90));
  const zLo = heightAtPx(fr, h - 30), zHi = heightAtPx(fr, 14);
  const zStep = niceStep(zHi - zLo, Math.max(2, (h - 44) / 34));

  const onDown = (e: React.PointerEvent) => {
    const { x, y } = at(e);
    const i = hitPoint(fr, d.points, x, y);
    if (i >= 0) {
      svg.current!.setPointerCapture(e.pointerId);
      setDrag({ index: i, x0: x, y0: y, moved: false, z: d.points[i].z, station: d.points[i].station });
      useProfile.setState({ point: i, segment: null, station: d.points[i].station, message: null });
      return;
    }
    const k = hitSegment(fr, segmentsOf(d.points, d.closed, end), x, y);
    if (k >= 0) { useProfile.setState({ segment: k, point: null, station: clampS(stationAtPx(fr, x)), message: null }); return; }
    useProfile.setState({ station: clampS(stationAtPx(fr, x)), point: null, segment: null, message: null });
  };
  const onMove = (e: React.PointerEvent) => {
    const { x, y } = at(e);
    if (drag) {
      const moved = drag.moved || Math.hypot(x - drag.x0, y - drag.y0) > 3;
      setDrag({ ...drag, moved, z: heightAtPx(fr, y), station: stationAtPx(fr, x) });
      return;
    }
    const s = stationAtPx(fr, x);
    useProfile.setState({ hover: s < d.start - 1e-9 || s > end + 1e-9 ? null : s });
  };
  const onUp = () => {
    if (!drag) return;
    const dg = drag;
    setDrag(null);
    if (!dg.moved) return;
    const p = d.points[dg.index];
    const slid = p.movable && Math.abs(dg.station - p.station) > 0.02;
    void run(planMove(d, dg.index, { z: round(dg.z, 1000), ...(slid ? { station: round(dg.station, 100) } : {}) }));
  };
  const onDouble = (e: React.MouseEvent) => {
    const { x, y } = at(e);
    if (hitPoint(fr, d.points, x, y) >= 0 || !caps.insert) return;
    void run(planInsert(d, round(clampS(stationAtPx(fr, x)), 100)));
  };

  const cursor = (s: number | null, cls: string) => s != null && s >= d.start - 1e-9 && s <= end + 1e-9
    ? <line className={cls} x1={sx(fr, s)} x2={sx(fr, s)} y1={14} y2={h - 30} /> : null;
  let lastLabel = -1e9;
  return (
    <svg ref={svg} className="profile-svg" width={w} height={h} viewBox={`0 0 ${w} ${h}`} role="img" aria-label={t('Long profile')} data-testid="profile-graph"
      onPointerDown={onDown} onPointerMove={onMove} onPointerUp={onUp} onPointerLeave={() => { if (!drag) useProfile.setState({ hover: null }); }} onDoubleClick={onDouble}>
      <rect x={0} y={0} width={w} height={h} fill="transparent" />
      {ticks(zLo, zHi, zStep).map((z) => (
        <g key={`z${z}`}><line className="profile-grid" x1={fr.ox} x2={Math.min(w - 8, sx(fr, end) + 6)} y1={sy(fr, z)} y2={sy(fr, z)} /><text className="profile-label" x={fr.ox - 6} y={sy(fr, z) + 3} textAnchor="end">{level(z)}</text></g>
      ))}
      {ticks(d.start, end, sStep).map((s) => (
        <g key={`s${s}`}><line className="profile-grid" x1={sx(fr, s)} x2={sx(fr, s)} y1={14} y2={h - 30} /><text className="profile-label" x={sx(fr, s)} y={h - 14} textAnchor="middle">{stationName(s)}</text></g>
      ))}
      <path d={seriesPath(fr, d.existing)} fill="none" stroke={EXISTING} strokeWidth={1.5} strokeDasharray="6 3"><title>{t('Existing ground')}</title></path>
      <path d={seriesPath(fr, d.design)} fill="none" stroke={DESIGN} strokeWidth={1} />
      <path d={seriesPath(fr, line)} fill="none" stroke={LINE} strokeWidth={2.5} strokeLinejoin="round"><title>{t(d.kind === 'featureLine' && d.section ? 'Section line' : 'Profile')}</title></path>
      {segs.map((g, i) => {
        const gr = gradeOfSegment(g);
        const wpx = (g.s1 - g.s0) * fr.k;
        const both = shown[g.from]?.designed && shown[g.to]?.designed;
        if (gr == null || wpx < 54 || !both) return null;
        const x = sx(fr, (g.s0 + g.s1) / 2), y = sy(fr, (g.z0 + g.z1) / 2);
        return <text key={`g${i}`} className={`profile-grade${segment === i ? ' on' : ''}`} x={x} y={y + (gr >= 0 ? 16 : -8)} textAnchor="middle" data-testid="profile-grade-label">{gradeText(gr)}</text>;
      })}
      {segment != null && segs[segment] && <line className="profile-seg-on" x1={sx(fr, segs[segment].s0)} y1={sy(fr, segs[segment].z0)} x2={sx(fr, segs[segment].s1)} y2={sy(fr, segs[segment].z1)} />}
      {cursor(station, 'profile-cursor on')}
      {cursor(hover, 'profile-cursor')}
      {shown.map((p, i) => {
        const x = sx(fr, p.station), y = sy(fr, p.z);
        const show = x - lastLabel > 44;
        if (show) lastLabel = x;
        return (
          <g key={i} data-testid="profile-point" data-index={i} style={{ cursor: p.locked ? 'not-allowed' : 'grab' }}>
            {point === i && <circle cx={x} cy={y} r={9} fill="none" stroke="var(--accent)" strokeWidth={1.5} />}
            <circle cx={x} cy={y} r={5} fill={p.designed ? LINE : 'var(--panel)'} stroke={LINE} strokeWidth={2}><title>{`${stationName(p.station)} · ${level(p.z)}`}</title></circle>
            {p.curve > 0 && <path d={`M${x - 10} ${y + 9}q10 -7 20 0`} fill="none" stroke={LINE} strokeWidth={1.2} />}
            {p.locked && <g transform={`translate(${x + 7} ${y - 15})`}><rect x={0} y={4} width={8} height={6} rx={1} fill={LINE} /><path d="M1.5 4V2.5a2.5 2.5 0 0 1 5 0V4" fill="none" stroke={LINE} strokeWidth={1.2} /></g>}
            {show && <text className="profile-label strong" x={x} y={y - 11} textAnchor="middle">{level(p.z)}</text>}
          </g>
        );
      })}
    </svg>
  );
}

/** What the cursor is over, in words. */
function Readout({ d }: { d: ProfileData }) {
  const hover = useProfile((s) => s.hover);
  const station = useProfile((s) => s.station);
  const s = hover ?? station;
  if (s == null) return <span className="muted small">{t('Hover the graph or the line in the plan; click to pick a station.')}</span>;
  const ex = valueAt(d.existing, s), de = valueAt(d.design, s), ln = valueAt(d.line, s);
  return (
    <span className="small profile-readout" data-testid="profile-readout">
      <b className="num">{stationName(s)}</b>
      {ex != null && <> · <span className="muted">Existing</span> <span className="num">{level(ex)}</span></>}
      {de != null && <> · <span className="muted">Design</span> <span className="num">{level(de)}</span></>}
      {ln != null && <> · <span className="muted">Line</span> <span className="num">{level(ln)}</span></>}
    </span>
  );
}

// ---------------------------------------------------------------------------------------------------------------- the point, the segment

function Inspector({ d }: { d: ProfileData }) {
  const point = useProfile((s) => s.point);
  const segment = useProfile((s) => s.segment);
  const keep = useProfile((s) => s.keep);
  const caps = capabilities(d);
  const p: ProfilePoint | undefined = point != null ? d.points[point] : undefined;
  const segs = segmentsOf(d.points, d.closed, d.start + d.length);
  const seg = segment != null ? segs[segment] : undefined;
  const unreadable = (what: string) => useProfile.setState({ message: t(what) });
  return (
    <div className="profile-inspector" data-testid="profile-inspector">
      {d.kind === 'corridor' && d.mode !== 'design' && (
        <div className="profile-box">
          <p className="small">{t('This profile follows the ground.')}</p>
          <button type="button" className="btn compact" data-testid="profile-design" onClick={() => void run({ command: 'civil.profileFromGround', args: { id: d.id, ground: 'proposed' } })}>Design the profile</button>
        </div>
      )}
      {d.kind === 'featureLine' && d.section && <SectionReach d={d} />}
      {p && point != null && (
        <div className="profile-box" data-testid="profile-point-fields">
          <div className="row between"><b className="small">{t('Point')} {point + 1}</b><span className="num small muted">{stationName(p.station)}</span></div>
          <label className="small">{t('Elevation')}
            <CommitInput value={level(p.z)} label={t('Elevation of the point')} testId="profile-z" disabled={!caps.edit || p.locked} width="6em"
              onCommit={(text) => { const z = parseLevel(text); if (z == null || !Number.isFinite(z)) { unreadable('That elevation does not read: type a number in the project\'s unit.'); return; } void run(planMove(d, point, { z })); }} />
          </label>
          <span className="muted small">{p.designed ? t('Designed') : t('On the ground')}</span>
          <label className="small">{t('Vertical curve')}
            <CommitInput value={p.curve > 0 ? formatLength(p.curve, 'm') : ''} placeholder="—" label={t('Length of the vertical curve')} testId="profile-curve" disabled={!caps.curve || p.locked || point === 0 || point === d.points.length - 1} width="5em"
              onCommit={(text) => { const v = text.trim() === '' ? 0 : parseLength(text, 'm'); if (v == null || !(v >= 0)) { unreadable('That length does not read: type a number in metres.'); return; } void run(planCurve(d, point, v)); }} /> <span className="muted small">m</span>
          </label>
          <div className="row small">
            <button type="button" className="btn compact" data-testid="profile-lock" disabled={!caps.lock} title={caps.lock ? t('A locked point keeps its height and station when the profile is edited') : t('Only a feature line\'s points can be locked.')}
              onClick={() => void run(planLock(d, point, !p.locked))}>{p.locked ? <LockOpen size={12} /> : <Lock size={12} />} {p.locked ? 'Unlock' : 'Lock'}</button>
            <button type="button" className="btn compact" data-testid="profile-delete" disabled={!caps.remove || p.locked} title={caps.remove ? t('Take the point out') : t('A hardscape keeps its corners: take one out in plan.')}
              onClick={() => { void run(planDelete(d, point)); useProfile.setState({ point: null }); }}><Trash2 size={12} /> Delete</button>
            {d.kind === 'featureLine' && !d.drape && d.contour !== true && p.designed && (
              <button type="button" className="btn compact" title={t('Take this point\'s height from the ground again')} onClick={() => void run({ command: 'civil.setFeatureLinePoints', args: { id: d.id, indices: [point], ground: true } })}>To ground</button>
            )}
          </div>
        </div>
      )}
      {seg && segment != null && (
        <div className="profile-box" data-testid="profile-segment-fields">
          <div className="row between"><b className="small">{t('Segment grade')}</b><span className="num small muted">{stationName(seg.s0)} → {stationName(seg.s1)}</span></div>
          <CommitInput value={gradeOfSegment(seg) != null ? gradeText(gradeOfSegment(seg)!) : ''} label={t('Grade of the segment')} testId="profile-grade" disabled={!caps.edit} width="6em"
            onCommit={(text) => void run(planGrade(d, seg, text, keep))} />
          <label className="small">{t('A typed grade keeps')}
            <select value={keep} aria-label={t('Which end a typed grade keeps')} onChange={(e) => useProfile.setState({ keep: e.target.value as 'start' | 'end' })} data-testid="profile-keep">
              <option value="start">its start</option>
              <option value="end">its end</option>
            </select>
          </label>
          <span className="muted small">{t('Type 2 %, 1:50 or -1,5 %.')}</span>
        </div>
      )}
      {!p && !seg && !(d.kind === 'corridor' && d.mode !== 'design') && !(d.kind === 'featureLine' && d.section) && (
        <p className="muted small">{t('Drag a point, or click it to type its height. Click a segment to type its grade. Double-click the line to add a point.')}</p>
      )}
    </div>
  );
}

/** A section line's influence width: how far from it the ground follows its edits. */
function SectionReach({ d }: { d: ProfileData }) {
  const used = d.reachUsed ?? d.reach ?? null;
  return (
    <div className="profile-box" data-testid="profile-reach">
      <b className="small">{t('Influence width')}</b>
      <div className="row small">
        <CommitInput value={d.reach != null ? formatLength(d.reach, 'm') : ''} placeholder={used != null ? formatLength(used, 'm') : '—'} label={t('Influence width')} testId="profile-reach-input" width="5em"
          onCommit={(text) => {
            if (text.trim() === '') { void run({ command: 'civil.updateFeatureLine', args: { id: d.id, reach: null } }); return; }
            const v = parseLength(text, 'm');
            if (v == null || !(v > 0)) { useProfile.setState({ message: t('That length does not read: type a number in metres.') }); return; }
            void run({ command: 'civil.updateFeatureLine', args: { id: d.id, reach: v } });
          }} /> <span className="muted">m</span>
      </div>
      <span className="muted small">{d.change != null && d.change > 1e-6 ? <><span>Largest change</span> {formatLength(d.change, 'm')} m · <span>the width must be at least twice that.</span></> : <span>The ground follows an edited point within this width each side; at least twice the largest change.</span>}</span>
    </div>
  );
}

// ---------------------------------------------------------------------------------------------------------------- cross sections

interface CrossDrag { index: number | null; x0: number; y0: number; moved: boolean; offset: number; z: number }

function CrossGraph({ d, c, w, h }: { d: ProfileData; c: CrossData; w: number; h: number }) {
  const ex = useProfile((s) => s.exaggeration);
  const hover = useProfile((s) => s.station);
  const [drag, setDrag] = useState<CrossDrag | null>(null);
  const svg = useRef<SVGSVGElement>(null);
  const [z0, z1] = useMemo(() => heightRange([c.existing, c.design, c.line], [c.z, ...(c.points ?? []).map((p) => p.z)]), [c]);
  const fr = useMemo(() => makeFrame({ s0: -c.width, s1: c.width, z0, z1, width: w, height: h, exaggeration: ex, padL: 44, padR: 10, padT: 22, padB: 24 }), [c.width, z0, z1, w, h, ex]);
  const at = (e: React.PointerEvent): { x: number; y: number } => { const r = svg.current!.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
  const points = c.points ?? [];
  const groundNear = (off: number, py: number): boolean => {
    const g = valueAt(c.design, off) ?? valueAt(c.existing, off);
    return g != null && Math.abs(sy(fr, g) - py) <= 12;
  };
  const onDown = (e: React.PointerEvent) => {
    const { x, y } = at(e);
    const i = hitPoint(fr, points.map((p) => ({ station: p.offset, z: p.z })), x, y);
    if (i >= 0) {
      if (points[i].locked) { useProfile.setState({ message: t('That point is locked.') }); return; }
      svg.current!.setPointerCapture(e.pointerId);
      setDrag({ index: i, x0: x, y0: y, moved: false, offset: points[i].offset, z: points[i].z });
      return;
    }
    const off = stationAtPx(fr, x);
    if (off >= -c.width && off <= c.width && groundNear(off, y)) {
      svg.current!.setPointerCapture(e.pointerId);
      setDrag({ index: null, x0: x, y0: y, moved: false, offset: off, z: heightAtPx(fr, y) });
    }
  };
  const onMove = (e: React.PointerEvent) => {
    if (!drag) return;
    const { x, y } = at(e);
    const moved = drag.moved || Math.hypot(x - drag.x0, y - drag.y0) > 3;
    const p = drag.index != null ? points[drag.index] : null;
    const off = drag.index == null || p?.movable ? Math.min(c.width - 0.01, Math.max(-c.width + 0.01, stationAtPx(fr, x))) : drag.offset;
    setDrag({ ...drag, moved, offset: moved ? off : drag.offset, z: heightAtPx(fr, y) });
  };
  const onUp = () => {
    if (!drag) return;
    const dg = drag;
    setDrag(null);
    useProfile.setState({ message: null });
    const offset = round(dg.offset, 100);
    if (dg.index != null) {
      if (!dg.moved) return;
      const p = points[dg.index];
      if (p.movable && Math.abs(offset - p.offset) > 0.02) void run(planCrossMove(c, dg.index, offset, round(dg.z, 1000)));
      else void run(planCrossPoint(d.id, c, p.offset, round(dg.z, 1000)));
      return;
    }
    const ground = valueAt(c.design, offset) ?? valueAt(c.existing, offset);
    void run(planCrossPoint(d.id, c, offset, round(dg.moved ? dg.z : ground ?? dg.z, 1000)));
  };
  const sStep = niceStep(2 * c.width, Math.max(2, w / 70));
  const zStep = niceStep(heightAtPx(fr, 22) - heightAtPx(fr, h - 24), Math.max(2, h / 36));
  const line = c.line ?? [];
  return (
    <figure className={`profile-cross${hover != null && Math.abs(hover - c.station) < 0.5 ? ' on' : ''}`} data-testid="profile-cross">
      <figcaption className="small"><b className="num">{c.label}</b> <span className="muted num">{level(c.z)}</span>
        <button type="button" className="icon mini" title={t('Take this section out')} onClick={() => useProfile.setState((s) => ({ stations: s.stations.filter((x) => Math.abs(x - c.station) > 0.005), interval: s.stations.some((x) => Math.abs(x - c.station) < 0.005) ? s.interval : 0 }))}><X size={11} /></button>
      </figcaption>
      <svg ref={svg} width={w} height={h} viewBox={`0 0 ${w} ${h}`} role="img" aria-label={`${t('Cross section')} ${c.label}`} onPointerDown={onDown} onPointerMove={onMove} onPointerUp={onUp}>
        {ticks(heightAtPx(fr, h - 24), heightAtPx(fr, 22), zStep).map((z) => <g key={`z${z}`}><line className="profile-grid" x1={fr.ox} x2={w - 8} y1={sy(fr, z)} y2={sy(fr, z)} /><text className="profile-label" x={fr.ox - 5} y={sy(fr, z) + 3} textAnchor="end">{level(z)}</text></g>)}
        {ticks(-c.width, c.width, sStep).map((o) => <g key={`o${o}`}><line className="profile-grid" x1={sx(fr, o)} x2={sx(fr, o)} y1={22} y2={h - 24} /><text className="profile-label" x={sx(fr, o)} y={h - 9} textAnchor="middle">{o === 0 ? '0' : o > 0 ? `+${o}` : o}</text></g>)}
        <line className="profile-axis" x1={sx(fr, 0)} x2={sx(fr, 0)} y1={22} y2={h - 24} />
        <path d={seriesPath(fr, c.existing)} fill="none" stroke={EXISTING} strokeWidth={1.4} strokeDasharray="5 3" />
        <path d={seriesPath(fr, c.design)} fill="none" stroke={DESIGN} strokeWidth={1} />
        {line.length > 1 && <path d={seriesPath(fr, line)} fill="none" stroke={LINE} strokeWidth={2} />}
        <path d={`M${sx(fr, 0)} ${sy(fr, c.z) - 6}l5 6l-5 6l-5 -6z`} fill={LINE} />
        {points.map((p, i) => {
          const dragged = drag?.index === i && drag.moved;
          const x = sx(fr, dragged ? drag.offset : p.offset), y = sy(fr, dragged ? drag!.z : p.z);
          return <circle key={i} cx={x} cy={y} r={4.5} fill={p.designed ? LINE : 'var(--panel)'} stroke={LINE} strokeWidth={1.8} data-testid="profile-cross-point" style={{ cursor: p.locked ? 'not-allowed' : 'grab' }}><title>{`${p.offset.toFixed(2)} · ${level(p.z)}`}</title></circle>;
        })}
        {drag?.index == null && drag?.moved && <circle cx={sx(fr, drag.offset)} cy={sy(fr, drag.z)} r={4.5} fill="none" stroke="var(--accent)" strokeWidth={1.8} />}
        <text className="profile-label" x={fr.ox} y={12}>{t('left')}</text>
        <text className="profile-label" x={w - 10} y={12} textAnchor="end">{t('right')}</text>
      </svg>
    </figure>
  );
}

function CrossTab({ d, w }: { d: ProfileData; w: number }) {
  const cross = useProfile((s) => s.cross);
  const stations = useProfile((s) => s.stations);
  const interval = useProfile((s) => s.interval);
  const width = useProfile((s) => s.width);
  const picked = useProfile((s) => s.station);
  const hovered = useProfile((s) => s.hover);
  const station = picked ?? hovered;
  const cols = Math.max(1, Math.floor(w / 300));
  const cw = Math.floor(w / cols) - 16;
  return (
    <div className="profile-cross-tab">
      <div className="profile-cross-bar small">
        <label>{t('Every')} <CommitInput value={interval > 0 ? formatLength(interval, 'm') : ''} placeholder="—" label={t('Interval between sections')} testId="profile-interval" width="4.5em"
          onCommit={(text) => { const v = text.trim() === '' ? 0 : parseLength(text, 'm'); if (v == null || v < 0) { useProfile.setState({ message: t('That length does not read: type a number in metres.') }); return; } useProfile.setState({ interval: v }); }} /> m</label>
        <label>{t('Width each way')} <CommitInput value={formatLength(width, 'm')} label={t('Width each way')} testId="profile-width" width="4.5em"
          onCommit={(text) => { const v = parseLength(text, 'm'); if (v == null || !(v >= 0.5)) { useProfile.setState({ message: t('That length does not read: type a number in metres.') }); return; } useProfile.setState({ width: v }); }} /> m</label>
        <button type="button" className="btn compact" data-testid="profile-section-here" disabled={station == null} title={t('A cross section at the picked station')}
          onClick={() => { if (station == null) return; const s = round(station, 100); useProfile.setState((st) => ({ stations: st.stations.some((x) => Math.abs(x - s) < 0.005) ? st.stations : [...st.stations, s].sort((a, b) => a - b) })); }}><Plus size={12} /> Section here</button>
        {(stations.length > 0 || interval > 0) && <button type="button" className="btn compact" onClick={() => useProfile.setState({ stations: [], interval: 0, cross: [] })}>Clear</button>}
      </div>
      {cross.length === 0
        ? <p className="muted small">{t('Pick a station in the graph or the plan and press “Section here”, or type an interval: each cross section shows the ground across the line.')}</p>
        : <div className="profile-cross-grid" style={{ gridTemplateColumns: `repeat(${cols}, 1fr)` }}>{cross.map((c) => <CrossGraph key={c.station} d={d} c={c} w={Math.max(180, cw)} h={170} />)}</div>}
    </div>
  );
}

// ---------------------------------------------------------------------------------------------------------------- sheets

function PlaceOnSheet({ d, onClose }: { d: ProfileData; onClose: () => void }) {
  const nodes = useSession((s) => s.nodes);
  const ex = useProfile((s) => s.exaggeration);
  const cross = useProfile((s) => s.cross);
  const stations = useProfile((s) => s.stations);
  const interval = useProfile((s) => s.interval);
  const width = useProfile((s) => s.width);
  const [long, setLong] = useState(true);
  const [sections, setSections] = useState(cross.length > 0);
  const [scale, setScale] = useState(500);
  const [crossScale, setCrossScale] = useState(100);
  const [sheet, setSheet] = useState('');
  const sheets = [...nodes.values()].filter((n) => n.type === 'LapinSheet');
  const go = async () => {
    const views: string[] = [];
    if (long) {
      const r = await execute('civil.addProfileView', { lineId: d.id, kind: 'long', exaggeration: ex, scale }) as { id?: string } | null;
      if (r?.id) views.push(r.id);
    }
    if (sections && cross.length > 0) {
      const r = await execute('civil.addProfileView', { lineId: d.id, kind: 'cross', exaggeration: ex, scale: crossScale, width, ...(stations.length ? { stations } : {}), ...(interval > 0 ? { interval } : {}) }) as { id?: string } | null;
      if (r?.id) views.push(r.id);
    }
    if (!views.length) { useProfile.setState({ message: useSession.getState().error }); return; }
    // Each drawing on a sheet of its own, unless a sheet was chosen: then they go on it, one under the other.
    let sheetId = sheet;
    let first = sheet;
    for (const [i, v] of views.entries()) {
      if (sheet) { await execute('sheet.addViewport', { id: sheet, viewId: v, ...(i > 0 ? { y: 150 } : {}) }); continue; }
      const nm = `${t('Profile')} · ${d.name}${views.length > 1 ? (i === 0 ? ` · ${t('Long profile')}` : ` · ${t('Cross sections')}`) : ''}`;
      const r = await execute('sheet.create', { name: nm, ...paperForNew(useSession.getState().nodes), viewId: v }) as { id?: string } | null;
      if (!r?.id) { useProfile.setState({ message: useSession.getState().error }); return; }
      if (i === 0) first = r.id;
      sheetId = r.id;
    }
    sheetId = first;
    useProfile.setState({ message: useSession.getState().error });
    onClose();
    useUi.setState({ view: 'sheet', sheetId, selection: sheetId, selected: [sheetId] });
    notify(t('The profile is on the sheet; its PDF and DXF print what it shows.'));
  };
  return (
    <div className="profile-place" data-testid="profile-place">
      <b className="small">{t('Place on a sheet')}</b>
      <label className="small"><input type="checkbox" checked={long} onChange={(e) => setLong(e.target.checked)} /> {t('Long profile')} <span className="muted">1 : </span>
        <select value={scale} onChange={(e) => setScale(Number(e.target.value))} aria-label={t('Scale of the long profile')} data-testid="profile-place-scale">{[100, 200, 250, 500, 1000, 2000].map((n) => <option key={n} value={n}>{n}</option>)}</select></label>
      <label className="small"><input type="checkbox" checked={sections} disabled={cross.length === 0} onChange={(e) => setSections(e.target.checked)} /> {t('Cross sections')} <span className="muted">1 : </span>
        <select value={crossScale} onChange={(e) => setCrossScale(Number(e.target.value))} aria-label={t('Scale of the cross sections')}>{[50, 100, 200, 250, 500].map((n) => <option key={n} value={n}>{n}</option>)}</select></label>
      <span className="muted small">{t('Heights exaggerated')} {ex}×</span>
      <label className="small">{t('On')} <select value={sheet} onChange={(e) => setSheet(e.target.value)} aria-label={t('Sheet')} data-testid="profile-place-sheet">
        <option value="">{t('A new sheet')}</option>{sheets.map((s) => <option key={s.id} value={s.id}>{String(s.props.number ?? '')} {s.name}</option>)}</select></label>
      <div className="row"><button type="button" className="btn compact" data-testid="profile-place-go" disabled={!long && !(sections && cross.length > 0)} onClick={() => void go()}>Place</button><button type="button" className="btn compact" onClick={onClose}>Cancel</button></div>
    </div>
  );
}

// ---------------------------------------------------------------------------------------------------------------- the panel

function Open({ id }: { id: string }) {
  const nodes = useSession((s) => s.nodes);
  const d = useProfile((s) => s.data);
  const tab = useProfile((s) => s.tab);
  const ex = useProfile((s) => s.exaggeration);
  const message = useProfile((s) => s.message);
  const hover = useProfile((s) => s.hover);
  const station = useProfile((s) => s.station);
  const stations = useProfile((s) => s.stations);
  const interval = useProfile((s) => s.interval);
  const width = useProfile((s) => s.width);
  const [placing, setPlacing] = useState(false);
  const [box, size] = useBox();
  const node = nodes.get(id);

  useEffect(() => { if (!node) closeProfile(); }, [node]);
  // The model changes (this line, its terrain): read again; an answer that comes after the next question is dropped.
  useEffect(() => {
    let live = true;
    void query<unknown>('civil.profile', { id }).then((r) => { if (live && useProfile.getState().lineId === id) useProfile.setState({ data: readProfile(r), ...(r ? {} : { message: t('This element has no profile.') }) }); });
    return () => { live = false; };
  }, [id, nodes]);
  useEffect(() => {
    if (stations.length === 0 && !(interval > 0)) { useProfile.setState({ cross: [] }); return; }
    let live = true;
    void query<CrossData[] | CrossData>('civil.crossProfile', { id, width, ...(stations.length ? { stations } : {}), ...(interval > 0 ? { interval } : {}) }).then((r) => {
      if (live && useProfile.getState().lineId === id) useProfile.setState({ cross: (Array.isArray(r) ? r : r ? [r] : []).map(withGround) });
    });
    return () => { live = false; };
  }, [id, nodes, stations, interval, width]);

  const caps = d ? capabilities(d) : null;
  const at = station ?? hover;
  const graphW = Math.max(240, size.w);
  return (
    <section className="profile-panel" role="region" aria-label={t('Profile')} data-testid="profile-panel">
      <header className="profile-head">
        <ChartSpline size={14} />
        <b className="profile-title">{node?.name ?? '…'}</b>
        {d && <span className="muted small num">{stationName(d.start)} → {stationName(d.start + d.length)} · {formatLength(d.length, 'm')} m</span>}
        <div className="profile-tabs" role="tablist">
          <button type="button" role="tab" aria-selected={tab === 'long'} className={tab === 'long' ? 'on' : ''} onClick={() => useProfile.setState({ tab: 'long' })}>Long profile</button>
          <button type="button" role="tab" aria-selected={tab === 'cross'} className={tab === 'cross' ? 'on' : ''} data-testid="profile-tab-cross" onClick={() => useProfile.setState({ tab: 'cross' })}>Cross sections</button>
        </div>
        <label className="small">{t('Heights')} <select value={ex} onChange={(e) => useProfile.setState({ exaggeration: Number(e.target.value) })} aria-label={t('Vertical exaggeration')} data-testid="profile-exaggeration" title={t('Vertical exaggeration')}>
          {EXAGGERATIONS.map((n) => <option key={n} value={n}>{n}×</option>)}</select></label>
        <button type="button" className="btn compact" data-testid="profile-add-point" disabled={!d || !caps?.insert || at == null} title={caps?.insert ? t('Add a point at the picked station') : t('A hardscape keeps its corners: add one in plan.')}
          onClick={() => { if (d && at != null) void run(planInsert(d, round(at, 100))); }}><Plus size={12} /> Point</button>
        <button type="button" className="btn compact" data-testid="profile-place-open" disabled={!d} title={t('The long profile and the cross sections as drawings on a sheet')} onClick={() => setPlacing((v) => !v)}><FileOutput size={12} /> Place on a sheet…</button>
        <button type="button" className="icon" data-testid="profile-close" title={t('Close')} onClick={closeProfile}><X size={14} /></button>
      </header>
      {message && <div className="profile-message" role="alert" data-testid="profile-message">{message}</div>}
      {placing && d && <PlaceOnSheet d={d} onClose={() => setPlacing(false)} />}
      <div className="profile-body">
        <div className="profile-main">
          {tab === 'long' && d && <div className="profile-bar"><Readout d={d} /><span className="profile-legend small"><i style={{ background: EXISTING }} />{' '}<span>Existing ground</span> <i style={{ background: DESIGN }} />{' '}<span>Design ground</span> <i style={{ background: LINE }} />{' '}<span>Profile</span></span></div>}
          <div ref={box} className="profile-graph">
            {!d && <p className="muted small">{t('Reading the profile…')}</p>}
            {d && tab === 'long' && <LongGraph d={d} w={graphW} h={Math.max(150, size.h)} />}
            {d && tab === 'cross' && <CrossTab d={d} w={graphW} />}
          </div>
        </div>
        {d && <Inspector d={d} />}
      </div>
    </section>
  );
}

export function ProfilePanel() {
  const id = useProfile((s) => s.lineId);
  const view = useUi((s) => s.view);
  // Under a plan or a 3D view; not over a sheet, a drawing or a table. A throw inside stops the panel alone, which Close puts away.
  return id && (view === 'plan' || view === '3d')
    ? <PartBoundary part="Profile panel" overlay onClose={closeProfile} resetKey={id}><Open id={id} /></PartBoundary>
    : null;
}
