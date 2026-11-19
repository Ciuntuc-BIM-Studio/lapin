/**
 * What a section or an elevation sees beyond its cut (issue #93, decision 721):
 * its depth (the far clip: typed here, or its far line's grip dragged in plan,
 * view.setDepth, one undo step) and, from its style or its own settings, what
 * it draws up to it: Full depth (everything, as before), Cut + near (what lies
 * within the near depth as before, what lies wholly farther in a thin grey
 * background pen, ArchiCAD's marked distant area) or Cut only (only what the
 * plane cuts, for details). Sheets, PDF and DXF print it as it shows; Show in
 * 3D keeps to the depth.
 */

import type { ReactNode } from 'react';
import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, GripView, RefLine, Vec2 } from '../../platform/module';
import { LengthField } from '../../shell/cells';
import { lengthText } from '../../engine/unitsStore';
import { BEYOND_MODES, DEFAULT_NEAR_DEPTH, type BeyondMode, type ViewStyle } from './viewStyles';
import { sectionPoints } from './sections';

const r3 = (v: number) => Math.round(v * 1000) / 1000;

/** The depth a section across an element sees without one of its own: its margin (0.5 m by default). */
function acrossDefault(node: NodeDto): number | null {
  if (typeof node.props.across !== 'string') return null;
  return typeof node.props.margin === 'number' ? node.props.margin : 0.5;
}

/** A section's or an elevation's depth as it draws: its own, an across section's margin, else none (without end). */
export function viewDepth(node: NodeDto): number | null {
  return typeof node.props.depth === 'number' && node.props.depth > 0 ? node.props.depth : acrossDefault(node);
}

/** In the view's settings: what it sees beyond its cut, how near is near, and its depth. */
export function BeyondFields({ view, resolved, set, reset }: { view: NodeDto; resolved: ViewStyle; set: (patch: Record<string, unknown>) => void; reset: (field: string) => ReactNode }) {
  const mode: BeyondMode = resolved.beyond ?? 'full';
  const near = resolved.nearDepth ?? DEFAULT_NEAR_DEPTH;
  const fallback = acrossDefault(view);
  const elevation = view.props.kind === 'elevation';
  return (
    <>
      <dt title="What the view draws beyond its cut: everything up to its depth, the near part with the rest in a thin grey background pen, or only what the plane cuts (for details)">Beyond the cut</dt>
      <dd>
        <select data-testid="view-beyond" value={mode} onChange={(e) => set({ beyond: e.target.value })}>
          {BEYOND_MODES.map(([k, l, d]) => <option key={k} value={k} title={d}>{l}</option>)}
        </select>{reset('beyond')}
        {elevation && mode === 'cut' && <span className="muted small"> only once its marker is moved into the building</span>}
      </dd>
      {mode === 'near' && (
        <>
          <dt title="How far from the cut elements draw in full; an element lying wholly farther goes to the background">Near depth</dt>
          <dd><LengthField testId="view-near-depth" metres={near} min={0} onCommit={(m) => { if (m != null && m !== near) set({ nearDepth: m }); }} />{reset('nearDepth')}</dd>
        </>
      )}
      <dt title={elevation ? 'How far the elevation sees, from the model\'s front (or its marker\'s line moved into the building); empty: without end. Also its far line\'s grip in plan.' : 'How far the section sees beyond its cut (its far clip); empty: without end. Also its far line\'s grip in plan.'}>Depth</dt>
      <dd>
        <LengthField testId="view-depth" metres={typeof view.props.depth === 'number' ? view.props.depth : null} min={0.01} blank
          placeholder={fallback != null ? lengthText(fallback) : 'unlimited'}
          onCommit={(m) => void execute('view.setDepth', { id: view.id, depth: m })} />
      </dd>
    </>
  );
}

/** In Manage → View styles: a style's two columns, what its sections and elevations see beyond the cut and how near is near. */
export function BeyondStyleCells({ style, update }: { style: ViewStyle; update: (patch: Partial<ViewStyle>) => void }) {
  const near = style.nearDepth ?? DEFAULT_NEAR_DEPTH;
  return (
    <>
      <td>
        <select data-testid="style-beyond" value={style.beyond ?? 'full'} onChange={(e) => update({ beyond: e.target.value as BeyondMode })}>
          {BEYOND_MODES.map(([k, l, d]) => <option key={k} value={k} title={d}>{l}</option>)}
        </select>
      </td>
      <td><LengthField testId="style-near-depth" metres={near} min={0} readOnly={(style.beyond ?? 'full') !== 'near'} onCommit={(m) => { if (m != null && m !== near) update({ nearDepth: m }); }} /></td>
    </>
  );
}

/** A grip's step: the depth the far line is dragged to (at least a centimetre), one undo step. */
const setDepth = (id: string, depth: number) => ({ command: 'view.setDepth', args: { id, depth: r3(Math.max(0.01, depth)) } });

/**
 * A section's far clip in plan (decision 721): a grip at its far line's middle, dragged along the look to set the depth;
 * Ctrl/⌘-click: without end again. A section without a depth has its grip as far out as its line is long (at least 2 m),
 * to drag in. A curved section has none (its pieces look every way).
 */
export function sectionFarGrips(node: NodeDto): Grip[] {
  if (node.type !== 'LapinView' || node.props.kind !== 'section' || node.props.curve === 'arc') return [];
  const pts = sectionPoints(node);
  if (pts.length < 2) return [];
  const [a, b] = [pts[0], pts[1]];
  const len = Math.hypot(b.x - a.x, b.y - a.y);
  if (len < 1e-9) return [];
  // A section looks left of its line, drawn from start to end (its first piece's).
  const look = { x: -(b.y - a.y) / len, y: (b.x - a.x) / len };
  const depth = viewDepth(node);
  const shown = depth ?? Math.max(2, Math.hypot(pts[pts.length - 1].x - a.x, pts[pts.length - 1].y - a.y) / 2);
  const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
  const at = { x: mid.x + look.x * shown, y: mid.y + look.y * shown };
  const own = typeof node.props.depth === 'number';
  return [{
    key: 'far', kind: 'edgeMid', at,
    title: depth == null ? 'Drag: how far the section sees (its far clip; now without end)' : 'Drag: how far the section sees (its far clip); Ctrl/⌘-click: without end',
    step: (to: Vec2) => setDepth(node.id, (to.x - mid.x) * look.x + (to.y - mid.y) * look.y),
    remove: () => (own ? { command: 'view.setDepth', args: { id: node.id, depth: null } } : null),
  }];
}

/** A section's far line, drawn over it when selected: its line moved along the look by its depth, piece by piece. */
export function sectionFarLine(node: NodeDto): RefLine[] {
  if (node.type !== 'LapinView' || node.props.kind !== 'section' || node.props.curve === 'arc') return [];
  const depth = viewDepth(node);
  const pts = sectionPoints(node);
  if (depth == null || pts.length < 2) return [];
  const lines: RefLine[] = [];
  for (let i = 0; i + 1 < pts.length; i++) {
    const [a, b] = [pts[i], pts[i + 1]];
    const len = Math.hypot(b.x - a.x, b.y - a.y);
    if (len < 1e-9) continue;
    const look = { x: -(b.y - a.y) / len, y: (b.x - a.x) / len };
    lines.push({ points: [{ x: a.x + look.x * depth, y: a.y + look.y * depth }, { x: b.x + look.x * depth, y: b.y + look.y * depth }], closed: false, kind: 'outline' });
  }
  return lines;
}

/** What an elevation's marker in plan tells of its far clip: where it starts seeing and where its depth ends, along its look. */
type ElevationFar = { ends?: number[][]; right?: number[]; min?: number; max?: number; seeFrom?: number; far?: number };

/** An elevation's far clip in plan (decision 721): once it has a depth, a grip on its far line's middle, dragged along the look. */
export function elevationFarGrips(node: NodeDto, view?: GripView): Grip[] {
  if (node.type !== 'LapinView' || node.props.kind !== 'elevation') return [];
  const { right, min, max, seeFrom, far } = (view?.mark?.data ?? {}) as ElevationFar;
  if (!right || typeof min !== 'number' || typeof max !== 'number' || typeof seeFrom !== 'number' || typeof far !== 'number') return [];
  const look = { x: -right[1], y: right[0] };
  const t = (min + max) / 2;
  const at = { x: right[0] * t + look.x * far, y: right[1] * t + look.y * far };
  return [{
    key: 'far', kind: 'edgeMid', at,
    title: 'Drag: how far the elevation sees (its far clip); Ctrl/⌘-click: without end',
    step: (to: Vec2) => setDepth(node.id, to.x * look.x + to.y * look.y - seeFrom),
    remove: () => ({ command: 'view.setDepth', args: { id: node.id, depth: null } }),
  }];
}
