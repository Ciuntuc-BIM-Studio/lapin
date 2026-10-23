/**
 * Wall types: the project's IfcWallTypes and their layer sets. Pick one to
 * edit its name and layers (material, thickness, the core), or make a new
 * one. Layers are listed from the face right of the drawn direction to the
 * left one; the core carries the axis. A type may also be composed in its
 * height: zones from the bottom up (a first course, the masonry, a ring
 * beam) that divide the core. Saving is one engine command; every wall of
 * the type follows.
 */

import { useEffect, useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialName, mm, ofClass, relatedOf, typeLayers } from '../../engine/model';

interface Row { material: string; thicknessMm: string; core: boolean }

/** A zone as the editor holds it: heights and blocks in mm, as typed. */
interface ZoneRow { key: string; name: string; role: string; heightMm: string; rest: boolean; material: string; block: string; rebar: string }

const ROLES = [['masonry', 'Masonry'], ['concrete', 'Concrete'], ['ringBeam', 'Ring beam']] as const;

type ZoneJson = { key?: string; name: string; role: string; height: number | 'rest'; material: string; color?: string; block?: { length: number; height: number; joint: number }; rebar?: number };

const toRow = (z: ZoneJson, materialOf: (id: string) => string): ZoneRow => ({
  key: z.key ?? '', name: z.name, role: z.role, rest: z.height === 'rest', heightMm: z.height === 'rest' ? '' : mm(z.height),
  material: materialOf(z.material), block: z.block ? `${mm(z.block.length)} × ${mm(z.block.height)} / ${mm(z.block.joint)}` : '', rebar: z.rebar ? String(z.rebar) : '',
});

/** "375 × 238 / 10" → the block in metres; empty: none. */
function parseBlock(text: string): { length: number; height: number; joint: number } | undefined {
  const n = text.match(/[\d.,]+/g)?.map((x) => Number(x.replace(',', '.')) / 1000) ?? [];
  return n.length >= 2 ? { length: n[0], height: n[1], joint: n[2] ?? 0 } : undefined;
}

const fromRow = (r: ZoneRow) => ({
  ...(r.key ? { key: r.key } : {}), name: r.name.trim(), role: r.role, material: r.material.trim(),
  height: r.rest ? 'rest' : Number(r.heightMm.replace(',', '.')) / 1000,
  ...(r.role === 'masonry' && parseBlock(r.block) ? { block: parseBlock(r.block) } : {}),
  ...(r.role !== 'masonry' && r.rebar.trim() ? { rebar: Number(r.rebar.replace(',', '.')) } : {}),
});

const blank = (): Row[] => [{ material: 'Render', thicknessMm: '20', core: false }, { material: 'Brick', thicknessMm: '250', core: true }, { material: 'Plaster', thicknessMm: '15', core: false }];

export function WallTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcWallType');
  const materials = ofClass(model, 'IfcMaterial');
  const current = useUi((s) => s.wallTypeId);
  const [editing, setEditing] = useState<string | null>(current ?? types[0]?.id ?? null);
  const [name, setName] = useState('');
  const [rows, setRows] = useState<Row[]>(blank());
  const [zones, setZones] = useState<ZoneRow[]>([]);
  const info = useSession((s) => s.info);

  // Load the picked type into the editor.
  useEffect(() => {
    const t = editing ? nodes.get(editing) : undefined;
    if (!t) { setName(''); setRows(blank()); setZones([]); return; }
    setName(t.name);
    setRows(typeLayers(model, t.id).map((l) => ({ material: materialName(model, l.material), thicknessMm: mm(l.thickness), core: l.core === true })));
    const stored = Array.isArray(t.props.zones) ? (t.props.zones as unknown as ZoneJson[]) : [];
    setZones(stored.map((z) => toRow(z, (id) => materialName(model, id))));
  }, [editing]);

  const close = () => useUi.setState({ dialog: null });
  const set = (i: number, patch: Partial<Row>) => setRows(rows.map((r, k) => (k === i ? { ...r, ...patch } : patch.core ? { ...r, core: false } : r)));
  const move = (i: number, d: number) => {
    const j = i + d;
    if (j < 0 || j >= rows.length) return;
    const next = [...rows];
    [next[i], next[j]] = [next[j], next[i]];
    setRows(next);
  };
  const total = rows.reduce((s, r) => s + (Number(r.thicknessMm.replace(',', '.')) || 0), 0);
  const layers = rows.map((r) => ({ material: r.material.trim(), thickness: Number(r.thicknessMm.replace(',', '.')) / 1000, core: r.core }));

  const zoneJson = zones.map(fromRow);
  const setZone = (i: number, patch: Partial<ZoneRow>) => setZones(zones.map((z, k) => (k === i ? { ...z, ...patch } : patch.rest ? { ...z, rest: false } : z)));
  const defaultZones = () => {
    const d = (info?.catalogs['architecture.defaultZones'] as unknown as ZoneJson[] | undefined) ?? [];
    const core = rows.find((r) => r.core)?.material ?? 'Brick';
    setZones(d.map((z) => toRow(z.key === 'masonry' ? { ...z, material: core } : z, (x) => x)));
  };

  const save = async () => {
    if (editing) await execute('architecture.updateWallType', { id: editing, name, layers, zones: zoneJson });
    else {
      const r = await execute('architecture.createWallType', { name, layers, zones: zoneJson }) as { id?: string } | null;
      if (r?.id) { setEditing(r.id); useUi.setState({ wallTypeId: r.id }); }
    }
  };
  const use = (id: string) => { useUi.setState({ wallTypeId: id }); };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Wall types">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Wall types</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}>
                <button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>
                  {t.name}<span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length} walls{current === t.id ? ' · drawing' : ''}</span>
                </button>
              </li>
            ))}
            <li><button type="button" className={`tree-item${editing === null ? ' on' : ''}`} onClick={() => setEditing(null)}><Plus size={12} /> New type</button></li>
          </ul>
          <div className="type-edit">
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its core and thickness)" onChange={(e) => setName(e.target.value)} /></label>
            <p className="muted small">Layers from the face right of the drawn direction to the left face. The core carries the wall's axis.</p>
            <table className="layer-edit">
              <thead><tr><th>Material</th><th>mm</th><th>Core</th><th /></tr></thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i}>
                    <td><input list="lapin-materials" value={r.material} onChange={(e) => set(i, { material: e.target.value })} /></td>
                    <td><input className="num" value={r.thicknessMm} onChange={(e) => set(i, { thicknessMm: e.target.value })} /></td>
                    <td><input type="radio" name="core" checked={r.core} onChange={() => set(i, { core: true })} /></td>
                    <td className="row-tools">
                      <button type="button" className="icon mini" title="Up" onClick={() => move(i, -1)}><ArrowUp size={12} /></button>
                      <button type="button" className="icon mini" title="Down" onClick={() => move(i, 1)}><ArrowDown size={12} /></button>
                      <button type="button" className="icon mini" title="Remove" onClick={() => setRows(rows.filter((_, k) => k !== i))}><Trash2 size={12} /></button>
                    </td>
                  </tr>
                ))}
                <tr className="total"><td>Total</td><td className="num">{Math.round(total)}</td><td colSpan={2} /></tr>
              </tbody>
            </table>
            <datalist id="lapin-materials">{materials.map((m) => <option key={m.id} value={m.name} />)}</datalist>
            <div className="row">
              <span className="eyebrow">Zones in height</span>
              <span className="muted small">bottom to top · they divide the core</span>
              <span className="grow" />
              {zones.length === 0
                ? <button type="button" className="btn" onClick={defaultZones}>First course, masonry, ring beam</button>
                : <button type="button" className="btn" onClick={() => setZones([])}>No zones</button>}
            </div>
            {zones.length > 0 && (
              <table className="layer-edit zones">
                <thead><tr><th>Zone</th><th>Role</th><th>mm</th><th>Rest</th><th>Material</th><th>Block L × H / joint · kg/m³</th><th /></tr></thead>
                <tbody>
                  {zones.map((z, i) => (
                    <tr key={i}>
                      <td><input value={z.name} onChange={(e) => setZone(i, { name: e.target.value })} /></td>
                      <td><select value={z.role} onChange={(e) => setZone(i, { role: e.target.value })}>{ROLES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></td>
                      <td><input className="num" disabled={z.rest} value={z.heightMm} onChange={(e) => setZone(i, { heightMm: e.target.value })} /></td>
                      <td><input type="radio" name="rest" checked={z.rest} onChange={() => setZone(i, { rest: true })} /></td>
                      <td><input list="lapin-materials" value={z.material} onChange={(e) => setZone(i, { material: e.target.value })} /></td>
                      <td>{z.role === 'masonry'
                        ? <input placeholder="375 × 238 / 10" value={z.block} onChange={(e) => setZone(i, { block: e.target.value })} />
                        : <input placeholder="100" value={z.rebar} onChange={(e) => setZone(i, { rebar: e.target.value })} />}</td>
                      <td className="row-tools">
                        <button type="button" className="icon mini" title="Remove" onClick={() => setZones(zones.filter((_, k) => k !== i))}><Trash2 size={12} /></button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            {zones.length > 0 && <button type="button" className="btn" onClick={() => setZones([...zones, { key: '', name: 'Zone', role: 'masonry', heightMm: '250', rest: false, material: 'Brick', block: '', rebar: '' }])}><Plus size={14} />Zone</button>}
            <div className="row">
              <button type="button" className="btn" onClick={() => setRows([...rows, { material: 'Plaster', thicknessMm: '15', core: false }])}><Plus size={14} />Layer</button>
              <span className="grow" />
              {editing && <button type="button" className="btn" onClick={() => use(editing)}>Draw with it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
