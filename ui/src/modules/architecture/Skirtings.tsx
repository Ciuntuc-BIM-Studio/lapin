/**
 * Skirtings (a tester's report #61, decision 254), the Finishes tab's Skirtings group: the tools (round the room
 * clicked, broken at its doors; along a line drawn on a wall's face), the skirting types' dialog (the profile with its
 * preview, the height and thickness, the material, the ready-made library) and the skirting in the inspector (its type,
 * the side its board lies on, the gaps at the doors).
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { FlipHorizontal2, Library, PanelBottom, Plus, Settings2, SquareSquare, Table2, X } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, ToolDef, Vec2 } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass, relatedOf, typeOf } from '../../engine/model';
import { LengthField } from '../../shell/cells';
import { pt } from '../../viewport/gripKit';
import { MaterialPicker } from '../core/MaterialPicker';
import { PendingDialog } from '../core/PendingDialog';
import { newElement } from './NewElement';
import { PROFILES, profilePath, type SkirtingSpec } from './skirtingProfile';

const chosenType = () => (useUi.getState().skirtingTypeId ? { typeId: useUi.getState().skirtingTypeId } : {});

/** A skirting round the room clicked (or inside the walls round the click), broken at its doors. */
export const skirtingInRoom: ToolDef = {
  id: 'architecture.skirtingInRoom', label: 'Skirting in room', icon: SquareSquare, shortcut: 'SK', properties: newElement('LapinSkirtingType', 'skirtingTypeId', 'skirtingTypes'),
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('architecture.addSkirting', { storeyId: ctx.storeyId, x: p.x, y: p.y, ...chosenType() }); },
};

/** A skirting along a line drawn on a wall's face (Enter finishes), its board on the side away from the wall. */
export const skirting: ToolDef = {
  id: 'architecture.skirting', label: 'Skirting', icon: PanelBottom, properties: newElement('LapinSkirtingType', 'skirtingTypeId', 'skirtingTypes'),
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => { await ctx.execute('architecture.addSkirting', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...chosenType() }); },
};

export const skirtingTypes: ToolDef = { id: 'architecture.skirtingTypes', label: 'Skirting types', icon: Settings2, run: () => useUi.setState({ dialog: 'skirtingTypes' }) };

export const skirtingSchedule: ToolDef = {
  id: 'architecture.skirtingSchedule', label: 'Skirting schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'skirtings' }).then((r) => { if (r) notify('Skirting schedule made: it is in the project browser, under Schedules.'); }),
};

/** A drawn skirting's points as grips (a room's follows its room, and has none). */
export function skirtingGrips(node: NodeDto): Grip[] {
  if (node.props.roomId) return [];
  const pts = ((node.props.points as unknown[] | undefined) ?? []).map(pt).filter((p): p is Vec2 => p !== null);
  if (pts.length < 2) return [];
  const min = node.props.closed ? 3 : 2;
  const send = (next: Vec2[]) => ({ command: 'architecture.updateSkirting', args: { id: node.id, points: next.map((p) => [p.x, p.y]) } });
  return pts.map((p, i) => ({
    key: `v${i}`, kind: 'vertex', at: p, title: 'Drag: the skirting\'s point',
    step: (to: Vec2) => send(pts.map((q, k) => (k === i ? to : q))),
    remove: pts.length > min ? () => send(pts.filter((_, k) => k !== i)) : undefined,
  }));
}

/** The board's section, drawn to scale against the wall's face and the floor. */
export function ProfilePreview({ spec, size = 120 }: { spec: Pick<SkirtingSpec, 'profile' | 'height' | 'thickness' | 'color'>; size?: number }) {
  const pad = 10;
  return (
    <svg className="skirting-preview" width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img" aria-label="Profile">
      <line x1={pad} y1={2} x2={pad} y2={size - pad} stroke="currentColor" strokeWidth={1.5} />
      <line x1={pad} y1={size - pad} x2={size - 2} y2={size - pad} stroke="currentColor" strokeWidth={1.5} />
      <path d={profilePath(spec, size, size, pad)} fill={spec.color} stroke="currentColor" strokeWidth={1} />
    </svg>
  );
}

/** The project's skirting types, one being edited, and the library's ready-made ones. */
export function SkirtingTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'LapinSkirtingType');
  const catalog = info?.catalogs['architecture.skirtings'] as unknown as { spec: SkirtingSpec; library: SkirtingSpec[] } | undefined;
  const [editing, setEditing] = useState<string | null>(types[0]?.id ?? null);
  const [spec, setSpec] = useState<SkirtingSpec | null>(null);
  // What a new type starts from (a new object on each New…, so the editor reloads).
  const [fresh, setFresh] = useState<{ from: SkirtingSpec | null }>({ from: null });
  const modelRef = useRef(model);
  modelRef.current = model;
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as SkirtingSpec | undefined) ?? fresh.from ?? catalog?.spec ?? null;
    setSpec(s ? structuredClone(s) : null);
  }, [editing, catalog, fresh]);
  if (!spec || !catalog) return <PendingDialog title="Skirting types" />;
  const set = (patch: Partial<SkirtingSpec>) => setSpec({ ...spec, ...patch });
  const close = () => useUi.setState({ dialog: null });
  const save = async () => {
    if (editing) await execute('architecture.updateSkirtingType', { id: editing, spec });
    else { const r = await execute('architecture.createSkirtingType', { spec }) as { id?: string } | null; if (r?.id) setEditing(r.id); }
  };
  const fromLibrary = async (name: string) => {
    const r = await execute('architecture.skirtingTypeFromLibrary', { name }) as { id?: string } | null;
    if (r?.id) setEditing(r.id);
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Skirting types">
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">Skirting types</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}><button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}><span data-no-i18n>{t.name}</span><span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span></button></li>
            ))}
            <li><button type="button" className="tree-item" onClick={() => { setEditing(null); setFresh({ from: null }); }}><Plus size={12} /> New skirting type</button></li>
            <li className="eyebrow"><Library size={12} /> From the library</li>
            {catalog.library.map((l) => (
              <li key={l.name}><button type="button" className="tree-item" data-library={l.name} onClick={() => void fromLibrary(l.name)}><span data-no-i18n>{l.name}</span></button></li>
            ))}
          </ul>
          <div className="type-edit">
            <div className="row" style={{ alignItems: 'flex-start', gap: 12 }}>
              <ProfilePreview spec={spec} />
              <div className="grow">
                <label className="field-line"><span className="eyebrow">Name</span><input value={spec.name} onChange={(e) => set({ name: e.target.value })} /></label>
                <label className="field-line"><span className="eyebrow">Profile</span>
                  <select data-field="skirting-profile" value={spec.profile} onChange={(e) => set({ profile: e.target.value as SkirtingSpec['profile'] })}>
                    {PROFILES.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
                  </select></label>
              </div>
            </div>
            <label className="field-line"><span className="eyebrow">Height</span>
              <LengthField testId="skirting-height" metres={spec.height} min={0.04} max={0.3} onCommit={(m) => { if (m != null) set({ height: m }); }} /></label>
            <label className="field-line"><span className="eyebrow">Thickness</span>
              <LengthField testId="skirting-thickness" metres={spec.thickness} min={0.005} max={0.04} onCommit={(m) => { if (m != null) set({ thickness: m }); }} /></label>
            <label className="field-line"><span className="eyebrow">Material</span><MaterialPicker value={spec.material} onChange={(material) => set({ material })} /></label>
            <label className="field-line"><span className="eyebrow">Colour</span><input type="color" value={spec.color} onChange={(e) => set({ color: e.target.value })} /></label>
            <p className="muted small">A skirting in room runs round the room along the walls’ faces and stops at each door.</p>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" onClick={() => useUi.setState({ skirtingTypeId: editing })}>Place with it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}

/** A skirting in the inspector: its type (with its profile), the side its board lies on, the gaps at the doors (its base offset is the class's own field). */
export function SkirtingSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'LapinSkirtingType');
  const type = typeOf(model, node.id);
  const spec = type?.props.spec as unknown as SkirtingSpec | undefined;
  const gaps = node.props.gaps !== false;
  const set = (args: Record<string, unknown>) => void execute('architecture.updateSkirting', { id: node.id, ...args });
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Skirting type</span>
        <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => set({ typeId: e.target.value })}>
          {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
        </select>
        <button type="button" className="link small" onClick={() => useUi.setState({ dialog: 'skirtingTypes' })}>Skirting types…</button>
      </label>
      {spec && <div className="row" style={{ gap: 8 }}><ProfilePreview spec={spec} size={64} /><span className="small">{PROFILES.find((p) => p.id === spec.profile)?.label}</span></div>}
      <dl className="fields">
        <dt>Doors</dt>
        <dd><label className="check"><input type="checkbox" checked={gaps} disabled={!node.editable} onChange={(e) => set({ gaps: e.target.checked })} /><span>Gaps at the doors</span></label></dd>
      </dl>
      {node.props.roomId
        ? <p className="muted small">It follows its room’s outline.</p>
        : <button type="button" className="btn small" disabled={!node.editable} onClick={() => set({ side: 'flip' })}><FlipHorizontal2 size={13} /> Flip side</button>}
    </div>
  );
}
