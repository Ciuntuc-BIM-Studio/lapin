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
import { useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { mm, ofClass, relatedOf } from '../../engine/model';
import { LintelRuleEditor } from './LintelRule';
import type { Rule } from './lintels';

interface Panel { col: number; row: number; operation: string; colSpan?: number; rowSpan?: number; glazed?: boolean }
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
}
interface Shape { kind: 'rect' | 'round' | 'segmental' | 'pointed' | 'gable' | 'circle'; rise?: number | null; bars: 'none' | 'sunburst' | 'mullions' | 'cross'; spokes: number }
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
export const JOINERY_STYLES: Record<string, { label: string; apply: (s: Spec) => Partial<Spec> }> = {
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
export function styleDetails(key: string, d: Details): Details {
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
interface Preview { problem: string | null; boxes: { role: string; x0: number; x1: number; z0: number; z1: number; y0: number; y1: number; profile?: number[] | null; holes?: number[][] | null }[]; panels: { operation: string; x0: number; x1: number; z0: number; z1: number }[]; ifcType: string }
interface Stroke { pts: [number, number][]; weight: string; closed: boolean; dashed: boolean }
interface Drawing { strokes: Stroke[] }
interface Drawings { elevation: Drawing; exterior: Drawing; plan: Drawing; section: Drawing }
type Preview2 = Preview & { drawings?: Drawings | null; symbols?: { convention: string; hardware: boolean } };

/** One of the symbol engine's drawings in a box: X across, Y up, fitted; strokes by weight (cut, outline, thin), dashed where asked. */
function Strokes({ drawing, w, h, flipX = false, label, children }: { drawing: Drawing | undefined; w: number; h: number; flipX?: boolean; label: string; children?: (sx: (x: number) => number, sy: (y: number) => number, k: number) => React.ReactNode }) {
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

/** A number field in mm, kept as text while typing. */
function Mm({ label, value, onChange }: { label: string; value: number; onChange: (m: number) => void }) {
  const [text, setText] = useState(mm(value));
  useEffect(() => setText(mm(value)), [value]);
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <input className="num" value={text} onChange={(e) => { setText(e.target.value); const v = Number(e.target.value.replace(',', '.')); if (Number.isFinite(v)) onChange(v / 1000); }} />
    </label>
  );
}

/** A material and a colour (the colour when the material is not in the project). */
function Look({ material, color, onChange }: { material?: string | null; color: string; onChange: (patch: { material?: string; color?: string }) => void }) {
  return (
    <>
      <label className="field-line"><span className="eyebrow">Material</span><input list="lapin-materials" value={material ?? ''} onChange={(e) => onChange({ material: e.target.value })} /></label>
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
        <label className="field-line"><span className="eyebrow">Slope (°)</span><input className="num" value={c.slope} onChange={(e) => { const v = Number(e.target.value); if (Number.isFinite(v)) onChange({ canopy: { ...c, slope: v } }); }} /></label>
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
          <option value="pointed">Pointed arch</option><option value="gable">Triangular head</option><option value="circle">Round or oval</option>
        </select></label>
      {(sh.kind === 'segmental' || sh.kind === 'pointed' || sh.kind === 'gable') && <Mm label="Rise of the head" value={sh.rise ?? (sh.kind === 'segmental' ? spec.width / 6 : sh.kind === 'pointed' ? spec.width * 0.866 : spec.width / 4)} onChange={(v) => setShape({ rise: v })} />}
      {sh.kind !== 'rect' && <label className="field-line"><span className="eyebrow">{sh.kind === 'circle' ? 'Bars' : 'Bars in the head'}</span>
        <select data-field="shape-bars" value={sh.bars} onChange={(e) => setShape({ bars: e.target.value as Shape['bars'] })}>
          <option value="none">None</option><option value="sunburst">Sunburst</option><option value="mullions">{sh.kind === 'circle' ? 'Upright' : 'The mullions carried up'}</option><option value="cross">Cross</option>
        </select></label>}
      {sh.kind !== 'rect' && sh.bars === 'sunburst' && <label className="field-line"><span className="eyebrow">Spokes</span><input className="num" value={sh.spokes} onChange={(e) => { const v = Math.round(Number(e.target.value)); if (v > 0) setShape({ spokes: v }); }} /></label>}
      {sh.kind !== 'circle' && <label className="field-line"><span className="eyebrow">Glazing bars</span>
        <select data-field="muntins" value={mu.pattern} onChange={(e) => setMuntins({ pattern: e.target.value as Muntins['pattern'] })}>
          <option value="none">None</option><option value="grid">Grid (colonial)</option><option value="prairie">Prairie</option><option value="diamond">Diamond lattice</option>
        </select></label>}
      {sh.kind !== 'circle' && (mu.pattern === 'grid' || mu.pattern === 'diamond') && <label className="field-line"><span className="eyebrow">Lites across</span><input className="num" value={mu.across} onChange={(e) => { const v = Math.round(Number(e.target.value)); if (v > 0) setMuntins({ across: v }); }} /></label>}
      {sh.kind !== 'circle' && mu.pattern === 'grid' && <label className="field-line"><span className="eyebrow">Lites up</span><input className="num" value={mu.up} onChange={(e) => { const v = Math.round(Number(e.target.value)); if (v > 0) setMuntins({ up: v }); }} /></label>}
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
        {slope && <label className="field-line"><span className="eyebrow">Slope (°)</span><input className="num" value={cur.slope} onChange={(e) => { const v = Number(e.target.value); if (Number.isFinite(v)) onChange({ ...cur, slope: v }); }} /></label>}
        <label className="field-line"><span className="eyebrow">Material</span><input list="lapin-materials" value={cur.material ?? ''} onChange={(e) => onChange({ ...cur, material: e.target.value })} /></label>
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
    const s = (t?.props.spec as unknown as Spec | undefined) ?? catalog?.defaultWindow ?? null;
    setSpec(s ? structuredClone(s) : null);
    if (s) { setCols(s.columns.join(' ')); setRows(s.rows.join(' ')); }
  }, [editing, catalog]);

  // The engine builds the preview from the spec as it is now.
  useEffect(() => {
    if (!spec) return;
    let live = true;
    void engine.call<Preview2>('doc.query', { name: 'architecture.joineryPreview', args: { spec } }).then((p) => { if (live) setPreview(p); }).catch(() => undefined);
    return () => { live = false; };
  }, [spec]);

  if (!spec || !catalog) return null;
  const set = (patch: Partial<Spec>) => setSpec({ ...spec, ...patch });
  const details: Details = spec.details ?? {};
  const setDetails = (patch: Partial<Details>) => set({ details: { ...details, ...patch } });
  const materials = ofClass(model, 'IfcMaterial');
  const operations = spec.kind === 'door' ? [...catalog.doorOperations, 'fixed'] : catalog.windowOperations;
  const close = () => useUi.setState({ dialog: null });
  const fresh = (kind: 'door' | 'window') => { setEditing(null); const d = structuredClone(kind === 'door' ? catalog.defaultDoor : catalog.defaultWindow); setSpec(d); setCols(d.columns.join(' ')); setRows(d.rows.join(' ')); };
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
  const fill = (role: string) => role === 'glass' ? spec.colors.glass : role === 'leaf' ? spec.colors.leaf : role === 'handle' || role === 'hardware' ? spec.hardware?.color ?? '#5a5a5a' : role === 'board' ? '#dcdcdc' : spec.colors.frame;

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
            <li><button type="button" className="tree-item" onClick={() => fresh('door')}><Plus size={12} /> New door</button></li>
            <li><button type="button" className="tree-item" onClick={() => fresh('window')}><Plus size={12} /> New window</button></li>
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
              <p className="eyebrow">Panels · seen from inside</p>
              <table className="panel-grid">
                <tbody>
                  {[...spec.rows.keys()].reverse().map((row) => (
                    <tr key={row}>
                      {spec.columns.map((_, col) => (
                        <td key={col}>
                          <select value={cellOp(col, row)} onChange={(e) => setCell(col, row, e.target.value)}>
                            {operations.map((o) => <option key={o} value={o}>{o}</option>)}
                          </select>
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
              <label className="field-line"><span className="eyebrow">Frame</span><input type="color" value={spec.colors.frame} onChange={(e) => set({ colors: { ...spec.colors, frame: e.target.value } })} /></label>
              <LintelRuleEditor inherit rule={spec.lintel ?? null} onChange={(r) => set({ lintel: r })} />
              <p className="eyebrow">Details</p>
              <datalist id="lapin-materials">{materials.map((m) => <option key={m.id} value={m.name} />)}</datalist>
              <label className="field-line"><span className="eyebrow">Reveal finish</span>
                <select value={details.reveal?.mode ?? 'wall'} onChange={(e) => setDetails({ reveal: { ...(details.reveal ?? { thickness: 0.015 }), mode: e.target.value as Reveal['mode'] } })}>
                  <option value="wall">The wall's finish layers</option><option value="material">A material</option><option value="none">None</option>
                </select>
              </label>
              {details.reveal?.mode === 'material' && <>
                <label className="field-line"><span className="eyebrow">Reveal material</span><input list="lapin-materials" value={details.reveal.material ?? ''} onChange={(e) => setDetails({ reveal: { ...details.reveal!, material: e.target.value } })} /></label>
                <Mm label="Reveal thickness" value={details.reveal.thickness} onChange={(v) => setDetails({ reveal: { ...details.reveal!, thickness: v } })} />
              </>}
              {spec.kind === 'window' && <SillFields label="Interior sill" sill={details.sillInside ?? null} fallback={{ ...SILL_IN, projection: spec.board.interior || SILL_IN.projection }} onChange={(v) => setDetails({ sillInside: v })} />}
              {spec.kind === 'window' && <SillFields label="Exterior sill" sill={details.sillOutside ?? null} fallback={{ ...SILL_OUT, projection: spec.board.exterior || SILL_OUT.projection }} slope onChange={(v) => setDetails({ sillOutside: v })} />}
              <label className="field-line check"><input type="checkbox" checked={details.surround?.on ?? false} onChange={(e) => setDetails({ surround: { ...(details.surround ?? SURROUND), on: e.target.checked } })} /><span>Surround on the facade</span></label>
              {details.surround?.on && <>
                <Mm label="Surround width" value={details.surround.width} onChange={(v) => setDetails({ surround: { ...details.surround!, width: v } })} />
                <Mm label="Surround projection" value={details.surround.projection} onChange={(v) => setDetails({ surround: { ...details.surround!, projection: v } })} />
                {spec.kind === 'window' && <label className="field-line check"><input type="checkbox" checked={details.surround.sill} onChange={(e) => setDetails({ surround: { ...details.surround!, sill: e.target.checked } })} /><span>Around the sill too</span></label>}
                <label className="field-line"><span className="eyebrow">Surround material</span><input list="lapin-materials" value={details.surround.material ?? ''} onChange={(e) => setDetails({ surround: { ...details.surround!, material: e.target.value } })} /></label>
              </>}
              {spec.kind === 'door' && <label className="field-line check"><input type="checkbox" checked={details.threshold?.on ?? false} onChange={(e) => setDetails({ threshold: { ...(details.threshold ?? THRESHOLD), on: e.target.checked } })} /><span>Threshold</span></label>}
              {spec.kind === 'door' && details.threshold?.on && <>
                <Mm label="Threshold height" value={details.threshold.height} onChange={(v) => setDetails({ threshold: { ...details.threshold!, height: v } })} />
                <label className="field-line"><span className="eyebrow">Threshold material</span><input list="lapin-materials" value={details.threshold.material ?? ''} onChange={(e) => setDetails({ threshold: { ...details.threshold!, material: e.target.value } })} /></label>
              </>}
              <DressingFields kind={spec.kind} height={spec.height} details={details} onChange={setDetails} />
              <HardwareFields spec={spec} onChange={set} />
              {spec.kind === 'door' && <label className="field-line"><span className="eyebrow">Leaf</span><input type="color" value={spec.colors.leaf} onChange={(e) => set({ colors: { ...spec.colors, leaf: e.target.value } })} /></label>}
            </div>
            <div className="joinery-preview">
              <Strokes drawing={outside ? preview?.drawings?.exterior : preview?.drawings?.elevation} w={W} h={H} label={outside ? 'Elevation from outside' : 'Elevation from inside'}>
                {(sx, sy, kk) => !outside && boxes.map((b, i) => b.profile
                  ? <path key={i} d={[b.profile, ...(b.holes ?? [])].map((l) => loopPath(l, sx, sy)).join(' ')} fillRule="evenodd" fill={fill(b.role)} stroke="none" opacity={b.role === 'glass' ? 0.7 : 1} />
                  : <rect key={i} x={sx(b.x0)} y={sy(b.z1)} width={(b.x1 - b.x0) * kk} height={(b.z1 - b.z0) * kk} fill={fill(b.role)} stroke="none" opacity={b.role === 'glass' ? 0.7 : 1} />)}
              </Strokes>
              <div className="joinery-small">
                <figure><Strokes drawing={preview?.drawings?.plan} w={150} h={90} label="Plan" /><figcaption className="muted small">Plan</figcaption></figure>
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
              {editing && <button type="button" className="btn" title="Delete the type (refused while elements use it)" onClick={() => void execute('element.delete', { id: editing }).then((r) => { if (r !== null) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
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
