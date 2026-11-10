/**
 * A curtain wall's cells (a tester's request, #133): its elevation as the engine builds it, every cell clickable. The cells
 * chosen take a panel type (glass, solid, a framed infill, or open), saved once in the project and given to any wall's cells,
 * or a door: a type's door at its own size, aligned in the cell with glass and framing round it, or filling the cell. The
 * settings are kept by cell, so they stay where they are when the grid changes; the doors are doors (tags, schedules, IFC).
 */

import { useEffect, useMemo, useState } from 'react';
import { DoorOpen, Plus, Save, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import { LengthField } from '../../shell/cells';
import { PendingDialog } from '../core/PendingDialog';
import { MaterialPicker } from '../core/MaterialPicker';
import { ProfilePickerField } from '../core/profileLibraries/ProfilePickerField';
import { clearKeys, panelJson, PANEL_KINDS, type PanelSpec } from './curtainCells';

interface Rect { key: string; role: string; x0: number; x1: number; z0: number; z1: number; outline?: [number, number][] }
interface CellInfo extends Rect { panelType: string | null; own: boolean; door: string | null }
interface DoorInfo { id: string; name: string; key: string; problem: string | null; fit: boolean; align: string; offset: number; type: string | null; x0?: number; x1?: number; z0?: number; z1?: number }
interface Cells { length: number; height: number; cells: CellInfo[]; pieces: Rect[]; doors: DoorInfo[] }
interface Section { name?: string; width: number; depth: number }

const NEW = '__new';

export function CurtainCellsDialog() {
  const id = useUi((s) => s.selection);
  const wall = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const version = useSession((s) => s.version);
  const close = () => useUi.setState({ dialog: null });
  const [data, setData] = useState<Cells | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [panelTypeId, setPanelTypeId] = useState('');
  const [draft, setDraft] = useState<(PanelSpec & { name: string; id?: string }) | null>(null);
  const [doorTypeId, setDoorTypeId] = useState(NEW);
  const [door, setDoor] = useState({ leaves: 1, operation: 'swing', width: 1.0, height: 2.2, glazed: true });
  const [frame, setFrame] = useState<Section | null>(null);
  const [fit, setFit] = useState(true);
  const [align, setAlign] = useState('center');
  const [offset, setOffset] = useState(0);

  useEffect(() => {
    if (!id) return;
    let live = true;
    void query<Cells>('architecture.curtainCells', { id }).then((r) => { if (live && r) setData(r); });
    return () => { live = false; };
  }, [id, version]);

  const model = { nodes, relations };
  const panelTypes = ofClass(model, 'IfcPlateType').filter((t) => t.props.panel);
  const doorTypes = ofClass(model, 'IfcDoorType');
  const cells = data?.cells ?? [];
  const chosen = useMemo(() => cells.filter((c) => selected.has(c.key)), [cells, selected]);
  const doorsChosen = (data?.doors ?? []).filter((d) => selected.has(d.key));

  if (!wall || wall.type !== 'IfcCurtainWall' || !data) return <PendingDialog title="Curtain wall panels and doors" />;

  const W = 560, H = 300, pad = 18;
  const k = Math.min((W - 2 * pad) / Math.max(data.length, 0.1), (H - 2 * pad) / Math.max(data.height, 0.1));
  const sx = (x: number) => pad + x * k, sy = (z: number) => H - pad - z * k;
  const click = (key: string, e: React.MouseEvent) => {
    const more = e.shiftKey || e.ctrlKey || e.metaKey;
    setSelected((cur) => {
      const next = new Set(more ? cur : []);
      if (cur.has(key) && more) next.delete(key); else next.add(key);
      return next;
    });
  };
  const keys = chosen.length === cells.length && cells.length > 1 ? ['panel'] : chosen.map((c) => c.key);
  const givePanel = () => { if (chosen.length && panelTypeId) void execute('architecture.setCurtainPanels', { id: wall.id, keys, typeId: panelTypeId }); };
  const clearPanel = () => { if (chosen.length) void execute('architecture.setCurtainPanels', { id: wall.id, keys: clearKeys(keys, chosen.map((c) => c.key)), clear: true }); };
  const saveDraft = async () => {
    if (!draft) return;
    const panel = panelJson(draft);
    if (draft.id) await execute('architecture.updateCurtainPanelType', { id: draft.id, name: draft.name, panel });
    else {
      const r = await execute('architecture.createCurtainPanelType', { name: draft.name, panel }) as { id?: string } | null;
      if (r?.id) setPanelTypeId(r.id);
    }
    setDraft(null);
  };
  const placeDoor = async () => {
    const free = chosen.filter((c) => !c.door && !c.outline).map((c) => c.key);
    if (!free.length) return;
    const args: Record<string, unknown> = { id: wall.id, keys: free, fit, align, offset };
    if (doorTypeId === NEW) args.door = { ...door, ...(frame ? { frameSection: frame } : {}) };
    else args.typeId = doorTypeId;
    await execute('architecture.addCurtainDoor', args);
  };
  const removeDoors = () => { if (doorsChosen.length) void execute('element.delete', { ids: doorsChosen.map((d) => d.id) }); };
  const pickFrame = async (profile: unknown, library: string | null) => {
    const used = await execute('profiles.use', { profile, library }) as { section?: Section } | null;
    if (used?.section) setFrame(used.section);
  };
  const fill = (p: Rect) => (p.role === 'panel' ? '#bcd7ee' : p.role === 'mullion' || p.role === 'transom' ? '#5b6b7c' : '#999');
  const typeName = (tid: string | null) => panelTypes.find((t) => t.id === tid)?.name;
  const first = chosen[0];
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Curtain wall panels and doors" data-testid="curtain-cells" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Curtain wall panels and doors · <span data-no-i18n>{wall.name}</span></span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Click a cell (Shift or Ctrl/⌘ adds more). The panel type or the door below goes to the ones chosen; they keep it when the grid changes.</p>
          <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} className="wall-profile-svg" aria-label="Elevation">
            {data.pieces.map((p) => p.outline && p.outline.length >= 3
              ? <polygon key={p.key} points={p.outline.map(([u, z]) => `${sx(u)},${sy(z)}`).join(' ')} fill={fill(p)} stroke="#1e2a3a" strokeWidth={0.3} />
              : <rect key={p.key} x={sx(p.x0)} y={sy(p.z1)} width={Math.max(1, (p.x1 - p.x0) * k)} height={Math.max(1, (p.z1 - p.z0) * k)} fill={fill(p)} stroke="#1e2a3a" strokeWidth={0.3} />)}
            {data.doors.filter((d) => d.x0 != null).map((d) => (
              <g key={d.id} data-door={d.key}>
                <rect x={sx(d.x0!)} y={sy(d.z1!)} width={(d.x1! - d.x0!) * k} height={(d.z1! - d.z0!) * k} fill="#c8a27a" stroke="#1e2a3a" strokeWidth={0.6} />
                <text x={sx((d.x0! + d.x1!) / 2)} y={sy((d.z0! + d.z1!) / 2)} textAnchor="middle" fontSize={11} fill="#1e2a3a" data-no-i18n>{d.name}</text>
              </g>
            ))}
            {cells.map((c) => (
              <rect key={c.key} x={sx(c.x0)} y={sy(c.z1)} width={Math.max(2, (c.x1 - c.x0) * k)} height={Math.max(2, (c.z1 - c.z0) * k)}
                fill={selected.has(c.key) ? 'var(--accent)' : c.own ? '#2f7f7a' : 'transparent'} fillOpacity={selected.has(c.key) ? 0.45 : c.own ? 0.3 : 0}
                stroke={selected.has(c.key) ? 'var(--accent)' : 'none'} style={{ cursor: 'pointer' }} data-cell={c.key} onClick={(e) => click(c.key, e)}>
                <title>{c.key}{typeName(c.panelType) ? ` · ${typeName(c.panelType)}` : ''}</title>
              </rect>
            ))}
          </svg>
          <div className="row wrap" style={{ gap: 6, margin: '6px 0' }}>
            <button type="button" className="btn small" onClick={() => setSelected(new Set(cells.map((c) => c.key)))}>Every cell</button>
            <button type="button" className="btn small" onClick={() => setSelected(new Set())}>None</button>
            <span className="muted small"><span data-no-i18n>{chosen.length}</span> <span>chosen</span>{first && <> · <span data-no-i18n>{first.key}</span></>}</span>
            {data.doors.filter((d) => d.problem).map((d) => <span key={d.id} className="warn small"><span data-no-i18n>{d.name}</span>: {d.problem}</span>)}
          </div>
          <div className="row" style={{ alignItems: 'flex-start', gap: 16, opacity: chosen.length ? 1 : 0.6 }}>
            <fieldset className="grow">
              <legend>Panel</legend>
              <label className="field-line"><span className="eyebrow">Panel type</span>
                <select data-field="cell-panel-type" value={panelTypeId} onChange={(e) => setPanelTypeId(e.target.value)}>
                  <option value="">—</option>
                  {panelTypes.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select>
              </label>
              <div className="row wrap" style={{ gap: 6 }}>
                <button type="button" className="btn small" data-action="cells-panel-new" onClick={() => setDraft({ name: '', kind: 'framed', thickness: 0.03, offset: 0, frameWidth: 0.05, frameDepth: 0.08, inset: 0.015, infill: 'solid' })}><Plus size={13} />New panel type…</button>
                {panelTypeId && <button type="button" className="btn small" onClick={() => { const t = panelTypes.find((x) => x.id === panelTypeId); if (t) setDraft({ ...(t.props.panel as unknown as PanelSpec), name: t.name, id: t.id }); }}>Edit</button>}
                <button type="button" className="btn small primary" data-action="cells-panel-apply" disabled={!chosen.length || !panelTypeId} onClick={givePanel}>Give to the chosen</button>
                <button type="button" className="btn small" data-action="cells-panel-reset" disabled={!chosen.some((c) => c.own)} onClick={clearPanel} title="The chosen cells go back to the curtain wall type's panel">The type&apos;s panel</button>
              </div>
              {draft && (
                <div className="fields-grid" data-testid="panel-type-editor" style={{ marginTop: 6 }}>
                  <label className="field-line"><span className="eyebrow">Name</span><input data-field="panel-name" value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} /></label>
                  <label className="field-line"><span className="eyebrow">Kind</span>
                    <select data-field="panel-kind" value={draft.kind} onChange={(e) => setDraft({ ...draft, kind: e.target.value as PanelSpec['kind'] })}>
                      {PANEL_KINDS.map((x) => <option key={x.id} value={x.id}>{x.label}</option>)}
                    </select>
                  </label>
                  {draft.kind !== 'empty' && <>
                    <label className="field-line"><span className="eyebrow">Thickness</span><LengthField metres={draft.thickness} min={0.001} onCommit={(v) => { if (v != null) setDraft({ ...draft, thickness: v }); }} /></label>
                    <label className="field-line" title="Its middle out of the curtain wall's axis, towards the outside"><span className="eyebrow">Offset out</span><LengthField metres={draft.offset} onCommit={(v) => { if (v != null) setDraft({ ...draft, offset: v }); }} /></label>
                    <label className="field-line"><span className="eyebrow">Material</span><MaterialPicker value={draft.material ?? ''} allowEmpty placeholder="the type's" onChange={(m) => setDraft({ ...draft, material: m || null })} /></label>
                  </>}
                  {draft.kind === 'framed' && <>
                    <label className="field-line"><span className="eyebrow">Infill</span>
                      <select value={draft.infill} onChange={(e) => setDraft({ ...draft, infill: e.target.value as PanelSpec['infill'] })}><option value="glass">Glass</option><option value="solid">Solid</option></select>
                    </label>
                    <label className="field-line"><span className="eyebrow">Frame width</span><LengthField metres={draft.frameWidth} min={0.001} onCommit={(v) => { if (v != null) setDraft({ ...draft, frameWidth: v }); }} /></label>
                    <label className="field-line"><span className="eyebrow">Frame depth</span><LengthField metres={draft.frameDepth} min={0.001} onCommit={(v) => { if (v != null) setDraft({ ...draft, frameDepth: v }); }} /></label>
                    <label className="field-line" title="How far the infill sits back from the frame's outer face"><span className="eyebrow">Inset</span><LengthField metres={draft.inset} min={0} onCommit={(v) => { if (v != null) setDraft({ ...draft, inset: v }); }} /></label>
                    <label className="field-line"><span className="eyebrow">Frame material</span><MaterialPicker value={draft.frameMaterial ?? ''} allowEmpty placeholder="the type's" onChange={(m) => setDraft({ ...draft, frameMaterial: m || null })} /></label>
                  </>}
                  <div className="row" style={{ gap: 6 }}>
                    <button type="button" className="btn small primary" data-action="panel-type-save" onClick={() => void saveDraft()}><Save size={13} />Save the panel type</button>
                    <button type="button" className="btn small" onClick={() => setDraft(null)}>Cancel</button>
                  </div>
                </div>
              )}
            </fieldset>
            <fieldset className="grow">
              <legend>Door</legend>
              <label className="field-line"><span className="eyebrow">Door type</span>
                <select data-field="cell-door-type" value={doorTypeId} onChange={(e) => setDoorTypeId(e.target.value)}>
                  <option value={NEW}>New curtain wall door</option>
                  {doorTypes.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select>
              </label>
              {doorTypeId === NEW && <>
                <label className="field-line"><span className="eyebrow">Leaves</span>
                  <select data-field="door-leaves" value={door.leaves} onChange={(e) => setDoor({ ...door, leaves: Number(e.target.value), width: Number(e.target.value) === 2 ? Math.max(door.width, 1.6) : door.width })}><option value={1}>Single</option><option value={2}>Double</option></select>
                </label>
                <label className="field-line"><span className="eyebrow">Opening</span>
                  <select data-field="door-operation" value={door.operation} onChange={(e) => setDoor({ ...door, operation: e.target.value })}><option value="swing">Swinging</option><option value="sliding">Sliding</option></select>
                </label>
                <label className="field-line"><span className="eyebrow">Width</span><LengthField metres={door.width} min={0.4} onCommit={(v) => { if (v != null) setDoor({ ...door, width: v }); }} /></label>
                <label className="field-line"><span className="eyebrow">Height</span><LengthField metres={door.height} min={1} onCommit={(v) => { if (v != null) setDoor({ ...door, height: v }); }} /></label>
                <label className="check-line"><input type="checkbox" checked={door.glazed} onChange={(e) => setDoor({ ...door, glazed: e.target.checked })} /> Glazed leaves</label>
                <label className="field-line"><span className="eyebrow">Frame profile</span>
                  <ProfilePickerField testId="curtain-door-frame" value={frame?.name ?? null} onPick={(p, library) => void pickFrame(p, library)} />
                </label>
              </>}
              <label className="check-line" title="Its width and height are the cell's, and follow it when the grid changes"><input type="checkbox" data-field="door-fit" checked={fit} onChange={(e) => setFit(e.target.checked)} /> Fill the cell</label>
              {!fit && <>
                <label className="field-line"><span className="eyebrow">In the cell</span>
                  <select data-field="door-align" value={align} onChange={(e) => setAlign(e.target.value)}><option value="left">Left</option><option value="center">Centre</option><option value="right">Right</option></select>
                </label>
                <label className="field-line" title="From where it is aligned, into the cell"><span className="eyebrow">Offset</span><LengthField metres={offset} onCommit={(v) => { if (v != null) setOffset(v); }} /></label>
              </>}
              <div className="row wrap" style={{ gap: 6 }}>
                <button type="button" className="btn small primary" data-action="cells-door-place" disabled={!chosen.some((c) => !c.door && !c.outline)} onClick={() => void placeDoor()}><DoorOpen size={13} />Door in the chosen</button>
                <button type="button" className="btn small" data-action="cells-door-remove" disabled={!doorsChosen.length} onClick={removeDoors}><Trash2 size={13} />Take the door out</button>
              </div>
              <p className="muted small">Hand, side and type: select the door. A door smaller than its cell gets a transom over it and a mullion beside it, glazed with the cell&apos;s panel.</p>
            </fieldset>
          </div>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
