/**
 * The status line's coordinates (decision 445): where the cursor is in a plan, read in the system chosen, the plan's
 * own (the internal origin's), the project base point's, the survey point's map coordinates (eastings and northings,
 * turned to true north) or the UCS's; a click chooses the system. Beside it the UCS chip, when one is set: World,
 * the base point, the survey point or a new one from its menu. And the snap menu: which object snaps the cursor
 * takes, the grid's snapping, polar tracking's angle (also in File → Preferences).
 */

import { useShallow } from 'zustand/react/shallow';
import { useUi } from '../app/store';
import { useLive } from '../viewport/liveInput';
import { formatLength, unitLabel, unit } from '../engine/unitsStore';
import { basePoint, mapFrame, setUcs, surveyPoint, toFrame, toMap, ucsOf, useLocation } from '../app/location';
import { POLAR_STEPS, SNAP_MODES, setPrefs, usePrefs, type CoordSystem } from '../app/prefs';
import { openContextMenu, type MenuItem } from './ContextMenu';
import { startCommand, useCad } from '../viewport/cad/session';
import type { SnapMode } from '../viewport/cad/snaps';
import { t } from '../i18n';

export const COORD_LABELS: Record<CoordSystem, string> = {
  internal: 'Internal origin', project: 'Project base point', survey: 'Survey point (map)', ucs: 'UCS',
};
export const SNAP_LABELS: Record<SnapMode, string> = {
  endpoint: 'Endpoint', midpoint: 'Midpoint', intersection: 'Intersection', center: 'Centre', quadrant: 'Quadrant', perpendicular: 'Perpendicular', nearest: 'Nearest',
};

const fixed3 = (v: number) => v.toFixed(3);

export function CursorCoords() {
  const view = useUi((s) => s.view);
  const cursor = useLive((s) => s.cursor);
  const coords = usePrefs((s) => s.coords);
  const loc = useLocation((s) => s.loc);
  if (view !== 'plan') return null;
  const ucs = ucsOf(loc);
  const u = unitLabel(unit());
  let text = '—';
  let system = coords;
  if (system === 'survey' && !surveyPoint(loc)) system = 'internal';
  if (system === 'ucs' && !ucs) system = 'internal';
  if (cursor) {
    if (system === 'survey') { const m = toMap(loc, cursor); text = `E ${fixed3(m.e)}  N ${fixed3(m.n)} m`; }
    else {
      const b = basePoint(loc);
      const p = system === 'project' ? { x: cursor.x - b.x, y: cursor.y - b.y } : system === 'ucs' && ucs ? toFrame(ucs, cursor) : cursor;
      text = `X ${formatLength(p.x)}  Y ${formatLength(p.y)}${u ? ` ${u}` : ''}`;
    }
  }
  const choose = (e: React.MouseEvent<HTMLButtonElement>) => {
    const r = e.currentTarget.getBoundingClientRect();
    const items: MenuItem[] = (['internal', 'project', 'survey', 'ucs'] as CoordSystem[]).map((c) => ({
      label: COORD_LABELS[c], checked: system === c, disabled: (c === 'survey' && !surveyPoint(loc)) || (c === 'ucs' && !ucs),
      onSelect: () => setPrefs({ coords: c }),
    }));
    openContextMenu(r.left, r.top - 4 - items.length * 28, items);
  };
  return (
    <button type="button" className="status-link status-coords" data-testid="cursor-coords" title={`${t('Coordinates at the cursor')}: ${t(COORD_LABELS[system])}`} onClick={choose}>
      <span data-no-i18n>{text}</span>
    </button>
  );
}

/** The UCS chip: shown while a UCS is set (WCS otherwise), its menu sets one. */
export function UcsChip() {
  const view = useUi((s) => s.view);
  const loc = useLocation((s) => s.loc);
  if (view !== 'plan') return null;
  const ucs = ucsOf(loc);
  const open = (e: React.MouseEvent<HTMLButtonElement>) => {
    const r = e.currentTarget.getBoundingClientRect();
    const b = basePoint(loc);
    const items: MenuItem[] = [
      { label: 'World (the plan’s own axes)', checked: !ucs, onSelect: () => void setUcs(null) },
      { label: 'At the project base point', onSelect: () => void setUcs({ origin: { x: b.x, y: b.y }, angle: 0 }) },
      { label: 'At the survey point, east and north', disabled: !surveyPoint(loc), onSelect: () => void setUcs(mapFrame(loc)) },
      { label: 'New: origin and X axis in the view (UCS)', onSelect: () => void startCommand('UCS') },
    ];
    openContextMenu(r.left, r.top - 4 - items.length * 28, items);
  };
  return (
    <button type="button" className={`cad-toggle${ucs ? ' on' : ''}`} data-testid="ucs-chip" title={ucs ? `UCS: ${formatLength(ucs.origin.x)}, ${formatLength(ucs.origin.y)} · ${Math.round(ucs.angle * 100) / 100}°` : 'World coordinates: click for a UCS'} onClick={open}>
      {ucs ? 'UCS' : 'WCS'}
    </button>
  );
}

/** The snap settings' menu (the SNAP switch's right click, or its ▾): each object snap on or off, the grid's snapping, polar's angle. */
export function openSnapMenu(x: number, y: number): void {
  const p = usePrefs.getState();
  const toggle = (m: SnapMode) => setPrefs({ snapModes: p.snapModes.includes(m) ? p.snapModes.filter((k) => k !== m) : [...p.snapModes, m] });
  const items: MenuItem[] = [
    ...SNAP_MODES.map((m) => ({ label: SNAP_LABELS[m], checked: p.snapModes.includes(m), onSelect: () => toggle(m) })),
    { label: 'Snap to the grid', checked: p.gridSnap, onSelect: () => setPrefs({ gridSnap: !p.gridSnap }) },
    ...POLAR_STEPS.map((a) => ({ label: `${t('Polar every')} ${a}°`, checked: p.polarStep === a, onSelect: () => { setPrefs({ polarStep: a }); useCad.setState({ polar: true }); } })),
    { label: 'Preferences…', onSelect: () => useUi.setState({ dialog: 'preferences' }) },
  ];
  openContextMenu(x, Math.max(8, y - items.length * 28), items);
}

/** The snaps chosen, for the SNAP switch's tooltip. */
export const useSnapSummary = () => usePrefs(useShallow((s) => s.snapModes)).map((m) => t(SNAP_LABELS[m])).join(', ');
