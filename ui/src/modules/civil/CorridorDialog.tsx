/**
 * The corridor configurator of a site path element (a path or road, a
 * retaining wall, a ditch, a railway track), on the one selected: its
 * cross-section at a station over the natural and proposed ground, its long
 * section with the red line (follow the ground, or PVIs you drag), the curve
 * at each vertex of its path (a radius with clothoids, or a corner), its
 * stations, the sections to draw and its joints. The engine builds
 * everything (civil.corridor, civil.crossSection, civil.setCorridor); this
 * draws and edits.
 */

import { ChevronLeft, ChevronRight, Plus, Trash2, X } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useUi } from '../../app/store';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { fromPairs } from '../../viewport/contour';
import { stationLabel } from './stations';

type Pair = [number, number];
type Curve = [number, number, number];
interface Spec {
  stationStart: number; interval: number; sections: number[];
  joints: { every: number; width: number; at: number[] };
  profile: { mode: 'ground' | 'design'; ground: 'existing' | 'proposed'; pvis: [number, number, number][] };
  /** [radius, spiral in, spiral out] per vertex of the path; empty: a plain polyline. */
  curves: Curve[];
}
interface CorridorData {
  kind: 'path' | 'retainingWall' | 'ditch' | 'track'; start: number; end: number; length: number; left: number; right: number;
  /** The axis runs on arcs and clothoids (some vertex has a radius that fits). */
  curved: boolean;
  /** Why the curves given could not be built, when the axis fell back to the polyline. */
  problem: string | null;
  stations: number[]; vertices: number[]; joints: number[]; spec: Spec; existing: Pair[]; proposed: Pair[]; design: Pair[];
}
interface Section { station: number; label: string; left: number; right: number; line: { start: Pair; end: Pair }; parts: { key: string; polygon: Pair[]; material?: string | null; hatch?: string | null }[]; existing: Pair[]; proposed: Pair[] }

/** The section hatches of the materials, as SVG patterns (8 px tiles, in the current colour): the engine's names (HatchLibrary), lines at 45° for one it does not know. */
function HatchDefs({ names }: { names: string[] }) {
  const tile = (name: string) => {
    switch (name) {
      case 'SOLID': return <rect width="8" height="8" fill="currentColor" />;
      case 'HORIZONTAL': return <path d="M0 4H8" />;
      case 'CROSS': return <path d="M0 8L8 0M0 0L8 8" />;
      case 'STEEL': return <path d="M0 8L8 0M-1 5L5 -1M3 9L9 3" />;
      case 'GRAVEL': return <><circle cx="2" cy="2" r="1.1" /><circle cx="6" cy="5" r="1.1" /><circle cx="3" cy="7" r="0.7" /></>;
      case 'EARTH': return <path d="M0 2H5M3 6H8" />;
      case 'CONCRETE': return <><path d="M0 8L8 0" /><circle cx="2" cy="2" r="0.6" fill="currentColor" /><circle cx="6" cy="6" r="0.6" fill="currentColor" /></>;
      case 'BRICK': return <path d="M0 4H8M4 0V4M0 4V8M8 4V8" />;
      case 'TILES': return <path d="M0 0H8V8" />;
      case 'INSULATION': case 'BATT': return <path d="M0 4L2 0L4 4L6 8L8 4" />;
      case 'WOOD': return <path d="M0 2C3 1 5 3 8 2M0 6C3 5 5 7 8 6" />;
      default: return <path d="M0 8L8 0" />;
    }
  };
  return (
    <defs>
      {names.map((n) => (
        <pattern key={n} id={`cs-hatch-${n}`} width="8" height="8" patternUnits="userSpaceOnUse">
          <g fill="none" stroke="currentColor" strokeWidth={0.6}>{tile(n)}</g>
        </pattern>
      ))}
    </defs>
  );
}

const PART_FILL: Record<string, string> = { surface: '#b8ad9c', base: '#a39888', gutter: '#9a9a9a', kerb: '#8f8f8f', sidewalk: '#c9c2b6', wall: '#a8a8a8', ditch: 'rgba(90,150,200,0.35)' };
const NATURAL = '#8a6a44';
const PROPOSED = '#3f8a4a';
const RED = '#c8372d';

const commandOf = (n: NodeDto) => (n.type === 'IfcPavement' ? 'civil.updatePath' : n.type === 'LapinRetainingWall' ? 'civil.updateRetainingWall' : n.type === 'LapinTrack' ? 'civil.updateTrack' : 'civil.updateDitch');

/** A plot's frame: data bounds to SVG pixels, y up; the vertical scale exaggerated by `exaggerate`. */
function frame(xs: number[], ys: number[], w: number, h: number, pad: number, equal: boolean) {
  const [x0, x1] = [Math.min(...xs), Math.max(...xs)];
  let [y0, y1] = [Math.min(...ys), Math.max(...ys)];
  if (y1 - y0 < 0.5) { const m = (y0 + y1) / 2; y0 = m - 0.25; y1 = m + 0.25; }
  const sx = (w - 2 * pad) / Math.max(x1 - x0, 1e-6);
  let sy = (h - 2 * pad) / Math.max(y1 - y0, 1e-6);
  if (equal) { const s = Math.min(sx, sy); const cy = (y0 + y1) / 2; sy = s; y0 = cy - (h - 2 * pad) / 2 / s; y1 = cy + (h - 2 * pad) / 2 / s; return { X: (x: number) => pad + (x - x0) * s + ((w - 2 * pad) - (x1 - x0) * s) / 2, Y: (y: number) => h - pad - (y - y0) * sy, inv: (px: number, py: number): Pair => [x0 + (px - pad - ((w - 2 * pad) - (x1 - x0) * s) / 2) / s, y0 + (h - pad - py) / sy], y0, y1 }; }
  return { X: (x: number) => pad + (x - x0) * sx, Y: (y: number) => h - pad - (y - y0) * sy, inv: (px: number, py: number): Pair => [x0 + (px - pad) / sx, y0 + (h - pad - py) / sy], y0, y1 };
}

const line = (pts: Pair[], X: (x: number) => number, Y: (y: number) => number) => pts.map((p, i) => `${i ? 'L' : 'M'}${X(p[0]).toFixed(1)} ${Y(p[1]).toFixed(1)}`).join('');

function CrossSectionPlot({ s }: { s: Section }) {
  const W = 640, H = 300;
  const all = [...s.parts.flatMap((p) => p.polygon), ...s.existing, ...s.proposed];
  if (all.length === 0) return <p className="muted small">Nothing to draw at this station.</p>;
  const f = frame(all.map((p) => p[0]), all.map((p) => p[1]), W, H, 28, true);
  const zAxis = s.parts.length ? Math.max(...s.parts.flatMap((p) => p.polygon.filter((q) => Math.abs(q[0]) < 1e-6).map((q) => q[1])), -Infinity) : NaN;
  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="corridor-plot" role="img" aria-label={`Cross-section at ${s.label}`}>
      <line x1={f.X(0)} x2={f.X(0)} y1={12} y2={H - 12} stroke="currentColor" strokeOpacity={0.35} strokeDasharray="6 3 1 3" />
      <HatchDefs names={[...new Set(s.parts.map((p) => p.hatch).filter((h): h is string => Boolean(h)))]} />
      {s.parts.map((p, i) => <path key={i} d={`${line(p.polygon, f.X, f.Y)}Z`} fill={PART_FILL[p.key] ?? '#bbb'} stroke="currentColor" strokeWidth={1}><title>{p.material ?? p.key}</title></path>)}
      {/* The material's section hatch over its fill, as a section sheet draws the layer. */}
      {s.parts.map((p, i) => p.hatch ? <path key={`h${i}`} d={`${line(p.polygon, f.X, f.Y)}Z`} fill={`url(#cs-hatch-${p.hatch})`} stroke="none" pointerEvents="none" /> : null)}
      <path d={line(s.existing, f.X, f.Y)} fill="none" stroke={NATURAL} strokeWidth={1.5} strokeDasharray="5 3" />
      <path d={line(s.proposed, f.X, f.Y)} fill="none" stroke={PROPOSED} strokeWidth={1.5} />
      {Number.isFinite(zAxis) && <text x={f.X(0) + 4} y={f.Y(zAxis) - 6} className="corridor-label">{zAxis.toFixed(2)}</text>}
      <text x={10} y={18} className="corridor-label">{s.label}</text>
      <text x={W - 10} y={18} textAnchor="end" className="corridor-label">left {s.left.toFixed(2)} · right {(-s.right).toFixed(2)} m</text>
    </svg>
  );
}

function LongSectionPlot({ d, station, onStation, onPvis }: { d: CorridorData; station: number; onStation: (s: number) => void; onPvis: (p: [number, number, number][]) => void }) {
  const W = 760, H = 300, PAD = 32;
  const design = d.spec.profile.mode === 'design';
  const [drag, setDrag] = useState<{ i: number; at: Pair } | null>(null);
  const svg = useRef<SVGSVGElement>(null);
  const pvis = d.spec.profile.pvis;
  const ys = [...d.existing, ...d.proposed, ...d.design].map((p) => p[1]).concat(pvis.map((p) => p[1]));
  const f = frame([d.start, d.end], ys.length ? ys : [0, 1], W, H, PAD, false);
  const toData = (e: { clientX: number; clientY: number }): Pair => { const r = svg.current!.getBoundingClientRect(); return f.inv((e.clientX - r.left) * (W / r.width), (e.clientY - r.top) * (H / r.height)); };
  const shown = pvis.map((p, i) => (drag?.i === i ? [drag.at[0], drag.at[1], p[2]] as [number, number, number] : p));
  const red = design && drag ? shown.map((p) => [p[0], p[1]] as Pair) : d.design;
  const round = (v: number, k = 100) => Math.round(v * k) / k;
  return (
    <svg ref={svg} viewBox={`0 0 ${W} ${H}`} className="corridor-plot" role="img" aria-label="Long section"
      onPointerMove={(e) => { if (drag) { const [s, z] = toData(e); const lo = drag.i === 0 ? d.start : pvis[drag.i - 1][0] + 0.5; const hi = drag.i === pvis.length - 1 ? d.end : pvis[drag.i + 1][0] - 0.5; setDrag({ i: drag.i, at: [drag.i === 0 || drag.i === pvis.length - 1 ? pvis[drag.i][0] : Math.min(hi, Math.max(lo, round(s))), round(z, 1000)] }); } }}
      onPointerUp={() => { if (drag) { onPvis(shown); setDrag(null); } }}
      onClick={(e) => {
        if (drag) return;
        const [s, z] = toData(e);
        if (design && e.shiftKey) { const next = [...pvis, [round(s), round(z, 1000), 0] as [number, number, number]].sort((a, b) => a[0] - b[0]); onPvis(next); return; }
        onStation(Math.min(d.end, Math.max(d.start, s)));
      }}>
      {d.stations.map((s) => <g key={s}><line x1={f.X(s)} x2={f.X(s)} y1={PAD - 8} y2={H - PAD} stroke="currentColor" strokeOpacity={0.12} /><text x={f.X(s)} y={H - 10} textAnchor="middle" className="corridor-label">{stationLabel(s)}</text></g>)}
      {d.vertices.map((s, i) => <line key={`v${i}`} x1={f.X(s)} x2={f.X(s)} y1={H - PAD} y2={H - PAD + 6} stroke="currentColor" />)}
      {d.joints.map((s) => <line key={`j${s}`} x1={f.X(s)} x2={f.X(s)} y1={PAD} y2={H - PAD} stroke="currentColor" strokeOpacity={0.5} strokeDasharray="2 3" />)}
      {[f.y0, (f.y0 + f.y1) / 2, f.y1].map((z) => <text key={z} x={4} y={f.Y(z) + 4} className="corridor-label">{z.toFixed(2)}</text>)}
      <path d={line(d.existing, f.X, f.Y)} fill="none" stroke={NATURAL} strokeWidth={1.5} strokeDasharray="5 3" />
      <path d={line(d.proposed, f.X, f.Y)} fill="none" stroke={PROPOSED} strokeWidth={1.5} />
      <path d={line(red, f.X, f.Y)} fill="none" stroke={RED} strokeWidth={2} />
      <line x1={f.X(station)} x2={f.X(station)} y1={PAD - 8} y2={H - PAD} stroke="var(--accent)" strokeWidth={1.5} />
      {design && shown.map((p, i) => (
        <g key={i}>
          <circle cx={f.X(p[0])} cy={f.Y(p[1])} r={6} fill="var(--panel)" stroke={RED} strokeWidth={2} style={{ cursor: 'grab' }}
            onPointerDown={(e) => { e.stopPropagation(); (e.target as Element).setPointerCapture(e.pointerId); setDrag({ i, at: [p[0], p[1]] }); }} />
          <text x={f.X(p[0])} y={f.Y(p[1]) - 10} textAnchor="middle" className="corridor-label">{p[1].toFixed(2)}</text>
        </g>
      ))}
    </svg>
  );
}

/** A number field committed on Enter or blur; empty restores. */
export function Num({ value, onCommit, step = 0.1, min, unit, width = 70 }: { value: number; onCommit: (v: number) => void; step?: number; min?: number; unit?: string; width?: number }) {
  const [text, setText] = useState(String(value));
  useEffect(() => setText(String(value)), [value]);
  const done = () => { const n = Number(text.replace(',', '.')); if (text.trim() !== '' && Number.isFinite(n) && n !== value) onCommit(min != null ? Math.max(min, n) : n); else setText(String(value)); };
  return <><input className="num" style={{ width }} type="number" step={step} value={text} onChange={(e) => setText(e.target.value)} onBlur={done} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />{unit && <span className="muted"> {unit}</span>}</>;
}

interface SideProps { gutter?: boolean; kerb?: boolean; sidewalk?: boolean; gutterWidth?: number; gutterDepth?: number; kerbWidth?: number; kerbHeight?: number; sidewalkWidth?: number }

function SideEditor({ label, side, onChange }: { label: string; side: SideProps; onChange: (patch: SideProps) => void }) {
  return (
    <fieldset className="corridor-side">
      <legend>{label}</legend>
      <label className="row"><input type="checkbox" checked={!!side.gutter} onChange={(e) => onChange({ gutter: e.target.checked })} /> Gutter</label>
      {side.gutter && <div className="row small">width <Num value={side.gutterWidth ?? 0.5} step={0.05} min={0.1} unit="m" width={60} onCommit={(v) => onChange({ gutterWidth: v })} /> depth <Num value={side.gutterDepth ?? 0.05} step={0.01} min={0} unit="m" width={60} onCommit={(v) => onChange({ gutterDepth: v })} /></div>}
      <label className="row"><input type="checkbox" checked={!!side.kerb || !!side.sidewalk} disabled={!!side.sidewalk} onChange={(e) => onChange({ kerb: e.target.checked })} /> Kerb</label>
      {(side.kerb || side.sidewalk) && <div className="row small">width <Num value={side.kerbWidth ?? 0.15} step={0.01} min={0.02} unit="m" width={60} onCommit={(v) => onChange({ kerbWidth: v })} /> up <Num value={side.kerbHeight ?? 0.12} step={0.01} min={0} unit="m" width={60} onCommit={(v) => onChange({ kerbHeight: v })} /></div>}
      <label className="row"><input type="checkbox" checked={!!side.sidewalk} onChange={(e) => onChange({ sidewalk: e.target.checked })} /> Sidewalk</label>
      {side.sidewalk && <div className="row small">width <Num value={side.sidewalkWidth ?? 1.5} step={0.1} min={0.3} unit="m" width={60} onCommit={(v) => onChange({ sidewalkWidth: v })} /></div>}
    </fieldset>
  );
}

function TemplateEditor({ node }: { node: NodeDto }) {
  const p = node.props;
  const num = (k: string, d: number) => (typeof p[k] === 'number' ? (p[k] as number) : d);
  const set = (args: Record<string, unknown>) => void execute(commandOf(node), { id: node.id, ...args });
  if (node.type === 'IfcPavement') {
    const road = p.template === 'road';
    const side = (k: 'left' | 'right'): SideProps => ((p[k] as SideProps | undefined) ?? (p.kerb === true ? { kerb: true, kerbWidth: num('kerbWidth', 0.1), kerbHeight: num('kerbHeight', 0.1) } : {}));
    return (
      <div className="corridor-form">
        <dl className="fields">
          <dt>Section</dt><dd><select value={road ? 'road' : 'alley'} onChange={(e) => set({ template: e.target.value, ...(e.target.value === 'road' ? { crossfall: 2.5, fall: 'crown' } : {}) })}><option value="alley">alley or path</option><option value="road">road, two lanes</option></select></dd>
          {road ? <><dt>Lane width</dt><dd><Num value={num('laneWidth', 3)} step={0.25} min={1} unit="m" onCommit={(v) => set({ laneWidth: v })} /></dd></>
            : <><dt>Width</dt><dd><Num value={num('width', 1.5)} step={0.1} min={0.2} unit="m" onCommit={(v) => set({ width: v })} /></dd></>}
          <dt>Crossfall</dt><dd><Num value={num('crossfall', road ? 2.5 : 0)} step={0.5} min={0} unit="%" onCommit={(v) => set({ crossfall: v })} />{' '}
            <select value={(p.fall as string | undefined) ?? (road ? 'crown' : 'right')} onChange={(e) => set({ fall: e.target.value })}><option value="crown">from a crown</option><option value="right">to the right</option><option value="left">to the left</option></select></dd>
          <dt>Surface course</dt><dd><Num value={num('surfaceThickness', 0.06)} step={0.01} min={0} unit="m" onCommit={(v) => set({ surfaceThickness: v })} /></dd>
          <dt>Base course</dt><dd><Num value={num('baseThickness', 0.2)} step={0.05} min={0} unit="m" onCommit={(v) => set({ baseThickness: v })} /></dd>
        </dl>
        <div className="corridor-sides">
          <SideEditor label="Left side" side={side('left')} onChange={(patch) => set({ left: { ...side('left'), ...patch }, kerb: false })} />
          <SideEditor label="Right side" side={side('right')} onChange={(patch) => set({ right: { ...side('right'), ...patch }, kerb: false })} />
        </div>
      </div>
    );
  }
  if (node.type === 'LapinTrack') {
    return (
      <dl className="fields corridor-form">
        <dt>Kind</dt><dd><select value={(p.kind as string | undefined) ?? 'main'} onChange={(e) => set({ kind: e.target.value })}><option value="main">main line</option><option value="siding">siding</option></select></dd>
        <dt /><dd><span className="muted small">The rails, sleepers and ballast come from the norm profile and the speed table, set in the inspector; the curves are under “Curves”.</span></dd>
      </dl>
    );
  }
  if (node.type === 'LapinRetainingWall') {
    return (
      <dl className="fields corridor-form">
        <dt>Stem thickness</dt><dd><Num value={num('thickness', 0.3)} step={0.05} min={0.1} unit="m" onCommit={(v) => set({ thickness: v })} /></dd>
        <dt>Height over the high ground</dt><dd><Num value={num('height', 1)} step={0.1} min={0} unit="m" onCommit={(v) => set({ height: v })} /></dd>
        <dt>High ground on the</dt><dd><select value={(p.side as string | undefined) ?? 'left'} onChange={(e) => set({ side: e.target.value })}><option value="left">left</option><option value="right">right</option></select></dd>
        <dt>Embedment</dt><dd><Num value={num('embed', 0.3)} step={0.05} min={0} unit="m" onCommit={(v) => set({ embed: v })} /></dd>
        <dt>Footing</dt><dd><Num value={num('footingWidth', 0.8)} step={0.05} min={0.1} unit="m" width={60} onCommit={(v) => set({ footingWidth: v })} /> × <Num value={num('footingThickness', 0.3)} step={0.05} min={0} unit="m" width={60} onCommit={(v) => set({ footingThickness: v })} /></dd>
      </dl>
    );
  }
  return (
    <dl className="fields corridor-form">
      <dt>Depth under the ground</dt><dd><Num value={num('depth', 0.5)} step={0.05} min={0.05} unit="m" onCommit={(v) => set({ depth: v })} /> <span className="muted small">when it follows the ground</span></dd>
      <dt>Bottom width</dt><dd><Num value={num('bottomWidth', 0.4)} step={0.05} min={0.05} unit="m" onCommit={(v) => set({ bottomWidth: v })} /></dd>
      <dt>Side slope</dt><dd>1 : <Num value={num('slope', 1)} step={0.25} min={0} onCommit={(v) => set({ slope: v })} /></dd>
    </dl>
  );
}

/**
 * The curve at each vertex of the element's path: a radius (0: a corner) and
 * a clothoid on each side of the arc; the ends have none. Saved as one list
 * per vertex (civil.setCorridor {curves}); the engine refuses curves that do
 * not fit between their neighbours, and says why when a path moved under its
 * curves (`problem`).
 */
function CurvesEditor({ node, d, onSave }: { node: NodeDto; d: CorridorData; onSave: (curves: Curve[]) => void }) {
  const pts = fromPairs(node.props.path);
  // An axis generated from another (a parallel track) has no vertices of its own to curve.
  const generated = !!node.props.reference && typeof node.props.reference === 'object';
  const curves: Curve[] = pts.map((_, i) => d.spec.curves?.[i] ?? [0, 0, 0]);
  const any = curves.some((c) => c.some((x) => x > 0));
  const put = (i: number, k: number, v: number) => onSave(curves.map((c, j) => (j === i ? c.map((x, m) => (m === k ? Math.max(0, v) : x)) as Curve : c)));
  const cell = (i: number, k: number, step: number) => (generated ? <span className="num">{curves[i][k] || '—'}</span> : <Num value={curves[i][k]} step={step} min={0} width={76} onCommit={(v) => put(i, k, v)} />);
  return (
    <div className="corridor-form">
      {generated
        ? <p className="muted small">A parallel track's curves come from its reference, offset: edit them on the reference.</p>
        : <p className="muted small">Give a vertex a radius to round the corner there; the clothoids run into and out of the arc. 0: a plain corner. Metres.</p>}
      <table className="panel-grid corridor-curves">
        <thead><tr><th>Vertex</th><th>Station</th><th>Radius</th><th>Spiral in</th><th>Spiral out</th></tr></thead>
        <tbody>
          {pts.map((_, i) => {
            const end = i === 0 || i === pts.length - 1;
            return (
              <tr key={i}>
                <td>{i + 1}</td>
                <td className="num">{d.vertices[i] != null ? stationLabel(d.vertices[i]) : ''}</td>
                {end ? <td colSpan={3} className="muted small">an end has no curve</td> : <><td>{cell(i, 0, 50)}</td><td>{cell(i, 1, 10)}</td><td>{cell(i, 2, 10)}</td></>}
              </tr>
            );
          })}
        </tbody>
      </table>
      <p className="muted small">{d.curved ? 'The axis runs on arcs and clothoids; the stations follow it.' : 'The axis is the polyline.'}</p>
      {d.problem && <p className="error-text">{d.problem}</p>}
      {!generated && <div className="row"><button type="button" className="btn compact" disabled={!any} onClick={() => onSave(curves.map(() => [0, 0, 0] as Curve))}>Clear curves</button></div>}
    </div>
  );
}

export function CorridorDialog() {
  const id = useUi((s) => s.selection);
  const nodes = useSession((s) => s.nodes);
  const node = id ? nodes.get(id) : undefined;
  const [tab, setTab] = useState<'section' | 'long' | 'curves' | 'stations'>('section');
  const [data, setData] = useState<CorridorData | null>(null);
  const [section, setSection] = useState<Section | null>(null);
  const [station, setStation] = useState<number | null>(null);
  const [newSection, setNewSection] = useState('');
  const [made, setMade] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  const valid = !!node && (node.type === 'IfcPavement' || node.type === 'LapinRetainingWall' || node.type === 'LapinTrack' || (node.type === 'IfcEarthworksCut' && Array.isArray(node.props.path)));

  // The model changes (this element, its terrain): read again.
  useEffect(() => { if (valid && id) void query<CorridorData>('civil.corridor', { id }).then(setData); }, [valid, id, nodes]);
  const at = station ?? data?.start ?? 0;
  useEffect(() => { if (valid && id && data) void query<Section>('civil.crossSection', { id, station: at }).then(setSection); }, [valid, id, data, at]);

  const spec = data?.spec;
  const setCorridor = (args: Record<string, unknown>) => void execute('civil.setCorridor', { id, ...args });
  const stepTo = (dir: number) => {
    if (!data) return;
    const marks = [...new Set([...data.stations, ...data.spec.sections, ...data.vertices])].sort((a, b) => a - b);
    const next = dir > 0 ? marks.find((s) => s > at + 1e-6) : [...marks].reverse().find((s) => s < at - 1e-6);
    if (next != null) setStation(next);
  };
  const viewStations = useMemo(() => (data ? [...new Set([...(data.spec.sections ?? [])])] : []), [data]);

  /** Section views across the element at the stations given: Lapin's own sections, drawn like any other. */
  const makeViews = async (stations: number[]) => {
    if (!id || !node) return;
    let n = 0;
    for (const s of stations) {
      const cs = await query<Section>('civil.crossSection', { id, station: s });
      if (!cs) continue;
      await execute('view.create', { kind: 'section', name: `${node.name} ${cs.label}`, start: cs.line.start, end: cs.line.end, depth: 0.5, scale: 50 });
      n++;
    }
    setMade(`${n} section view${n === 1 ? '' : 's'} made, in the project browser under Sections.`);
  };

  if (!valid || !node) {
    return (
      <div className="dialog-backdrop" onClick={close}>
        <div className="dialog" role="dialog" aria-label="Corridor" onClick={(e) => e.stopPropagation()}>
          <header className="dialog-head"><span className="panel-title">Corridor</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
          <div className="dialog-body"><p>Select a path, a retaining wall, a ditch or a track first.</p></div>
        </div>
      </div>
    );
  }

  const pvis = spec?.profile.pvis ?? [];
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide corridor-dialog" role="dialog" aria-label="Corridor" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">{node.name} <span className="muted small">· {data ? `${stationLabel(data.start)} → ${stationLabel(data.end)}, ${data.length.toFixed(2)} m${data.curved ? ', curved' : ''}` : '…'}</span>{data?.problem && <span className="chip warn" title={data.problem} onClick={() => setTab('curves')}>curves</span>}</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="tabs-inline" role="tablist">
            {([['section', 'Cross-section'], ['long', 'Long section'], ['curves', 'Curves'], ['stations', 'Stations and joints']] as const).map(([k, l]) => <button key={k} type="button" role="tab" aria-selected={tab === k} className={tab === k ? 'on' : ''} onClick={() => setTab(k)}>{l}</button>)}
          </div>

          {tab === 'section' && (
            <div className="corridor-grid">
              <div>
                <div className="row corridor-bar">
                  <button type="button" className="icon" title="Previous station" onClick={() => stepTo(-1)}><ChevronLeft size={15} /></button>
                  <input type="range" min={data?.start ?? 0} max={data?.end ?? 1} step={0.01} value={at} onChange={(e) => setStation(Number(e.target.value))} aria-label="Station" />
                  <button type="button" className="icon" title="Next station" onClick={() => stepTo(1)}><ChevronRight size={15} /></button>
                  <span className="num">{stationLabel(at)}</span>
                  <button type="button" className="btn compact" title="Keep this station for a section" onClick={() => setCorridor({ sections: [...(spec?.sections ?? []), Math.round(at * 100) / 100] })}><Plus size={12} /> Section here</button>
                </div>
                {section && <CrossSectionPlot s={section} />}
                <p className="muted small corridor-legend"><span style={{ color: NATURAL }}>- - natural ground</span> · <span style={{ color: PROPOSED }}>— proposed ground</span> · the element as it stands at this station</p>
              </div>
              <TemplateEditor node={node} />
            </div>
          )}

          {tab === 'long' && data && spec && (
            <div className="corridor-grid">
              <div>
                <LongSectionPlot d={data} station={at} onStation={setStation} onPvis={(next) => setCorridor({ profile: { mode: 'design', pvis: next } })} />
                <p className="muted small corridor-legend"><span style={{ color: NATURAL }}>- - natural</span> · <span style={{ color: PROPOSED }}>— proposed</span> · <span style={{ color: RED }}>— red line</span>. Click to move the station; with a designed profile, drag a PVI, Shift-click to add one.</p>
              </div>
              <div className="corridor-form">
                <dl className="fields">
                  <dt>Red line</dt>
                  <dd><select value={spec.profile.mode === 'design' ? 'design' : spec.profile.ground} onChange={(e) => {
                    const v = e.target.value;
                    if (v === 'design') { if (pvis.length >= 2) setCorridor({ profile: { mode: 'design' } }); else void execute('civil.profileFromGround', { id, ground: spec.profile.ground }); }
                    else setCorridor({ profile: { mode: 'ground', ground: v } });
                  }}>
                    <option value="proposed">follows the proposed ground</option><option value="existing">follows the natural ground</option><option value="design">designed (PVIs)</option>
                  </select></dd>
                </dl>
                <div className="row">
                  <button type="button" className="btn compact" onClick={() => void execute('civil.profileFromGround', { id, ground: 'existing' })}>Fit to natural ground</button>
                  <button type="button" className="btn compact" onClick={() => void execute('civil.profileFromGround', { id, ground: 'proposed' })}>Fit to proposed ground</button>
                </div>
                {node.type === 'LapinRetainingWall' && <p className="muted small">On a wall the red line is the crown.</p>}
                {node.type === 'IfcEarthworksCut' && <p className="muted small">On a ditch the red line is the bottom.</p>}
                {spec.profile.mode === 'design' && (
                  <table className="panel-grid corridor-pvis">
                    <thead><tr><th>Station</th><th>Elevation</th><th>Curve</th><th /></tr></thead>
                    <tbody>
                      {pvis.map((p, i) => {
                        const put = (k: number, v: number) => setCorridor({ profile: { mode: 'design', pvis: pvis.map((q, j) => (j === i ? q.map((x, m) => (m === k ? v : x)) : q)) } });
                        return (
                          <tr key={i}>
                            <td><Num value={p[0]} step={1} width={72} onCommit={(v) => put(0, v)} /></td>
                            <td><Num value={p[1]} step={0.01} width={72} onCommit={(v) => put(1, v)} /></td>
                            <td>{i > 0 && i < pvis.length - 1 ? <Num value={p[2]} step={1} min={0} width={56} onCommit={(v) => put(2, v)} /> : <span className="muted">—</span>}</td>
                            <td><button type="button" className="icon" title="Remove" disabled={pvis.length <= 2} onClick={() => setCorridor({ profile: { mode: 'design', pvis: pvis.filter((_, j) => j !== i) } })}><Trash2 size={13} /></button></td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                )}
                {spec.profile.mode === 'design' && pvis.length >= 2 && (
                  <p className="muted small">Grades: {pvis.slice(1).map((p, i) => `${(((p[1] - pvis[i][1]) / (p[0] - pvis[i][0])) * 100).toFixed(2)} %`).join(' · ')}</p>
                )}
              </div>
            </div>
          )}

          {tab === 'curves' && data && (
            <div className="corridor-grid even">
              <CurvesEditor node={node} d={data} onSave={(curves) => setCorridor({ curves })} />
              <div className="corridor-form">
                <p className="eyebrow">Horizontal alignment</p>
                <p className="muted small">Each vertex is a point of intersection (PI): the arc of its radius is fitted between the two straights, with a clothoid of the length given before and after it. The arc and both clothoids must fit on the straights to the neighbouring vertices, else the engine refuses the curve and says why.</p>
                {node.type === 'LapinTrack' && <p className="muted small">On a track the arcs are the cant's curves: design the cant in the inspector once the radii are set.</p>}
              </div>
            </div>
          )}

          {tab === 'stations' && data && spec && (
            <div className="corridor-grid even">
              <div className="corridor-form">
                <dl className="fields">
                  <dt>Start station</dt><dd><Num value={spec.stationStart} step={1} unit="m" onCommit={(v) => setCorridor({ stationStart: v })} /> <span className="muted small">{stationLabel(spec.stationStart)}</span></dd>
                  <dt>Stations every</dt><dd><Num value={spec.interval} step={1} min={0.5} unit="m" onCommit={(v) => setCorridor({ interval: v })} /> <span className="muted small">{data.stations.length} stations</span></dd>
                  <dt>Joints every</dt><dd><Num value={spec.joints.every} step={1} min={0} unit="m" onCommit={(v) => setCorridor({ joints: { every: v } })} /> <span className="muted small">0: unbroken</span></dd>
                  <dt>Joint width</dt><dd><Num value={Math.round(spec.joints.width * 1000)} step={5} min={1} unit="mm" onCommit={(v) => setCorridor({ joints: { width: v / 1000 } })} /></dd>
                  <dt>Joints at</dt><dd><input defaultValue={spec.joints.at.join(', ')} key={spec.joints.at.join(',')} placeholder="stations, comma between" onBlur={(e) => setCorridor({ joints: { at: e.target.value.split(',').map((x) => Number(x.trim())).filter((x) => x.toString() !== '' && Number.isFinite(x)) } })} /></dd>
                </dl>
                <p className="muted small">Corners are mitred, so the element runs on unbroken; it breaks only at its joints ({data.joints.length}).</p>
              </div>
              <div className="corridor-form">
                <p className="eyebrow">Sections to draw</p>
                <ul className="plain">
                  {viewStations.length === 0 && <li className="muted small">None yet: add stations here or with “Section here”.</li>}
                  {viewStations.map((s) => (
                    <li key={s} className="row"><span className="grow num">{stationLabel(s)}</span>
                      <button type="button" className="icon" title="Remove" onClick={() => setCorridor({ sections: spec.sections.filter((x) => x !== s) })}><Trash2 size={13} /></button></li>
                  ))}
                </ul>
                <div className="row">
                  <input className="num" style={{ width: 90 }} type="number" step={1} value={newSection} placeholder="station" onChange={(e) => setNewSection(e.target.value)} />
                  <button type="button" className="btn compact" disabled={!Number.isFinite(Number(newSection)) || newSection.trim() === ''} onClick={() => { setCorridor({ sections: [...spec.sections, Number(newSection)] }); setNewSection(''); }}><Plus size={12} /> Add</button>
                </div>
                <div className="row">
                  <button type="button" className="btn" disabled={viewStations.length === 0} onClick={() => void makeViews(viewStations)}>Section views at these</button>
                  <button type="button" className="btn" onClick={() => void makeViews(data.stations)}>At every station ({data.stations.length})</button>
                </div>
                {made && <p className="muted small">{made}</p>}
              </div>
            </div>
          )}
        </div>
        <footer className="dialog-foot"><span className="muted small grow">Each change is one undo step.</span><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
