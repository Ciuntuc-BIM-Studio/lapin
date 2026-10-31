/**
 * False ceilings (report #44, decision 192): the tools (a ceiling over an outline or over the room
 * clicked, a light panel or an air diffuser in the cell clicked), the ceiling types' dialog (the
 * system, the tile, the grid, the hangers, the colours) and the ceiling in the inspector (its type and
 * height, fixtures laid out every so many cells).
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Fan, Grid3x3, LayoutGrid, Lightbulb, Plus, Settings2, X } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import type { ToolDef } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { mm, ofClass, relatedOf, typeOf } from '../../engine/model';

export interface CeilingSpec {
  name: string; system: 'grid' | 'plain'; tileWidth: number; tileLength: number; thickness: number;
  gridWidth: number; gridDepth: number; rotation: number; hangers: boolean; hangerSpacing: number;
  tileMaterial: string; gridMaterial: string; tileColor: string; gridColor: string; lightColor: string; diffuserColor: string;
}

/** A ceiling: click its corners, then the first one again (or Enter). */
export const ceiling: ToolDef = {
  id: 'architecture.ceiling', label: 'Ceiling', icon: Grid3x3, shortcut: 'CL',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    await ctx.execute('architecture.addCeiling', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(useUi.getState().ceilingTypeId ? { typeId: useUi.getState().ceilingTypeId } : {}) });
  },
};

/** A ceiling over the room clicked, following its outline. */
export const ceilingInRoom: ToolDef = {
  id: 'architecture.ceilingInRoom', label: 'Ceiling in a room', icon: LayoutGrid,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    await ctx.execute('architecture.addCeiling', { storeyId: ctx.storeyId, x: p.x, y: p.y, ...(useUi.getState().ceilingTypeId ? { typeId: useUi.getState().ceilingTypeId } : {}) });
  },
};

/** A light panel or an air diffuser in the ceiling's cell under the click (a click on one takes it away when the same kind). */
const fixture = (kind: 'light' | 'diffuser'): ToolDef => ({
  id: kind === 'light' ? 'architecture.ceilingLight' : 'architecture.ceilingDiffuser', label: kind === 'light' ? 'Light panel' : 'Air diffuser', icon: kind === 'light' ? Lightbulb : Fan,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('architecture.ceilingFixture', { storeyId: ctx.storeyId, kind, x: p.x, y: p.y }); },
});
export const ceilingLight = fixture('light');
export const ceilingDiffuser = fixture('diffuser');
export const ceilingTypes: ToolDef = { id: 'architecture.ceilingTypes', label: 'Ceiling types', icon: Settings2, run: () => useUi.setState({ dialog: 'ceilingTypes' }) };

function Mm({ label, value, onChange }: { label: string; value: number; onChange: (m: number) => void }) {
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <input type="number" step={1} value={Math.round(value * 1000)} onChange={(e) => { const n = Number(e.target.value); if (Number.isFinite(n) && n > 0) onChange(n / 1000); }} /><span className="muted small"> mm</span>
    </label>
  );
}

/** The project's ceiling types, one being edited. */
export function CeilingTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'LapinCeilingType');
  const catalog = info?.catalogs['architecture.ceilings'] as unknown as { spec: CeilingSpec; plain: CeilingSpec } | undefined;
  const [editing, setEditing] = useState<string | null>(types[0]?.id ?? null);
  const [spec, setSpec] = useState<CeilingSpec | null>(null);
  const modelRef = useRef(model);
  modelRef.current = model;
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as CeilingSpec | undefined) ?? catalog?.spec ?? null;
    setSpec(s ? structuredClone(s) : null);
  }, [editing, catalog]);
  if (!spec || !catalog) return null;
  const set = (patch: Partial<CeilingSpec>) => setSpec({ ...spec, ...patch });
  const close = () => useUi.setState({ dialog: null });
  const save = async () => {
    if (editing) await execute('architecture.updateCeilingType', { id: editing, spec });
    else { const r = await execute('architecture.createCeilingType', { spec }) as { id?: string } | null; if (r?.id) setEditing(r.id); }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Ceiling types">
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">Ceiling types</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}><button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>{t.name}<span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span></button></li>
            ))}
            <li><button type="button" className="tree-item" onClick={() => { setEditing(null); setSpec(structuredClone(catalog.spec)); }}><Plus size={12} /> New grid ceiling</button></li>
            <li><button type="button" className="tree-item" onClick={() => { setEditing(null); setSpec(structuredClone(catalog.plain)); }}><Plus size={12} /> New plasterboard ceiling</button></li>
          </ul>
          <div className="type-edit">
            <label className="field-line"><span className="eyebrow">Name</span><input value={spec.name} onChange={(e) => set({ name: e.target.value })} /></label>
            <label className="field-line"><span className="eyebrow">System</span>
              <select data-field="ceiling-system" value={spec.system} onChange={(e) => set({ system: e.target.value as CeilingSpec['system'] })}>
                <option value="grid">Tiles on an exposed grid</option><option value="plain">Plasterboard, continuous</option>
              </select></label>
            <Mm label={spec.system === 'grid' ? 'Tile width' : 'Module width'} value={spec.tileWidth} onChange={(v) => set({ tileWidth: v })} />
            <Mm label={spec.system === 'grid' ? 'Tile length' : 'Module length'} value={spec.tileLength} onChange={(v) => set({ tileLength: v })} />
            <Mm label="Thickness" value={spec.thickness} onChange={(v) => set({ thickness: v })} />
            <label className="field-line"><span className="eyebrow">Material</span><input value={spec.tileMaterial} onChange={(e) => set({ tileMaterial: e.target.value })} /></label>
            <label className="field-line"><span className="eyebrow">Colour</span><input type="color" value={spec.tileColor} onChange={(e) => set({ tileColor: e.target.value })} /></label>
            {spec.system === 'grid' && <>
              <p className="eyebrow">Grid</p>
              <Mm label="Face width" value={spec.gridWidth} onChange={(v) => set({ gridWidth: v })} />
              <Mm label="Depth" value={spec.gridDepth} onChange={(v) => set({ gridDepth: v })} />
              <label className="field-line"><span className="eyebrow">Rotation (°)</span><input type="number" value={spec.rotation} onChange={(e) => set({ rotation: Number(e.target.value) || 0 })} /></label>
              <label className="field-line"><span className="eyebrow">Grid material</span><input value={spec.gridMaterial} onChange={(e) => set({ gridMaterial: e.target.value })} /></label>
              <label className="field-line"><span className="eyebrow">Grid colour</span><input type="color" value={spec.gridColor} onChange={(e) => set({ gridColor: e.target.value })} /></label>
              <label className="field-line check"><input type="checkbox" checked={spec.hangers} onChange={(e) => set({ hangers: e.target.checked })} /><span>Hangers up to the structure</span></label>
              {spec.hangers && <Mm label="Hanger spacing" value={spec.hangerSpacing} onChange={(v) => set({ hangerSpacing: v })} />}
            </>}
            <p className="eyebrow">In the cells</p>
            <label className="field-line"><span className="eyebrow">Light panels</span><input type="color" value={spec.lightColor} onChange={(e) => set({ lightColor: e.target.value })} /></label>
            <label className="field-line"><span className="eyebrow">Air diffusers</span><input type="color" value={spec.diffuserColor} onChange={(e) => set({ diffuserColor: e.target.value })} /></label>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" onClick={() => useUi.setState({ ceilingTypeId: editing })}>Place with it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

/** A ceiling in the inspector: its type, its height, its fixtures laid out every so many cells. */
export function CeilingSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'LapinCeilingType');
  const type = typeOf({ nodes, relations }, node.id);
  const [every, setEvery] = useState(3);
  const height = typeof node.props.height === 'number' ? node.props.height : 2.7;
  const fixtures = (node.props.fixtures as { kind: string }[] | undefined) ?? [];
  const set = (args: Record<string, unknown>) => void execute('architecture.updateCeiling', { id: node.id, ...args });
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Ceiling type</span>
        <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => set({ typeId: e.target.value })}>
          {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
        <button type="button" className="link small" onClick={() => useUi.setState({ dialog: 'ceilingTypes' })}>Ceiling types…</button>
      </label>
      <dl className="fields">
        <dt>Height</dt>
        <dd><input className="num" type="number" step={10} data-field="ceiling-height" value={mm(height)} onChange={(e) => { const v = Number(e.target.value); if (v > 0) set({ height: v / 1000 }); }} /> <span className="muted">mm</span></dd>
        <dt>Light panels</dt><dd><span className="num">{fixtures.filter((f) => f.kind === 'light').length}</span></dd>
        <dt>Air diffusers</dt><dd><span className="num">{fixtures.filter((f) => f.kind === 'diffuser').length}</span></dd>
      </dl>
      <p className="eyebrow">Lay out</p>
      <div className="row small">
        <span>Every</span>
        <input className="num" type="number" min={1} max={20} value={every} style={{ width: 48 }} onChange={(e) => setEvery(Math.max(1, Number(e.target.value) || 1))} />
        <span>cells</span>
      </div>
      <div className="row" style={{ flexWrap: 'wrap' }}>
        <button type="button" className="btn" data-action="ceiling-lights" onClick={() => void execute('architecture.ceilingLayout', { id: node.id, kind: 'light', every })}><Lightbulb size={14} />Light panels</button>
        <button type="button" className="btn" onClick={() => void execute('architecture.ceilingLayout', { id: node.id, kind: 'diffuser', every, offset: 0 })}><Fan size={14} />Air diffusers</button>
        <button type="button" className="btn" onClick={() => void execute('architecture.ceilingLayout', { id: node.id, kind: 'none' })}>Clear</button>
      </div>
      <p className="muted small">Light panel and Air diffuser (Architecture → Ceiling) put one in the cell you click.</p>
    </div>
  );
}
