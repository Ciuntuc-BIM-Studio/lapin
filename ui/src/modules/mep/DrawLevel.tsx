/**
 * The height a run is drawn at, on screen: beside the cursor (the live box) the
 * plane and the elevation of the leg being drawn, above the storey and, on a
 * storey not at zero, above the project's zero, with the rise or drop at the
 * last point when the plane changed there; in the command line's bar the plane
 * and elevation to start from and whether legs are kept square. The letters
 * (F, C, W, E) are the tool's options once a point is placed (keys.ts).
 */

import { useEffect } from 'react';
import type { ToolDef, Vec2 } from '../../platform/module';
import { activeStorey, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { claimValue } from '../../app/valueOwner';
import { formatLength, parseLength, useLive } from '../../viewport/liveInput';
import { unitLabel } from '../../engine/unitsStore';
import { LengthField } from '../../shell/cells';
import { services, type Defaults } from './network';
import { setMep, useMep, type Plane } from './prefs';
import { ELEVATION_KEY, PLANE_KEYS, change, elevationLevel, planeLevel, resolve, syncLegs, useDrawLevel, type Level } from './levels';

/** A system's defaults as its runs take them: its own over its service's. */
export function systemDefaults(systemId: string | null | undefined): Defaults | null {
  const system = systemId ? useSession.getState().nodes.get(systemId) : undefined;
  if (!system) return null;
  const base = services().find((s) => s.id === system.props.service)?.defaults;
  const own = system.props.defaults as Partial<Defaults> | undefined;
  return base || own ? { ...(base ?? {}), ...(own ?? {}) } as Defaults : null;
}

/** An elevation signed as levels are written: +2400, −150. */
export const signed = (m: number): string => `${m < -1e-9 ? '−' : '+'}${formatLength(Math.abs(m))}`;

/** How a drawing tool's legs start and where their heights go: the Run tool's are its bar's (kept for the next run), a redrawn run's its own. */
export interface LevelSource {
  /** The height a new path starts at, from its first point. */
  start: (first: Vec2) => Level;
  /** The system whose planes the heights are read from (the path's first point, when there is one). */
  defaults: (first: Vec2 | null) => Defaults | null;
  /** A height chosen is the tool's for its next paths too (the Run tool). */
  sticky?: boolean;
}

/** A leg's height chosen: for the leg being drawn (a rise or drop at the last point when legs are drawn before it). */
function choose(l: Level, src: LevelSource): void {
  // A point placed since the cursor last moved (the key pressed right after the click): the leg up to it keeps the height it was drawn at.
  const s = useDrawLevel.getState();
  const live = useLive.getState();
  if (live.owner === 'tool' && live.base && s.first && s.last && Math.hypot(live.base.x - s.last.x, live.base.y - s.last.y) > 1e-9) {
    useDrawLevel.setState({ legs: [...s.legs, s.current], last: live.base });
  }
  useDrawLevel.setState({ current: l, chosen: true });
  if (src.sticky) setMep({ plane: l.plane, elevation: l.elevation });
}

let releaseTyping: (() => void) | null = null;

/** The elevation typed after E lets the keys go (Enter took it, Esc dropped it, the tool changed). */
export function stopTyping(): void {
  releaseTyping?.();
  releaseTyping = null;
  if (useDrawLevel.getState().typing != null) useDrawLevel.setState({ typing: null });
}

/** E: the value keys go to the elevation (valueOwner.ts) until Enter sets it; Esc drops it, the path going on. */
function startTyping(src: LevelSource): void {
  if (releaseTyping) return;
  useDrawLevel.setState({ typing: '' });
  const off = claimValue({
    text: () => useDrawLevel.getState().typing ?? '',
    set: (text) => useDrawLevel.setState({ typing: text }),
    enter: () => {
      const v = parseLength(useDrawLevel.getState().typing ?? '');
      stopTyping();
      if (v != null) choose(elevationLevel(v), src);
    },
  });
  // Read before the plan's own Esc (which would drop the points placed) and the key router's.
  const esc = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); stopTyping(); } };
  window.addEventListener('keydown', esc, true);
  releaseTyping = () => { off(); window.removeEventListener('keydown', esc, true); };
}

/** The tool's option letters: F floor, C ceiling, W wall, E an elevation typed (Enter sets it). */
export const levelLetters = (src: LevelSource): NonNullable<ToolDef['letters']> => ({
  keys: () => `${Object.keys(PLANE_KEYS).join('')}${ELEVATION_KEY}`,
  take: (key) => {
    if (key === ELEVATION_KEY) { startTyping(src); return; }
    const plane = PLANE_KEYS[key];
    if (!plane) return;
    stopTyping();
    choose(planeLevel(plane), src);
  },
});

/** The legs brought up to the points placed (in the gesture's constrain, and at its end). */
export function trackLegs(placed: Vec2[], src: LevelSource): void {
  const s = useDrawLevel.getState();
  const next = syncLegs(s, placed, src.start);
  const same = next.first === s.first && next.current === s.current && next.legs.length === s.legs.length && next.legs.every((l, i) => l === s.legs[i]);
  const last = placed[placed.length - 1] ?? null;
  if (!same || s.chosen || last !== s.last) useDrawLevel.setState({ ...next, chosen: false, last });
}

/** A path ended (made, or the tool left): the next one starts afresh. */
export function endLegs(): void {
  stopTyping();
  useDrawLevel.setState({ legs: [], first: null, last: null, chosen: false });
}

const PLANE_WORD: Record<Plane, string> = { floor: 'floor', ceiling: 'ceiling', wall: 'wall' };

/** Beside the cursor: the leg's plane and elevation, its elevation above the project's zero, the rise or drop at the last point; the elevation being typed. */
export function LevelCells({ level, defaults }: { level: Level; defaults: Defaults | null }) {
  const s = useDrawLevel();
  const storeyZ = Number(activeStorey()?.props.elevation ?? 0) || 0;
  const r = resolve(level, defaults);
  const step = change({ current: level, legs: s.legs }, defaults);
  return <>
    {s.typing != null
      ? <span className="live-cell on typed" data-testid="mep-level-typing"><span className="live-label">elevation</span>{s.typing || '?'}<span className="live-unit">{unitLabel()}</span></span>
      : <span className="live-cell ro" data-testid="mep-level">
        {r.plane ? <span>{PLANE_WORD[r.plane]}</span> : <span>elevation</span>}
        {r.z != null && <span data-no-i18n>{signed(r.z)}</span>}
      </span>}
    {r.z != null && Math.abs(storeyZ) > 1e-9 && s.typing == null && <span className="live-cell ro" data-testid="mep-level-abs"><span className="live-label">±0</span><span data-no-i18n>{signed(storeyZ + r.z)}</span></span>}
    {step != null && <span className="live-cell typed" data-testid="mep-level-change"><span className="live-label">{step > 0 ? '↑' : '↓'}</span><span data-no-i18n>{signed(step)}</span></span>}
  </>;
}

/** A drawing tool's cells beside the cursor: the leg being drawn. */
export const levelInfo = (src: LevelSource) => function LevelInfo() {
  const current = useDrawLevel((s) => s.current);
  const first = useDrawLevel((s) => s.first);
  return <LevelCells level={current} defaults={src.defaults(first)} />;
};

/** The Run tool's bar: the plane (or elevation) new runs start at, the keys that change it while drawing, and square legs. */
export function RunLevelOptions() {
  const { plane, elevation, square } = useMep();
  return (
    <span className="row" data-mep-run-options>
      <span className="muted small">Plane</span>
      <select value={elevation != null ? 'elevation' : plane ?? ''} data-testid="mep-run-plane"
        onChange={(e) => { const v = e.target.value; if (v === 'elevation') setMep({ plane: null, elevation: elevation ?? 1 }); else setMep({ plane: (v || null) as Plane | null, elevation: null }); }}>
        <option value="">as the system</option><option value="floor">floor</option><option value="ceiling">ceiling</option><option value="wall">wall</option><option value="elevation">elevation</option>
      </select>
      {elevation != null && <LengthField metres={elevation} onCommit={(v) => { if (v != null) setMep({ elevation: v }); }} />}
      <label className="row" title="Legs at right angles to the plan's axes, unless the material's laying rules say otherwise; off: at any angle">
        <input type="checkbox" checked={square} onChange={(e) => setMep({ square: e.target.checked })} /><span>Ortho</span>
      </label>
      <span className="muted small">While drawing: F floor, C ceiling, W wall, E a typed elevation</span>
    </span>
  );
}

/** Mounted with the module: an elevation being typed, and the legs, end with the tool. */
export function DrawLevelReset() {
  const tool = useUi((s) => s.tool);
  useEffect(() => { endLegs(); }, [tool]);
  return null;
}
