/**
 * A view's crop region and annotation crop (forum topic 68, as Revit's View Crop and Annotation Crop): in View settings,
 * Crop view on or off, Crop region visible (its frame drawn on screen and on paper), the region's corners, and the annotation
 * crop with its offsets beyond the region (paper mm); in the view, the frame's grips (a click on it selects the view) drag
 * its corners and edges or move it. The engine cuts the drawing (`view.setCrop`, Views.Render), so sheets, PDF and DXF
 * print what the view shows. The corners are kept as the view shows (a turned plan's square to the screen).
 */

import { Crop as CropIcon } from 'lucide-react';
import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, Vec2 } from '../../platform/module';
import { LengthField, NumberField } from '../../shell/cells';
import { pt } from '../../viewport/gripKit';
import { lengthText } from '../../engine/unitsStore';
import { orientationOf, turnOf } from '../../viewport/orientation';
import { ucsOf, useLocation } from '../../app/location';

export interface ViewCrop { on: boolean; visible: boolean; min: Vec2; max: Vec2; annotation: boolean; offset: [number, number, number, number] }

/** A view's crop as kept on it (null: never cropped). */
export function cropOf(props: Record<string, unknown>): ViewCrop | null {
  const c = props.crop as Record<string, unknown> | undefined;
  const a = c ? pt(c.min) : null, b = c ? pt(c.max) : null;
  if (!c || !a || !b) return null;
  const off = Array.isArray(c.offset) && c.offset.length === 4 && c.offset.every((x) => typeof x === 'number') ? (c.offset as [number, number, number, number]) : [10, 10, 10, 10] as [number, number, number, number];
  return { on: c.on === true, visible: c.visible !== false, min: { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y) }, max: { x: Math.max(a.x, b.x), y: Math.max(a.y, b.y) }, annotation: c.annotation === true, offset: off };
}

const rot = (p: Vec2, a: number): Vec2 => (a === 0 ? p : { x: p.x * Math.cos(a) - p.y * Math.sin(a), y: p.x * Math.sin(a) + p.y * Math.cos(a) });

/** The turn (radians) a view shows its drawing with: a plan's orientation, nothing for a section or an elevation. */
function turnOfView(node: NodeDto): number {
  if ((node.props.kind ?? 'plan') !== 'plan') return 0;
  return turnOf(orientationOf(node.props.orientation, ucsOf(useLocation.getState().loc)));
}

/** The crop region's grips while the view is selected and its frame drawn: corners and edges to drag, its middle to move it. */
export function cropGrips(node: NodeDto): Grip[] {
  const c = cropOf(node.props);
  if (!c || !c.on || !c.visible) return [];
  const turn = turnOfView(node);
  // The grips are in the drawing's coordinates; the region is kept as the view shows it (turned).
  const toView = (p: Vec2) => rot(p, -turn), toLocal = (p: Vec2) => rot(p, turn);
  const [x0, x1, y0, y1] = [c.min.x, c.max.x, c.min.y, c.max.y];
  const set = (min: Vec2, max: Vec2) => ({ command: 'view.setCrop', args: { id: node.id, min: [min.x, min.y], max: [max.x, max.y] } });
  const g = (key: string, kind: Grip['kind'], at: Vec2, title: string, move: (to: Vec2) => [Vec2, Vec2]): Grip =>
    ({ key: `crop-${key}`, kind, at: toView(at), view: node.id, title, step: (to) => set(...move(toLocal(to))) });
  const mid = { x: (x0 + x1) / 2, y: (y0 + y1) / 2 };
  return [
    g('sw', 'vertex', { x: x0, y: y0 }, 'Drag: the corner', (to) => [to, { x: x1, y: y1 }]),
    g('se', 'vertex', { x: x1, y: y0 }, 'Drag: the corner', (to) => [{ x: x0, y: to.y }, { x: to.x, y: y1 }]),
    g('ne', 'vertex', { x: x1, y: y1 }, 'Drag: the corner', (to) => [{ x: x0, y: y0 }, to]),
    g('nw', 'vertex', { x: x0, y: y1 }, 'Drag: the corner', (to) => [{ x: to.x, y: y0 }, { x: x1, y: to.y }]),
    g('s', 'edgeMid', { x: mid.x, y: y0 }, 'Drag: the edge', (to) => [{ x: x0, y: to.y }, { x: x1, y: y1 }]),
    g('e', 'edgeMid', { x: x1, y: mid.y }, 'Drag: the edge', (to) => [{ x: x0, y: y0 }, { x: to.x, y: y1 }]),
    g('n', 'edgeMid', { x: mid.x, y: y1 }, 'Drag: the edge', (to) => [{ x: x0, y: y0 }, { x: x1, y: to.y }]),
    g('w', 'edgeMid', { x: x0, y: mid.y }, 'Drag: the edge', (to) => [{ x: to.x, y: y0 }, { x: x1, y: y1 }]),
    g('mid', 'move', mid, 'Drag: move the crop region', (to) => [{ x: x0 + to.x - mid.x, y: y0 + to.y - mid.y }, { x: x1 + to.x - mid.x, y: y1 + to.y - mid.y }]),
  ];
}

/** View settings' crop rows (a plan, a section, an elevation, a reference plane). */
export function ViewCropFields({ view }: { view: NodeDto }) {
  const c = cropOf(view.props);
  const set = (patch: Record<string, unknown>) => void execute('view.setCrop', { id: view.id, ...patch });
  const corner = (which: 'min' | 'max', axis: 'x' | 'y') => (m: number | null) => {
    if (m == null || !c) return;
    const p = { ...c[which], [axis]: m };
    set({ [which]: [p.x, p.y] });
  };
  const sides = ['Left', 'Bottom', 'Right', 'Top'] as const;
  return (
    <>
      <dt title="Only what lies inside the crop region is drawn, its lines cut at its edge, on screen, on sheets, in PDF and DXF (as Revit's Crop view); off, the view draws everything and keeps the region"><CropIcon size={11} /> Crop view</dt>
      <dd><input type="checkbox" data-testid="view-crop" checked={!!c?.on} onChange={(e) => set({ on: e.target.checked })} /></dd>
      {c && (
        <>
          <dt title="The crop region's frame drawn on screen and printed; hidden, neither (the view stays cropped). Click the frame to drag its corners and edges">Crop region visible</dt>
          <dd><input type="checkbox" data-testid="view-crop-visible" checked={c.visible} onChange={(e) => set({ visible: e.target.checked })} /></dd>
          <dt title="The crop region's corners, as the view shows (a turned plan's on the screen)">Crop region</dt>
          <dd className="crop-corners">
            <LengthField testId="view-crop-x0" metres={c.min.x} onCommit={corner('min', 'x')} title="Left" />
            <LengthField testId="view-crop-y0" metres={c.min.y} onCommit={corner('min', 'y')} title="Bottom" />
            <LengthField testId="view-crop-x1" metres={c.max.x} onCommit={corner('max', 'x')} title="Right" />
            <LengthField testId="view-crop-y1" metres={c.max.y} onCommit={corner('max', 'y')} title="Top" />
            <span className="muted small">{`${lengthText(c.max.x - c.min.x)} × ${lengthText(c.max.y - c.min.y)}`}</span>
          </dd>
          <dt title="Annotations (dimensions, tags, texts, symbols) wholly outside a second region round the crop region are not drawn, one across it cut at its edge (as Revit's Annotation crop)">Annotation crop</dt>
          <dd><input type="checkbox" data-testid="view-crop-annotation" checked={c.annotation} onChange={(e) => set({ annotation: e.target.checked })} /></dd>
          {c.annotation && (
            <>
              <dt title="How far beyond the crop region the annotation crop reaches, in millimetres on paper: left, bottom, right, top">Annotation offsets (mm)</dt>
              <dd className="crop-corners">
                {sides.map((s, i) => (
                  <NumberField key={s} testId={`view-crop-offset-${s.toLowerCase()}`} value={c.offset[i]} min={0} title={s} onCommit={(n) => { if (n == null) return; const o = [...c.offset]; o[i] = n; set({ offset: o }); }} />
                ))}
              </dd>
            </>
          )}
        </>
      )}
    </>
  );
}
