/**
 * Formwork: the configurator, for floors and for beams. The project's
 * formwork types of the kind on the left; on the right the one being edited.
 * A floor system: plywood on secondary beams on primary beams on props (or
 * panels on props), its beams' catalogue lengths, its spacing table by the
 * floor's thickness, the edges' upstand, its props. A beam system: the
 * plywood box on bearers, the yokes' spacing by the beam's depth, the props
 * under it, the chamfer fillets. The section shows the layers for the
 * thickness or the beam chosen, with the spacings and the prop the engine
 * picks. Saving is one engine command; every formwork of the type follows.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { ofClass, relatedOf } from '../../engine/model';
import { Mm, MmList, Num } from './fields';
import { setSiteWorks, useSiteWorks, type FormworkKind } from './prefs';

interface PropSize { name: string; min: number; max: number }
interface Row { thickness: number; secondary: number; primary: number; prop: number }
interface Yoke { depth: number; spacing: number }
export interface SlabSpec {
  system: 'beams' | 'panels'; plywood: number; secondaryHeight: number; primaryHeight: number; beamWidth: number; lengths: number[]; spacing: Row[];
  edgeInset: number; upstand: number; props: PropSize[]; panelLength: number; panelWidth: number; panelThickness: number; reuses: number;
}
export interface BeamSpec { plywood: number; bearerHeight: number; bearerWidth: number; bearers: number; propSpacing: number; yokes: Yoke[]; chamfer: number; props: PropSize[]; reuses: number }
export interface UprightSpec { panelWidths: number[]; panelHeights: number[]; panelThickness: number; tieAlong: number; tieUp: number; clampSpacing: number; braceSpacing: number; reuses: number }
type Spec = SlabSpec & BeamSpec & UprightSpec & { article: string; articleText: string };
interface Preview {
  secondary?: number; primary?: number; prop?: number | string; propSize?: string | null; propLength?: number; yokeSpacing?: number; propSpacing?: number; rowThickness?: number;
  across?: number[]; up?: number[]; panels?: number; ties?: number; clamps?: number; area?: number;
}
const KINDS: [FormworkKind, string, string][] = [['slab', 'Floors', 'floor'], ['beam', 'Beams', 'beam'], ['upright', 'Walls & columns', 'wall and column']];
const CATALOG: Record<FormworkKind, string> = { slab: 'sitework.slabFormworkDefault', beam: 'sitework.beamFormworkDefault', upright: 'sitework.uprightFormworkDefault' };
const NAME: Record<FormworkKind, string> = { slab: 'Floor formwork', beam: 'Beam formwork', upright: 'Wall and column formwork' };

const Line = ({ label, children }: { label: string; children: React.ReactNode }) => <label className="field-line"><span className="eyebrow">{label}</span><span>{children}</span></label>;
const r2 = (v: number) => Math.round(v * 100) / 100;

/** A small table of rows edited in place (mm columns). */
function Table<T extends object>({ rows, columns, onChange, fresh }: { rows: T[]; columns: [keyof T, string, 'mm' | 'text'][]; onChange: (rows: T[]) => void; fresh: () => T }) {
  return (
    <table className="panel-grid spec-grid">
      <thead><tr>{columns.map(([, label, kind]) => <th key={label}>{label}{kind === 'mm' ? ' (mm)' : ''}</th>)}<th /></tr></thead>
      <tbody>
        {rows.map((row, i) => (
          <tr key={i}>
            {columns.map(([key, label, kind]) => (
              <td key={label}>
                {kind === 'text'
                  ? <input value={String(row[key] ?? '')} onChange={(e) => onChange(rows.map((x, k) => (k === i ? { ...x, [key]: e.target.value } : x)))} />
                  : <Mm unit={false} value={row[key] as number} min={0} onCommit={(v) => v != null && onChange(rows.map((x, k) => (k === i ? { ...x, [key]: v } : x)))} />}
              </td>
            ))}
            <td><button type="button" className="icon" title="Remove" disabled={rows.length <= 1} onClick={() => onChange(rows.filter((_, k) => k !== i))}>×</button></td>
          </tr>
        ))}
      </tbody>
      <tfoot><tr><td colSpan={columns.length + 1}><button type="button" className="link small" onClick={() => onChange([...rows, fresh()])}>Add a row</button></td></tr></tfoot>
    </table>
  );
}

export function FormworkDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const [kind, setKind] = useState<FormworkKind>(useSiteWorks.getState().formworkKind);
  const types = ofClass(model, 'LapinFormworkType').filter((t) => (t.props.kind ?? 'slab') === kind);
  const fallback = info?.catalogs[CATALOG[kind]] as unknown as Spec | undefined;
  const chosen = useSiteWorks.getState().formworkTypeId;
  const [editing, setEditing] = useState<string | null>(chosen && nodes.get(chosen)?.props.kind === kind ? chosen : types[0]?.id ?? null);
  const [name, setName] = useState('');
  const [spec, setSpec] = useState<Spec | null>(null);
  /** The kind the spec was loaded for: until it is the kind shown, the form waits (a floor's spec has no panels). */
  const [specKind, setSpecKind] = useState<FormworkKind | null>(null);
  const [size, setSize] = useState({ thickness: 0.2, height: 2.8, width: 0.3, depth: 0.5, length: 3 });
  const [preview, setPreview] = useState<Preview | null>(null);

  const modelRef = useRef(model);
  modelRef.current = model;
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    setSpec(fallback ? { ...structuredClone(fallback), ...((t?.props.spec as Partial<Spec> | undefined) ?? {}) } : null);
    setSpecKind(kind);
    setName(t?.name ?? NAME[kind]);
  }, [editing, fallback, kind]);

  useEffect(() => {
    if (!spec || specKind !== kind) return;
    let live = true;
    void engine.call<Preview>('doc.query', { name: 'sitework.formworkPreview', args: { kind, spec, ...size, width: kind === 'upright' ? size.length : size.width } }).then((p) => { if (live) setPreview(p); }).catch(() => undefined);
    return () => { live = false; };
  }, [spec, size, kind, specKind]);

  if (!spec || specKind !== kind) return null;
  const set = (patch: Partial<Spec>) => setSpec({ ...spec, ...patch });
  const close = () => useUi.setState({ dialog: null });
  const switchKind = (k: FormworkKind) => { setKind(k); setSiteWorks({ formworkKind: k }); setEditing(ofClass(model, 'LapinFormworkType').find((t) => (t.props.kind ?? 'slab') === k)?.id ?? null); };
  const fresh = () => { setEditing(null); setSpec(structuredClone(fallback ?? spec)); setName(`${NAME[kind]} ${types.length + 1}`); };
  const save = async () => {
    if (editing) await execute('sitework.updateFormworkType', { id: editing, name, spec });
    else {
      const r = await execute('sitework.createFormworkType', { name, kind, spec }) as { id?: string } | null;
      if (r?.id) setEditing(r.id);
    }
  };
  const props = <Table rows={spec.props} columns={[['name', 'Prop', 'text'], ['min', 'From', 'mm'], ['max', 'To', 'mm']]} onChange={(p) => set({ props: p })} fresh={() => ({ name: 'Prop', min: 1.5, max: 2.5 })} />;

  // The section, across: a floor's layers under it down to the floor below, or a beam's box on its bearers.
  const W = 360, H = 260, pad = 16;
  const svg = kind === 'upright' ? uprightElevation(spec, size, preview, W, H, pad) : kind === 'beam' ? beamSection(spec, size, W, H, pad) : slabSection(spec, size, preview, W, H, pad);

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Formwork">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Formwork</span>
          <span className="tabs-inline">
            {KINDS.map(([k, label]) => <button key={k} type="button" className={`tab${kind === k ? ' on' : ''}`} onClick={() => switchKind(k)}>{label}</button>)}
          </span>
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
            <li><button type="button" className="tree-item" onClick={fresh}><Plus size={12} /> New {KINDS.find(([k]) => k === kind)![2]} formwork type</button></li>
          </ul>
          <div className="joinery-edit">
            <div className="joinery-form">
              <Line label="Name"><input value={name} onChange={(e) => setName(e.target.value)} /></Line>
              {kind === 'upright' ? <>
                <Line label="Panel widths"><MmList value={spec.panelWidths} onCommit={(panelWidths) => set({ panelWidths })} /></Line>
                <Line label="Panel heights"><MmList value={spec.panelHeights} onCommit={(panelHeights) => set({ panelHeights })} /></Line>
                <Line label="Panel depth"><Mm value={spec.panelThickness} min={10} onCommit={(v) => v && set({ panelThickness: v })} /></Line>
                <Line label="Ties every"><Mm value={spec.tieAlong} min={300} onCommit={(v) => v && set({ tieAlong: v })} /> <span className="muted small">along</span></Line>
                <Line label="and every"><Mm value={spec.tieUp} min={300} onCommit={(v) => v && set({ tieUp: v })} /> <span className="muted small">up</span></Line>
                <Line label="Column clamps every"><Mm value={spec.clampSpacing} min={200} onCommit={(v) => v && set({ clampSpacing: v })} /></Line>
                <Line label="Push-pull props every"><Mm value={spec.braceSpacing} min={500} onCommit={(v) => v && set({ braceSpacing: v })} /></Line>
              </> : kind === 'slab' ? <>
                <Line label="System">
                  <select value={spec.system} onChange={(e) => set({ system: e.target.value as SlabSpec['system'] })}>
                    <option value="beams">plywood on timber beams and props</option><option value="panels">panels on props</option>
                  </select>
                </Line>
                {spec.system === 'beams' ? <>
                  <Line label="Plywood"><Mm value={spec.plywood} min={5} onCommit={(v) => v && set({ plywood: v })} /></Line>
                  <Line label="Secondary beams"><Mm value={spec.secondaryHeight} min={0} onCommit={(v) => v != null && set({ secondaryHeight: v })} /> <span className="muted small">high</span></Line>
                  <Line label="Primary beams"><Mm value={spec.primaryHeight} min={0} onCommit={(v) => v != null && set({ primaryHeight: v })} /> <span className="muted small">high</span></Line>
                  <Line label="Beam width"><Mm value={spec.beamWidth} min={20} onCommit={(v) => v && set({ beamWidth: v })} /></Line>
                  <Line label="Beam lengths"><MmList value={spec.lengths} onCommit={(lengths) => set({ lengths })} /></Line>
                </> : <>
                  <Line label="Panel"><Mm value={spec.panelLength} min={300} onCommit={(v) => v && set({ panelLength: v })} /> × <Mm value={spec.panelWidth} min={300} onCommit={(v) => v && set({ panelWidth: v })} /></Line>
                  <Line label="Panel depth"><Mm value={spec.panelThickness} min={20} onCommit={(v) => v && set({ panelThickness: v })} /></Line>
                </>}
                <Line label="First beam in from the edge"><Mm value={spec.edgeInset} min={0} onCommit={(v) => v != null && set({ edgeInset: v })} /></Line>
                <Line label="Edge upstand"><Mm value={spec.upstand} min={0} onCommit={(v) => v != null && set({ upstand: v })} /></Line>
                <p className="eyebrow">Spacing by the floor's thickness (no more than)</p>
                <Table rows={spec.spacing} columns={[['thickness', 'Floor up to', 'mm'], ['secondary', 'Secondary', 'mm'], ['primary', 'Primary', 'mm'], ['prop', 'Props', 'mm']]}
                  onChange={(spacing) => set({ spacing })} fresh={() => ({ thickness: 0.3, secondary: 0.4, primary: 1.5, prop: 1.0 })} />
              </> : <>
                <Line label="Plywood"><Mm value={spec.plywood} min={5} onCommit={(v) => v && set({ plywood: v })} /></Line>
                <Line label="Bearers"><Num value={spec.bearers} min={1} onCommit={(v) => set({ bearers: Math.round(v) })} /> × <Mm value={spec.bearerWidth} min={20} onCommit={(v) => v && set({ bearerWidth: v })} /> × <Mm value={spec.bearerHeight} min={0} onCommit={(v) => v != null && set({ bearerHeight: v })} /></Line>
                <Line label="Props every"><Mm value={spec.propSpacing} min={300} onCommit={(v) => v && set({ propSpacing: v })} /></Line>
                <Line label="Chamfer fillet"><Mm value={spec.chamfer} min={0} onCommit={(v) => v != null && set({ chamfer: v })} /> <span className="muted small">0: none</span></Line>
                <p className="eyebrow">Yokes by the beam's depth (no more than)</p>
                <Table rows={spec.yokes} columns={[['depth', 'Beam up to', 'mm'], ['spacing', 'Yokes every', 'mm']]} onChange={(yokes) => set({ yokes })} fresh={() => ({ depth: 1.0, spacing: 0.45 })} />
              </>}
              {kind !== 'upright' && <><p className="eyebrow">Props</p>{props}</>}
              <Line label="Reuses"><Num value={spec.reuses} min={1} onCommit={(v) => set({ reuses: Math.round(v) })} /></Line>
              <p className="eyebrow">Bill of quantities</p>
              <Line label="Article code"><input value={spec.article ?? ''} placeholder="as the norms the estimate uses" onChange={(e) => set({ article: e.target.value })} /></Line>
              <Line label="Article text"><input value={spec.articleText ?? ''} onChange={(e) => set({ articleText: e.target.value })} /></Line>
            </div>
            <div className="joinery-preview">
              {svg}
              <div className="row">
                {kind === 'upright'
                  ? <Line label="Wall"><Mm value={size.length} min={300} onCommit={(v) => v && setSize({ ...size, length: v })} /></Line>
                  : kind === 'slab'
                  ? <Line label="Floor"><Mm value={size.thickness} min={50} onCommit={(v) => v && setSize({ ...size, thickness: v })} /></Line>
                  : <><Line label="Beam"><Mm value={size.width} min={100} onCommit={(v) => v && setSize({ ...size, width: v })} /></Line><Line label="×"><Mm value={size.depth} min={100} onCommit={(v) => v && setSize({ ...size, depth: v })} /></Line></>}
                <Line label="clear height"><Mm value={size.height} min={500} onCommit={(v) => v && setSize({ ...size, height: v })} /></Line>
              </div>
              {kind === 'upright' ? <>
                <p className="muted small">{(preview?.across ?? []).map(r2).join(' + ')} m along × {(preview?.up ?? []).map(r2).join(' + ')} m up · {preview?.panels ?? 0} panels both faces · {preview?.ties ?? 0} ties · {preview?.clamps ?? 0} clamps on a column · {preview?.area ?? 0} m²</p>
                <p className="muted small">Walls: both faces, the free ends, less the openings; a layered wall's ring beams and concrete bands alone. Columns and footings: their sides. What stands against a face is left out; a floor cast on top stops it.</p>
              </> : <>
              <p className="muted small">
                {kind === 'slab'
                  ? (spec.system === 'beams' ? `Secondary every ${r2(preview?.secondary ?? 0)} m · primary every ${r2(preview?.primary ?? 0)} m · props every ${r2(Number(preview?.prop ?? 0))} m` : `Panels ${r2(spec.panelLength)} × ${r2(spec.panelWidth)} m on props`)
                  : `Yokes every ${r2(preview?.yokeSpacing ?? 0)} m · props every ${r2(preview?.propSpacing ?? 0)} m`}
              </p>
              <p className="muted small">Prop {r2(preview?.propLength ?? 0)} m: {(kind === 'slab' ? preview?.propSize : (preview?.prop as string | null | undefined)) ?? <span className="error-text">no prop of the type takes it</span>}</p>
              </>}
              <p className="muted small">IFC: IfcElementAssembly (FORMWORK) over IfcPlate and IfcMember, temporary; a product resource FORMWORK</p>
            </div>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while formwork uses it)" onClick={() => void execute('element.delete', { id: editing }).then((r) => { if (r !== null) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
              {editing && <button type="button" className="btn" onClick={() => setSiteWorks({ formworkTypeId: editing, formworkKind: kind })}>Use it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}

/** A floor's formwork in section: the floor, the plywood, the secondary beams cut across, a primary beam along, the props down to the floor below. */
function slabSection(s: Spec, size: { thickness: number; height: number }, p: Preview | null, W: number, H: number, pad: number) {
  const span = 3;
  const total = size.height + size.thickness;
  const k = Math.min((W - 2 * pad) / span, (H - 2 * pad) / total);
  const x = (v: number) => pad + v * k;
  const z = (v: number) => H - pad - v * k;
  const soffit = size.height;
  const panels = s.system === 'panels';
  const stack = panels ? s.panelThickness : s.plywood + s.secondaryHeight + s.primaryHeight;
  const under = soffit - stack;
  const sec = p?.secondary ?? 0.5;
  const prop = Number(p?.prop ?? 1.5);
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Section">
      <rect x={x(0)} y={z(soffit + size.thickness)} width={span * k} height={size.thickness * k} fill="#c9ced6" stroke="#1e2a3a" strokeWidth={0.6} />
      <line x1={pad / 2} x2={W - pad / 2} y1={z(0)} y2={z(0)} stroke="#6b4e2e" strokeWidth={1.2} />
      {panels
        ? <rect x={x(0)} y={z(soffit)} width={span * k} height={s.panelThickness * k} fill="#d9b77a" stroke="#8a6a2a" strokeWidth={0.4} />
        : <>
          <rect x={x(0)} y={z(soffit)} width={span * k} height={Math.max(1, s.plywood * k)} fill="#d9b77a" />
          {Array.from({ length: Math.floor(span / sec) + 1 }, (_, i) => <rect key={i} x={x(i * sec)} y={z(soffit - s.plywood)} width={Math.max(1.5, s.beamWidth * k)} height={s.secondaryHeight * k} fill="#c48a3a" />)}
          <rect x={x(0)} y={z(soffit - s.plywood - s.secondaryHeight)} width={span * k} height={s.primaryHeight * k} fill="#c48a3a" opacity={0.8} />
        </>}
      {Array.from({ length: Math.floor(span / prop) + 1 }, (_, i) => <line key={i} x1={x(0.15 + i * prop)} x2={x(0.15 + i * prop)} y1={z(under)} y2={z(0)} stroke="#d0502a" strokeWidth={2} />)}
    </svg>
  );
}

/** A beam's formwork in section: the beam and the floor's wings, the plywood box, the bearers, a prop. */
function beamSection(s: Spec, size: { width: number; depth: number; height: number }, W: number, H: number, pad: number) {
  const wing = 0.6;
  const span = size.width + 2 * wing;
  const top = size.height;
  const k = Math.min((W - 2 * pad) / span, (H - 2 * pad) / (top + 0.1));
  const x = (v: number) => pad + v * k;
  const z = (v: number) => H - pad - v * k;
  const b0 = wing;
  const bottom = top - size.depth;
  const slab = 0.2;
  const under = bottom - s.plywood - s.bearerHeight;
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Section">
      <rect x={x(0)} y={z(top)} width={span * k} height={slab * k} fill="#c9ced6" stroke="#1e2a3a" strokeWidth={0.6} />
      <rect x={x(b0)} y={z(top)} width={size.width * k} height={size.depth * k} fill="#c9ced6" stroke="#1e2a3a" strokeWidth={0.6} />
      <rect x={x(b0 - s.plywood)} y={z(bottom)} width={(size.width + 2 * s.plywood) * k} height={Math.max(1, s.plywood * k)} fill="#d9b77a" />
      <rect x={x(b0 - s.plywood)} y={z(top - slab)} width={Math.max(1, s.plywood * k)} height={(size.depth - slab) * k} fill="#d9b77a" />
      <rect x={x(b0 + size.width)} y={z(top - slab)} width={Math.max(1, s.plywood * k)} height={(size.depth - slab) * k} fill="#d9b77a" />
      {Array.from({ length: s.bearers }, (_, i) => {
        const y = s.bearers === 1 ? b0 + size.width / 2 - s.bearerWidth / 2 : b0 + i * (size.width - s.bearerWidth) / (s.bearers - 1);
        return <rect key={i} x={x(y)} y={z(bottom - s.plywood)} width={s.bearerWidth * k} height={s.bearerHeight * k} fill="#c48a3a" />;
      })}
      <line x1={x(b0 + size.width / 2)} x2={x(b0 + size.width / 2)} y1={z(under)} y2={z(0)} stroke="#d0502a" strokeWidth={2} />
      <line x1={pad / 2} x2={W - pad / 2} y1={z(0)} y2={z(0)} stroke="#6b4e2e" strokeWidth={1.2} />
    </svg>
  );
}

/** A wall's formwork in elevation: its panels across and up, at the length and height chosen. */
function uprightElevation(s: Spec, size: { length: number; height: number }, p: Preview | null, W: number, H: number, pad: number) {
  const across = p?.across ?? [];
  const up = p?.up ?? [];
  const len = Math.max(across.reduce((a, b) => a + b, 0), size.length);
  const tall = Math.max(up.reduce((a, b) => a + b, 0), size.height);
  const k = Math.min((W - 2 * pad) / len, (H - 2 * pad) / tall);
  const x = (v: number) => pad + v * k;
  const z = (v: number) => H - pad - v * k;
  const xs = [0]; for (const a of across) xs.push(xs[xs.length - 1] + a);
  const zs = [0]; for (const u of up) zs.push(zs[zs.length - 1] + u);
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Elevation">
      {xs.slice(0, -1).flatMap((x0, i) => zs.slice(0, -1).map((z0, j) => (
        <rect key={`${i}-${j}`} x={x(x0)} y={z(z0 + up[j])} width={across[i] * k} height={up[j] * k} fill="#d9b77a" stroke="#8a6a2a" strokeWidth={0.8} />
      )))}
      <rect x={x(0)} y={z(size.height)} width={size.length * k} height={size.height * k} fill="none" stroke="#b5532d" strokeDasharray="4 3" strokeWidth={1} />
      {Array.from({ length: Math.max(0, Math.ceil(size.length / s.tieAlong)) }, (_, i) => Array.from({ length: Math.max(0, Math.ceil(size.height / s.tieUp)) }, (_, j) => (
        <circle key={`t${i}-${j}`} cx={x((i + 0.5) * size.length / Math.ceil(size.length / s.tieAlong))} cy={z((j + 0.5) * size.height / Math.ceil(size.height / s.tieUp))} r={2.5} fill="#1e2a3a" />
      )))}
      <line x1={pad / 2} x2={W - pad / 2} y1={z(0)} y2={z(0)} stroke="#6b4e2e" strokeWidth={1.2} />
    </svg>
  );
}
