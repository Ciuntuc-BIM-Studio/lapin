/**
 * Scaffolds: the configurator. The project's scaffold types on the left; on
 * the right the one being edited — its system (modular or frames), its bay
 * lengths, width and lift, its distance from the wall, decks, guardrail and
 * toe boards, anchors, the base jacks' reach, access bays and the net — with
 * the elevation the engine lays out for a run of the length and height chosen,
 * and what it counts. Saving is one engine command; every scaffold of the type
 * is laid out again.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { ofClass, relatedOf } from '../../engine/model';
import { Mm, MmList, Num } from './fields';
import { setSiteWorks, useSiteWorks } from './prefs';
import { openingType } from './typeChoice';

export interface ScaffoldSpec {
  system: 'modular' | 'frame'; bays: number[]; width: number; lift: number; wallGap: number; decks: 'every' | 'top'; guardrail: number; toeBoard: boolean;
  deckBoard: number; anchorSpacing: number; anchorEveryLifts: number; reach: number; jack: number; accessEvery: number; net: boolean; tube: number;
}
interface Preview { problems?: string[]; bays?: number[]; decks?: number[]; lifts?: number; lift?: number; guardTop?: number; anchors?: number[][]; area?: number; counts?: Record<string, number> }

const COUNTS: [string, string][] = [
  ['bays', 'Bays'], ['standards', 'Standards'], ['frames', 'Frames'], ['ledgers', 'Ledgers'], ['transoms', 'Transoms'], ['guardrails', 'Guardrails'],
  ['deckBoards', 'Deck boards'], ['toeBoards', 'Toe boards'], ['diagonals', 'Diagonals'], ['baseJacks', 'Base jacks'], ['anchors', 'Anchors'], ['accessBays', 'Access bays'],
];

const Line = ({ label, children }: { label: string; children: React.ReactNode }) => <label className="field-line"><span className="eyebrow">{label}</span><span>{children}</span></label>;

export function ScaffoldDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'LapinScaffoldType');
  const fallback = info?.catalogs['sitework.scaffoldDefault'] as unknown as ScaffoldSpec | undefined;
  // The selected scaffold's type first: what the user is looking at is what they mean to change.
  const [editing, setEditing] = useState<string | null>(() => openingType(model, useUi.getState().selected, 'LapinScaffold', useSiteWorks.getState().scaffoldTypeId, types));
  const [name, setName] = useState('');
  const [spec, setSpec] = useState<ScaffoldSpec | null>(null);
  const [size, setSize] = useState({ length: 10, height: 6 });
  const [preview, setPreview] = useState<Preview | null>(null);

  const modelRef = useRef(model);
  modelRef.current = model;
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    setSpec(fallback ? { ...structuredClone(fallback), ...((t?.props.spec as Partial<ScaffoldSpec> | undefined) ?? {}) } : null);
    setName(t?.name ?? 'Facade scaffold');
  }, [editing, fallback]);

  useEffect(() => {
    if (!spec) return;
    let live = true;
    void engine.call<Preview>('doc.query', { name: 'sitework.scaffoldPreview', args: { spec, ...size } }).then((p) => { if (live) setPreview(p); }).catch(() => undefined);
    return () => { live = false; };
  }, [spec, size]);

  if (!spec) return null;
  const set = (patch: Partial<ScaffoldSpec>) => setSpec({ ...spec, ...patch });
  const close = () => useUi.setState({ dialog: null });
  const fresh = () => { setEditing(null); setSpec(structuredClone(fallback ?? spec)); setName(`Facade scaffold ${types.length + 1}`); };
  const save = async () => {
    if (editing) await execute('sitework.updateScaffoldType', { id: editing, name, spec });
    else {
      const r = await execute('sitework.createScaffoldType', { name, spec }) as { id?: string } | null;
      if (r?.id) { setEditing(r.id); setSiteWorks({ scaffoldTypeId: r.id }); }
    }
  };

  // The elevation: along the run to the right, up from the ground; scaled into the box.
  const W = 380, H = 260, pad = 14;
  const top = Math.max(size.height, preview?.guardTop ?? size.height);
  const along = (preview?.bays ?? []).reduce((a, b) => a + b, 0) || size.length;
  const k = Math.min((W - 2 * pad) / along, (H - 2 * pad) / top);
  const ex = (x: number) => pad + x * k;
  const ez = (z: number) => H - pad - z * k;
  const stations = [0];
  for (const b of preview?.bays ?? []) stations.push(stations[stations.length - 1] + b);

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Scaffolds">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Scaffolds</span>
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
            <li><button type="button" className="tree-item" onClick={fresh}><Plus size={12} /> New scaffold type</button></li>
          </ul>
          <div className="joinery-edit">
            <div className="joinery-form">
              <Line label="Name"><input value={name} onChange={(e) => setName(e.target.value)} /></Line>
              <Line label="System">
                <select value={spec.system} onChange={(e) => set({ system: e.target.value as ScaffoldSpec['system'] })}>
                  <option value="modular">modular (standards, ledgers, transoms)</option><option value="frame">frames</option>
                </select>
              </Line>
              <Line label="Bay lengths"><MmList value={spec.bays} onCommit={(bays) => set({ bays })} /></Line>
              <Line label="Width"><Mm value={spec.width} min={300} onCommit={(v) => v && set({ width: v })} /></Line>
              <Line label="Lift"><Mm value={spec.lift} min={500} onCommit={(v) => v && set({ lift: v })} /></Line>
              <Line label="From the wall"><Mm value={spec.wallGap} min={0} onCommit={(v) => v != null && set({ wallGap: v })} /></Line>
              <Line label="Decks">
                <select value={spec.decks} onChange={(e) => set({ decks: e.target.value as ScaffoldSpec['decks'] })}>
                  <option value="every">on every lift</option><option value="top">on the top lift only</option>
                </select>
              </Line>
              <Line label="Deck board"><Mm value={spec.deckBoard} min={100} onCommit={(v) => v && set({ deckBoard: v })} /></Line>
              <Line label="Guardrail"><Mm value={spec.guardrail} min={0} onCommit={(v) => v != null && set({ guardrail: v })} /></Line>
              <Line label="Toe boards"><input type="checkbox" checked={spec.toeBoard} onChange={(e) => set({ toeBoard: e.target.checked })} /></Line>
              <Line label="Top deck within"><Mm value={spec.reach} min={0} onCommit={(v) => v != null && set({ reach: v })} /> <span className="muted small">of the work top</span></Line>
              <Line label="Anchors every"><Mm value={spec.anchorSpacing} min={500} onCommit={(v) => v && set({ anchorSpacing: v })} /></Line>
              <Line label="on every n-th lift"><Num value={spec.anchorEveryLifts} min={1} onCommit={(v) => set({ anchorEveryLifts: Math.round(v) })} /></Line>
              <Line label="Base jacks take"><Mm value={spec.jack} min={0} onCommit={(v) => v != null && set({ jack: v })} /></Line>
              <Line label="An access bay every"><Num value={spec.accessEvery} step={5} min={0} onCommit={(v) => set({ accessEvery: v })} /> <span className="muted small">m (0: none)</span></Line>
              <Line label="Protective net"><input type="checkbox" checked={spec.net} onChange={(e) => set({ net: e.target.checked })} /></Line>
            </div>
            <div className="joinery-preview">
              <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Elevation">
                <line x1={pad / 2} x2={W - pad / 2} y1={ez(0)} y2={ez(0)} stroke="#6b4e2e" strokeWidth={1.2} />
                <line x1={ex(0)} x2={ex(along)} y1={ez(size.height)} y2={ez(size.height)} stroke="#b5532d" strokeDasharray="4 3" strokeWidth={0.8} />
                {stations.map((x, i) => <line key={`s${i}`} x1={ex(x)} x2={ex(x)} y1={ez(0)} y2={ez(preview?.guardTop ?? 0)} stroke="#4a5160" strokeWidth={1.2} />)}
                {(preview?.decks ?? []).map((z, i) => <rect key={`d${i}`} x={ex(0)} y={ez(z)} width={along * k} height={2.5} fill="#b8925a" />)}
                {(preview?.decks ?? []).map((z, i) => <line key={`g${i}`} x1={ex(0)} x2={ex(along)} y1={ez(z + spec.guardrail)} y2={ez(z + spec.guardrail)} stroke="#4a5160" strokeWidth={0.6} />)}
                {(preview?.anchors ?? []).map(([x, z], i) => <circle key={`a${i}`} cx={ex(x)} cy={ez(z)} r={2.5} fill="#d0a020" />)}
              </svg>
              <div className="row">
                <Line label="Preview length"><Num value={size.length} step={0.5} min={0.5} onCommit={(v) => setSize({ ...size, length: v })} /> <span className="muted small">m</span></Line>
                <Line label="work top"><Num value={size.height} step={0.5} min={1} onCommit={(v) => setSize({ ...size, height: v })} /> <span className="muted small">m</span></Line>
              </div>
              <p className="muted small">
                {(preview?.bays ?? []).map((b) => Math.round(b * 100) / 100).join(' + ')} m · {preview?.lifts ?? 0} lift(s) · {Math.round((preview?.area ?? 0) * 10) / 10} m²
              </p>
              <p className="muted small">
                {COUNTS.filter(([key]) => (preview?.counts?.[key] ?? 0) > 0).map(([key, label]) => `${label} ${preview?.counts?.[key]}`).join(' · ')}
              </p>
              <p className="muted small">IFC: IfcElementAssembly (SCAFFOLD) over IfcMember, IfcPlate and IfcDiscreteAccessory, temporary</p>
              {(preview?.problems ?? []).map((p) => <p key={p} className="error-text">{p}</p>)}
            </div>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while scaffolds use it)" onClick={() => void execute('element.delete', { id: editing }).then((r) => { if (r !== null) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
              {editing && <button type="button" className="btn" onClick={() => setSiteWorks({ scaffoldTypeId: editing })}>Draw with it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
