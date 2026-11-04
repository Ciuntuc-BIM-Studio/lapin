/**
 * Room finishes (decision 265), the Finishes tab's Room group: the configurator (a finish's floor from the top down, its
 * walls from the wall's face into the room with their pieces up the height, its ceiling from the slab down, the walls'
 * height, the skirting, the floor through the doors, a structural slab under the floor; the ready-made library), the
 * room's finish in its properties; the tools are in roomFinishTools.ts.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Layers, Library, Plus, Settings2, Trash2, X } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import { formatLength } from '../../engine/unitsStore';
import { LengthField } from '../../shell/cells';
import { MaterialPicker } from '../core/MaterialPicker';
import { PendingDialog } from '../core/PendingDialog';

interface FinishPart { material: string; height: number; thickness?: number | null }
interface FinishLayer { material: string; thickness: number; parts?: FinishPart[] | null }
interface FinishSlab { material: string; thickness: number; extend: number }
export interface RoomFinishSpec {
  name: string; floor?: FinishLayer[] | null; walls?: FinishLayer[] | null; ceiling?: FinishLayer[] | null;
  wallHeight?: number | null; skirting?: string | null; thresholds: boolean; slab?: FinishSlab | null;
}
type Catalog = { spec: RoomFinishSpec; library: RoomFinishSpec[] };

const TYPE = 'LapinRoomFinishType';

/** One part's layers: material and thickness each; on the walls, each layer's pieces up the height. */
function LayersTable({ layers, onChange, pieces, testId }: { layers: FinishLayer[]; onChange: (l: FinishLayer[]) => void; pieces?: boolean; testId: string }) {
  const set = (i: number, patch: Partial<FinishLayer>) => onChange(layers.map((l, k) => (k === i ? { ...l, ...patch } : l)));
  const setPart = (i: number, k: number, patch: Partial<FinishPart>) => set(i, { parts: (layers[i].parts ?? []).map((p, j) => (j === k ? { ...p, ...patch } : p)) });
  const move = (i: number, d: number) => {
    const j = i + d;
    if (j < 0 || j >= layers.length) return;
    const next = [...layers];
    [next[i], next[j]] = [next[j], next[i]];
    onChange(next);
  };
  return (
    <table className="layer-edit" data-testid={testId}>
      <thead><tr><th>Material</th><th>Thickness</th><th /></tr></thead>
      <tbody>
        {layers.map((l, i) => [
          <tr key={i}>
            <td><MaterialPicker value={l.material} onChange={(material) => set(i, { material })} /></td>
            <td className="num"><LengthField metres={l.thickness} min={0} onCommit={(m) => { if (m != null && m >= 0) set(i, { thickness: m }); }} /></td>
            <td className="row-tools">
              <button type="button" className="icon mini" title="Up" onClick={() => move(i, -1)}>↑</button>
              <button type="button" className="icon mini" title="Down" onClick={() => move(i, 1)}>↓</button>
              {pieces && <button type="button" className="icon mini" data-testid="finish-part-add" title="Another material up the height (tiles to 2.10 m; the layer's own above)"
                onClick={() => set(i, { parts: [...(l.parts ?? []), { material: 'Ceramic tiles', height: (l.parts ?? []).length === 0 ? 2.1 : 0.5 }] })}><Layers size={12} /></button>}
              <button type="button" className="icon mini" title="Remove" onClick={() => onChange(layers.filter((_, k) => k !== i))}><Trash2 size={12} /></button>
            </td>
          </tr>,
          pieces && (l.parts ?? []).length > 0 && (
            <tr key={`${i}-parts`} className="layer-parts">
              <td colSpan={3}>
                <span className="muted small">From the floor up:</span>
                {(l.parts ?? []).map((p, k) => (
                  <span key={k} className="layer-part">
                    <MaterialPicker value={p.material} onChange={(material) => setPart(i, k, { material })} allowEmpty placeholder="nothing" />
                    <LengthField className="layer-height" metres={p.height} min={0} title="How high this piece goes" onCommit={(m) => { if (m != null && m > 0) setPart(i, k, { height: m }); }} />
                    <LengthField className="layer-height" metres={p.thickness ?? null} blank placeholder={formatLength(l.thickness)} min={0} title="Its own thickness (empty: the layer's)" onCommit={(m) => setPart(i, k, { thickness: m && m > 0 ? m : null })} />
                    <button type="button" className="icon mini" title="Remove the piece" onClick={() => set(i, { parts: (l.parts ?? []).filter((_, j) => j !== k) })}><X size={11} /></button>
                  </span>
                ))}
                <span className="muted small">then {l.material} to the top</span>
              </td>
            </tr>
          ),
        ])}
      </tbody>
    </table>
  );
}

const PARTS = [
  ['floor', 'Floor', 'from the finished floor down', 'Ceramic tiles'],
  ['walls', 'Walls', "from the wall's face into the room", 'Plaster'],
  ['ceiling', 'Ceiling', 'from the slab down', 'Plaster'],
] as const;

export function RoomFinishesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const selected = useUi((s) => s.selected);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, TYPE);
  const skirtings = ofClass(model, 'LapinSkirtingType');
  const catalog = info?.catalogs['architecture.roomFinishes'] as unknown as Catalog | undefined;
  const skirtingLibrary = ((info?.catalogs['architecture.skirtings'] as unknown as { library?: { name: string }[] } | undefined)?.library ?? []).map((s) => s.name);
  const [editing, setEditing] = useState<string | null>(useUi.getState().roomFinishTypeId ?? types[0]?.id ?? null);
  const [spec, setSpec] = useState<RoomFinishSpec | null>(null);
  const [fresh, setFresh] = useState<{ from: RoomFinishSpec | null }>({ from: null });
  const modelRef = useRef(model);
  modelRef.current = model;
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as RoomFinishSpec | undefined) ?? fresh.from ?? catalog?.spec ?? null;
    setSpec(s ? structuredClone(s) : null);
  }, [editing, catalog, fresh]);
  if (!spec || !catalog) return <PendingDialog title="Room finishes" />;
  const set = (patch: Partial<RoomFinishSpec>) => setSpec({ ...spec, ...patch });
  const close = () => useUi.setState({ dialog: null });
  const rooms = selected.filter((id) => nodes.get(id)?.type === 'IfcSpace');
  const using = editing ? [...nodes.values()].filter((n) => n.type === 'IfcSpace' && n.props.finishType === editing).length : 0;
  const save = async (): Promise<string | null> => {
    if (editing) { await execute('architecture.updateRoomFinishType', { id: editing, spec }); return editing; }
    const r = await execute('architecture.createRoomFinishType', { spec }) as { id?: string } | null;
    if (r?.id) { setEditing(r.id); useUi.setState({ roomFinishTypeId: r.id }); }
    return r?.id ?? null;
  };
  const apply = async () => {
    const id = await save();
    if (id && rooms.length) await execute('architecture.applyRoomFinish', { roomIds: rooms, typeId: id });
  };
  const fromLibrary = async (name: string) => {
    const r = await execute('architecture.roomFinishTypeFromLibrary', { name }) as { id?: string } | null;
    if (r?.id) setEditing(r.id);
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Room finishes" data-i18n-ctx="finish">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Room finishes</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}><button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}><span data-no-i18n>{t.name}</span></button></li>
            ))}
            <li><button type="button" className="tree-item" onClick={() => { setEditing(null); setFresh({ from: null }); }}><Plus size={12} /> New finish</button></li>
            <li className="eyebrow"><Library size={12} /> From the library</li>
            {catalog.library.map((l) => (
              <li key={l.name}><button type="button" className="tree-item" data-library={l.name} onClick={() => void fromLibrary(l.name)}><span data-no-i18n>{l.name}</span></button></li>
            ))}
          </ul>
          <div className="type-edit room-finish-edit">
            <label className="field-line"><span className="eyebrow">Name</span><input value={spec.name} onChange={(e) => set({ name: e.target.value })} /></label>
            {PARTS.map(([key, label, hint, material]) => {
              const layers = spec[key] ?? [];
              return (
                <section key={key} className="finish-part" data-part={key}>
                  <div className="row">
                    <span className="eyebrow">{label}</span>
                    <span className="muted small">{hint}</span>
                    <span className="grow" />
                    {layers.length === 0 && <span className="muted small">none</span>}
                    <button type="button" className="btn small" onClick={() => set({ [key]: [...layers, { material, thickness: 0.01 }] })}><Plus size={12} />Layer</button>
                  </div>
                  {layers.length > 0 && <LayersTable testId={`finish-${key}`} layers={layers} pieces={key === 'walls'} onChange={(l) => set({ [key]: l.length ? l : null })} />}
                  {key === 'walls' && layers.length > 0 && (
                    <label className="field-line"><span className="eyebrow">Height</span>
                      <LengthField metres={spec.wallHeight ?? null} blank placeholder="up to the ceiling" min={0} onCommit={(m) => set({ wallHeight: m && m > 0 ? m : null })} /></label>
                  )}
                  {key === 'floor' && layers.length > 0 && (
                    <label className="check"><input type="checkbox" checked={spec.thresholds} onChange={(e) => set({ thresholds: e.target.checked })} /> Through the doors, to the wall's axis</label>
                  )}
                </section>
              );
            })}
            <label className="field-line"><span className="eyebrow">Skirting</span>
              <select value={spec.skirting ?? ''} onChange={(e) => set({ skirting: e.target.value || null })}>
                <option value="">none</option>
                {[...new Set([...skirtings.map((s) => s.name), ...skirtingLibrary])].map((n) => <option key={n} value={n} data-no-i18n>{n}</option>)}
              </select></label>
            <section className="finish-part" data-part="slab">
              <label className="check"><input type="checkbox" data-testid="finish-slab" checked={Boolean(spec.slab)}
                onChange={(e) => set({ slab: e.target.checked ? { material: 'Reinforced concrete', thickness: 0.15, extend: 0 } : null })} /> A structural slab under the floor <span className="muted small">(a floor of its own, in the structure)</span></label>
              {spec.slab && (
                <div className="row">
                  <MaterialPicker value={spec.slab.material} onChange={(material) => set({ slab: { ...spec.slab!, material } })} />
                  <LengthField metres={spec.slab.thickness} min={0} title="Thickness" onCommit={(m) => { if (m != null && m > 0) set({ slab: { ...spec.slab!, thickness: m } }); }} />
                  <LengthField metres={spec.slab.extend} min={0} max={1} title="How far past the room's outline, under the walls" onCommit={(m) => set({ slab: { ...spec.slab!, extend: m ?? 0 } })} />
                  <span className="muted small">past the outline</span>
                </div>
              )}
            </section>
            <div className="row">
              {editing && <span className="muted small">{using === 1 ? 'Laid in 1 room' : `Laid in ${using} rooms`}</span>}
              <span className="grow" />
              <button type="button" className="btn" disabled={rooms.length === 0} title={rooms.length ? 'Save, then lay it in the rooms selected' : 'Select rooms first (or use Finish room and click in one)'} onClick={() => void apply()}>
                {rooms.length ? `Finish ${rooms.length} selected rooms` : 'Finish selected rooms'}</button>
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save' : 'Create'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}

/** A room's finish in its properties: which one, laid again or taken off. */
export function RoomFinishField({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const types = [...nodes.values()].filter((n) => n.type === TYPE);
  const current = typeof node.props.finishType === 'string' ? node.props.finishType : '';
  return (
    <span className="row">
      <select data-field="room-finish" value={current} onChange={(e) => void (e.target.value
        ? execute('architecture.applyRoomFinish', { roomIds: [node.id], typeId: e.target.value })
        : execute('architecture.removeRoomFinish', { roomIds: [node.id] }))}>
        <option value="">none</option>
        {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
      </select>
      <button type="button" className="icon mini" title="Room finishes…" onClick={() => useUi.setState({ dialog: 'roomFinishes', roomFinishTypeId: current || null })}><Settings2 size={12} /></button>
    </span>
  );
}
