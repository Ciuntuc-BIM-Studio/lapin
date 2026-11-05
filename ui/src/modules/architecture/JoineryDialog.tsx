/**
 * Doors and windows: the configurator. The project's door and window types
 * on the left; on the right the one being edited — its size, frame, grid of
 * columns and rows, each panel's operation — with its drawings as the
 * engine's symbol engine makes them (the elevation from inside or outside,
 * the plan, the section), the project's symbol convention, and what IFC
 * calls it.
 * Saving is one engine command; every door or window of the type is
 * refitted to its new size.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ofClass, relatedOf } from '../../engine/model';
import { LengthField, NumberField } from '../../shell/cells';
import { MaterialPicker } from '../core/MaterialPicker';
import { PendingDialog } from '../core/PendingDialog';
import { ParamsSection } from '../../shell/ParamsSection';
import { deleteType } from '../core/deleteType';
import { LintelRuleEditor } from './LintelRule';
import type { Rule } from './lintels';

interface Panel { col: number; row: number; operation: string; colSpan?: number; rowSpan?: number; glazed?: boolean }

/**
 * A hinged door leaf's operation as the schedules name its hand (report #91): looking from the handle towards the
 * hinges, a leaf opening to the left is a left swing. The operation ids are IFC's hand (hinges on the left of someone
 * pushing it open: the left of the Panels grid, seen from outside), so swing-left is a right swing.
 */
const DOOR_HAND_LABELS: Record<string, string> = {
  'swing-left': 'Swing right (hinges at left)', 'swing-right': 'Swing left (hinges at right)',
  'double-acting-left': 'Double acting right (hinges at left)', 'double-acting-right': 'Double acting left (hinges at right)',
  'folding-left': 'Folding right (hinges at left)', 'folding-right': 'Folding left (hinges at right)',
};
interface Spec {
  kind: 'door' | 'window'; name: string; mark: string; width: number; height: number; sillHeight: number;
  frame: { depth: number; width: number; mullion: number; transom: number; offset: number };
  sash: { width: number; depth: number; leafThickness: number; glassThickness: number };
  columns: number[]; rows: number[]; panels: Panel[];
  board: { interior: number; exterior: number }; threshold: number;
  colors: { frame: string; glass: string; leaf: string }; isExternal: boolean; thermalTransmittance?: number | null;
  lintel?: Rule | null;
  details?: Details | null;
  shape?: Shape | null;
  muntins?: Muntins | null;
  hardware?: Hardware | null;
  materials?: Materials | null;
}
/** The parts' materials by name (report #76); a part without one keeps its colour. */
interface Materials { frame?: string | null; sash?: string | null; glass?: string | null; leaf?: string | null; board?: string | null; hardware?: string | null }
type MaterialPart = keyof Materials;
interface Shape { kind: 'rect' | 'round' | 'segmental' | 'elliptical' | 'radiused' | 'pointed' | 'gable' | 'circle'; rise?: number | null; bars: 'none' | 'sunburst' | 'mullions' | 'cross'; spokes: number }
interface Muntins { pattern: 'none' | 'grid' | 'prairie' | 'diamond'; across: number; up: number; width: number; margin: number }
interface Hardware { handle: 'auto' | 'lever' | 'knob' | 'pull' | 'window' | 'none'; height: number; color?: string | null }
interface Keystone { on: boolean; width: number; height: number; projection: number; material?: string | null; color: string }
interface Head { on: boolean; style: 'cornice' | 'pediment' | 'segmental' | 'hood'; overhang: number; height: number; projection: number; rise: number; gap: number; material?: string | null; color: string }
interface Apron { on: boolean; height: number; projection: number; consoles: boolean; material?: string | null; color: string }
interface Reveal { mode: 'wall' | 'material' | 'none'; material?: string | null; thickness: number }
interface Sill { on: boolean; projection: number; thickness: number; overhang: number; slope: number; material?: string | null }
interface Surround { on: boolean; width: number; projection: number; sill: boolean; material?: string | null }
interface Threshold { on: boolean; height: number; material?: string | null }
interface Shutters { on: boolean; style: 'louvred' | 'panel' | 'planks'; leaves: number; open: boolean; thickness: number; gap: number; overlap: number; along: number; rise: number; height?: number | null; pitch: number; material?: string | null; color: string }
interface Grille { on: boolean; side: 'revealInside' | 'reveal' | 'outside' | 'inside'; pattern: 'horizontal' | 'vertical' | 'grid'; bar: number; spacing: number; frameWidth: number; gap: number; overlap: number; along: number; rise: number; material?: string | null; color: string }
interface WindowBox { on: boolean; width?: number | null; depth: number; height: number; gap: number; drop: number; along: number; material?: string | null; color: string }
interface Canopy { on: boolean; projection: number; thickness: number; overhang: number; rise: number; slope: number; gap: number; along: number; material?: string | null; color: string }
interface Details { reveal?: Reveal | null; sillInside?: Sill | null; sillOutside?: Sill | null; surround?: Surround | null; threshold?: Threshold | null; shutters?: Shutters | null; grille?: Grille | null; windowBox?: WindowBox | null; canopy?: Canopy | null; keystone?: Keystone | null; head?: Head | null; apron?: Apron | null }
const SILL_IN: Sill = { on: true, projection: 0.03, thickness: 0.02, overhang: 0.02, slope: 0 };
const SILL_OUT: Sill = { on: true, projection: 0.04, thickness: 0.02, overhang: 0.03, slope: 5 };
const SURROUND: Surround = { on: true, width: 0.08, projection: 0.02, sill: true };
const THRESHOLD: Threshold = { on: true, height: 0.02 };
const SHUTTERS: Shutters = { on: true, style: 'louvred', leaves: 2, open: true, thickness: 0.03, gap: 0.01, overlap: 0.03, along: 0, rise: 0, height: null, pitch: 0.05, color: '#4f6f52' };
const GRILLE: Grille = { on: true, side: 'revealInside', pattern: 'horizontal', bar: 0.012, spacing: 0.1, frameWidth: 0.03, gap: 0.03, overlap: 0.05, along: 0, rise: 0, color: '#2f2f2f' };
const WINDOW_BOX: WindowBox = { on: true, width: null, depth: 0.2, height: 0.18, gap: 0, drop: 0.06, along: 0, color: '#8a5a3c' };
const CANOPY: Canopy = { on: true, projection: 0.6, thickness: 0.06, overhang: 0.15, rise: 0.15, slope: 5, gap: 0, along: 0, color: '#6b6b6b' };
const SHAPE: Shape = { kind: 'rect', rise: null, bars: 'none', spokes: 5 };
const MUNTINS: Muntins = { pattern: 'none', across: 2, up: 3, width: 0.022, margin: 0.1 };
const HARDWARE: Hardware = { handle: 'auto', height: 1.05, color: null };
const KEYSTONE: Keystone = { on: true, width: 0.14, height: 0.24, projection: 0.03, color: '#e6dfd0' };
const HEAD: Head = { on: true, style: 'cornice', overhang: 0.08, height: 0.1, projection: 0.1, rise: 0.25, gap: 0, color: '#e6dfd0' };
const APRON: Apron = { on: true, height: 0.2, projection: 0.03, consoles: true, color: '#e6dfd0' };

/**
 * Architectural styles for a door or window (decision 188, after BubbleBIM's style packs): each sets the
 * outline, the glazing bars and the ornaments of the facade at once; everything stays editable after.
 */
const JOINERY_STYLES: Record<string, { label: string; apply: (s: Spec) => Partial<Spec> }> = {
  modern: { label: 'Modern', apply: () => ({ shape: null, muntins: null }) },
  classical: { label: 'Classical', apply: () => ({ shape: null, muntins: { ...MUNTINS, pattern: 'grid', across: 2, up: 3 } }) },
  georgian: { label: 'Georgian', apply: () => ({ shape: null, muntins: { ...MUNTINS, pattern: 'grid', across: 3, up: 3 } }) },
  victorian: { label: 'Victorian', apply: (s) => ({ shape: { ...SHAPE, kind: 'segmental', rise: Math.round(s.width / 6 * 1000) / 1000 }, muntins: { ...MUNTINS, pattern: 'grid', across: 1, up: 2 } }) },
  romanesque: { label: 'Romanesque', apply: () => ({ shape: { ...SHAPE, kind: 'round', bars: 'none' }, muntins: null }) },
  baroque: { label: 'Baroque', apply: () => ({ shape: { ...SHAPE, kind: 'round', bars: 'sunburst', spokes: 5 }, muntins: { ...MUNTINS, pattern: 'grid', across: 2, up: 3 } }) },
  gothic: { label: 'Gothic', apply: () => ({ shape: { ...SHAPE, kind: 'pointed', rise: null, bars: 'mullions' }, muntins: null }) },
  tudor: { label: 'Tudor', apply: (s) => ({ shape: { ...SHAPE, kind: 'pointed', rise: Math.round(s.width * 0.3 * 1000) / 1000, bars: 'mullions' }, muntins: { ...MUNTINS, pattern: 'diamond', across: 4 } }) },
  prairie: { label: 'Prairie', apply: () => ({ shape: null, muntins: { ...MUNTINS, pattern: 'prairie', margin: 0.1 } }) },
  traditional: { label: 'Traditional', apply: () => ({ shape: null, muntins: { ...MUNTINS, pattern: 'grid', across: 1, up: 2 } }) },
  oculus: { label: 'Round window', apply: (s) => ({ shape: { ...SHAPE, kind: 'circle', bars: 'cross' }, muntins: null, height: s.width, columns: [1], rows: [1], panels: [{ col: 0, row: 0, operation: 'fixed' }] }) },
};

/** The details each style adds on the facade (and takes away: a style's own ornaments replace another's). */
function styleDetails(key: string, d: Details): Details {
  const bare: Details = { ...d, surround: d.surround ? { ...d.surround, on: false } : null, keystone: null, head: null, apron: null };
  const stone: Sill = { ...SILL_OUT, thickness: 0.04, projection: 0.05 };
  switch (key) {
    case 'classical': return { ...bare, surround: { ...SURROUND, width: 0.16, projection: 0.04 }, head: { ...HEAD, style: 'cornice', height: 0.12, projection: 0.12 }, apron: { ...APRON }, sillOutside: stone };
    case 'georgian': return { ...bare, surround: { ...SURROUND, width: 0.12, projection: 0.03 }, keystone: { ...KEYSTONE }, sillOutside: stone };
    case 'victorian': return { ...bare, surround: { ...SURROUND, width: 0.1, projection: 0.03 }, keystone: { ...KEYSTONE, height: 0.2 }, apron: { ...APRON, consoles: true }, sillOutside: stone };
    case 'romanesque': return { ...bare, surround: { ...SURROUND, width: 0.2, projection: 0.04, sill: false }, keystone: { ...KEYSTONE } };
    case 'baroque': return { ...bare, surround: { ...SURROUND, width: 0.14, projection: 0.04 }, keystone: { ...KEYSTONE }, head: { ...HEAD, style: 'segmental', rise: 0.2 }, apron: { ...APRON }, sillOutside: stone };
    case 'gothic': return { ...bare, surround: { ...SURROUND, width: 0.1, projection: 0.03, sill: false }, head: { ...HEAD, style: 'hood', height: 0.08, projection: 0.06 } };
    case 'tudor': return { ...bare, head: { ...HEAD, style: 'hood', height: 0.08, projection: 0.06 } };
    case 'prairie': return { ...bare, sillOutside: { ...SILL_OUT, projection: 0.06 } };
    case 'traditional': return { ...bare, surround: { ...SURROUND, width: 0.12, projection: 0.025 }, head: { ...HEAD, style: 'cornice', height: 0.06, overhang: 0.06, projection: 0.07 } };
    case 'oculus': return { ...bare, surround: { ...SURROUND, width: 0.12, projection: 0.03, sill: false }, keystone: { ...KEYSTONE, height: 0.18 } };
    default: return bare;
  }
}
export interface Preview { problem: string | null; boxes: { role: string; x0: number; x1: number; z0: number; z1: number; y0: number; y1: number; profile?: number[] | null; holes?: number[][] | null }[]; panels: { operation: string; x0: number; x1: number; z0: number; z1: number }[]; ifcType: string }
export interface Stroke { pts: [number, number][]; weight: string; closed: boolean; dashed: boolean }
export interface Drawing { strokes: Stroke[] }
export interface Drawings { elevation: Drawing; exterior: Drawing; plan: Drawing; section: Drawing }
export type Preview2 = Preview & { drawings?: Drawings | null; symbols?: { convention: string; hardware: boolean } };

/**
 * One of the symbol engine's drawings in a box: X across, Y up, fitted; strokes by weight (cut, outline, thin), dashed where asked.
 * The engine draws in the type's frame, the model's mirror image: flipX shows the door or window as it stands (report #83),
 * an elevation from inside with a swing-left door's hinges on the right, the plan from above.
 */
export function Strokes({ drawing, w, h, flipX = false, label, children }: { drawing: Drawing | undefined; w: number; h: number; flipX?: boolean; label: string; children?: (sx: (x: number) => number, sy: (y: number) => number, k: number) => React.ReactNode }) {
  const pts = (drawing?.strokes ?? []).flatMap((s) => s.pts);
  if (pts.length === 0) return <svg viewBox={`0 0 ${w} ${h}`} width={w} height={h} aria-label={label} />;
  const xs = pts.map((p) => (flipX ? -p[0] : p[0])), ys = pts.map((p) => p[1]);
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const pad = 10, k = Math.min((w - 2 * pad) / Math.max(x1 - x0, 1e-6), (h - 2 * pad) / Math.max(y1 - y0, 1e-6));
  const sx = (x: number) => (w - (x1 - x0) * k) / 2 + ((flipX ? -x : x) - x0) * k;
  const sy = (y: number) => h - (h - (y1 - y0) * k) / 2 - (y - y0) * k;
  const width = (wt: string) => wt === 'cut' ? 1.6 : wt === 'outline' ? 0.9 : 0.5;
  return (
    <svg viewBox={`0 0 ${w} ${h}`} width={w} height={h} aria-label={label}>
      {children?.(sx, sy, k)}
      {(drawing?.strokes ?? []).map((s, i) => (
        <polyline key={i} points={s.pts.map((p) => `${sx(p[0]).toFixed(1)},${sy(p[1]).toFixed(1)}`).join(' ') + (s.closed ? ` ${sx(s.pts[0][0]).toFixed(1)},${sy(s.pts[0][1]).toFixed(1)}` : '')}
          fill="none" stroke="#1e2a3a" strokeWidth={width(s.weight)} strokeDasharray={s.dashed ? '4 3' : undefined} strokeLinejoin="round" />
      ))}
    </svg>
  );
}

const ROLE_ORDER: Record<string, number> = { board: 0, frame: 1, leaf: 2, sash: 3, glass: 4, muntin: 5, hardware: 6, handle: 7 };
/** An outline (X, Z pairs) as an SVG path in the elevation's scale. */
const loopPath = (pts: number[], sx: (x: number) => number, sy: (y: number) => number) =>
  pts.length < 6 ? '' : 'M' + Array.from({ length: pts.length / 2 }, (_, i) => `${sx(pts[2 * i]).toFixed(1)},${sy(pts[2 * i + 1]).toFixed(1)}`).join('L') + 'Z';
const weights = (text: string) => text.split(/[ ,;]+/).map(Number).filter((v) => v > 0);

/** A labelled length (the shared field: the project's unit, one commit on blur or Enter). */
function Mm({ label, value, onChange }: { label: string; value: number; onChange: (m: number) => void }) {
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <LengthField metres={value} onCommit={(m) => { if (m != null) onChange(m); }} />
    </label>
  );
}

/** A labelled number (degrees, counts): the shared field. */
function Num({ label, value, onChange, integer, min }: { label: string; value: number; onChange: (n: number) => void; integer?: boolean; min?: number }) {
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <NumberField value={value} integer={integer} min={min} onCommit={(n) => { if (n != null) onChange(n); }} />
    </label>
  );
}

/** The parts a door or window names materials for, in the configurator's order. */
const PARTS: { part: MaterialPart; label: string; door?: boolean; window?: boolean }[] = [
  { part: 'frame', label: 'Frame material' }, { part: 'sash', label: 'Sash material', window: true }, { part: 'glass', label: 'Glass material' },
  { part: 'leaf', label: 'Leaf material', door: true }, { part: 'board', label: 'Board material', window: true }, { part: 'hardware', label: 'Hardware material' },
];

/**
 * The parts' materials (report #76), and the button that gives every door or window of the project the same ones: all window
 * frames one dark grey metal, all glass one blue glass. One undo step (architecture.joineryMaterials).
 */
function PartMaterials({ spec, onChange }: { spec: Spec; onChange: (m: Materials) => void }) {
  const mats = spec.materials ?? {};
  const parts = PARTS.filter((p) => (spec.kind === 'door' ? !p.window : !p.door));
  const applyAll = async () => {
    const args: Record<string, unknown> = { kind: spec.kind };
    for (const { part } of parts) args[part] = mats[part] ?? '';
    const r = await execute('architecture.joineryMaterials', args);
    if (r) notify(spec.kind === 'door' ? 'Every door type has these materials now.' : 'Every window type has these materials now.');
  };
  return (
    <div className="part-materials" data-testid="joinery-materials">
      <p className="eyebrow">Materials</p>
      {parts.map(({ part, label }) => (
        <label key={part} className="field-line"><span className="eyebrow">{label}</span>
          <span data-part={part}><MaterialPicker value={mats[part] ?? ''} allowEmpty placeholder={part === 'sash' ? 'as the frame' : 'its colour'} onChange={(m) => onChange({ ...mats, [part]: m || null })} /></span></label>
      ))}
      <button type="button" className="btn compact" data-action="joinery-materials-all" title="Every type of this kind takes these materials, part by part: one undo step" onClick={() => void applyAll()}>
        {spec.kind === 'door' ? 'Use for all doors' : 'Use for all windows'}
      </button>
    </div>
  );
}

/** A labelled material of the project's; empty: the default (null). */
function Material({ label = 'Material', value, onChange }: { label?: string; value?: string | null; onChange: (material: string | null) => void }) {
  return <label className="field-line"><span className="eyebrow">{label}</span><MaterialPicker value={value ?? ''} allowEmpty onChange={(m) => onChange(m || null)} /></label>;
}

/** A material and a colour (the colour when the material is not in the project). */
function Look({ material, color, onChange }: { material?: string | null; color: string; onChange: (patch: { material?: string | null; color?: string }) => void }) {
  return (
    <>
      <Material value={material} onChange={(m) => onChange({ material: m })} />
      <label className="field-line"><span className="eyebrow">Colour</span><input type="color" value={color} onChange={(e) => onChange({ color: e.target.value })} /></label>
    </>
  );
}

/** Where a dressing sits, set by hand: along the wall from the window's axis, up, and off the wall's face. */
function Place({ along, rise, gap, gapLabel = 'From the wall face', onChange }: { along: number; rise?: number; gap: number; gapLabel?: string; onChange: (patch: { along?: number; rise?: number; gap?: number }) => void }) {
  return (
    <>
      <Mm label={gapLabel} value={gap} onChange={(v) => onChange({ gap: v })} />
      <Mm label="From the window's axis" value={along} onChange={(v) => onChange({ along: v })} />
      {rise !== undefined && <Mm label="Up or down" value={rise} onChange={(v) => onChange({ rise: v })} />}
    </>
  );
}

/**
 * The dressings on the facade (reports #38, #39): shutters, a safety grille, a window box, a canopy. Each sits by
 * the wall's faces where the window is (or the surround's face), and may be moved by hand from the window's axis and off the face.
 */
function DressingFields({ kind, height, details, onChange }: { kind: 'door' | 'window'; height: number; details: Details; onChange: (patch: Partial<Details>) => void }) {
  const sh = details.shutters, g = details.grille, wb = details.windowBox, c = details.canopy;
  return (
    <>
      <p className="eyebrow">On the facade</p>
      <label className="field-line check"><input type="checkbox" data-dressing="shutters" checked={sh?.on ?? false} onChange={(e) => onChange({ shutters: { ...(sh ?? SHUTTERS), on: e.target.checked } })} /><span>Shutters</span></label>
      {sh?.on && <>
        <label className="field-line"><span className="eyebrow">Style</span>
          <select value={sh.style} onChange={(e) => onChange({ shutters: { ...sh, style: e.target.value as Shutters['style'] } })}>
            <option value="louvred">Louvred</option><option value="panel">Panel</option><option value="planks">Planks</option>
          </select></label>
        <label className="field-line"><span className="eyebrow">Leaves</span>
          <select value={sh.leaves} onChange={(e) => onChange({ shutters: { ...sh, leaves: Number(e.target.value) } })}>
            <option value={1}>One leaf</option><option value={2}>Two leaves</option>
          </select></label>
        <label className="field-line check"><input type="checkbox" data-dressing="shutters-open" checked={sh.open} onChange={(e) => onChange({ shutters: { ...sh, open: e.target.checked } })} /><span>Shown open</span></label>
        <Mm label="Thickness" value={sh.thickness} onChange={(v) => onChange({ shutters: { ...sh, thickness: v } })} />
        <Mm label="Past the jambs" value={sh.overlap} onChange={(v) => onChange({ shutters: { ...sh, overlap: v } })} />
        <Mm label="Height" value={sh.height ?? height} onChange={(v) => onChange({ shutters: { ...sh, height: v } })} />
        {sh.style === 'louvred' && <Mm label="Louvre pitch" value={sh.pitch} onChange={(v) => onChange({ shutters: { ...sh, pitch: v } })} />}
        <Place along={sh.along} rise={sh.rise} gap={sh.gap} onChange={(p) => onChange({ shutters: { ...sh, ...p } })} />
        <Look material={sh.material} color={sh.color} onChange={(p) => onChange({ shutters: { ...sh, ...p } })} />
      </>}
      <label className="field-line check"><input type="checkbox" data-dressing="grille" checked={g?.on ?? false} onChange={(e) => onChange({ grille: { ...(g ?? GRILLE), on: e.target.checked } })} /><span>Safety grille</span></label>
      {g?.on && <>
        <label className="field-line"><span className="eyebrow">Fixed</span>
          <select value={g.side} onChange={(e) => onChange({ grille: { ...g, side: e.target.value as Grille['side'] } })}>
            <option value="revealInside">In the reveal, inside</option><option value="reveal">In the reveal, outside</option><option value="outside">On the facade</option><option value="inside">On the interior face</option>
          </select></label>
        <label className="field-line"><span className="eyebrow">Bars</span>
          <select value={g.pattern} onChange={(e) => onChange({ grille: { ...g, pattern: e.target.value as Grille['pattern'] } })}>
            <option value="horizontal">Horizontal</option><option value="vertical">Vertical</option><option value="grid">Both ways</option>
          </select></label>
        <Mm label="Bar size" value={g.bar} onChange={(v) => onChange({ grille: { ...g, bar: v } })} />
        <Mm label="Bar spacing" value={g.spacing} onChange={(v) => onChange({ grille: { ...g, spacing: v } })} />
        <Mm label="Frame width" value={g.frameWidth} onChange={(v) => onChange({ grille: { ...g, frameWidth: v } })} />
        {(g.side === 'outside' || g.side === 'inside') && <Mm label="Past the opening" value={g.overlap} onChange={(v) => onChange({ grille: { ...g, overlap: v } })} />}
        <Place along={g.along} rise={g.rise} gap={g.gap} gapLabel={g.side === 'reveal' || g.side === 'revealInside' ? 'Set back from the face' : 'From the wall face'} onChange={(p) => onChange({ grille: { ...g, ...p } })} />
        <Look material={g.material} color={g.color} onChange={(p) => onChange({ grille: { ...g, ...p } })} />
      </>}
      {kind === 'window' && <label className="field-line check"><input type="checkbox" data-dressing="windowBox" checked={wb?.on ?? false} onChange={(e) => onChange({ windowBox: { ...(wb ?? WINDOW_BOX), on: e.target.checked } })} /><span>Window box</span></label>}
      {kind === 'window' && wb?.on && <>
        <Mm label="Width" value={wb.width ?? 0} onChange={(v) => onChange({ windowBox: { ...wb, width: v > 0 ? v : null } })} />
        <Mm label="Depth" value={wb.depth} onChange={(v) => onChange({ windowBox: { ...wb, depth: v } })} />
        <Mm label="Height" value={wb.height} onChange={(v) => onChange({ windowBox: { ...wb, height: v } })} />
        <Mm label="Below the opening" value={wb.drop} onChange={(v) => onChange({ windowBox: { ...wb, drop: v } })} />
        <Place along={wb.along} gap={wb.gap} onChange={(p) => onChange({ windowBox: { ...wb, ...p } })} />
        <Look material={wb.material} color={wb.color} onChange={(p) => onChange({ windowBox: { ...wb, ...p } })} />
      </>}
      <OrnamentFields kind={kind} details={details} onChange={onChange} />
      <label className="field-line check"><input type="checkbox" data-dressing="canopy" checked={c?.on ?? false} onChange={(e) => onChange({ canopy: { ...(c ?? CANOPY), on: e.target.checked } })} /><span>Canopy</span></label>
      {c?.on && <>
        <Mm label="Projection" value={c.projection} onChange={(v) => onChange({ canopy: { ...c, projection: v } })} />
        <Mm label="Thickness" value={c.thickness} onChange={(v) => onChange({ canopy: { ...c, thickness: v } })} />
        <Mm label="Past the jambs" value={c.overhang} onChange={(v) => onChange({ canopy: { ...c, overhang: v } })} />
        <Mm label="Above the head" value={c.rise} onChange={(v) => onChange({ canopy: { ...c, rise: v } })} />
        <Num label="Slope (°)" value={c.slope} onChange={(v) => onChange({ canopy: { ...c, slope: v } })} />
        <Place along={c.along} gap={c.gap} onChange={(p) => onChange({ canopy: { ...c, ...p } })} />
        <Look material={c.material} color={c.color} onChange={(p) => onChange({ canopy: { ...c, ...p } })} />
      </>}
    </>
  );
}

/** The ornaments of the facade (decision 188): a keystone, what crowns the opening, an apron with its consoles under a window. */
function OrnamentFields({ kind, details, onChange }: { kind: 'door' | 'window'; details: Details; onChange: (patch: Partial<Details>) => void }) {
  const k = details.keystone, h = details.head, a = details.apron;
  return (
    <>
      <label className="field-line check"><input type="checkbox" data-dressing="keystone" checked={k?.on ?? false} onChange={(e) => onChange({ keystone: { ...(k ?? KEYSTONE), on: e.target.checked } })} /><span>Keystone</span></label>
      {k?.on && <>
        <Mm label="Width" value={k.width} onChange={(v) => onChange({ keystone: { ...k, width: v } })} />
        <Mm label="Height" value={k.height} onChange={(v) => onChange({ keystone: { ...k, height: v } })} />
        <Mm label="Projection" value={k.projection} onChange={(v) => onChange({ keystone: { ...k, projection: v } })} />
        <Look material={k.material} color={k.color} onChange={(p) => onChange({ keystone: { ...k, ...p } })} />
      </>}
      <label className="field-line check"><input type="checkbox" data-dressing="head" checked={h?.on ?? false} onChange={(e) => onChange({ head: { ...(h ?? HEAD), on: e.target.checked } })} /><span>Crown over the opening</span></label>
      {h?.on && <>
        <label className="field-line"><span className="eyebrow">Style</span>
          <select data-field="head-style" value={h.style} onChange={(e) => onChange({ head: { ...h, style: e.target.value as Head['style'] } })}>
            <option value="cornice">Cornice</option><option value="pediment">Pediment</option><option value="segmental">Segmental pediment</option><option value="hood">Hood mould</option>
          </select></label>
        <Mm label="Height" value={h.height} onChange={(v) => onChange({ head: { ...h, height: v } })} />
        <Mm label="Projection" value={h.projection} onChange={(v) => onChange({ head: { ...h, projection: v } })} />
        {h.style !== 'hood' && <Mm label="Past the jambs" value={h.overhang} onChange={(v) => onChange({ head: { ...h, overhang: v } })} />}
        {(h.style === 'pediment' || h.style === 'segmental') && <Mm label="Rise" value={h.rise} onChange={(v) => onChange({ head: { ...h, rise: v } })} />}
        <Mm label="Above the head" value={h.gap} onChange={(v) => onChange({ head: { ...h, gap: v } })} />
        <Look material={h.material} color={h.color} onChange={(p) => onChange({ head: { ...h, ...p } })} />
      </>}
      {kind === 'window' && <label className="field-line check"><input type="checkbox" data-dressing="apron" checked={a?.on ?? false} onChange={(e) => onChange({ apron: { ...(a ?? APRON), on: e.target.checked } })} /><span>Apron under the sill</span></label>}
      {kind === 'window' && a?.on && <>
        <Mm label="Height" value={a.height} onChange={(v) => onChange({ apron: { ...a, height: v } })} />
        <Mm label="Projection" value={a.projection} onChange={(v) => onChange({ apron: { ...a, projection: v } })} />
        <label className="field-line check"><input type="checkbox" checked={a.consoles} onChange={(e) => onChange({ apron: { ...a, consoles: e.target.checked } })} /><span>Consoles</span></label>
        <Look material={a.material} color={a.color} onChange={(p) => onChange({ apron: { ...a, ...p } })} />
      </>}
    </>
  );
}

/** The outline, the head's bars and the glazing bars (decision 188). */
function ShapeFields({ spec, onChange }: { spec: Spec; onChange: (patch: Partial<Spec>) => void }) {
  const sh = spec.shape ?? SHAPE, mu = spec.muntins ?? MUNTINS;
  const setShape = (p: Partial<Shape>) => onChange({ shape: { ...sh, ...p } });
  const setMuntins = (p: Partial<Muntins>) => onChange({ muntins: { ...mu, ...p } });
  return (
    <>
      <label className="field-line"><span className="eyebrow">Shape</span>
        <select data-field="shape" value={sh.kind} onChange={(e) => setShape({ kind: e.target.value as Shape['kind'], rise: null })}>
          <option value="rect">Rectangle</option><option value="round">Round arch</option><option value="segmental">Segmental arch</option>
          <option value="elliptical">Elliptical arch</option><option value="radiused">Radiused corners</option>
          <option value="pointed">Pointed arch</option><option value="gable">Triangular head</option><option value="circle">Round or oval</option>
        </select></label>
      {(sh.kind === 'segmental' || sh.kind === 'elliptical' || sh.kind === 'pointed' || sh.kind === 'gable') && <Mm label="Rise of the head" value={sh.rise ?? (sh.kind === 'segmental' ? spec.width / 6 : sh.kind === 'pointed' ? spec.width * 0.866 : spec.width / 4)} onChange={(v) => setShape({ rise: v })} />}
      {sh.kind === 'radiused' && <Mm label="Corner radius" value={sh.rise ?? 0.15} onChange={(v) => setShape({ rise: v })} />}
      {sh.kind !== 'rect' && <label className="field-line"><span className="eyebrow">{sh.kind === 'circle' ? 'Bars' : 'Bars in the head'}</span>
        <select data-field="shape-bars" value={sh.bars} onChange={(e) => setShape({ bars: e.target.value as Shape['bars'] })}>
          <option value="none">None</option><option value="sunburst">Sunburst</option><option value="mullions">{sh.kind === 'circle' ? 'Upright' : 'The mullions carried up'}</option><option value="cross">Cross</option>
        </select></label>}
      {sh.kind !== 'rect' && sh.bars === 'sunburst' && <Num label="Spokes" value={sh.spokes} integer min={1} onChange={(v) => setShape({ spokes: v })} />}
      {sh.kind !== 'circle' && <label className="field-line"><span className="eyebrow">Glazing bars</span>
        <select data-field="muntins" value={mu.pattern} onChange={(e) => setMuntins({ pattern: e.target.value as Muntins['pattern'] })}>
          <option value="none">None</option><option value="grid">Grid (colonial)</option><option value="prairie">Prairie</option><option value="diamond">Diamond lattice</option>
        </select></label>}
      {sh.kind !== 'circle' && (mu.pattern === 'grid' || mu.pattern === 'diamond') && <Num label="Lites across" value={mu.across} integer min={1} onChange={(v) => setMuntins({ across: v })} />}
      {sh.kind !== 'circle' && mu.pattern === 'grid' && <Num label="Lites up" value={mu.up} integer min={1} onChange={(v) => setMuntins({ up: v })} />}
      {sh.kind !== 'circle' && mu.pattern === 'prairie' && <Mm label="Margin" value={mu.margin} onChange={(v) => setMuntins({ margin: v })} />}
      {sh.kind !== 'circle' && mu.pattern !== 'none' && <Mm label="Bar width" value={mu.width} onChange={(v) => setMuntins({ width: v })} />}
    </>
  );
}

/** The handles (report #48): their style, a door handle's height, their colour. */
function HardwareFields({ spec, onChange }: { spec: Spec; onChange: (patch: Partial<Spec>) => void }) {
  const hw = spec.hardware ?? HARDWARE;
  return (
    <>
      <label className="field-line"><span className="eyebrow">Handle</span>
        <select data-field="handle" value={hw.handle} onChange={(e) => onChange({ hardware: { ...hw, handle: e.target.value as Hardware['handle'] } })}>
          <option value="auto">{spec.kind === 'door' ? 'Lever (default)' : 'Window handle (default)'}</option>
          <option value="lever">Lever</option><option value="knob">Knob</option><option value="pull">Pull bar</option><option value="window">Window handle</option><option value="none">None</option>
        </select></label>
      {spec.kind === 'door' && hw.handle !== 'none' && <Mm label="Handle height" value={hw.height} onChange={(v) => onChange({ hardware: { ...hw, height: v } })} />}
      {hw.handle !== 'none' && <label className="field-line"><span className="eyebrow">Handle colour</span><input type="color" value={hw.color ?? '#5a5a5a'} onChange={(e) => onChange({ hardware: { ...hw, color: e.target.value } })} /></label>}
    </>
  );
}

/** A sill's fields: on or off, then its sizes (mm), slope (°, the exterior one) and material. */
function SillFields({ label, sill, fallback, slope = false, onChange }: { label: string; sill: Sill | null; fallback: Sill; slope?: boolean; onChange: (s: Sill) => void }) {
  const on = sill?.on ?? false;
  const cur = sill ?? fallback;
  return (
    <>
      <label className="field-line check"><input type="checkbox" checked={on} onChange={(e) => onChange({ ...cur, on: e.target.checked })} /><span>{label}</span></label>
      {on && <>
        <Mm label="Projection" value={cur.projection} onChange={(v) => onChange({ ...cur, projection: v })} />
        <Mm label="Thickness" value={cur.thickness} onChange={(v) => onChange({ ...cur, thickness: v })} />
        <Mm label="Past the jambs" value={cur.overhang} onChange={(v) => onChange({ ...cur, overhang: v })} />
        {slope && <Num label="Slope (°)" value={cur.slope} onChange={(v) => onChange({ ...cur, slope: v })} />}
        <Material value={cur.material} onChange={(material) => onChange({ ...cur, material })} />
      </>}
    </>
  );
}

export function JoineryDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = [...ofClass(model, 'IfcDoorType'), ...ofClass(model, 'IfcWindowType')];
  const catalog = info?.catalogs['architecture.joinery'] as unknown as { windowOperations: string[]; doorOperations: string[]; defaultDoor: Spec; defaultWindow: Spec } | undefined;
  const [editing, setEditing] = useState<string | null>(types[0]?.id ?? null);
  // What a new type starts from (a new object each time New door or New window is clicked, so the editor reloads).
  const [fresh, setFresh] = useState<{ kind: 'door' | 'window' }>({ kind: 'window' });
  const [spec, setSpec] = useState<Spec | null>(null);
  const [cols, setCols] = useState('1');
  const [rows, setRows] = useState('1');
  const [preview, setPreview] = useState<Preview2 | null>(null);
  const [outside, setOutside] = useState(false);

  // The editor loads when the picked type changes, not on every diff (which
  // would throw away what is being typed): it reads the model through a ref.
  const modelRef = useRef(model);
  modelRef.current = model;
  // Load the picked type (or a fresh default) into the editor.
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as Spec | undefined) ?? (fresh.kind === 'door' ? catalog?.defaultDoor : catalog?.defaultWindow) ?? null;
    setSpec(s ? structuredClone(s) : null);
    if (s) { setCols(s.columns.join(' ')); setRows(s.rows.join(' ')); }
  }, [editing, catalog, fresh]);

  // The engine builds the preview from the spec as it is now.
  useEffect(() => {
    if (!spec) return;
    let live = true;
    void query<Preview2>('architecture.joineryPreview', { spec }).then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [spec]);

  if (!spec || !catalog) return <PendingDialog title="Doors and windows" />;
  // A name the dialog gave ("Door 900×2100") follows the size as it is typed (report #51); one the user wrote stays.
  const sizeName = (s: Pick<Spec, 'kind' | 'width' | 'height'>) => `${s.kind === 'door' ? 'Door' : 'Window'} ${Math.round(s.width * 1000)}×${Math.round(s.height * 1000)}`;
  const set = (patch: Partial<Spec>) => {
    const next = { ...spec, ...patch };
    if (('width' in patch || 'height' in patch) && !('name' in patch) && spec.name === sizeName(spec)) next.name = sizeName(next);
    setSpec(next);
  };
  const details: Details = spec.details ?? {};
  const setDetails = (patch: Partial<Details>) => set({ details: { ...details, ...patch } });
  const operations = spec.kind === 'door' ? [...catalog.doorOperations, 'fixed'] : catalog.windowOperations;
  const close = () => useUi.setState({ dialog: null });
  const startNew = (kind: 'door' | 'window') => { setEditing(null); setFresh({ kind }); };
  const setGrid = (c: string, r: string) => {
    setCols(c); setRows(r);
    const cw = weights(c), rw = weights(r);
    if (cw.length && rw.length) set({ columns: cw, rows: rw, panels: spec.panels.filter((p) => p.col < cw.length && p.row < rw.length) });
  };
  const cellOp = (col: number, row: number) => spec.panels.find((p) => p.col === col && p.row === row)?.operation ?? 'fixed';
  const setCell = (col: number, row: number, operation: string) =>
    set({ panels: [...spec.panels.filter((p) => !(p.col === col && p.row === row)), { col, row, operation }] });

  const save = async () => {
    if (editing) await execute('architecture.updateJoineryType', { id: editing, spec });
    else {
      const r = await execute('architecture.createJoineryType', { spec }) as { id?: string } | null;
      if (r?.id) setEditing(r.id);
    }
  };

  // The elevation's box; the strokes are fitted into it.
  const W = 300, H = 300;
  const boxes = [...(preview?.boxes ?? [])].sort((a, b) => (ROLE_ORDER[a.role] ?? 9) - (ROLE_ORDER[b.role] ?? 9));
  // A part's colour in the preview: its material's (report #76), else the type's colour for it.
  const materialColor = (role: string): string | null => {
    const m = spec.materials;
    const name = !m ? null : role === 'frame' ? m.frame : role === 'sash' || role === 'muntin' ? m.sash ?? m.frame : role === 'glass' ? m.glass : role === 'leaf' ? m.leaf : role === 'board' ? m.board : role === 'handle' || role === 'hardware' ? m.hardware : null;
    const mat = name ? ofClass(model, 'IfcMaterial').find((x) => x.name.toLowerCase() === name.toLowerCase()) : undefined;
    return typeof mat?.props.color === 'string' ? mat.props.color : null;
  };
  const fill = (role: string) => materialColor(role) ?? colorOf(role);
  const colorOf = (role: string) => role === 'glass' ? spec.colors.glass : role === 'leaf' ? spec.colors.leaf : role === 'handle' || role === 'hardware' ? spec.hardware?.color ?? '#5a5a5a' : role === 'board' ? '#dcdcdc' : spec.colors.frame;

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Doors and windows">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Doors and windows</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}>
                <button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>
                  {t.name}<span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span>
                </button>
              </li>
            ))}
            <li><button type="button" className="tree-item" onClick={() => startNew('door')}><Plus size={12} /> New door</button></li>
            <li><button type="button" className="tree-item" onClick={() => startNew('window')}><Plus size={12} /> New window</button></li>
          </ul>
          <div className="joinery-edit">
            <div className="joinery-form">
              <label className="field-line"><span className="eyebrow">Name</span><input value={spec.name} onChange={(e) => set({ name: e.target.value })} /></label>
              <label className="field-line"><span className="eyebrow">Mark</span><input value={spec.mark} onChange={(e) => set({ mark: e.target.value })} /></label>
              <Mm label="Width" value={spec.width} onChange={(v) => set({ width: v })} />
              <Mm label="Height" value={spec.height} onChange={(v) => set({ height: v })} />
              {spec.kind === 'window' && <Mm label="Sill" value={spec.sillHeight} onChange={(v) => set({ sillHeight: v })} />}
              <label className="field-line"><span className="eyebrow">Style</span>
                <select data-field="style" value="" title="Sets the shape, the glazing bars and the ornaments at once; each stays editable" onChange={(e) => {
                  const st = JOINERY_STYLES[e.target.value];
                  if (!st) return;
                  const patch = st.apply(spec);
                  set({ ...patch, details: styleDetails(e.target.value, spec.details ?? {}) });
                  if (patch.columns) setCols(patch.columns.join(' '));
                  if (patch.rows) setRows(patch.rows.join(' '));
                }}>
                  <option value="">Apply a style…</option>
                  {Object.entries(JOINERY_STYLES).filter(([k]) => spec.kind === 'window' || k !== 'oculus').map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
                </select></label>
              <ShapeFields spec={spec} onChange={set} />
              <Mm label="Frame depth" value={spec.frame.depth} onChange={(v) => set({ frame: { ...spec.frame, depth: v } })} />
              <Mm label="Frame width" value={spec.frame.width} onChange={(v) => set({ frame: { ...spec.frame, width: v } })} />
              <label className="field-line"><span className="eyebrow">Columns of panes</span><input data-field="columns" value={cols} title="Relative widths, e.g. 1 1 or 2 1" onChange={(e) => setGrid(e.target.value, rows)} /></label>
              <label className="field-line"><span className="eyebrow">Rows of panes</span><input value={rows} title="Relative heights, bottom to top" onChange={(e) => setGrid(cols, e.target.value)} /></label>
              <p className="eyebrow">Panels · seen from outside</p>
              <table className="panel-grid">
                <tbody>
                  {[...spec.rows.keys()].reverse().map((row) => (
                    <tr key={row}>
                      {spec.columns.map((_, col) => (
                        <td key={col}>
                          <select value={cellOp(col, row)} onChange={(e) => setCell(col, row, e.target.value)}>
                            {operations.map((o) => <option key={o} value={o}>{DOOR_HAND_LABELS[o] ?? o}</option>)}
                          </select>
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
              <PartMaterials spec={spec} onChange={(materials) => set({ materials })} />
              {!spec.materials?.frame && <label className="field-line"><span className="eyebrow">Frame</span><input type="color" value={spec.colors.frame} onChange={(e) => set({ colors: { ...spec.colors, frame: e.target.value } })} /></label>}
              <LintelRuleEditor inherit rule={spec.lintel ?? null} onChange={(r) => set({ lintel: r })} />
              <p className="eyebrow">Details</p>

              <label className="field-line"><span className="eyebrow">Reveal finish</span>
                <select value={details.reveal?.mode ?? 'wall'} onChange={(e) => setDetails({ reveal: { ...(details.reveal ?? { thickness: 0.015 }), mode: e.target.value as Reveal['mode'] } })}>
                  <option value="wall">The wall's finish layers</option><option value="material">A material</option><option value="none">None</option>
                </select>
              </label>
              {details.reveal?.mode === 'material' && <>
                <Material label="Reveal material" value={details.reveal.material} onChange={(material) => setDetails({ reveal: { ...details.reveal!, material } })} />
                <Mm label="Reveal thickness" value={details.reveal.thickness} onChange={(v) => setDetails({ reveal: { ...details.reveal!, thickness: v } })} />
              </>}
              {spec.kind === 'window' && <SillFields label="Interior sill" sill={details.sillInside ?? null} fallback={{ ...SILL_IN, projection: spec.board.interior || SILL_IN.projection }} onChange={(v) => setDetails({ sillInside: v })} />}
              {spec.kind === 'window' && <SillFields label="Exterior sill" sill={details.sillOutside ?? null} fallback={{ ...SILL_OUT, projection: spec.board.exterior || SILL_OUT.projection }} slope onChange={(v) => setDetails({ sillOutside: v })} />}
              <label className="field-line check"><input type="checkbox" checked={details.surround?.on ?? false} onChange={(e) => setDetails({ surround: { ...(details.surround ?? SURROUND), on: e.target.checked } })} /><span>Surround on the facade</span></label>
              {details.surround?.on && <>
                <Mm label="Surround width" value={details.surround.width} onChange={(v) => setDetails({ surround: { ...details.surround!, width: v } })} />
                <Mm label="Surround projection" value={details.surround.projection} onChange={(v) => setDetails({ surround: { ...details.surround!, projection: v } })} />
                {spec.kind === 'window' && <label className="field-line check"><input type="checkbox" checked={details.surround.sill} onChange={(e) => setDetails({ surround: { ...details.surround!, sill: e.target.checked } })} /><span>Around the sill too</span></label>}
                <Material label="Surround material" value={details.surround.material} onChange={(material) => setDetails({ surround: { ...details.surround!, material } })} />
              </>}
              {spec.kind === 'door' && <label className="field-line check"><input type="checkbox" checked={details.threshold?.on ?? false} onChange={(e) => setDetails({ threshold: { ...(details.threshold ?? THRESHOLD), on: e.target.checked } })} /><span>Threshold</span></label>}
              {spec.kind === 'door' && details.threshold?.on && <>
                <Mm label="Threshold height" value={details.threshold.height} onChange={(v) => setDetails({ threshold: { ...details.threshold!, height: v } })} />
                <Material label="Threshold material" value={details.threshold.material} onChange={(material) => setDetails({ threshold: { ...details.threshold!, material } })} />
              </>}
              <DressingFields kind={spec.kind} height={spec.height} details={details} onChange={setDetails} />
              <HardwareFields spec={spec} onChange={set} />
              {spec.kind === 'door' && !spec.materials?.leaf && <label className="field-line"><span className="eyebrow">Leaf</span><input type="color" value={spec.colors.leaf} onChange={(e) => set({ colors: { ...spec.colors, leaf: e.target.value } })} /></label>}
              <TypeParams typeId={editing} kind={spec.kind} />
            </div>
            <div className="joinery-preview">
              <Strokes drawing={outside ? preview?.drawings?.exterior : preview?.drawings?.elevation} w={W} h={H} flipX label={outside ? 'Elevation from outside' : 'Elevation from inside'}>
                {(sx, sy, kk) => !outside && boxes.map((b, i) => b.profile
                  ? <path key={i} d={[b.profile, ...(b.holes ?? [])].map((l) => loopPath(l, sx, sy)).join(' ')} fillRule="evenodd" fill={fill(b.role)} stroke="none" opacity={b.role === 'glass' ? 0.7 : 1} />
                  : <rect key={i} x={Math.min(sx(b.x0), sx(b.x1))} y={sy(b.z1)} width={(b.x1 - b.x0) * kk} height={(b.z1 - b.z0) * kk} fill={fill(b.role)} stroke="none" opacity={b.role === 'glass' ? 0.7 : 1} />)}
              </Strokes>
              <div className="joinery-small">
                <figure><Strokes drawing={preview?.drawings?.plan} w={150} h={90} flipX label="Plan" /><figcaption className="muted small">Plan</figcaption></figure>
                <figure><Strokes drawing={preview?.drawings?.section} w={90} h={150} label="Section" /><figcaption className="muted small">Section</figcaption></figure>
              </div>
              <div className="row joinery-symbols">
                <label className="small"><input type="checkbox" data-field="outside" checked={outside} onChange={(e) => setOutside(e.target.checked)} /> From outside</label>
                <select className="mini-select" title="Opening symbols in elevation" value={preview?.symbols?.convention ?? 'handle'} onChange={(e) => void execute('architecture.joinerySymbols', { convention: e.target.value }).then(() => setSpec({ ...spec }))}>
                  <option value="handle">Apex at the handle (makers)</option><option value="hinge">Apex at the hinges (DIN 1356)</option>
                </select>
                <label className="small"><input type="checkbox" checked={preview?.symbols?.hardware ?? false} onChange={(e) => void execute('architecture.joinerySymbols', { hardware: e.target.checked }).then(() => setSpec({ ...spec }))} /> Hardware</label>
              </div>
              <p className="muted small">IFC: {preview?.ifcType ?? '—'}</p>
              {preview?.problem && <p className="error-text">{preview.problem}</p>}
            </div>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while elements use it)" onClick={() => void deleteType(editing).then((ok) => { if (ok) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
              {editing && <button type="button" className="btn" onClick={() => useUi.setState(spec.kind === 'door' ? { doorTypeId: editing } : { windowTypeId: editing })}>Place with it</button>}
              <button type="button" className="btn primary" disabled={Boolean(preview?.problem)} onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}

/** What a type's parameters offer to add at once: the fire rating in IFC's own set (its schedule row reads it), a manufacturer, a link. */
const PARAM_PRESETS = (kind: 'door' | 'window'): { label: string; name: string; pset?: string }[] => [
  { label: 'Fire rating', name: 'FireRating', pset: kind === 'door' ? 'Pset_DoorCommon' : 'Pset_WindowCommon' },
  { label: 'Manufacturer', name: 'Manufacturer' },
  { label: 'Product code', name: 'Product code' },
  { label: 'Website', name: 'Website' },
];

/**
 * The type's own parameters (decision 251): a fire rating, a manufacturer, a website, any of the user's; its doors or
 * windows inherit them, and the joinery schedules show each as a row. Saved as they are typed, the type being in the
 * project (a new type has them once saved).
 */
function TypeParams({ typeId, kind }: { typeId: string | null; kind: 'door' | 'window' }) {
  const type = useSession((s) => (typeId ? s.nodes.get(typeId) : undefined));
  if (!type) return <><p className="eyebrow">Parameters · in schedules</p><p className="muted small">Save the type to give it parameters (a fire rating, a manufacturer…).</p></>;
  const has = new Set(((Array.isArray(type.props.params) ? type.props.params : []) as { name: string }[]).map((p) => p.name.toLowerCase()));
  const presets = PARAM_PRESETS(kind).filter((p) => !has.has(p.name.toLowerCase()));
  return (
    <div className="type-params" data-testid="joinery-params">
      <p className="eyebrow">Parameters · in schedules</p>
      {presets.length > 0 && (
        <div className="row wrap">
          {presets.map((p) => (
            <button key={p.name} type="button" className="chip" title={p.pset ? `${p.pset}.${p.name}, as IFC keeps it` : 'A text parameter of the type'}
              onClick={() => void execute('element.setParam', { ids: [type.id], name: p.name, value: '', ...(p.pset ? { pset: p.pset } : { kind: 'text' }) })}>
              <Plus size={11} /> {p.label}
            </button>
          ))}
        </div>
      )}
      <ParamsSection node={type} ids={() => [type.id]} />
    </div>
  );
}
