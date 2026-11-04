/**
 * Wall types: the project's IfcWallTypes and their layer sets. Pick one to
 * edit its name and layers (material, thickness, the core), or make a new
 * one. Layers are listed from the face right of the drawn direction to the
 * left one; the core carries the axis. A type may also be composed in its
 * height: zones from the bottom up (a first course, the masonry, a ring
 * beam) that divide the core; any other layer may change material up its
 * height (decision 264: tiles to 2.10 m, plaster over them). Under the layers, the type's sweeps and
 * reveals (decision 298: WallTypeSweeps). Saving is one engine command; every wall of the type follows.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowDown, ArrowUp, Layers, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialName, ofClass, relatedOf, typeLayers } from '../../engine/model';
import { formatLength } from '../../engine/unitsStore';
import { LengthField, NumberField } from '../../shell/cells';
import { layerRankHint } from './layerRank';
import { LintelRuleEditor } from './LintelRule';
import { DEFAULT_RULE, type Rule } from './lintels';
import { MaterialPicker } from '../core/MaterialPicker';
import { TypeFiles } from './TypeFiles';
import { deleteType } from '../core/deleteType';
import { WallLayersLegend } from './WallLayersLegend';
import { WallTypeSweeps, sweepJson, sweepRows, type TypeSweepRow } from './WallTypeSweeps';

/** A layer as the editor holds it: lengths in metres; from and to null for the whole height. */
interface Row { material: string; thickness: number; core: boolean; from: number | null; to: number | null; parts: Part[]; priority: number | null }

/** A piece of a layer in its height, from the layer's bottom up (decision 264); above the last, the layer's own material. */
interface Part { material: string; height: number }

/** A block's length, height and joint, in metres. */
type Block = { length: number; height: number; joint: number };

/** A zone as the editor holds it: lengths in metres. */
interface ZoneRow { key: string; name: string; role: string; height: number; rest: boolean; material: string; block: Block | null; rebar: number | null }

const ROLES = [['masonry', 'Masonry'], ['concrete', 'Concrete'], ['ringBeam', 'Ring beam']] as const;

type ZoneJson = { key?: string; name: string; role: string; height: number | 'rest'; material: string; color?: string; block?: Block; rebar?: number };

const toRow = (z: ZoneJson, materialOf: (id: string) => string): ZoneRow => ({
  key: z.key ?? '', name: z.name, role: z.role, rest: z.height === 'rest', height: z.height === 'rest' ? 0 : z.height,
  material: materialOf(z.material), block: z.block ?? null, rebar: z.rebar ?? null,
});

/** The block a first typed size starts from: the usual length, height and joint. */
const DEFAULT_BLOCK: Block = { length: 0.375, height: 0.238, joint: 0.01 };

const fromRow = (r: ZoneRow) => ({
  ...(r.key ? { key: r.key } : {}), name: r.name.trim(), role: r.role, material: r.material.trim(),
  height: r.rest ? 'rest' : r.height,
  ...(r.role === 'masonry' && r.block ? { block: r.block } : {}),
  ...(r.role !== 'masonry' && r.rebar ? { rebar: r.rebar } : {}),
});

const layer = (material: string, thickness: number, core = false): Row => ({ material, thickness, core, from: null, to: null, parts: [], priority: null });
const blank = (): Row[] => [layer('Render', 0.02), layer('Brick', 0.25, true), layer('Plaster', 0.015)];

export function WallTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcWallType');
  const current = useUi((s) => s.wallTypeId);
  const [editing, setEditing] = useState<string | null>(current ?? types[0]?.id ?? null);
  const [name, setName] = useState('');
  const [rows, setRows] = useState<Row[]>(blank());
  const [zones, setZones] = useState<ZoneRow[]>([]);
  const [lintels, setLintels] = useState<Rule>(DEFAULT_RULE);
  const [sweeps, setSweeps] = useState<TypeSweepRow[]>([]);
  const info = useSession((s) => s.info);

  // The editor loads when the picked type changes, not on every diff (which
  // would throw away what is being typed): it reads the model through a ref.
  const modelRef = useRef(model);
  modelRef.current = model;
  // Load the picked type into the editor.
  useEffect(() => {
    const model = modelRef.current;
    const t = editing ? model.nodes.get(editing) : undefined;
    if (!t) { setName(''); setRows(blank()); setZones([]); setLintels(DEFAULT_RULE); setSweeps([]); return; }
    setSweeps(sweepRows(t.props.sweeps, (id) => materialName(model, id)));
    setLintels((t.props.lintels as unknown as Rule | undefined) ?? DEFAULT_RULE);
    setName(t.name);
    setRows(typeLayers(model, t.id).map((l) => ({ material: materialName(model, l.material), thickness: l.thickness, core: l.core === true, from: l.from || null, to: l.to || null,
      parts: (l.parts ?? []).map((p) => ({ material: materialName(model, p.material), height: p.height })), priority: typeof l.priority === 'number' ? l.priority : null })));
    const stored = Array.isArray(t.props.zones) ? (t.props.zones as unknown as ZoneJson[]) : [];
    setZones(stored.map((z) => toRow(z, (id) => materialName(model, id))));
  }, [editing]);

  // A refusal speaks of what was saved then (report #79: "Every layer needs a thickness." under layers that all had one): it goes once the type is edited or another picked.
  useEffect(() => { if (useSession.getState().error) useSession.setState({ error: null }); }, [editing, name, rows, zones, lintels, sweeps]);

  const close = () => useUi.setState({ dialog: null });
  const set = (i: number, patch: Partial<Row>) => setRows(rows.map((r, k) => (k === i ? { ...r, ...patch } : patch.core ? { ...r, core: false } : r)));
  const move = (i: number, d: number) => {
    const j = i + d;
    if (j < 0 || j >= rows.length) return;
    const next = [...rows];
    [next[i], next[j]] = [next[j], next[i]];
    setRows(next);
  };
  const total = rows.reduce((s, r) => s + r.thickness, 0);
  // A layer's own height (report #44): from and up to, over the wall's base; empty, the whole height.
  const height = (v: number | null) => (v != null && v > 0 ? v : null);
  const layers = rows.map((r) => ({
    material: r.material.trim(), thickness: r.thickness, core: r.core, from: height(r.from), to: height(r.to),
    ...(!r.core && r.parts.length > 0 ? { parts: r.parts.map((p) => ({ material: p.material.trim(), height: p.height })) } : {}),
    ...(r.priority != null ? { priority: r.priority } : {}),
  }));
  const setPart = (i: number, k: number, patch: Partial<Part>) => set(i, { parts: rows[i].parts.map((p, j) => (j === k ? { ...p, ...patch } : p)) });

  const zoneJson = zones.map(fromRow);
  const setZone = (i: number, patch: Partial<ZoneRow>) => setZones(zones.map((z, k) => (k === i ? { ...z, ...patch } : patch.rest ? { ...z, rest: false } : z)));
  const defaultZones = () => {
    const d = (info?.catalogs['architecture.defaultZones'] as unknown as ZoneJson[] | undefined) ?? [];
    const core = rows.find((r) => r.core)?.material ?? 'Brick';
    setZones(d.map((z) => toRow(z.key === 'masonry' ? { ...z, material: core } : z, (x) => x)));
  };

  const save = async () => {
    const sweepList = sweeps.map(sweepJson);
    if (editing) await execute('architecture.updateWallType', { id: editing, name, layers, zones: zoneJson, lintels, sweeps: sweepList });
    else {
      const r = await execute('architecture.createWallType', { name, layers, zones: zoneJson, lintels, sweeps: sweepList }) as { id?: string } | null;
      if (r?.id) { setEditing(r.id); useUi.setState({ wallTypeId: r.id }); }
    }
    // The sweeps as saved, with the keys the engine gave the new ones: saved again, the walls keep the same sweeps.
    const saved = editing ? useSession.getState().nodes.get(editing) : undefined;
    if (saved) setSweeps(sweepRows(saved.props.sweeps, (id) => materialName({ nodes: useSession.getState().nodes, relations: useSession.getState().relations }, id)));
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
            <li><TypeFiles kind="wall" /></li>
          </ul>
          <div className="type-edit">
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its core and thickness)" onChange={(e) => setName(e.target.value)} /></label>
            <p className="muted small">Layers from the face right of the drawn direction to the left face. The core carries the wall's axis.</p>
            <WallLayersLegend layers={rows} />
            <table className="layer-edit">
              <thead><tr><th>Material</th><th>Thickness</th><th>Core</th><th title="How high the layer goes over the wall's base; empty: the whole height">From</th><th title="How high the layer goes over the wall's base; empty: the whole height">Up to</th><th title="Where walls join, a layer goes on through the other wall's layers of a lower priority and stops at the first as high or higher; empty: its material's priority in the cut order, else 50 for the core and 0 for the rest">Priority</th><th /></tr></thead>
              <tbody>
                {rows.map((r, i) => [
                  <tr key={i}>
                    <td><MaterialPicker value={r.material} onChange={(material) => set(i, { material })} /></td>
                    <td className="num"><LengthField metres={r.thickness} min={0} onCommit={(m) => { if (m != null) set(i, { thickness: m }); }} /></td>
                    <td><input type="radio" name="core" checked={r.core} onChange={() => set(i, { core: true })} /></td>
                    <td><LengthField className="layer-height" testId="layer-from" metres={r.from} placeholder="0" readOnly={r.core} noUnit blank min={0} onCommit={(m) => set(i, { from: m })} /></td>
                    <td><LengthField className="layer-height" testId="layer-to" metres={r.to} placeholder="top" readOnly={r.core} noUnit blank min={0} onCommit={(m) => set(i, { to: m })} /></td>
                    <td><NumberField className="layer-priority" testId="layer-priority" value={r.priority} placeholder={String(layerRankHint(model, r.material, r.core))} blank min={0} max={100} integer
                      title="Where walls join, a layer goes on through the other wall's layers of a lower priority and stops at the first as high or higher; empty: its material's priority in the cut order, else 50 for the core and 0 for the rest" onCommit={(n) => set(i, { priority: n })} /></td>
                    <td className="row-tools">
                      <button type="button" className="icon mini" title="Up" onClick={() => move(i, -1)}><ArrowUp size={12} /></button>
                      <button type="button" className="icon mini" title="Down" onClick={() => move(i, 1)}><ArrowDown size={12} /></button>
                      {!r.core && <button type="button" className="icon mini" data-testid="layer-part-add" title="Another material up the layer's height (tiles to 2.10 m, the layer's own above)"
                        onClick={() => set(i, { parts: [...r.parts, { material: 'Ceramic tiles', height: r.parts.length === 0 ? 2.1 : 0.5 }] })}><Layers size={12} /></button>}
                      <button type="button" className="icon mini" title="Remove" onClick={() => setRows(rows.filter((_, k) => k !== i))}><Trash2 size={12} /></button>
                    </td>
                  </tr>,
                  !r.core && r.parts.length > 0 && (
                    <tr key={`${i}-parts`} className="layer-parts">
                      <td colSpan={7} data-i18n-ctx="finish">
                        <span className="muted small">Up its height, from {r.from ? formatLength(r.from) : 'the bottom'}:</span>
                        {r.parts.map((p, k) => (
                          <span key={k} className="layer-part" data-testid="layer-part">
                            <MaterialPicker value={p.material} onChange={(material) => setPart(i, k, { material })} />
                            <LengthField className="layer-height" metres={p.height} min={0} title="How high this piece goes" onCommit={(m) => { if (m != null && m > 0) setPart(i, k, { height: m }); }} />
                            <button type="button" className="icon mini" title="Remove the piece" onClick={() => set(i, { parts: r.parts.filter((_, j) => j !== k) })}><X size={11} /></button>
                          </span>
                        ))}
                        <span className="muted small">then {r.material || 'the layer\'s material'} to {r.to ? formatLength(r.to) : 'the top'}</span>
                      </td>
                    </tr>
                  ),
                ])}
                <tr className="total"><td>Total</td><td className="num">{formatLength(total)}</td><td colSpan={4} /></tr>
              </tbody>
            </table>
            <WallTypeSweeps rows={sweeps} onChange={setSweeps} />
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
                <thead><tr><th>Zone</th><th>Role</th><th>Height</th><th>Rest</th><th>Material</th><th>Block L × H / joint · kg/m³</th><th /></tr></thead>
                <tbody>
                  {zones.map((z, i) => (
                    <tr key={i}>
                      <td><input value={z.name} onChange={(e) => setZone(i, { name: e.target.value })} /></td>
                      <td><select value={z.role} onChange={(e) => setZone(i, { role: e.target.value })}>{ROLES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></td>
                      <td className="num"><LengthField metres={z.rest ? null : z.height} readOnly={z.rest} min={0} onCommit={(m) => { if (m != null) setZone(i, { height: m }); }} /></td>
                      <td><input type="radio" name="rest" checked={z.rest} onChange={() => setZone(i, { rest: true })} /></td>
                      <td><MaterialPicker value={z.material} onChange={(material) => setZone(i, { material })} allowEmpty placeholder="the layer's" /></td>
                      <td>{z.role === 'masonry'
                        ? <span className="block-size">{(['length', 'height', 'joint'] as const).map((k) => (
                          <LengthField key={k} className="layer-height" metres={z.block?.[k]} placeholder={formatLength(DEFAULT_BLOCK[k])} noUnit={k !== 'joint'} min={0}
                            title={k === 'length' ? 'Block length' : k === 'height' ? 'Block height' : 'Joint'}
                            onCommit={(m) => { if (m != null) setZone(i, { block: { ...(z.block ?? DEFAULT_BLOCK), [k]: m } }); }} />))}</span>
                        : <NumberField value={z.rebar} placeholder="100" suffix="kg/m³" min={0} blank onCommit={(n) => setZone(i, { rebar: n })} />}</td>
                      <td className="row-tools">
                        <button type="button" className="icon mini" title="Remove" onClick={() => setZones(zones.filter((_, k) => k !== i))}><Trash2 size={12} /></button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <LintelRuleEditor rule={lintels} onChange={(r) => setLintels(r ?? DEFAULT_RULE)} />
            {zones.length > 0 && <button type="button" className="btn" onClick={() => setZones([...zones, { key: '', name: 'Zone', role: 'masonry', height: 0.25, rest: false, material: 'Brick', block: null, rebar: null }])}><Plus size={14} />Zone</button>}
            <div className="row">
              <button type="button" className="btn" onClick={() => setRows([...rows, layer('Plaster', 0.015)])}><Plus size={14} />Layer</button>
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while elements use it)" onClick={() => void deleteType(editing).then((ok) => { if (ok) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
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
