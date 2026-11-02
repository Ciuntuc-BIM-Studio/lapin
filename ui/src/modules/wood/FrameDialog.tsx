/**
 * The timber frame configurator (docs/plan-wood.md, decision 239): the project's wall types on the left; on the right the
 * one being edited — built as a timber frame or not, its studs, plates and noggins, where the module starts, whether it
 * carries load, the openings' fitting gaps and their rules by width — beside a sample wall the engine frames as it would
 * (a window and a door, every member). Two presets: the Nordic practice and the Romanian. Saving is one command.
 */

import { useEffect, useMemo, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ofClass, relatedOf } from '../../engine/model';

export type Header = { kind: string; section: string; plies: number };
export type Rule = { maxWidth: number; kings: number; jacks: number; header: Header; sill: number };
export interface Spec {
  construction: 'timberFrame'; studSection: string; spacing: number; grade: string; bottomPlates: number; topPlates: number; endStuds: number;
  noggins: number[]; layoutFrom: 'start' | 'project'; loadBearing: boolean; gapSide: number; gapTop: number; gapBottom: number; minGap: number; minPier: number; openings: Rule[];
  corner?: 'three' | 'california' | 'l'; tee?: 'double' | 'ladder';
  boards?: { layer: number; width: number; height: number }[];
  /** Prefabricated panels (decision 245): the longest panel, 0 for a frame built on site; the shortest. */
  panelMax?: number; panelMin?: number;
}
export interface Framed {
  problem?: string; length: number; height: number;
  members: { role: string; section: string; opening?: string | null; face: number[] }[];
  insulation: number[][]; counts: Record<string, number>; warnings: string[];
  panels?: [number, number][];
}

export const SECTIONS = ['45x95 mm', '45x120 mm', '45x145 mm', '45x170 mm', '45x195 mm', '45x220 mm', '45x245 mm', '50x100 mm', '50x150 mm', '50x200 mm', '70x220 mm', '90x315 mm', '90x360 mm', '115x360 mm'];
export const GRADES = ['C14', 'C16', 'C18', 'C24', 'C30', 'GL24h', 'GL28h', 'GL30c'];
export const HEADERS = ['none', 'solid', 'lvl', 'glulam'];

const NORDIC: Spec = {
  construction: 'timberFrame', studSection: '45x145 mm', spacing: 0.6, grade: 'C24', bottomPlates: 1, topPlates: 2, endStuds: 2, noggins: [], layoutFrom: 'start', loadBearing: true,
  gapSide: 0.01, gapTop: 0.01, gapBottom: 0.01, minGap: 0.05, minPier: 0.2,
  openings: [
    { maxWidth: 0.9, kings: 1, jacks: 1, header: { kind: 'solid', section: '45x195 mm', plies: 2 }, sill: 1 },
    { maxWidth: 1.8, kings: 2, jacks: 2, header: { kind: 'solid', section: '45x245 mm', plies: 2 }, sill: 2 },
    { maxWidth: 99, kings: 2, jacks: 2, header: { kind: 'glulam', section: '90x360 mm', plies: 1 }, sill: 2 },
  ],
};
const ROMANIAN: Spec = {
  ...NORDIC, studSection: '50x150 mm', spacing: 0.625, noggins: [1.25], gapSide: 0.015, gapTop: 0.015, gapBottom: 0.015,
  openings: [
    { maxWidth: 0.9, kings: 1, jacks: 1, header: { kind: 'solid', section: '50x200 mm', plies: 2 }, sill: 1 },
    { maxWidth: 1.8, kings: 2, jacks: 2, header: { kind: 'solid', section: '50x200 mm', plies: 3 }, sill: 2 },
    { maxWidth: 99, kings: 2, jacks: 2, header: { kind: 'glulam', section: '115x360 mm', plies: 1 }, sill: 2 },
  ],
};

/** A CLT wall or floor type's rules (decision 241). */
export interface CltSpec {
  construction: 'clt'; plies: number[]; grade: string; maxLength: number; maxWidth: number; joint: string; minEdge: number; gap: number; direction: string;
  /** Decision 246: the openings' corners as the router leaves them, and the joint's sizes. */
  corners?: 'square' | 'round' | 'dogbone'; cornerRadius?: number; lapWidth?: number; splineWidth?: number; splineDepth?: number;
}
export const CLT_WALL: CltSpec = { construction: 'clt', plies: [40, 40, 40], grade: 'C24', maxLength: 16, maxWidth: 3.5, joint: 'halfLap', minEdge: 0.3, gap: 0.01, direction: 'auto', corners: 'square', cornerRadius: 0.02, lapWidth: 0.1, splineWidth: 0.12, splineDepth: 0.027 };
export const CLT_FLOOR: CltSpec = { ...CLT_WALL, plies: [40, 20, 40, 20, 40] };
interface CltPreview { problem?: string; length: number; height: number; joints: number[]; openings: number[][]; cutters?: number[][]; warnings: string[]; label: string }

/** A joisted floor type's rules (decision 244). */
export interface JoistSpec { construction: 'joists'; section: string; spacing: number; grade: string; direction: string; rim: boolean; blockingRows: number; trimmers: number; headerPlies: number; fill: boolean }
const JOISTS: JoistSpec = { construction: 'joists', section: '45x220 mm', spacing: 0.6, grade: 'C24', direction: 'auto', rim: true, blockingRows: 1, trimmers: 2, headerPlies: 2, fill: false };
interface JoistPreview { problem?: string; members: { role: string; pts: number[] }[]; counts: Record<string, number>; warnings: string[] }

/** A sample floor, 6 × 4 m with a stairwell: its joists in plan. */
function JoistPlan({ p }: { p: JoistPreview }) {
  const width = 480, height = 330, pad = 14;
  const k = Math.min((width - 2 * pad) / 6, (height - 2 * pad) / 4);
  const X = (x: number) => pad + x * k;
  const Y = (y: number) => height - pad - y * k;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} aria-label="Floor joists">
      <rect x={X(0)} y={Y(4)} width={6 * k} height={4 * k} fill="#f6efe2" stroke="#8a8378" strokeWidth={0.5} />
      <rect x={X(0.6)} y={Y(1.6)} width={2.8 * k} height={1 * k} fill="#ffffff" stroke="#8a8378" strokeDasharray="3 2" strokeWidth={0.6} />
      {p.members.map((m, i) => {
        const pts: string[] = [];
        for (let j = 0; j + 1 < m.pts.length; j += 2) pts.push(`${X(m.pts[j])},${Y(m.pts[j + 1])}`);
        return <polygon key={i} points={pts.join(' ')} fill={ROLE_FILL[m.role] ?? (m.role === 'rim' || m.role === 'trimmer' ? '#a87a46' : '#c99a64')} stroke="#4a3418" strokeWidth={0.4}><title>{m.role}</title></polygon>;
      })}
    </svg>
  );
}

/** A sample CLT wall: its panels between the joints, its rough openings. */
function CltElevation({ p }: { p: CltPreview }) {
  const width = 560, height = 200, pad = 14;
  const k = Math.min((width - 2 * pad) / p.length, (height - 2 * pad) / p.height);
  const X = (u: number) => pad + u * k;
  const Z = (z: number) => height - pad - z * k;
  const edges = [0, ...p.joints, p.length];
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} aria-label="CLT panels">
      {edges.slice(0, -1).map((a, i) => <rect key={i} x={X(a) + 1} y={Z(p.height)} width={(edges[i + 1] - a) * k - 2} height={p.height * k} fill={i % 2 ? '#e2c08e' : '#d9b27c'} stroke="#4a3418" strokeWidth={0.6} />)}
      {(p.cutters ?? []).map((c, i) => {
        const pts: string[] = [];
        for (let j = 0; j + 1 < c.length; j += 2) pts.push(`${X(c[j])},${Z(c[j + 1])}`);
        return <polygon key={`c${i}`} points={pts.join(' ')} fill="#ffffff" stroke="#4a3418" strokeWidth={0.6} />;
      })}
      {!p.cutters && p.openings.map(([l, r, b, t], i) => <rect key={`o${i}`} x={X(l)} y={Z(t)} width={(r - l) * k} height={(t - b) * k} fill="#ffffff" stroke="#4a3418" strokeWidth={0.6} />)}
      {edges.slice(0, -1).map((a, i) => <text key={`t${i}`} x={(X(a) + X(edges[i + 1])) / 2} y={Z(p.height) + 14} textAnchor="middle" fontSize={10}>{Math.round((edges[i + 1] - a) * 1000)}</text>)}
    </svg>
  );
}

export const ROLE_FILL: Record<string, string> = {
  plate: '#b48650', end: '#c99a64', stud: '#d4a970', king: '#c08a50', jack: '#cf9a5c', header: '#a87a46', sill: '#a87a46', cripple: '#dcb27c', noggin: '#c99a64',
};

/** A length in mm, kept as text while typing. */
function Mm({ label, value, onChange, title }: { label: string; value: number; onChange: (m: number) => void; title?: string }) {
  const [text, setText] = useState(String(Math.round(value * 1000)));
  useEffect(() => setText(String(Math.round(value * 1000))), [value]);
  return (
    <label className="field-line wood-field" title={title}><span className="eyebrow">{label}</span>
      <input className="num" value={text} onChange={(e) => setText(e.target.value)} onBlur={() => { const v = Number(text.replace(',', '.')); if (Number.isFinite(v) && v >= 0) onChange(v / 1000); else setText(String(Math.round(value * 1000))); }} />
    </label>
  );
}

function Count({ label, value, onChange, min = 0, max = 4 }: { label: string; value: number; onChange: (n: number) => void; min?: number; max?: number }) {
  return (
    <label className="field-line wood-field"><span className="eyebrow">{label}</span>
      <input className="num" type="number" min={min} max={max} value={value} onChange={(e) => onChange(Math.max(min, Math.min(max, Math.round(Number(e.target.value) || 0))))} />
    </label>
  );
}

/** A framed wall's elevation: its members by role, the insulation, cotes of the rough openings. */
export function FrameElevation({ f, width = 560, height = 260 }: { f: Framed; width?: number; height?: number }) {
  const pad = 14;
  const k = Math.min((width - 2 * pad) / Math.max(0.1, f.length), (height - 2 * pad) / Math.max(0.1, f.height));
  const X = (u: number) => pad + u * k;
  const Z = (z: number) => height - pad - z * k;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} aria-label="Frame elevation">
      {f.insulation.map(([u0, u1, z0, z1l, z1r], i) => <polygon key={`i${i}`} points={`${X(u0)},${Z(z0)} ${X(u1)},${Z(z0)} ${X(u1)},${Z(z1r)} ${X(u0)},${Z(z1l)}`} fill="#f3e6a8" stroke="none" />)}
      {f.members.map((m, i) => {
        const pts: string[] = [];
        for (let j = 0; j + 1 < m.face.length; j += 2) pts.push(`${X(m.face[j])},${Z(m.face[j + 1])}`);
        return <polygon key={i} points={pts.join(' ')} fill={ROLE_FILL[m.role] ?? '#c99a64'} stroke="#4a3418" strokeWidth={0.5}><title>{`${m.role} ${m.section}`}</title></polygon>;
      })}
      {(f.panels?.length ?? 0) > 1 && f.panels!.slice(1).map(([u0], i) => <line key={`p${i}`} x1={X(u0)} x2={X(u0)} y1={Z(f.height) - 6} y2={Z(0) + 4} stroke="#0b5cad" strokeWidth={1.5} strokeDasharray="5 3" />)}
      {(f.panels?.length ?? 0) > 1 && f.panels!.map(([u0, u1], i) => <text key={`t${i}`} x={X((u0 + u1) / 2)} y={pad - 3} fontSize={10} textAnchor="middle" fill="#0b5cad">{`P${i + 1} · ${Math.round((u1 - u0) * 1000)}`}</text>)}
    </svg>
  );
}

export function FrameDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = [...ofClass(model, 'IfcWallType'), ...ofClass(model, 'IfcSlabType')];
  const [editing, setEditing] = useState<string | null>(useUi.getState().wallTypeId ?? types[0]?.id ?? null);
  const type = editing ? nodes.get(editing) : undefined;
  const isSlab = type?.type === 'IfcSlabType';
  const stored = (type?.props.wood as (Spec | CltSpec) | undefined) ?? null;
  const [spec, setSpec] = useState<Spec>(stored?.construction === 'timberFrame' ? stored as Spec : NORDIC);
  const [clt, setClt] = useState<CltSpec>(stored?.construction === 'clt' ? stored as CltSpec : isSlab ? CLT_FLOOR : CLT_WALL);
  const [mode, setMode] = useState<'layers' | 'frame' | 'clt' | 'joists'>(stored?.construction === 'clt' ? 'clt' : (stored?.construction as string) === 'joists' ? 'joists' : stored ? 'frame' : 'layers');
  const [joists, setJoists] = useState<JoistSpec>((stored?.construction as string) === 'joists' ? stored as unknown as JoistSpec : JOISTS);
  const [joistPreview, setJoistPreview] = useState<JoistPreview | null>(null);
  useEffect(() => { let live = true; void query<JoistPreview>('wood.joistPreview', { spec: joists }).then((p) => { if (live) setJoistPreview(p); }); return () => { live = false; }; }, [joists]);
  const setJ = (patch: Partial<JoistSpec>) => setJoists((j) => ({ ...j, ...patch }));
  const on = mode === 'frame';
  const setOn = (v: boolean) => setMode(v ? 'frame' : 'layers');
  const [cltPreview, setCltPreview] = useState<CltPreview | null>(null);
  useEffect(() => { let live = true; void query<CltPreview>('wood.cltPreview', { spec: clt }).then((p) => { if (live) setCltPreview(p); }); return () => { live = false; }; }, [clt]);
  const [pliesText, setPliesText] = useState(clt.plies.join(' '));
  useEffect(() => setPliesText(clt.plies.join(' ')), [clt.plies]);
  const [preview, setPreview] = useState<Framed | null>(null);
  const [layers, setLayers] = useState<{ index: number; name: string; thickness: number; core: boolean }[]>([]);
  useEffect(() => { if (editing) void query<typeof layers>('wood.layers', { id: editing }).then((l) => setLayers(l ?? [])); }, [editing, nodes]);
  // Loaded when the type picked changes, not on every diff (that would throw away what is being typed).
  useEffect(() => {
    const n = editing ? useSession.getState().nodes.get(editing) : undefined;
    const s = (n?.props.wood ?? null) as (Spec | CltSpec) | null;
    setSpec(s?.construction === 'timberFrame' ? structuredClone(s as Spec) : structuredClone(NORDIC));
    setClt(s?.construction === 'clt' ? structuredClone(s as CltSpec) : structuredClone(n?.type === 'IfcSlabType' ? CLT_FLOOR : CLT_WALL));
    setJoists((s?.construction as string) === 'joists' ? structuredClone(s as unknown as JoistSpec) : structuredClone(JOISTS));
    setMode(s?.construction === 'clt' ? 'clt' : (s?.construction as string) === 'joists' ? 'joists' : s ? 'frame' : 'layers');
  }, [editing]);
  useEffect(() => {
    let live = true;
    // A prefabricated type's sample is longer, so it shows its joints.
    void query<Framed>('wood.preview', spec.panelMax ? { spec, length: 10, openings: [{ along: 1.7, sill: 0.9, width: 1.2, height: 1.3 }, { along: 4.6, sill: 0, width: 0.9, height: 2.1, door: true }, { along: 7.8, sill: 0.9, width: 1.8, height: 1.3 }] } : { spec }).then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [spec]);

  const set = (patch: Partial<Spec>) => setSpec((s) => ({ ...s, ...patch }));
  const setRule = (i: number, patch: Partial<Rule>) => set({ openings: spec.openings.map((r, k) => (k === i ? { ...r, ...patch } : r)) });
  const setHeader = (i: number, patch: Partial<Header>) => setRule(i, { header: { ...spec.openings[i].header, ...patch } });
  const close = () => useUi.setState({ dialog: null });
  const save = () => {
    if (!editing) return;
    if (mode === 'frame') void execute('wood.setWallFrame', { id: editing, spec });
    else if (mode === 'clt') void execute('wood.setClt', { id: editing, spec: clt });
    else if (mode === 'joists') void execute('wood.setFloorJoists', { id: editing, spec: joists });
    else void execute(stored?.construction === 'clt' ? 'wood.setClt' : (stored?.construction as string) === 'joists' ? 'wood.setFloorJoists' : 'wood.setWallFrame', { id: editing, spec: null });
  };
  const setC = (patch: Partial<CltSpec>) => setClt((c) => ({ ...c, ...patch }));
  const [nogginText, setNogginText] = useState(spec.noggins.map((n) => Math.round(n * 1000)).join(' '));
  useEffect(() => setNogginText(spec.noggins.map((n) => Math.round(n * 1000)).join(' ')), [spec.noggins]);

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Timber frame">
      <div className="dialog wide wood-dialog">
        <header className="dialog-head">
          <span className="panel-title">Timber frame</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}>
                <button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} data-class={t.type} onClick={() => setEditing(t.id)}>
                  {t.name}<span className="muted small"> · {{ clt: 'CLT', joists: 'joists', timberFrame: 'timber frame' }[(t.props.wood as { construction?: string } | undefined)?.construction ?? ''] ?? 'layers'} · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span>
                </button>
              </li>
            ))}
          </ul>
          {type && (
            <div className="joinery-edit">
              <div className="joinery-form">
                <label className="field-line wood-field"><span className="eyebrow">Built</span>
                  <select value={mode} data-testid="wood-mode" onChange={(e) => setMode(e.target.value as typeof mode)}>
                    <option value="layers">of its layers</option>
                    {!isSlab && <option value="frame">as a timber frame</option>}
                    {isSlab && <option value="joists">on joists</option>}
                    <option value="clt">in CLT panels</option>
                  </select>
                </label>
                {!isSlab && <label className="field-line wood-field"><span className="eyebrow">Timber frame</span>
                  <input type="checkbox" checked={on} data-testid="wood-on" onChange={(e) => setOn(e.target.checked)} />
                </label>}
                {mode === 'joists' && (
                  <>
                    <label className="field-line wood-field"><span className="eyebrow">Joists</span>
                      <select value={joists.section} onChange={(e) => setJ({ section: e.target.value })}>{SECTIONS.map((s) => <option key={s}>{s}</option>)}</select>
                    </label>
                    <Mm label="Spacing" value={joists.spacing} onChange={(v) => setJ({ spacing: v })} />
                    <label className="field-line wood-field"><span className="eyebrow">Grade</span>
                      <select value={joists.grade} onChange={(e) => setJ({ grade: e.target.value })}>{GRADES.map((g) => <option key={g}>{g}</option>)}</select>
                    </label>
                    <label className="field-line wood-field"><span className="eyebrow">Span</span>
                      <select value={joists.direction} onChange={(e) => setJ({ direction: e.target.value })}><option value="auto">the short way</option><option value="x">along x</option><option value="y">along y</option></select>
                    </label>
                    <label className="field-line wood-field"><span className="eyebrow">Rim joist</span><input type="checkbox" checked={joists.rim} onChange={(e) => setJ({ rim: e.target.checked })} /></label>
                    <Count label="Blocking rows" value={joists.blockingRows} max={5} onChange={(n) => setJ({ blockingRows: n })} />
                    <Count label="Trimmers" value={joists.trimmers} min={1} onChange={(n) => setJ({ trimmers: n })} />
                    <Count label="Header plies" value={joists.headerPlies} min={1} onChange={(n) => setJ({ headerPlies: n })} />
                    <label className="field-line wood-field" title="The cavities between the joists filled with the core's material"><span className="eyebrow">Fill the cavities</span><input type="checkbox" checked={joists.fill} onChange={(e) => setJ({ fill: e.target.checked })} /></label>
                  </>
                )}
                {mode === 'clt' && (
                  <>
                    <label className="field-line wood-field" title="Each ply's thickness, outside to outside, in mm: 3, 5, 7 or 9 of them"><span className="eyebrow">Plies (mm)</span>
                      <input value={pliesText} onChange={(e) => setPliesText(e.target.value)} onBlur={() => setC({ plies: pliesText.split(/[ ,;/]+/).map(Number).filter((x) => x > 0) })} />
                    </label>
                    <label className="field-line wood-field"><span className="eyebrow">Grade</span>
                      <select value={clt.grade} onChange={(e) => setC({ grade: e.target.value })}>{[...GRADES, 'T14', 'T22'].map((g) => <option key={g}>{g}</option>)}</select>
                    </label>
                    <Mm label="Longest panel" value={clt.maxLength} onChange={(v) => setC({ maxLength: v })} />
                    <Mm label={isSlab ? 'Widest panel' : 'Highest panel'} value={clt.maxWidth} onChange={(v) => setC({ maxWidth: v })} />
                    <label className="field-line wood-field"><span className="eyebrow">Joint</span>
                      <select value={clt.joint} onChange={(e) => setC({ joint: e.target.value })}><option value="butt">butt</option><option value="halfLap">half lap</option><option value="spline">spline</option></select>
                    </label>
                    {clt.joint === 'halfLap' && <Mm label="Lap" value={clt.lapWidth ?? 0.1} onChange={(v) => setC({ lapWidth: v })} title="How far the two halves overlap" />}
                    {clt.joint === 'spline' && <Mm label="Spline width" value={clt.splineWidth ?? 0.12} onChange={(v) => setC({ splineWidth: v })} title="The plywood strip let into a rebate across the joint" />}
                    {clt.joint === 'spline' && <Mm label="Spline depth" value={clt.splineDepth ?? 0.027} onChange={(v) => setC({ splineDepth: v })} />}
                    {!isSlab && (
                      <label className="field-line wood-field" title="A router cannot cut a square inside corner: it leaves its radius, or overcuts so a square frame still fits"><span className="eyebrow">Opening corners</span>
                        <select data-testid="clt-corners" value={clt.corners ?? 'square'} onChange={(e) => setC({ corners: e.target.value as CltSpec['corners'] })}>
                          <option value="square">square</option><option value="round">the router's radius</option><option value="dogbone">dog-bone overcut</option>
                        </select>
                      </label>
                    )}
                    {!isSlab && (clt.corners ?? 'square') !== 'square' && <Mm label="Router radius" value={clt.cornerRadius ?? 0.02} onChange={(v) => setC({ cornerRadius: v })} />}
                    {!isSlab && <Mm label="Opening to panel edge" value={clt.minEdge} onChange={(v) => setC({ minEdge: v })} title="No joint closer than this to an opening's side" />}
                    {!isSlab && <Mm label="Fitting gap" value={clt.gap} onChange={(v) => setC({ gap: v })} />}
                    {isSlab && (
                      <label className="field-line wood-field"><span className="eyebrow">Span</span>
                        <select value={clt.direction} onChange={(e) => setC({ direction: e.target.value })}><option value="auto">the short way</option><option value="x">along x</option><option value="y">along y</option></select>
                      </label>
                    )}
                  </>
                )}
                {on && (
                  <>
                    <div className="row">
                      <span className="eyebrow">Presets</span>
                      <button type="button" className="btn compact" onClick={() => setSpec(structuredClone(NORDIC))}>Nordic 45×145 / 600</button>
                      <button type="button" className="btn compact" onClick={() => setSpec(structuredClone(ROMANIAN))}>Romanian 50×150 / 625</button>
                    </div>
                    <label className="field-line wood-field"><span className="eyebrow">Studs</span>
                      <select value={spec.studSection} onChange={(e) => set({ studSection: e.target.value })}>{SECTIONS.map((s) => <option key={s}>{s}</option>)}</select>
                    </label>
                    <Mm label="Spacing" value={spec.spacing} onChange={(v) => set({ spacing: v })} />
                    <label className="field-line wood-field"><span className="eyebrow">Grade</span>
                      <select value={spec.grade} onChange={(e) => set({ grade: e.target.value })}>{GRADES.map((g) => <option key={g}>{g}</option>)}</select>
                    </label>
                    <Count label="Bottom plates" value={spec.bottomPlates} min={1} max={3} onChange={(n) => set({ bottomPlates: n })} />
                    <Count label="Top plates" value={spec.topPlates} min={1} max={3} onChange={(n) => set({ topPlates: n })} />
                    <Count label="End studs" value={spec.endStuds} min={1} max={3} onChange={(n) => set({ endStuds: n })} />
                    <label className="field-line wood-field" title="Heights over the wall's base, in mm, e.g. 1250"><span className="eyebrow">Noggin rows (mm)</span>
                      <input value={nogginText} onChange={(e) => setNogginText(e.target.value)} onBlur={() => set({ noggins: nogginText.split(/[ ,;]+/).map(Number).filter((x) => x > 0).map((x) => x / 1000) })} />
                    </label>
                    <label className="field-line wood-field"><span className="eyebrow">Module from</span>
                      <select value={spec.layoutFrom} onChange={(e) => set({ layoutFrom: e.target.value as Spec['layoutFrom'] })}>
                        <option value="start">the wall's start</option><option value="project">the project's origin</option>
                      </select>
                    </label>
                    <label className="field-line wood-field" title="Where another wall stops against this one's end (a butt corner; a mitred one keeps the end studs)"><span className="eyebrow">Corners</span>
                      <select value={spec.corner ?? 'three'} onChange={(e) => set({ corner: e.target.value as Spec['corner'] })}>
                        <option value="three">three studs</option><option value="california">California (a flat stud inside)</option><option value="l">end studs only</option>
                      </select>
                    </label>
                    <label className="field-line wood-field" title="Where another wall stops against this one in its run"><span className="eyebrow">T junctions</span>
                      <select value={spec.tee ?? 'double'} onChange={(e) => set({ tee: e.target.value as Spec['tee'] })}>
                        <option value="double">a stud each side</option><option value="ladder">studs and ladder blocking</option>
                      </select>
                    </label>
                    <label className="field-line wood-field" title="Made in the shop in panels, the joints clear of the openings and on the module where they can be"><span className="eyebrow">Prefabricated panels</span>
                      <input type="checkbox" data-testid="wood-panels" checked={(spec.panelMax ?? 0) > 0} onChange={(e) => set({ panelMax: e.target.checked ? 4.8 : 0 })} />
                    </label>
                    {(spec.panelMax ?? 0) > 0 && (
                      <>
                        <Mm label="Longest panel" value={spec.panelMax!} onChange={(v) => set({ panelMax: v })} title="As long as the shop's table and the truck allow" />
                        <Mm label="Shortest panel" value={spec.panelMin ?? 0.6} onChange={(v) => set({ panelMin: v })} />
                      </>
                    )}
                    <label className="field-line wood-field" title="A partition carries nothing: a flat member over its openings"><span className="eyebrow">Load-bearing</span>
                      <input type="checkbox" checked={spec.loadBearing} onChange={(e) => set({ loadBearing: e.target.checked })} />
                    </label>
                    <p className="eyebrow">Boards</p>
                    {layers.filter((l) => !l.core).map((l) => {
                      const b = (spec.boards ?? []).find((x) => x.layer === l.index);
                      const setBoard = (patch: Partial<{ width: number; height: number }> | null) => set({ boards: patch === null
                        ? (spec.boards ?? []).filter((x) => x.layer !== l.index)
                        : [...(spec.boards ?? []).filter((x) => x.layer !== l.index), { layer: l.index, width: b?.width ?? 1.2, height: b?.height ?? 2.7, ...patch }] });
                      return (
                        <div key={l.index} className="row wood-board" title="Laid as sheets: upright, their joints on the studs' module, cut at the rough openings">
                          <label className="row"><input type="checkbox" checked={Boolean(b)} onChange={(e) => setBoard(e.target.checked ? {} : null)} /> {l.name} <span className="muted small">{Math.round(l.thickness * 1000)} mm</span></label>
                          {b && <>
                            <input className="num" defaultValue={Math.round(b.width * 1000)} key={`bw${l.index}${b.width}`} title="Sheet width (mm)" onBlur={(e) => setBoard({ width: Number(e.target.value) / 1000 })} />
                            <span>×</span>
                            <input className="num" defaultValue={Math.round(b.height * 1000)} key={`bh${l.index}${b.height}`} title="Sheet height (mm)" onBlur={(e) => setBoard({ height: Number(e.target.value) / 1000 })} />
                          </>}
                        </div>
                      );
                    })}
                    <p className="eyebrow">Openings</p>
                    <Mm label="Gap at the sides" value={spec.gapSide} onChange={(v) => set({ gapSide: v })} title="The rough opening is the door or window and these gaps" />
                    <Mm label="Gap on top" value={spec.gapTop} onChange={(v) => set({ gapTop: v })} />
                    <Mm label="Gap under a window" value={spec.gapBottom} onChange={(v) => set({ gapBottom: v })} />
                    <Mm label="Narrowest pier" value={spec.minPier} onChange={(v) => set({ minPier: v })} title="Two openings closer than this share one header" />
                    <table className="wood-rules">
                      <thead><tr><th title="The rough opening's width, up to">≤ mm</th><th title="King studs a side">K</th><th title="Jack studs a side">J</th><th>Header</th><th>Section</th><th title="Plies">×</th><th title="Sill plies">S</th><th /></tr></thead>
                      <tbody>
                        {spec.openings.map((r, i) => (
                          <tr key={i}>
                            <td><input className="num" defaultValue={r.maxWidth >= 98 ? '' : Math.round(r.maxWidth * 1000)} key={`w${i}${r.maxWidth}`} placeholder="any"
                              onBlur={(e) => setRule(i, { maxWidth: e.target.value.trim() === '' ? 99 : Number(e.target.value) / 1000 })} /></td>
                            <td><input className="num" type="number" min={0} max={4} value={r.kings} onChange={(e) => setRule(i, { kings: Number(e.target.value) })} /></td>
                            <td><input className="num" type="number" min={0} max={4} value={r.jacks} onChange={(e) => setRule(i, { jacks: Number(e.target.value) })} /></td>
                            <td><select value={r.header.kind} onChange={(e) => setHeader(i, { kind: e.target.value })}>{HEADERS.map((h) => <option key={h}>{h}</option>)}</select></td>
                            <td><select value={r.header.section} onChange={(e) => setHeader(i, { section: e.target.value })}>{[...new Set([...SECTIONS, r.header.section])].map((s) => <option key={s}>{s}</option>)}</select></td>
                            <td><input className="num" type="number" min={1} max={4} value={r.header.plies} onChange={(e) => setHeader(i, { plies: Number(e.target.value) })} /></td>
                            <td><input className="num" type="number" min={0} max={3} value={r.sill} onChange={(e) => setRule(i, { sill: Number(e.target.value) })} /></td>
                            <td>{spec.openings.length > 1 && <button type="button" className="icon mini" title="Remove the band" onClick={() => set({ openings: spec.openings.filter((_, k) => k !== i) })}><Trash2 size={11} /></button>}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    <button type="button" className="btn compact" onClick={() => set({ openings: [...spec.openings, { ...spec.openings[spec.openings.length - 1], maxWidth: 99 }].sort((a, b) => a.maxWidth - b.maxWidth) })}><Plus size={12} /> Band</button>
                  </>
                )}
              </div>
              <div className="joinery-preview">
                {on && preview && !preview.problem && <FrameElevation f={preview} />}
                {on && preview?.problem && <p className="error-text">{preview.problem}</p>}
                {on && preview && !preview.problem && (
                  <>
                    {spec.panelMax
                      ? <p className="muted small">A sample wall, 10 m × 2.7 m: two windows and a door, cut into panels.</p>
                      : <p className="muted small">A sample wall, 6 m × 2.7 m: a window 1200 × 1300, a door 900 × 2100.</p>}
                    <p className="muted small">{Object.entries(preview.counts).map(([k, v]) => `${v} ${k}`).join(' · ')}</p>
                    {preview.warnings.map((w, i) => <p key={i} className="error-text">{w}</p>)}
                  </>
                )}
                {mode === 'clt' && !isSlab && cltPreview && !cltPreview.problem && (
                  <>
                    <CltElevation p={cltPreview} />
                    <p className="muted small">{`A sample wall, 12 m: ${cltPreview.label}, ${cltPreview.joints.length + 1} panels.`}</p>
                    {cltPreview.warnings.map((w, i) => <p key={i} className="error-text">{w}</p>)}
                  </>
                )}
                {mode === 'clt' && cltPreview?.problem && <p className="error-text">{cltPreview.problem}</p>}
                {mode === 'joists' && joistPreview && !joistPreview.problem && (
                  <>
                    <JoistPlan p={joistPreview} />
                    <p className="muted small">A sample floor, 6 × 4 m, with a stairwell</p>
                    <p className="muted small">{Object.entries(joistPreview.counts).map(([k, v]) => `${v} ${k}`).join(' · ')}</p>
                    {joistPreview.warnings.map((w, i) => <p key={i} className="error-text">{w}</p>)}
                  </>
                )}
                {mode === 'joists' && joistPreview?.problem && <p className="error-text">{joistPreview.problem}</p>}
                {mode === 'clt' && isSlab && <p className="muted small">The floor in panels across its span, each at most as wide and as long as the press makes them.</p>}
                {mode === 'layers' && <p className="muted small">Built of its layers. Tick Timber frame to build its core as studs, plates and the openings' framing.</p>}
              </div>
              <div className="row joinery-actions">
                <span className="grow" />
                <button type="button" className="btn primary" disabled={Boolean((on && preview?.problem) || (mode === 'clt' && cltPreview?.problem) || (mode === 'joists' && joistPreview?.problem))} onClick={save}>Save type</button>
              </div>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
