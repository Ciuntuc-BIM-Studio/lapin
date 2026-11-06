/**
 * Bay, bow and box windows in the joinery configurator (report #60): how a window projects from its wall. Its faces are
 * the grid's columns (a bay's and box's first and last columns are its sides, the columns between them its centre; a
 * bow has a face per column), so each panel keeps its own operation; then the projection, the sides' angle, the seat
 * inside, the sill outside, the base under it and the head over it with its flashing, each with its material. A style
 * (Lapin's, or the project's own, saved here) sets them all at once.
 */

import { Pencil, Save, Trash2 } from 'lucide-react';
import { notify } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { MaterialPicker } from '../core/MaterialPicker';
import { t } from '../../i18n';

export interface BaySeat { on: boolean; thickness: number; overhang: number; material?: string | null }
export interface BayBase { kind: 'none' | 'floor' | 'slab' | 'corbel'; thickness: number; overhang: number; brackets: number; material?: string | null }
export interface BayHead { kind: 'none' | 'flat' | 'hipped' | 'lean-to' | 'slab'; thickness: number; overhang: number; pitch: number; material?: string | null; flashing: boolean; flashingMaterial?: string | null }
export interface BaySill { on: boolean; projection: number; thickness: number; material?: string | null }
export interface Bay {
  kind: 'bay' | 'bow' | 'box'; projection: number; angle: number; sides: 'glass' | 'solid'; sideMaterial?: string | null;
  seat?: BaySeat | null; base?: BayBase | null; head?: BayHead | null; sill?: BaySill | null;
}
/** What of a window's spec a bay changes. */
export interface BaySpec { width: number; height: number; sillHeight: number; columns: number[]; rows: number[]; panels: { col: number; row: number; operation: string }[]; bay?: Bay | null }

const SEAT: BaySeat = { on: true, thickness: 0.04, overhang: 0.05 };
const BASE: BayBase = { kind: 'none', thickness: 0.15, overhang: 0.05, brackets: 2 };
const HEAD: BayHead = { kind: 'hipped', thickness: 0.12, overhang: 0.1, pitch: 30, flashing: true };
const SILL: BaySill = { on: true, projection: 0.04, thickness: 0.03 };
export const BAY: Bay = { kind: 'bay', projection: 0.6, angle: 45, sides: 'glass', seat: SEAT, base: BASE, head: HEAD, sill: SILL };

interface Style { id: string; label: string; description?: string; spec: BaySpec }
interface OwnStyle { id: string; label: string; spec: BaySpec }

function Len({ label, value, onChange, title }: { label: string; value: number; onChange: (m: number) => void; title?: string }) {
  return <label className="field-line" title={title}><span className="eyebrow">{label}</span><LengthField metres={value} onCommit={(m) => { if (m != null) onChange(m); }} /></label>;
}
function Num({ label, value, onChange, integer }: { label: string; value: number; onChange: (n: number) => void; integer?: boolean }) {
  return <label className="field-line"><span className="eyebrow">{label}</span><NumberField value={value} integer={integer} onCommit={(n) => { if (n != null) onChange(n); }} /></label>;
}
function Mat({ label, value, onChange }: { label: string; value?: string | null; onChange: (m: string | null) => void }) {
  return <label className="field-line"><span className="eyebrow">{label}</span><MaterialPicker value={value ?? ''} allowEmpty onChange={(m) => onChange(m || null)} /></label>;
}

/** A bay's faces: three or more columns, each with a panel (the sides open, the centre fixed). */
function asFaces(spec: BaySpec): Pick<BaySpec, 'columns' | 'panels'> {
  if (spec.columns.length >= 3) return { columns: spec.columns, panels: spec.panels };
  return { columns: [1, 2, 1], panels: [{ col: 0, row: 0, operation: 'side-left' }, { col: 1, row: 0, operation: 'fixed' }, { col: 2, row: 0, operation: 'side-right' }] };
}

export function BayFields({ spec, onChange, onGrid }: { spec: BaySpec; onChange: (patch: Partial<BaySpec>) => void; onGrid: (columns: number[]) => void }) {
  const catalog = (useSession((s) => s.info?.catalogs['architecture.bayStyles']) as unknown as Style[] | undefined) ?? [];
  const project = useSession((s) => [...s.nodes.values()].find((n) => n.type === 'IfcProject'));
  const own = (project?.props.bayStyles as OwnStyle[] | undefined) ?? [];
  const bay = spec.bay ?? null;
  const set = (patch: Partial<Bay>) => onChange({ bay: { ...(bay ?? BAY), ...patch } });
  const kind = (k: string) => {
    if (k === 'none') { onChange({ bay: null }); return; }
    const faces = asFaces(spec);
    onGrid(faces.columns);
    onChange({ ...faces, bay: { ...(bay ?? BAY), kind: k as Bay['kind'], ...(k === 'box' ? { angle: 90 } : k === 'bay' && (bay?.angle ?? 45) >= 90 ? { angle: 45 } : {}) } });
  };
  const apply = (id: string) => {
    const s = catalog.find((x) => x.id === id)?.spec ?? own.find((x) => x.id === id)?.spec;
    if (!s) return;
    onGrid(s.columns);
    onChange({ width: s.width, height: s.height, sillHeight: s.sillHeight, columns: s.columns, rows: s.rows, panels: s.panels, bay: s.bay ?? null });
  };
  const save = async () => {
    const name = window.prompt(t('Name of the bay window style'), '');
    if (!name?.trim()) return;
    const r = await execute('architecture.saveBayStyle', { name, spec: { ...spec } });
    if (r) notify(t('Saved as a style of the project.'));
  };
  const seat = bay?.seat ?? { ...SEAT, on: false };
  const sill = bay?.sill ?? { ...SILL, on: false };
  const base = bay?.base ?? BASE;
  const head = bay?.head ?? { ...HEAD, kind: 'none' as const };
  return (
    <div className="bay-fields" data-testid="bay-fields">
      <p className="eyebrow">Projecting window</p>
      <label className="field-line"><span className="eyebrow">Projects</span>
        <select data-field="bay-kind" value={bay?.kind ?? 'none'} onChange={(e) => kind(e.target.value)}>
          <option value="none">No: flat in the wall</option><option value="bay">Bay (angled sides)</option><option value="bow">Bow (on an arc)</option><option value="box">Box (square sides)</option>
        </select></label>
      <label className="field-line"><span className="eyebrow">Bay style</span>
        <select data-field="bay-style" value="" title="Sets the faces, the projection, the seat, the base and the roof at once; each stays editable" onChange={(e) => apply(e.target.value)}>
          <option value="">Apply a bay style…</option>
          <optgroup label={t('Lapin')}>{catalog.map((s) => <option key={s.id} value={s.id} title={s.description}>{s.label}</option>)}</optgroup>
          {own.length > 0 && <optgroup label={t('This project')}>{own.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.label}</option>)}</optgroup>}
        </select></label>
      {bay && <>
        <p className="muted small">{bay.kind === 'bow' ? 'The columns are its faces: one face each, their widths sharing the arc.' : 'The columns are its faces: the first and last its sides, those between them its centre.'}</p>
        <Len label="Projection" title="From the wall's exterior face to the frames' outer face" value={bay.projection} onChange={(v) => set({ projection: v })} />
        {bay.kind === 'bay' && <Num label="Sides to the wall (°)" value={bay.angle} onChange={(v) => set({ angle: v })} />}
        {bay.kind !== 'bow' && <label className="field-line"><span className="eyebrow">Sides</span>
          <select data-field="bay-sides" value={bay.sides} onChange={(e) => set({ sides: e.target.value as Bay['sides'] })}><option value="glass">Glazed</option><option value="solid">Solid</option></select></label>}
        {bay.kind !== 'bow' && bay.sides === 'solid' && <Mat label="Sides' material" value={bay.sideMaterial} onChange={(m) => set({ sideMaterial: m })} />}

        <label className="field-line check"><input type="checkbox" data-field="bay-seat" checked={seat.on} onChange={(e) => set({ seat: { ...seat, on: e.target.checked } })} /><span>Seat inside</span></label>
        {seat.on && <>
          <Len label="Seat thickness" value={seat.thickness} onChange={(v) => set({ seat: { ...seat, thickness: v } })} />
          <Len label="Into the room" value={seat.overhang} onChange={(v) => set({ seat: { ...seat, overhang: v } })} />
          <Mat label="Seat material" value={seat.material} onChange={(m) => set({ seat: { ...seat, material: m } })} />
        </>}
        <label className="field-line check"><input type="checkbox" data-field="bay-sill" checked={sill.on} onChange={(e) => set({ sill: { ...sill, on: e.target.checked } })} /><span>Sill outside</span></label>
        {sill.on && <>
          <Len label="Past the faces" value={sill.projection} onChange={(v) => set({ sill: { ...sill, projection: v } })} />
          <Len label="Sill thickness" value={sill.thickness} onChange={(v) => set({ sill: { ...sill, thickness: v } })} />
          <Mat label="Sill material" value={sill.material} onChange={(m) => set({ sill: { ...sill, material: m } })} />
        </>}

        <label className="field-line"><span className="eyebrow">Base</span>
          <select data-field="bay-base" value={base.kind} onChange={(e) => set({ base: { ...base, kind: e.target.value as BayBase['kind'] } })}>
            <option value="none">None (hung on the wall)</option><option value="floor">Walls down to the floor</option><option value="slab">A projecting slab</option><option value="corbel">A slab on corbels</option>
          </select></label>
        {base.kind !== 'none' && <>
          <Len label={base.kind === 'floor' ? 'Wall thickness' : 'Slab thickness'} value={base.thickness} onChange={(v) => set({ base: { ...base, thickness: v } })} />
          <Len label="Past the faces" value={base.overhang} onChange={(v) => set({ base: { ...base, overhang: v } })} />
          {base.kind === 'corbel' && <Num label="Corbels" integer value={base.brackets} onChange={(v) => set({ base: { ...base, brackets: Math.round(v) } })} />}
          <Mat label="Base material" value={base.material} onChange={(m) => set({ base: { ...base, material: m } })} />
        </>}

        <label className="field-line"><span className="eyebrow">Head</span>
          <select data-field="bay-head" value={head.kind} onChange={(e) => set({ head: { ...HEAD, ...head, kind: e.target.value as BayHead['kind'] } })}>
            <option value="none">None</option><option value="hipped">Hipped roof</option><option value="lean-to">Lean-to roof</option><option value="flat">Flat roof</option><option value="slab">Slab</option>
          </select></label>
        {head.kind !== 'none' && <>
          <Len label="Thickness at the eaves" value={head.thickness} onChange={(v) => set({ head: { ...head, thickness: v } })} />
          <Len label="Past the faces" value={head.overhang} onChange={(v) => set({ head: { ...head, overhang: v } })} />
          {(head.kind === 'hipped' || head.kind === 'lean-to') && <Num label="Pitch (°)" value={head.pitch} onChange={(v) => set({ head: { ...head, pitch: v } })} />}
          <Mat label="Roof material" value={head.material} onChange={(m) => set({ head: { ...head, material: m } })} />
          <label className="field-line check"><input type="checkbox" checked={head.flashing} onChange={(e) => set({ head: { ...head, flashing: e.target.checked } })} /><span>Flashing against the wall</span></label>
          {head.flashing && <Mat label="Flashing material" value={head.flashingMaterial} onChange={(m) => set({ head: { ...head, flashingMaterial: m } })} />}
        </>}
        <div className="row wrap" style={{ gap: 6 }}>
          <button type="button" className="btn compact" data-action="bay-save-style" title="Keep this bay, bow or box as a style of the project, to give other window types" onClick={() => void save()}><Save size={13} />Save as bay style…</button>
        </div>
      </>}
      {own.length > 0 && (
        <ul className="type-list compact" data-testid="bay-own-styles">
          {own.map((s) => (
            <li key={s.id} className="row" style={{ gap: 4 }}>
              <span className="grow small" data-no-i18n>{s.label}</span>
              <button type="button" className="icon mini" title="Rename" onClick={() => { const n = window.prompt(t('Name of the bay window style'), s.label); if (n?.trim()) void execute('architecture.renameBayStyle', { styleId: s.id, name: n }); }}><Pencil size={11} /></button>
              <button type="button" className="icon mini" title="Delete the style (the window types made with it stay as they are)" onClick={() => { if (window.confirm(t('Delete this bay window style?'))) void execute('architecture.deleteBayStyle', { styleId: s.id }); }}><Trash2 size={11} /></button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
