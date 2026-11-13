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
import { StyleSelect, sameAs } from '../../shell/StyleSelect';
import { deleteType } from '../core/deleteType';
import { SaveTypeToLibrary } from '../core/SaveTypeToLibrary';
import { LintelRuleEditor } from './LintelRule';
import type { Rule } from './lintels';
import { BayFields, type Bay } from './BayFields';
import { JoineryPreview3D } from './JoineryPreview3D';
import { barWidths, fitBars, leafPairs, merge, neighbour, normalised, resized, shifted, split, withBar, type Bars, type GridSpec } from './joineryGrid';
import { JoineryDataButton } from './JoineryData';
import { ProfilePickerField } from '../core/profileLibraries/ProfilePickerField';
import { GARAGE_LABELS, GARAGE_OPERATIONS, garageName, garageOf, sizeLabel, withGarageKind, type Garage, type GarageOperation } from './garageDoors';

interface Panel { col: number; row: number; operation: string; colSpan?: number; rowSpan?: number; glazed?: boolean; infill?: string | null }
/** A profile system's finer shapes (decision 370): the frame's rebate, the sash lapping over it, glazing beads, a drip. */
interface Profile { system: string; rebate: number; overlap: number; bead: number; drip: boolean }
interface LibrarySection { name?: string; width: number; depth: number; [key: string]: unknown }

/**
 * A hinged door leaf's operation as the schedules name its hand (report #91) and a window's: looking from the handle towards the
 * hinges, a leaf opening to the left is a left swing. The operation ids are IFC's hand (hinges on the left of someone
 * pushing it open: the left of the Panels grid, seen from outside), so swing-left is a right swing; a window's hand is
 * seen from inside, the side its sashes open to, so side-left is hung right.
 */
const HAND_LABELS: Record<string, string> = {
  'swing-left': 'Swing right (hinges at left)', 'swing-right': 'Swing left (hinges at right)',
  'double-acting-left': 'Double acting right (hinges at left)', 'double-acting-right': 'Double acting left (hinges at right)',
  'folding-left': 'Folding right (hinges at left)', 'folding-right': 'Folding left (hinges at right)',
  // A window's hand seen from inside, the side its sashes open to; the grid is seen from outside.
  'side-left': 'Side hung right (hinges at left)', 'side-right': 'Side hung left (hinges at right)',
  'tilt-turn-left': 'Tilt & turn right (hinges at left)', 'tilt-turn-right': 'Tilt & turn left (hinges at right)',
  // Garage doors (topic #48): one panel filling the opening.
  ...GARAGE_LABELS,
};
/** Two leaves meeting: butt, a rebated meeting stile or a cover strip (astragal); the active leaf opens first with the handle, the passive one has flush bolts. */
export interface Meeting { kind: 'butt' | 'rebated' | 'astragal'; active: 'left' | 'right'; width?: number | null; thickness?: number | null; bolts: boolean }
interface Spec {
  kind: 'door' | 'window'; name: string; mark: string; width: number; height: number; sillHeight: number;
  frame: { depth: number; width: number; mullion: number; transom: number; offset: number };
  sash: { width: number; depth: number; leafThickness: number; glassThickness: number };
  columns: number[]; rows: number[]; panels: Panel[];
  /** One width per column boundary (left to right) and per row boundary (bottom up), metres: null the frame's, 0 none (decision 466). */
  mullions?: Bars; transoms?: Bars;
  /** How two door leaves meeting over a boundary without a mullion are made; one setting for the type. */
  meeting?: Meeting | null;
  board: { interior: number; exterior: number }; threshold: number;
  colors: { frame: string; glass: string; leaf: string }; isExternal: boolean; thermalTransmittance?: number | null;
  lintel?: Rule | null;
  details?: Details | null;
  shape?: Shape | null;
  muntins?: Muntins | null;
  hardware?: Hardware | null;
  materials?: Materials | null;
  /** A window that projects from its wall (report #60): a bay, a bow or a box. */
  bay?: Bay | null;
  profile?: Profile | null;
  /** The library profiles whose real outlines the frame's and the sash's straight members take (decision 417), with their sizes. */
  frameSection?: LibrarySection | null;
  sashSection?: LibrarySection | null;
  /** The style last applied (a key of JOINERY_STYLES; report #111): its select shows it, "(modified)" once a field differs. */
  style?: string | null;
  /** The frame's place across the wall as a value (report #76); none: the frame's offset (centred). */
  position?: Position | null;
  /** Frameless toughened glass, no frame or sash (tester's question 7): what holds a leaf, the glass, the gaps; none: framed. */
  frameless?: Frameless | null;
  /** A garage door's settings (topic #48, decision 530), on a door whose panel is a garage door's operation. */
  garage?: Garage | null;
}
/** How a frameless leaf is held (pivots with patch fittings, or glass hinges), the glass's thickness, the gaps round it and under a leaf. */
interface Frameless { fixing: 'pivot' | 'hinges' | 'floor-spring'; glass: number; gap: number; floor: number;
  /** On a floor spring (topic #28): the model ("auto" or an id of the catalog's floorSprings), the pivot's offset from the leaf's edge, the bottom fitting, the hold-open angle (degrees, 0 none). */
  spring?: string; pivot?: number; bottom?: 'patch' | 'strap'; holdOpen?: number }
/** A floor spring the engine offers (indicative): the heaviest and widest leaf it carries, its closing force (EN 1154). */
interface FloorSpringModel { id: string; maxMass: number; maxWidth: number; strength: string }
const FRAMELESS: Frameless = { fixing: 'pivot', glass: 0.012, gap: 0.004, floor: 0.008 };
/** What a frameless pane may do: a door's leaves swing, swing both ways or slide; a window's panes are fixed. */
const FRAMELESS_DOOR_OPS = ['swing-left', 'swing-right', 'double-acting-left', 'double-acting-right', 'door-sliding-left', 'door-sliding-right', 'fixed'];
/** From the wall's exterior finish face, its core's exterior face or its middle plane, the frame (its outer face; from the middle, its centre) this far back. */
export interface Position { from: 'exterior' | 'core' | 'centre' | 'interior'; distance: number }
/** The parts' materials by name (report #76); a part without one keeps its colour. */
interface Materials { frame?: string | null; sash?: string | null; glass?: string | null; leaf?: string | null; board?: string | null; hardware?: string | null }
type MaterialPart = keyof Materials;
/** `transom: false` (report #112): no transom at the spring line, the top row of panes following the arch. */
interface Shape { kind: 'rect' | 'round' | 'segmental' | 'elliptical' | 'radiused' | 'pointed' | 'gable' | 'circle'; rise?: number | null; bars: 'none' | 'sunburst' | 'mullions' | 'cross'; spokes: number; transom?: boolean | null }
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
  // Report #112: one glazed sash following the arch all round, side-hung, no bar under the arch.
  'arched-casement': { label: 'Arched casement', apply: (s) => ({ shape: { ...SHAPE, kind: 'segmental', rise: Math.round(s.width / 4 * 1000) / 1000, transom: false }, muntins: null, columns: [1], rows: [1], panels: [{ col: 0, row: 0, operation: s.kind === 'door' ? 'swing-left' : 'side-left' }] }) },
  baroque: { label: 'Baroque', apply: () => ({ shape: { ...SHAPE, kind: 'round', bars: 'sunburst', spokes: 5 }, muntins: { ...MUNTINS, pattern: 'grid', across: 2, up: 3 } }) },
  gothic: { label: 'Gothic', apply: () => ({ shape: { ...SHAPE, kind: 'pointed', rise: null, bars: 'mullions' }, muntins: null }) },
  tudor: { label: 'Tudor', apply: (s) => ({ shape: { ...SHAPE, kind: 'pointed', rise: Math.round(s.width * 0.3 * 1000) / 1000, bars: 'mullions' }, muntins: { ...MUNTINS, pattern: 'diamond', across: 4 } }) },
  prairie: { label: 'Prairie', apply: () => ({ shape: null, muntins: { ...MUNTINS, pattern: 'prairie', margin: 0.1 } }) },
  traditional: { label: 'Traditional', apply: () => ({ shape: null, muntins: { ...MUNTINS, pattern: 'grid', across: 1, up: 2 } }) },
  oculus: { label: 'Round window', apply: (s) => ({ shape: { ...SHAPE, kind: 'circle', bars: 'cross' }, muntins: null, height: s.width, columns: [1], rows: [1], panels: [{ col: 0, row: 0, operation: 'fixed' }] }) },
  // Tester's question 7: toughened glass alone, a leaf on pivots with patch fittings and a pull bar (a window: one fixed pane).
  'frameless-glass': { label: 'Frameless glass', apply: (s) => ({ frameless: { ...FRAMELESS }, shape: null, muntins: null, bay: null, profile: null, frameSection: null, sashSection: null,
    columns: [1], rows: [1], panels: [{ col: 0, row: 0, operation: s.kind === 'door' ? 'swing-left' : 'fixed' }] }) },
};
// Every other style is framed: choosing one takes frameless glass away.
for (const [k, st] of Object.entries(JOINERY_STYLES)) if (k !== 'frameless-glass') { const apply = st.apply; st.apply = (s) => ({ frameless: null, ...apply(s) }); }

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
/** Whether a type no longer is what its style makes of it (report #111): a field the style sets changed since. */
export function styleModified(spec: Spec): boolean {
  const st = spec.style ? JOINERY_STYLES[spec.style] : undefined;
  return st != null && !sameAs({ ...st.apply(spec), details: styleDetails(spec.style!, spec.details ?? {}) }, spec);
}

export interface Preview { problem: string | null; boxes: { role: string; x0: number; x1: number; z0: number; z1: number; y0: number; y1: number; profile?: number[] | null; holes?: number[][] | null }[]; panels: { operation: string; x0: number; x1: number; z0: number; z1: number; col?: number; row?: number; colSpan?: number; rowSpan?: number; infill?: string }[]; ifcType: string; grid?: { inner: number[]; cols: [number, number][]; rows: [number, number][]; mullions?: number[]; transoms?: number[]; meetings?: boolean[]; pairs?: { boundary: number; active: number; passive: number; kind: string }[] } | null }
export interface Stroke { pts: [number, number][]; weight: string; closed: boolean; dashed: boolean }
export interface Drawing { strokes: Stroke[] }
export interface Drawings { elevation: Drawing; exterior: Drawing; plan: Drawing; section: Drawing }
export type Preview2 = Preview & { drawings?: Drawings | null; symbols?: { convention: string; hardware: boolean };
  /** A garage door's panels and the room it needs, as the engine works them out (topic #48). */
  garage?: { panels: number; headroom: number; backroom: number; sideroom: number } | null };

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
          fill="none" stroke="#1e2a3a" strokeWidth={width(s.weight)} strokeDasharray={s.dashed ? '4 3' : undefined} strokeLinejoin="round" pointerEvents="none" />
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
  // Frameless glass (tester's question 7) has only its glass, its fittings and a window's boards.
  const parts = PARTS.filter((p) => (spec.kind === 'door' ? !p.window : !p.door) && (!spec.frameless || ['glass', 'hardware', 'board'].includes(p.part)));
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
      {sh.kind !== 'rect' && sh.kind !== 'circle' && <label className="field-line check"><input type="checkbox" data-field="shape-transom" checked={sh.transom !== false} onChange={(e) => setShape({ transom: e.target.checked ? null : false })} /><span>Transom at the springing</span></label>}
      {sh.kind !== 'rect' && (sh.kind === 'circle' || sh.transom !== false) && <label className="field-line"><span className="eyebrow">{sh.kind === 'circle' ? 'Bars' : 'Bars in the head'}</span>
        <select data-field="shape-bars" value={sh.bars} onChange={(e) => setShape({ bars: e.target.value as Shape['bars'] })}>
          <option value="none">None</option><option value="sunburst">Sunburst</option><option value="mullions">{sh.kind === 'circle' ? 'Upright' : 'The mullions carried up'}</option><option value="cross">Cross</option>
        </select></label>}
      {sh.kind !== 'rect' && (sh.kind === 'circle' || sh.transom !== false) && sh.bars === 'sunburst' && <Num label="Spokes" value={sh.spokes} integer min={1} onChange={(v) => setShape({ spokes: v })} />}
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

/** A garage door's size the list offers (indicative). */
interface GarageSize { region: string; width: number; height: number }

/**
 * A garage door (topic #48, decision 530): its kind, a common size, its panels and finish, a wicket door, the track (sectional),
 * the roll box (roller) or the mechanism (up-and-over), the room it needs (blank: the kind's, as the engine works it out).
 */
function GarageFields({ spec, sizes, preview, onChange }: { spec: Spec; sizes?: GarageSize[]; preview: Preview2 | null; onChange: (patch: Partial<Spec>) => void }) {
  const op = garageOf(spec);
  if (!op) return null;
  const g: Garage = spec.garage ?? {};
  const setG = (p: Partial<Garage>) => onChange({ garage: { ...g, ...p } });
  const auto = preview?.garage;
  const room = (key: 'headroom' | 'backroom' | 'sideroom', label: string) => (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <LengthField metres={g[key] ?? auto?.[key] ?? 0} onCommit={(m) => setG({ [key]: m } as Partial<Garage>)} />
      {g[key] != null && <button type="button" data-testid={`garage-${key}-auto`} title="The kind's, as the engine works it out" onClick={() => setG({ [key]: null } as Partial<Garage>)}>Auto</button>}
    </label>
  );
  return (
    <div data-testid="garage-fields">
      <p className="eyebrow">Garage door</p>
      <label className="field-line"><span className="eyebrow">Kind</span>
        <select data-field="garage-kind" value={op} onChange={(e) => onChange(withGarageKind(spec, e.target.value as GarageOperation))}>
          {GARAGE_OPERATIONS.map((o) => <option key={o} value={o}>{GARAGE_LABELS[o]}</option>)}
        </select></label>
      {sizes && sizes.length > 0 && <label className="field-line"><span className="eyebrow">Common size</span>
        <select data-field="garage-size" title="Common sizes, indicative: check the maker's" value=""
          onChange={(e) => { const z = sizes[Number(e.target.value)]; if (z) onChange({ width: z.width, height: z.height, name: garageName(op, z.width, z.height) }); }}>
          <option value="">{`${Math.round(spec.width * 1000)} × ${Math.round(spec.height * 1000)}`}</option>
          {sizes.map((z, i) => <option key={i} value={i}>{sizeLabel(z)}</option>)}
        </select></label>}
      {op !== 'roller' && <label className="field-line"><span className="eyebrow">Finish</span>
        <select data-field="garage-finish" value={g.finish ?? 'ribbed'} onChange={(e) => setG({ finish: e.target.value as Garage['finish'] })}>
          <option value="ribbed">Ribbed</option><option value="flat">Flat</option><option value="v-groove">V-groove</option><option value="glazed-top">Glazed top panel</option>
        </select></label>}
      {(op === 'sectional-overhead' || op.startsWith('sectional-side')) && <label className="field-line"><span className="eyebrow">Panels</span>
        <NumberField value={g.sections ?? auto?.panels ?? 4} integer min={2} onCommit={(n) => setG({ sections: n ?? null })} />
        {g.sections != null && <button type="button" title="From the door's height" onClick={() => setG({ sections: null })}>Auto</button>}</label>}
      {op === 'sectional-overhead' && <>
        <label className="field-line"><span className="eyebrow">Track</span>
          <select data-field="garage-track" value={g.track ?? 'standard'} onChange={(e) => setG({ track: e.target.value as Garage['track'] })}>
            <option value="standard">Standard</option><option value="low-headroom">Low headroom</option><option value="high-lift">High lift</option>
          </select></label>
        {g.track === 'high-lift' && <Mm label="Extra lift" value={g.lift ?? 0.6} onChange={(v) => setG({ lift: v })} />}
        <label className="field-line check"><input type="checkbox" data-field="garage-wicket" checked={!!g.wicket} onChange={(e) => setG({ wicket: e.target.checked })} /><span>Wicket door</span></label>
        {g.wicket && <>
          <Mm label="Wicket width" value={g.wicketWidth ?? 0.9} onChange={(v) => setG({ wicketWidth: v })} />
          <label className="field-line"><span className="eyebrow">Wicket side</span>
            <select data-field="garage-wicket-side" value={g.wicketSide ?? 'left'} onChange={(e) => setG({ wicketSide: e.target.value as Garage['wicketSide'] })}>
              <option value="left">Left, seen from inside</option><option value="centre">Centre</option><option value="right">Right, seen from inside</option>
            </select></label>
        </>}
      </>}
      {op === 'roller' && <label className="field-line"><span className="eyebrow">Roll box</span>
        <select data-field="garage-box" value={g.box ?? 'inside'} onChange={(e) => setG({ box: e.target.value as Garage['box'] })}>
          <option value="inside">Inside</option><option value="outside">Outside</option>
        </select></label>}
      {op === 'up-and-over' && <label className="field-line"><span className="eyebrow">Mechanism</span>
        <select data-field="garage-mechanism" value={g.mechanism ?? 'canopy'} onChange={(e) => setG({ mechanism: e.target.value as Garage['mechanism'] })}>
          <option value="canopy">Canopy</option><option value="retractable">Retractable</option>
        </select></label>}
      {room('headroom', 'Headroom over the opening')}
      {room('backroom', 'Backroom')}
      {room('sideroom', 'Sideroom')}
      {op !== 'roller' && <label className="field-line check"><input type="checkbox" data-field="garage-tracks" checked={g.tracks ?? true} onChange={(e) => setG({ tracks: e.target.checked })} /><span>Tracks in 3D</span></label>}
      <p className="muted small">Sizes and clearances are indicative: check the maker's.</p>
    </div>
  );
}

/** Frameless glass (tester's question 7): on or off, what holds a door's leaves, the glass, the gaps. */
function FramelessFields({ spec, springs, onChange }: { spec: Spec; springs?: FloorSpringModel[]; onChange: (patch: Partial<Spec>) => void }) {
  const fl = spec.frameless;
  const door = spec.kind === 'door';
  const setFl = (p: Partial<Frameless>) => onChange({ frameless: { ...(fl ?? FRAMELESS), ...p } });
  return (
    <>
      <label className="field-line check" title="Toughened glass alone: no frame, no sash"><input type="checkbox" data-field="frameless" checked={!!fl}
        onChange={(e) => onChange(e.target.checked
          ? { frameless: { ...FRAMELESS }, shape: null, muntins: null, bay: null, profile: null, frameSection: null, sashSection: null,
              panels: spec.panels.map((p) => ({ ...p, operation: door ? (FRAMELESS_DOOR_OPS.includes(p.operation) ? p.operation : 'swing-left') : 'fixed' })) }
          : { frameless: null })} /><span>Frameless glass</span></label>
      {fl && <>
        {door && <label className="field-line"><span className="eyebrow">Fixing</span>
          <select data-field="frameless-fixing" value={fl.fixing} onChange={(e) => setFl({ fixing: e.target.value as Frameless['fixing'] })}>
            <option value="pivot">Pivots and patch fittings</option><option value="hinges">Glass hinges</option><option value="floor-spring">Floor spring (floor hinge)</option>
          </select></label>}
        {door && fl.fixing === 'floor-spring' && <FloorSpringFields spec={spec} fl={fl} springs={springs} onChange={onChange} setFl={setFl} />}
        <Mm label="Glass thickness" value={fl.glass} onChange={(v) => setFl({ glass: v })} />
        <Mm label="Gap round the glass" value={fl.gap} onChange={(v) => setFl({ gap: v })} />
        {door && <Mm label="Gap under the leaf" value={fl.floor} onChange={(v) => setFl({ floor: v })} />}
      </>}
    </>
  );
}

/**
 * A frameless leaf on a floor spring (topic #28): the spring's model (auto: the lightest that carries the leaf; the engine
 * refuses one too light), single or double action (the leaves' operations), the pivot's offset from the edge, the bottom
 * fitting, the hold-open angle.
 */
function FloorSpringFields({ spec, fl, springs, onChange, setFl }: { spec: Spec; fl: Frameless; springs?: FloorSpringModel[];
  onChange: (patch: Partial<Spec>) => void; setFl: (p: Partial<Frameless>) => void }) {
  const leaves = spec.panels.filter((p) => p.operation.startsWith('swing') || p.operation.startsWith('double-acting'));
  const action = leaves.length > 0 && leaves.every((p) => p.operation.startsWith('double-acting')) ? 'double' : 'single';
  const setAction = (a: string) => onChange({ panels: spec.panels.map((p) => {
    const hand = p.operation.endsWith('-right') ? 'right' : 'left';
    return p.operation.startsWith('swing') || p.operation.startsWith('double-acting') ? { ...p, operation: `${a === 'double' ? 'double-acting' : 'swing'}-${hand}` } : p;
  }) });
  return (
    <div data-testid="floor-spring-fields">
      <label className="field-line"><span className="eyebrow">Floor spring</span>
        <select data-field="floor-spring-model" value={fl.spring ?? 'auto'} onChange={(e) => setFl({ spring: e.target.value })}>
          <option value="auto">Automatic (the lightest that carries the leaf)</option>
          {(springs ?? []).map((m) => <option key={m.id} value={m.id}>{`${m.maxMass} kg, ${Math.round(m.maxWidth * 1000)} mm, ${m.strength}`}</option>)}
        </select></label>
      <label className="field-line"><span className="eyebrow">Opening action</span>
        <select data-field="floor-spring-action" value={action} onChange={(e) => setAction(e.target.value)}>
          <option value="single">Single action</option><option value="double">Double action (swings both ways)</option>
        </select></label>
      <Mm label="Pivot from the edge" value={fl.pivot ?? 0.055} onChange={(v) => setFl({ pivot: v })} />
      <label className="field-line"><span className="eyebrow">Bottom fitting</span>
        <select data-field="floor-spring-bottom" value={fl.bottom ?? 'patch'} onChange={(e) => setFl({ bottom: e.target.value as Frameless['bottom'] })}>
          <option value="patch">Bottom patch fitting</option><option value="strap">Heel strap</option>
        </select></label>
      <label className="field-line"><span className="eyebrow">Hold-open</span>
        <select data-field="floor-spring-hold" value={String(fl.holdOpen ?? 0)} onChange={(e) => setFl({ holdOpen: Number(e.target.value) })}>
          <option value="0">None</option>
          {[80, 90, 105, 120].map((a) => <option key={a} value={a}>{`${a}°`}</option>)}
        </select></label>
      <p className="muted small">Indicative models; check the maker's data for the leaf's weight and the closing force.</p>
    </div>
  );
}

/** The handles (report #48): their style, a door handle's height, their colour. */
function HardwareFields({ spec, onChange }: { spec: Spec; onChange: (patch: Partial<Spec>) => void }) {
  const hw = spec.hardware ?? HARDWARE;
  return (
    <>
      <label className="field-line"><span className="eyebrow">Handle</span>
        <select data-field="handle" value={hw.handle} onChange={(e) => onChange({ hardware: { ...hw, handle: e.target.value as Hardware['handle'] } })}>
          <option value="auto">{spec.frameless ? 'Pull bar (default)' : spec.kind === 'door' ? 'Lever (default)' : 'Window handle (default)'}</option>
          <option value="lever">Lever</option><option value="knob">Knob</option><option value="pull">Pull bar</option><option value="window">Window handle</option><option value="none">None</option>
        </select></label>
      {spec.kind === 'door' && hw.handle !== 'none' && <Mm label="Handle height" value={hw.height} onChange={(v) => onChange({ hardware: { ...hw, height: v } })} />}
      {hw.handle !== 'none' && <label className="field-line"><span className="eyebrow">Handle colour</span><input type="color" value={hw.color ?? '#5a5a5a'} onChange={(e) => onChange({ hardware: { ...hw, color: e.target.value } })} /></label>}
    </>
  );
}

/** A sill's fields: on or off, then its sizes (mm), slope (°, the exterior one) and material. */
function SillFields({ label, sill, fallback, slope = false, board = 0, onChange, onBoard }: { label: string; sill: Sill | null; fallback: Sill; slope?: boolean; board?: number; onChange: (s: Sill) => void; onBoard?: (m: number) => void }) {
  // On when the detailed sill is, or (none set) when the type's plain board is drawn; off takes both away (a tester's wish: no sills).
  const on = sill ? sill.on : board > 0;
  const cur = sill ?? fallback;
  return (
    <>
      <label className="field-line check"><input type="checkbox" data-sill={label} checked={on} onChange={(e) => { onChange({ ...cur, on: e.target.checked }); if (!e.target.checked) onBoard?.(0); }} /><span>{label}</span></label>
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

const PROFILE_LABELS: Record<string, string> = { pvc: 'PVC', aluminium: 'Aluminium', timber: 'Timber', steel: 'Steel (slim)' };
/** A size kept as a relative weight: millimetres to a tenth. */
const roundW = (v: number) => Math.round(v * 10) / 10;

/** The columns' widths or the rows' heights in mm, each typed in; the last takes up the difference. */
function SizeRow({ label, sizes, field, onCommit }: { label: string; sizes: number[]; field: string; onCommit: (i: number, mm: number) => void }) {
  return (
    <div className="field-line size-row"><span className="eyebrow">{label}</span>
      <span className="row wrap" style={{ gap: 4 }}>
        {sizes.map((v, i) => (
          <input key={`${i}:${v}`} data-field={`${field}-${i}`} className="num mini" defaultValue={String(v)} title={i === sizes.length - 1 ? 'The last one takes up the rest' : 'mm'} style={{ width: 64 }}
            onBlur={(e) => { const n = Number(e.target.value.replace(',', '.')); if (n > 0 && Math.abs(n - v) > 0.05) onCommit(i, n); }}
            onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
        ))}
        <span className="muted small">mm</span>
      </span>
    </div>
  );
}

const INFILL_LABELS: Record<string, string> = { glass: 'Glass', solid: 'Solid panel' };

/**
 * The pane picked (decision 370): how it opens, what fills it (glass, or a solid panel: a door's lower panel, a window's
 * opaque infill), split in two at a distance from its left or bottom edge, merged with the pane beside it.
 */
function PaneEditor({ spec, preview, pane, operations, onPick, onPanels, onGrid, colSizes, rowSizes, at, setAt }: {
  spec: Spec; preview: Preview2 | null; pane: { col: number; row: number } | null; operations: string[];
  at: { col: string; row: string }; setAt: (a: { col: string; row: string }) => void;
  onPick: (p: { col: number; row: number } | null) => void; onPanels: (p: Panel[]) => void; onGrid: (g: GridSpec) => void; colSizes: number[]; rowSizes: number[];
}) {
  const g: GridSpec = { columns: spec.columns, rows: spec.rows, panels: spec.panels, frame: spec.frame, mullions: spec.mullions, transoms: spec.transoms };
  const panes = normalised(g) as Panel[];
  const cur = panes.find((p) => pane && p.col === pane.col && p.row === pane.row) ?? panes[0];
  const rect = preview?.panels.find((p) => p.col === cur?.col && p.row === cur?.row);
  if (!cur) return null;
  const width = rect ? Math.round((rect.x1 - rect.x0) * 1000) : 0, height = rect ? Math.round((rect.z1 - rect.z0) * 1000) : 0;
  const patch = (x: Partial<Panel>) => onPanels(panes.map((p) => (p.col === cur.col && p.row === cur.row ? { ...p, ...x } : p)));
  const infill = cur.infill ?? (spec.kind === 'door' && !['fixed'].includes(cur.operation) && !(catalogWindowOps.has(cur.operation)) ? (cur.glazed ? 'glass' : 'solid') : 'glass');
  const doSplit = (axis: 'col' | 'row') => {
    const total = axis === 'col' ? width : height;
    const value = Number((axis === 'col' ? at.col : at.row).replace(',', '.')) || total / 2;
    const r = split(g, axis === 'col' ? colSizes : rowSizes, cur, axis, value);
    if (!r) { notify('Each part must keep 50 mm at least.'); return; }
    onGrid(r.spec as unknown as GridSpec);
  };
  const doMerge = (side: 'left' | 'right' | 'below' | 'above') => {
    const r = merge(g, { cols: colSizes, rows: rowSizes }, cur, side);
    if (!r) return;
    onGrid(r.spec);
    // The merged pane picked: the one covering the merged pane's corner (grid lines may have gone, so it is looked up again).
    const corner = { col: Math.min(cur.col, side === 'left' ? cur.col - 1 : cur.col), row: Math.min(cur.row, side === 'below' ? cur.row - 1 : cur.row) };
    const merged = (normalised(r.spec) as Panel[]).find((p) => p.col <= corner.col && corner.col < p.col + (p.colSpan ?? 1) && p.row <= corner.row && corner.row < p.row + (p.rowSpan ?? 1));
    onPick(merged ? { col: merged.col, row: merged.row } : null);
  };
  // A side to merge with is shown as it is seen from inside (the elevation's view): left and right swap.
  const sides: { side: 'left' | 'right' | 'below' | 'above'; label: string }[] = [
    { side: 'right', label: '← Merge left' }, { side: 'left', label: 'Merge right →' }, { side: 'above', label: '↑ Merge up' }, { side: 'below', label: '↓ Merge down' }];
  return (
    <div className="pane-editor" data-testid="pane-editor">
      <p className="eyebrow"><span>Panes</span> · <span data-no-i18n>{panes.length}</span> · <span>Click one in the elevation</span></p>
      <div className="row wrap pane-chips">
        {panes.map((p) => (
          <button key={`${p.col}:${p.row}`} type="button" className={`chip${p === cur ? ' on' : ''}`} data-pane={`${p.col}:${p.row}`} onClick={() => onPick({ col: p.col, row: p.row })}>
            {p.col + 1}·{p.row + 1}{(p.colSpan ?? 1) > 1 || (p.rowSpan ?? 1) > 1 ? ` (${p.colSpan ?? 1}×${p.rowSpan ?? 1})` : ''}
          </button>
        ))}
      </div>
      <p className="muted small"><span>Pane</span> <span data-no-i18n>{cur.col + 1}·{cur.row + 1}: {width} × {height} mm</span></p>
      <label className="field-line"><span className="eyebrow">Opens</span>
        <select data-field="pane-operation" value={cur.operation} onChange={(e) => patch({ operation: e.target.value })}>
          {operations.map((o) => <option key={o} value={o}>{HAND_LABELS[o] ?? o}</option>)}
        </select></label>
      {!spec.frameless && <label className="field-line"><span className="eyebrow">Infill</span>
        <select data-field="pane-infill" value={infill} onChange={(e) => patch({ infill: e.target.value, glazed: e.target.value === 'glass' })}>
          {Object.entries(INFILL_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select></label>}
      <div className="field-line"><span className="eyebrow">Split</span>
        <span className="row wrap" style={{ gap: 4 }}>
          <input className="num mini" style={{ width: 64 }} data-field="split-col-at" placeholder={String(Math.round(width / 2))} value={at.col} onChange={(e) => setAt({ ...at, col: e.target.value })} title="mm from the pane's left edge (seen from outside)" />
          <button type="button" className="btn small" data-action="split-col" onClick={() => doSplit('col')}>Into columns</button>
          <input className="num mini" style={{ width: 64 }} data-field="split-row-at" placeholder={String(Math.round(height / 2))} value={at.row} onChange={(e) => setAt({ ...at, row: e.target.value })} title="mm from the pane's bottom edge" />
          <button type="button" className="btn small" data-action="split-row" onClick={() => doSplit('row')}>Into rows</button>
        </span></div>
      <div className="field-line"><span className="eyebrow">Merge</span>
        <span className="row wrap" style={{ gap: 4 }}>
          {sides.map(({ side, label }) => (
            <button key={side} type="button" className="btn small" data-action={`merge-${side}`} disabled={!neighbour(g, cur, side)} onClick={() => doMerge(side)}>{label}</button>
          ))}
        </span></div>
    </div>
  );
}
/** The window operations: a pane of a door that opens as one is a sash, glass unless asked. */
const catalogWindowOps = new Set(['side-left', 'side-right', 'tilt-turn-left', 'tilt-turn-right', 'bottom-hung', 'top-hung', 'pivot-horizontal', 'pivot-vertical', 'sliding-left', 'sliding-right', 'sliding-vertical']);

/**
 * The mullion or transom picked in the elevation (decision 466): its width, the frame's by default, or none, so two panes meet.
 * Its width shows as the engine drew it.
 */
function BarField({ bar, spec, onChange }: { bar: { axis: 'col' | 'row'; i: number } | null; spec: Spec; onChange: (v: number | null) => void }) {
  if (!bar) {
    const lines = spec.columns.length > 1 || spec.rows.length > 1;
    return lines ? <p className="muted small" data-testid="bar-hint">Click a mullion or a transom in the elevation to give it its own width.</p> : null;
  }
  const col = bar.axis === 'col';
  const own = (col ? spec.mullions : spec.transoms)?.[bar.i] ?? null;
  const effective = own ?? (col ? spec.frame.mullion : spec.frame.transom);
  return (
    <div className="pane-editor" data-testid="bar-field">
      <p className="eyebrow"><span>{col ? 'Mullion' : 'Transom'}</span> <span data-no-i18n>{bar.i + 1}</span></p>
      <Mm label="Width" value={effective} onChange={(v) => onChange(v < 0 ? null : v)} />
      <label className="field-line check"><input type="checkbox" data-field="bar-none" checked={own === 0} onChange={(e) => onChange(e.target.checked ? 0 : null)} /><span>{col ? 'No mullion' : 'No transom'}</span></label>
      <div className="field-line"><span />
        <button type="button" className="btn small" data-action="bar-default" disabled={own == null} title="Takes the width of the frame's mullions and transoms again" onClick={() => onChange(null)}>Default (frame's)</button>
      </div>
    </div>
  );
}

/**
 * How two door leaves meet where there is no mullion between them (decision 466): butt, a rebated meeting stile or a cover strip over
 * the joint; the active leaf (the one that opens first, with the handle) and flush bolts in the passive one. One setting for the type.
 */
function MeetingFields({ spec, preview, defaults, onChange }: { spec: Spec; preview: Preview2 | null; defaults?: Meeting & { astragalWidth?: number }; onChange: (m: Meeting) => void }) {
  const g: GridSpec = { columns: spec.columns, rows: spec.rows, panels: spec.panels, frame: spec.frame };
  const meets = (preview?.grid?.pairs?.length ?? 0) > 0 || leafPairs(g).some((i) => (spec.mullions?.[i] ?? spec.frame.mullion) === 0);
  if (!meets) return null;
  const m: Meeting = spec.meeting ?? { kind: 'butt', active: defaults?.active ?? 'right', bolts: false };
  const set = (patch: Partial<Meeting>) => onChange({ ...m, ...patch });
  const width = m.width ?? (m.kind === 'astragal' ? defaults?.astragalWidth ?? 0.045 : defaults?.width ?? 0.014);
  const thickness = m.thickness ?? defaults?.thickness ?? 0.012;
  const pairs = preview?.grid?.pairs ?? [];
  // A pair of double-acting leaves always meets butt.
  const butt = pairs.length > 0 && pairs.every((p) => p.kind === 'butt') && m.kind !== 'butt';
  return (
    <div className="pane-editor" data-testid="meeting-fields">
      <p className="eyebrow">Leaves meeting</p>
      <label className="field-line"><span className="eyebrow">Meeting</span>
        <select data-field="meeting-kind" value={m.kind} onChange={(e) => set({ kind: e.target.value as Meeting['kind'], width: null })}>
          <option value="butt">Butt</option><option value="rebated">Rebated</option><option value="astragal">Cover strip</option>
        </select></label>
      <label className="field-line"><span className="eyebrow">Active leaf</span>
        <select data-field="meeting-active" title="The leaf that opens first and has the handle, as the grid reads (the left one is hinged on the left, seen from outside)" value={m.active} onChange={(e) => set({ active: e.target.value as Meeting['active'] })}>
          <option value="left">Left</option><option value="right">Right</option>
        </select></label>
      {m.kind === 'rebated' && <Mm label="Rebate overlap" value={width} onChange={(v) => set({ width: v > 0 ? Math.min(v, 0.12) : null })} />}
      {m.kind === 'astragal' && <>
        <Mm label="Cover strip width" value={width} onChange={(v) => set({ width: v > 0 ? Math.min(v, 0.12) : null })} />
        <Mm label="Cover strip thickness" value={thickness} onChange={(v) => set({ thickness: v > 0 ? Math.min(v, 0.04) : null })} />
      </>}
      <label className="field-line check"><input type="checkbox" data-field="meeting-bolts" checked={m.bolts} onChange={(e) => set({ bolts: e.target.checked })} /><span>Flush bolts on the passive leaf</span></label>
      {butt && <p className="muted small">Double-acting leaves always meet butt.</p>}
    </div>
  );
}

/** Over the elevation: each pane to pick, outlined when picked; each mullion and transom to drag. */
function PaneOverlay({ preview, pane, drag, bar, sx, sy, kk, onPick, onBar, onDrag, onDrop }: {
  preview: Preview2 | null; pane: { col: number; row: number } | null; drag: { axis: 'col' | 'row'; i: number; from: number; at: number } | null;
  bar: { axis: 'col' | 'row'; i: number } | null; onBar: (b: { axis: 'col' | 'row'; i: number }) => void;
  sx: (x: number) => number; sy: (y: number) => number; kk: number;
  onPick: (p: { col: number; row: number }) => void; onDrag: (d: { axis: 'col' | 'row'; i: number; from: number; at: number } | null) => void; onDrop: (d: { axis: 'col' | 'row'; i: number; from: number; at: number }) => void;
}) {
  const grid = preview?.grid;
  if (!grid || !preview) return null;
  const [ix0, ix1, iz0, iz1] = grid.inner;
  // Back from the drawing's coordinates to the type's (its scale is linear, X perhaps mirrored).
  const toX = (svgX: number) => (svgX - sx(0)) / (sx(1) - sx(0));
  const toZ = (svgY: number) => (svgY - sy(0)) / (sy(1) - sy(0));
  const svgPoint = (e: React.PointerEvent<SVGElement>) => {
    const svg = (e.currentTarget as SVGElement).ownerSVGElement!;
    const m = svg.getScreenCTM()!.inverse();
    const p = new DOMPoint(e.clientX, e.clientY).matrixTransform(m);
    return p;
  };
  const lines: { axis: 'col' | 'row'; i: number; at: number }[] = [
    ...grid.cols.slice(0, -1).map((c, i) => ({ axis: 'col' as const, i, at: (c[1] + grid.cols[i + 1][0]) / 2 })),
    ...grid.rows.slice(0, -1).map((r, i) => ({ axis: 'row' as const, i, at: (r[1] + grid.rows[i + 1][0]) / 2 })),
  ];
  return (
    <g className="pane-overlay">
      {preview.panels.map((p) => {
        const on = pane ? p.col === pane.col && p.row === pane.row : false;
        return <rect key={`${p.col}:${p.row}`} data-pane-rect={`${p.col}:${p.row}`} x={Math.min(sx(p.x0), sx(p.x1))} y={sy(p.z1)} width={(p.x1 - p.x0) * kk} height={(p.z1 - p.z0) * kk}
          fill={on ? 'rgba(197, 90, 46, 0.12)' : 'transparent'} stroke={on ? '#c55a2e' : 'none'} strokeWidth={1.5} style={{ cursor: 'pointer' }}
          onClick={() => p.col != null && p.row != null && onPick({ col: p.col, row: p.row })} />;
      })}
      {lines.map((l) => {
        const live = drag && drag.axis === l.axis && drag.i === l.i ? drag.at : l.at;
        const common = {
          style: { cursor: l.axis === 'col' ? 'col-resize' : 'row-resize' } as React.CSSProperties,
          onPointerDown: (e: React.PointerEvent<SVGElement>) => { (e.currentTarget as SVGElement).setPointerCapture(e.pointerId); onDrag({ axis: l.axis, i: l.i, from: l.at, at: l.at }); },
          onPointerMove: (e: React.PointerEvent<SVGElement>) => { if (!drag || drag.axis !== l.axis || drag.i !== l.i) return; const q = svgPoint(e); onDrag({ ...drag, at: l.axis === 'col' ? toX(q.x) : toZ(q.y) }); },
          onPointerUp: () => {
            if (!drag || drag.axis !== l.axis || drag.i !== l.i) return;
            // A click without a drag picks the bar, to give it a width of its own.
            if (Math.abs(drag.at - drag.from) < 0.002) { onDrag(null); onBar({ axis: l.axis, i: l.i }); } else onDrop(drag);
          },
        };
        return l.axis === 'col'
          ? <rect key={`c${l.i}`} data-divider={`col-${l.i}`} x={sx(live) - 4} y={sy(iz1)} width={8} height={(iz1 - iz0) * kk} fill={drag?.axis === 'col' && drag.i === l.i ? 'rgba(197,90,46,0.35)' : 'transparent'} {...common} />
          : <rect key={`r${l.i}`} data-divider={`row-${l.i}`} x={Math.min(sx(ix0), sx(ix1))} y={sy(live) - 4} width={(ix1 - ix0) * kk} height={8} fill={drag?.axis === 'row' && drag.i === l.i ? 'rgba(197,90,46,0.35)' : 'transparent'} {...common} />;
      })}
      {bar && (() => {
        // The picked bar as it is drawn: from one pane's edge to the next one's (a line where it is 0).
        const a = bar.axis === 'col' ? grid.cols : grid.rows;
        if (bar.i + 1 >= a.length) return null;
        const lo = a[bar.i][1], hi = a[bar.i + 1][0];
        return bar.axis === 'col'
          ? <rect data-bar-picked={`col-${bar.i}`} x={Math.min(sx(lo), sx(hi)) - 1.5} y={sy(iz1)} width={Math.abs(sx(hi) - sx(lo)) + 3} height={(iz1 - iz0) * kk} fill="rgba(197,90,46,0.25)" stroke="#c55a2e" strokeDasharray="3 2" pointerEvents="none" />
          : <rect data-bar-picked={`row-${bar.i}`} x={Math.min(sx(ix0), sx(ix1))} y={Math.min(sy(lo), sy(hi)) - 1.5} width={(ix1 - ix0) * kk} height={Math.abs(sy(hi) - sy(lo)) + 3} fill="rgba(197,90,46,0.25)" stroke="#c55a2e" strokeDasharray="3 2" pointerEvents="none" />;
      })()}
      {drag && <text x={8} y={14} fontSize={11} fill="#c55a2e">{Math.round((drag.at - drag.from) * 1000)} mm</text>}
    </g>
  );
}

export function JoineryDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = [...ofClass(model, 'IfcDoorType'), ...ofClass(model, 'IfcWindowType')];
  const catalog = info?.catalogs['architecture.joinery'] as unknown as { windowOperations: string[]; doorOperations: string[]; defaultDoor: Spec; defaultWindow: Spec;
    doorPresets?: Record<string, Spec>; defaultMeeting?: Meeting & { astragalWidth?: number }; floorSprings?: FloorSpringModel[];
    garage?: { sizes?: GarageSize[] };
    profileSystems?: Record<string, { frame: Spec['frame']; sash: Spec['sash']; profile: Profile }> } | undefined;
  const bayStyles = info?.catalogs['architecture.bayStyles'] as unknown as { spec: Spec }[] | undefined;
  const [editing, setEditing] = useState<string | null>(types[0]?.id ?? null);
  // What a new type starts from (a new object each time New door or New window is clicked, so the editor reloads).
  const [fresh, setFresh] = useState<{ kind: 'door' | 'window'; bay?: boolean; preset?: string }>({ kind: 'window' });
  const [spec, setSpec] = useState<Spec | null>(null);
  const [cols, setCols] = useState('1');
  const [rows, setRows] = useState('1');
  const [preview, setPreview] = useState<Preview2 | null>(null);
  const [outside, setOutside] = useState(false);
  // The pane picked in the pane editor (its first cell), and a grid line being dragged in the elevation.
  const [pane, setPane] = useState<{ col: number; row: number } | null>(null);
  const [splitAt, setSplitAt] = useState<{ col: string; row: string }>({ col: '', row: '' });
  const pickPane = (p: { col: number; row: number } | null) => { setPane(p); setSplitAt({ col: '', row: '' }); };
  const [drag, setDrag] = useState<{ axis: 'col' | 'row'; i: number; from: number; at: number } | null>(null);
  // The mullion or transom picked in the elevation, to give it a width of its own.
  const [bar, setBar0] = useState<{ axis: 'col' | 'row'; i: number } | null>(null);
  const pickBar = (b: { axis: 'col' | 'row'; i: number } | null) => setBar0(b);

  // The editor loads when the picked type changes, not on every diff (which
  // would throw away what is being typed): it reads the model through a ref.
  const modelRef = useRef(model);
  modelRef.current = model;
  // Load the picked type (or a fresh default) into the editor.
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const bayStyle = fresh.bay ? (bayStyles?.[0]?.spec as unknown as Spec | undefined) : undefined;
    const preset = fresh.preset ? catalog?.doorPresets?.[fresh.preset] : undefined;
    const s = (t?.props.spec as unknown as Spec | undefined) ?? preset ?? bayStyle ?? (fresh.kind === 'door' ? catalog?.defaultDoor : catalog?.defaultWindow) ?? null;
    setSpec(s ? structuredClone(s) : null);
    if (s) { setCols(s.columns.join(' ')); setRows(s.rows.join(' ')); }
  }, [editing, catalog, fresh, bayStyles]);

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
  // A library profile for the frame or the sash (decision 417): copied into the project, its real outline the straight members' bodies, with its size.
  const pickMemberProfile = (member: 'frame' | 'sash', profile: unknown, library: string | null) => {
    void (execute('profiles.use', { profile, library }) as Promise<{ section?: LibrarySection } | null>).then((r) => {
      const section = r?.section;
      if (!section) return;
      if (member === 'frame') set({ frame: { ...spec.frame, width: section.width, depth: section.depth }, frameSection: section });
      else set({ sash: { ...spec.sash, width: section.width, depth: section.depth }, sashSection: section });
    });
  };
  const set = (patch: Partial<Spec>) => {
    const next = { ...spec, ...patch };
    // The bars stay with their lines when columns or rows are set another way (a style, the relative sizes, a bay).
    if ('columns' in patch && !('mullions' in patch)) next.mullions = fitBars(spec.mullions, next.columns.length);
    if ('rows' in patch && !('transoms' in patch)) next.transoms = fitBars(spec.transoms, next.rows.length);
    if (('width' in patch || 'height' in patch) && !('name' in patch) && spec.name === sizeName(spec)) next.name = sizeName(next);
    setSpec(next);
  };
  const details: Details = spec.details ?? {};
  const setDetails = (patch: Partial<Details>) => set({ details: { ...details, ...patch } });
  const operations = spec.frameless ? (spec.kind === 'door' ? FRAMELESS_DOOR_OPS : ['fixed']) : spec.kind === 'door' ? [...catalog.doorOperations, 'fixed'] : catalog.windowOperations;
  const close = () => useUi.setState({ dialog: null });
  const startNew = (kind: 'door' | 'window', bay = false, preset?: string) => { setEditing(null); setFresh({ kind, bay, preset }); pickBar(null); };
  const setGrid = (c: string, r: string) => {
    setCols(c); setRows(r);
    const cw = weights(c), rw = weights(r);
    if (cw.length && rw.length) set({ columns: cw, rows: rw, panels: spec.panels.filter((p) => p.col < cw.length && p.row < rw.length) });
  };
  // The grid's sizes in mm, from the engine's preview (decision 370); set one, the last takes the rest.
  // Worked out from the spec's own weights over the inner span (which only the size and frame change), never from a preview
  // that may still be on its way: quick splits one after another each see the grid as it now is.
  // Each bar has its own width (decision 466): the sizes share what the bars leave.
  const sizesOf = (weights: number[], span: number | undefined, bars: number[]) => {
    if (span == null || weights.length === 0) return [];
    const total = weights.reduce((a, b) => a + Math.max(b, 0), 0) || 1;
    const usable = span * 1000 - bars.reduce((a, b) => a + b, 0);
    return weights.map((w) => Math.round((usable * Math.max(w, 0)) / total * 10) / 10);
  };
  const inner = preview?.grid?.inner;
  const colSizes = sizesOf(spec.columns, inner ? inner[1] - inner[0] : undefined, spec.frameless ? spec.columns.slice(1).map(() => spec.frameless!.gap * 1000) : barWidths(spec.mullions, spec.columns.length, spec.frame.mullion));
  const rowSizes = sizesOf(spec.rows, inner ? inner[3] - inner[2] : undefined, spec.frameless ? spec.rows.slice(1).map(() => spec.frameless!.gap * 1000) : barWidths(spec.transoms, spec.rows.length, spec.frame.transom));
  const setSizes = (axis: 'col' | 'row', i: number, value: number) => {
    const next = resized(axis === 'col' ? colSizes : rowSizes, i, value);
    if (!next) { notify('That leaves too little for the others (50 mm at least).'); return; }
    if (axis === 'col') { set({ columns: next }); setCols(next.map(roundW).join(' ')); }
    else { set({ rows: next }); setRows(next.map(roundW).join(' ')); }
  };

  // A bar's width set (decision 466); a mullion made 0 between two door leaves gives the type its meeting, from the catalogue's default.
  const setBar = (axis: 'col' | 'row', i: number, value: number | null) => {
    const patch: Partial<Spec> = axis === 'col' ? { mullions: withBar(spec.mullions, spec.columns.length, i, value) } : { transoms: withBar(spec.transoms, spec.rows.length, i, value) };
    const g: GridSpec = { columns: spec.columns, rows: spec.rows, panels: spec.panels, frame: spec.frame };
    if (axis === 'col' && value === 0 && !spec.meeting && spec.kind === 'door' && !spec.frameless && catalog.defaultMeeting && leafPairs(g).includes(i)) {
      const { astragalWidth: _a, ...m } = catalog.defaultMeeting;
      patch.meeting = { ...m, width: null, thickness: null };
    }
    set(patch);
  };
  const barCount = bar ? (bar.axis === 'col' ? spec.columns.length : spec.rows.length) - 1 : 0;
  const barOn = bar && bar.i < barCount ? bar : null;

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
            {catalog.doorPresets?.double && <li><button type="button" className="tree-item" data-action="new-double-door" onClick={() => startNew('door', false, 'double')}><Plus size={12} /> New double door</button></li>}
            {catalog.doorPresets?.['garage-sectional'] && <li><button type="button" className="tree-item" data-action="new-garage-door" onClick={() => startNew('door', false, 'garage-sectional')}><Plus size={12} /> New garage door</button></li>}
            {catalog.doorPresets?.['garage-side-hinged'] && <li><button type="button" className="tree-item" data-action="new-side-hinged-garage-doors" onClick={() => startNew('door', false, 'garage-side-hinged')}><Plus size={12} /> New side-hinged garage doors</button></li>}
            {catalog.doorPresets?.['floor-spring'] && <li><button type="button" className="tree-item" data-action="new-floor-spring-door" onClick={() => startNew('door', false, 'floor-spring')}><Plus size={12} /> New frameless door on floor spring</button></li>}
            <li><button type="button" className="tree-item" onClick={() => startNew('window')}><Plus size={12} /> New window</button></li>
            <li><button type="button" className="tree-item" data-action="new-bay-window" onClick={() => startNew('window', true)}><Plus size={12} /> New bay window</button></li>
          </ul>
          <div className="joinery-edit">
            <div className="joinery-form">
              <label className="field-line"><span className="eyebrow">Name</span><input value={spec.name} onChange={(e) => set({ name: e.target.value })} /></label>
              <label className="field-line"><span className="eyebrow">Mark</span><input value={spec.mark} onChange={(e) => set({ mark: e.target.value })} /></label>
              <Mm label="Width" value={spec.width} onChange={(v) => set({ width: v })} />
              <Mm label="Height" value={spec.height} onChange={(v) => set({ height: v })} />
              {spec.kind === 'window' && <Mm label="Sill" value={spec.sillHeight} onChange={(v) => set({ sillHeight: v })} />}
              <label className="field-line"><span className="eyebrow">Style</span>
                <StyleSelect field="style" title="Sets the shape, the glazing bars and the ornaments at once; each stays editable"
                  value={spec.style && JOINERY_STYLES[spec.style] ? spec.style : null} modified={styleModified(spec)}
                  groups={[{ items: Object.entries(JOINERY_STYLES).filter(([k]) => spec.kind === 'window' || k !== 'oculus').map(([k, v]) => ({ id: k, label: v.label })) }]}
                  onApply={(key) => {
                    const st = JOINERY_STYLES[key];
                    if (!st) return;
                    const patch = st.apply(spec);
                    set({ ...patch, details: styleDetails(key, spec.details ?? {}), style: key });
                    if (patch.columns) setCols(patch.columns.join(' '));
                    if (patch.rows) setRows(patch.rows.join(' '));
                  }} /></label>
              <GarageFields spec={spec} sizes={catalog.garage?.sizes} preview={preview} onChange={(patch) => {
                set(patch);
                if (patch.columns) setCols(patch.columns.join(' '));
                if (patch.rows) setRows(patch.rows.join(' '));
              }} />
              {!spec.bay && !garageOf(spec) && <FramelessFields spec={spec} springs={catalog.floorSprings} onChange={set} />}
              {!spec.bay && !spec.frameless && !garageOf(spec) && <ShapeFields spec={spec} onChange={set} />}
              {spec.kind === 'window' && !spec.frameless && <BayFields spec={spec} onChange={(patch) => set(patch as Partial<Spec>)} onGrid={(c) => setCols(c.join(' '))} />}
              {!spec.frameless && <>
              <Mm label="Frame depth" value={spec.frame.depth} onChange={(v) => set({ frame: { ...spec.frame, depth: v }, frameSection: null })} />
              <Mm label="Frame width" value={spec.frame.width} onChange={(v) => set({ frame: { ...spec.frame, width: v }, frameSection: null })} />
              <label className="field-line"><span className="eyebrow">Position in the wall</span>
                <select data-field="position-from" title="Where the frame sits across the wall, as a value from a face of the wall" value={spec.position?.from ?? ''}
                  onChange={(e) => set({ position: e.target.value ? { from: e.target.value as Position['from'], distance: spec.position?.distance ?? 0 } : null })}>
                  <option value="">Centred in the wall</option><option value="exterior">From the exterior finish</option><option value="core">From the core</option><option value="centre">From the wall’s centre</option><option value="interior">From the interior finish</option>
                </select></label>
              </>}
              {spec.frameless && <label className="field-line"><span className="eyebrow">Position in the wall</span>
                <select data-field="position-from" title="Where the glass sits across the wall, as a value from a face of the wall" value={spec.position?.from ?? ''}
                  onChange={(e) => set({ position: e.target.value ? { from: e.target.value as Position['from'], distance: spec.position?.distance ?? 0 } : null })}>
                  <option value="">Centred in the wall</option><option value="exterior">From the exterior finish</option><option value="core">From the core</option><option value="centre">From the wall’s centre</option><option value="interior">From the interior finish</option>
                </select></label>}
              {spec.position && <Mm label="Set back" value={spec.position.distance} onChange={(v) => set({ position: { ...spec.position!, distance: v } })} />}
              {!spec.frameless && <>
              <Mm label="Mullion width" value={spec.frame.mullion} onChange={(v) => set({ frame: { ...spec.frame, mullion: v } })} />
              <Mm label="Transom width" value={spec.frame.transom} onChange={(v) => set({ frame: { ...spec.frame, transom: v } })} />
              <label className="field-line"><span className="eyebrow">Frame from a library</span><ProfilePickerField testId="joinery-frame-library" value={spec.frameSection?.name ?? null} onPick={(p, library) => pickMemberProfile('frame', p, library)} />
                {spec.frameSection && <button type="button" data-testid="joinery-frame-plain" title="Keeps its size as a plain rectangle" onClick={() => set({ frameSection: null })}>Plain</button>}</label>
              {spec.kind === 'window' && <label className="field-line"><span className="eyebrow">Sash from a library</span><ProfilePickerField testId="joinery-sash-library" value={spec.sashSection?.name ?? null} onPick={(p, library) => pickMemberProfile('sash', p, library)} />
                {spec.sashSection && <button type="button" data-testid="joinery-sash-plain" title="Keeps its size as a plain rectangle" onClick={() => set({ sashSection: null })}>Plain</button>}</label>}
              <label className="field-line"><span className="eyebrow">Profile system</span>
                <select data-field="profile-system" data-i18n-ctx="profiles" value={spec.profile?.system ?? ''} title="Sets the frame, mullion and sash sizes and the profile's finer shapes; each stays editable" onChange={(e) => {
                  const sys = catalog.profileSystems?.[e.target.value];
                  if (!e.target.value) set({ profile: null });
                  else if (sys) set({ frame: { ...sys.frame, offset: spec.frame.offset }, sash: sys.sash, profile: sys.profile, frameSection: null, sashSection: null });
                }}>
                  <option value="">Simple (plain profiles)</option>
                  {Object.keys(catalog.profileSystems ?? {}).map((k) => <option key={k} value={k}>{PROFILE_LABELS[k] ?? k}</option>)}
                  {spec.profile && !catalog.profileSystems?.[spec.profile.system] && <option value={spec.profile.system}>{spec.profile.system}</option>}
                </select></label>
              {spec.profile && <>
                <Mm label="Rebate" value={spec.profile.rebate} onChange={(v) => set({ profile: { ...spec.profile!, rebate: v } })} />
                <Mm label="Sash overlap" value={spec.profile.overlap} onChange={(v) => set({ profile: { ...spec.profile!, overlap: v } })} />
                <Mm label="Glazing bead" value={spec.profile.bead} onChange={(v) => set({ profile: { ...spec.profile!, bead: v } })} />
                {spec.kind === 'window' && <label className="field-line check"><input type="checkbox" checked={spec.profile.drip} onChange={(e) => set({ profile: { ...spec.profile!, drip: e.target.checked } })} /><span>Drip under the bottom rail</span></label>}
              </>}
              </>}
              <label className="field-line"><span className="eyebrow">Columns (relative)</span><input data-field="columns" value={cols} title="Divide into columns by relative widths, e.g. 1 1 for two equal ones or 2 1" onChange={(e) => setGrid(e.target.value, rows)} /></label>
              <label className="field-line"><span className="eyebrow">Rows (relative)</span><input value={rows} title="Divide into rows by relative heights, bottom to top" onChange={(e) => setGrid(cols, e.target.value)} /></label>
              {colSizes.length > 1 && <SizeRow label="Column widths" sizes={colSizes} field="col-size" onCommit={(i, mm) => setSizes('col', i, mm)} />}
              {rowSizes.length > 1 && <SizeRow label="Row heights · bottom to top" sizes={rowSizes} field="row-size" onCommit={(i, mm) => setSizes('row', i, mm)} />}
              {!spec.frameless && !spec.bay && <BarField bar={barOn} spec={spec} onChange={(v) => barOn && setBar(barOn.axis, barOn.i, v)} />}
              <PaneEditor spec={spec} preview={preview} pane={pane} operations={operations}
                onPick={pickPane} onPanels={(panels) => set({ panels })} at={splitAt} setAt={setSplitAt}
                onGrid={(g) => { set({ columns: g.columns, rows: g.rows, panels: g.panels, mullions: g.mullions, transoms: g.transoms }); setCols(g.columns.map(roundW).join(' ')); setRows(g.rows.map(roundW).join(' ')); }}
                colSizes={colSizes} rowSizes={rowSizes} />
              {spec.kind === 'door' && !spec.frameless && <MeetingFields spec={spec} preview={preview} defaults={catalog.defaultMeeting} onChange={(meeting) => set({ meeting })} />}
              <PartMaterials spec={spec} onChange={(materials) => set({ materials })} />
              {!spec.materials?.frame && !spec.frameless && <label className="field-line"><span className="eyebrow">Frame</span><input type="color" value={spec.colors.frame} onChange={(e) => set({ colors: { ...spec.colors, frame: e.target.value } })} /></label>}
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
              {spec.kind === 'window' && <SillFields label="Interior sill" sill={details.sillInside ?? null} board={spec.board.interior} fallback={{ ...SILL_IN, projection: spec.board.interior || SILL_IN.projection }}
                onChange={(v) => setDetails({ sillInside: v })} onBoard={(m) => set({ board: { ...spec.board, interior: m }, details: { ...details, sillInside: { ...(details.sillInside ?? SILL_IN), on: false } } })} />}
              {spec.kind === 'window' && <SillFields label="Exterior sill" sill={details.sillOutside ?? null} board={spec.board.exterior} fallback={{ ...SILL_OUT, projection: spec.board.exterior || SILL_OUT.projection }} slope
                onChange={(v) => setDetails({ sillOutside: v })} onBoard={(m) => set({ board: { ...spec.board, exterior: m }, details: { ...details, sillOutside: { ...(details.sillOutside ?? SILL_OUT), on: false } } })} />}
              <label className="field-line check"><input type="checkbox" data-field="jamb-linings" checked={(details.reveal?.mode ?? 'wall') !== 'none'}
                onChange={(e) => setDetails({ reveal: { ...(details.reveal ?? { thickness: 0.015 }), mode: e.target.checked ? 'wall' : 'none' } })} /><span>Jamb linings (the reveal's finish)</span></label>
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
              <p className="eyebrow">Data</p>
              {editing ? <JoineryDataButton type={model.nodes.get(editing) ?? null} /> : <p className="muted small">Save the type to give it data (U-value, g-value, safety glass, fire rating, fittings).</p>}
              <TypeParams typeId={editing} kind={spec.kind} />
            </div>
            <div className="joinery-preview">
              <Strokes drawing={outside ? preview?.drawings?.exterior : preview?.drawings?.elevation} w={W} h={H} flipX label={outside ? 'Elevation from outside' : 'Elevation from inside'}>
                {(sx, sy, kk) => <>
                  {!outside && boxes.map((b, i) => b.profile
                    ? <path key={i} d={[b.profile, ...(b.holes ?? [])].map((l) => loopPath(l, sx, sy)).join(' ')} fillRule="evenodd" fill={fill(b.role)} stroke="none" opacity={b.role === 'glass' ? 0.7 : 1} />
                    : <rect key={i} x={Math.min(sx(b.x0), sx(b.x1))} y={sy(b.z1)} width={(b.x1 - b.x0) * kk} height={(b.z1 - b.z0) * kk} fill={fill(b.role)} stroke="none" opacity={b.role === 'glass' ? 0.7 : 1} />)}
                  {!outside && !spec.bay && <PaneOverlay preview={preview} pane={pane} drag={drag} bar={barOn} onBar={pickBar} sx={sx} sy={sy} kk={kk} onPick={pickPane} onDrag={setDrag}
                    onDrop={(d) => {
                      const sizes = d.axis === 'col' ? colSizes : rowSizes;
                      const next = shifted(sizes, d.i, Math.round((d.at - d.from) * 1000));
                      setDrag(null);
                      if (!next) return;
                      if (d.axis === 'col') { set({ columns: next }); setCols(next.map(roundW).join(' ')); } else { set({ rows: next }); setRows(next.map(roundW).join(' ')); }
                    }} />}
                </>}
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
              <JoineryPreview3D spec={spec} width={W} height={190} />
            </div>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <SaveTypeToLibrary ids={[editing]} />}
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
