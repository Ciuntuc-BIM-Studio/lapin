/**
 * Structure types: columns and beams (their section), strip foundations
 * (the stem's layers, the footing's size, its level) and pad footings (the
 * steps, bottom up). A new type is made here, and a type clicked in the list
 * is changed here (report #42), its elements following it; the inspector
 * gives an element another one.
 */

import { useState } from 'react';
import { Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass, typeLayers } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { describeType } from './describe';
import { structureHooks } from './hooks';
import { deleteType } from '../core/deleteType';
import { useStandards } from '../../engine/unitsStore';
import { formatSteps, parseSteps, type Step } from './steps';
import { Cell, LengthField } from '../../shell/cells';

type Kind = 'column' | 'beam' | 'strip' | 'pad';
const num = (m: unknown, fallback: number) => (typeof m === 'number' ? m : fallback);

/** A labelled length of the type (the shared field). */
const Len = ({ label, value, onChange, min }: { label: string; value: number; onChange: (m: number) => void; min?: number }) => (
  <label className="field-line"><span className="eyebrow">{label}</span><LengthField metres={value} min={min} onCommit={(m) => { if (m != null) onChange(m); }} /></label>
);

export function StructureTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = { nodes, relations };
  const [kind, setKind] = useState<Kind>('column');
  // The type being changed; null: a new one is made.
  const [editing, setEditing] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [shape, setShape] = useState<'rectangle' | 'circle'>('rectangle');
  // Lengths in metres; the steps as typed.
  const units = useStandards((s) => s.units);
  const [w, setW] = useState(0.3);
  const [d, setD] = useState(0.3);
  const [stem, setStem] = useState(1.4);
  const [fw, setFw] = useState(0.6);
  const [fh, setFh] = useState(0.4);
  const [bottom, setBottom] = useState(-1.8);
  const [core, setCore] = useState(0.3);
  const [steps, setSteps] = useState<Step[]>([{ length: 1.8, width: 1.8, height: 0.5 }, { length: 1, width: 1, height: 0.4 }]);
  const list = kind === 'column' ? ofClass(model, 'IfcColumnType') : kind === 'beam' ? ofClass(model, 'IfcBeamType')
    : ofClass(model, 'IfcFootingType').filter((t) => t.props.kind === kind);

  const edited = editing ? nodes.get(editing) ?? null : null;

  /** A type clicked: its values in the fields, to be changed. */
  const pick = (t: NodeDto) => {
    setEditing(t.id);
    setName(t.name);
    const sec = t.props.section as { kind?: string; width?: number; depth?: number } | undefined;
    if (sec) { setShape(sec.kind === 'circle' ? 'circle' : 'rectangle'); setW(num(sec.width, 0.3)); setD(num(sec.depth, 0.3)); }
    if (t.props.kind === 'strip') {
      setStem(num(t.props.stemHeight, 1.4)); setFw(num(t.props.footingWidth, 0.6)); setFh(num(t.props.footingHeight, 0.4)); setBottom(num(t.props.bottom, -1.8));
      const layers = typeLayers(model, t.id);
      const core = layers.find((l) => l.core) ?? layers.reduce<(typeof layers)[number] | undefined>((a, l) => (!a || l.thickness > a.thickness ? l : a), undefined);
      if (core) setCore(core.thickness);
    }
    if (t.props.kind === 'pad') {
      setBottom(num(t.props.bottom, -1.8));
      setSteps((t.props.steps as Step[] | undefined) ?? []);
    }
  };
  const fresh = () => { setEditing(null); setName(''); };
  const switchKind = (k: Kind) => { setKind(k); fresh(); };

  const save = async () => {
    if (!edited) return;
    const section = { kind: shape, width: w, depth: shape === 'circle' ? w : d };
    const args: Record<string, unknown> = { id: edited.id, name };
    if (kind === 'column' || kind === 'beam') args.section = section;
    else if (kind === 'strip') Object.assign(args, { stemHeight: stem, footingWidth: fw, footingHeight: fh, bottom, core });
    else Object.assign(args, { bottom, steps });
    // The name as it was: left to the engine, which renames a type still named after its size.
    if (name.trim() === edited.name) delete args.name;
    await execute('structure.updateType', args);
  };

  const create = async () => {
    const section = { kind: shape, width: w, depth: shape === 'circle' ? w : d };
    if (kind === 'column') await execute('structure.createColumnType', { name, section });
    else if (kind === 'beam') await execute('structure.createBeamType', { name, section });
    else if (kind === 'strip') {
      await execute('structure.createStripType', {
        name, stemHeight: stem, footingWidth: fw, footingHeight: fh, bottom,
        layers: [
          { material: 'Protection board', thickness: 0.02 }, { material: 'XPS insulation', thickness: 0.1 },
          { material: 'Waterproofing', thickness: 0.005 }, { material: 'Reinforced concrete', thickness: core, core: true },
        ],
      });
    } else {
      await execute('structure.createPadType', { name, bottom, steps });
    }
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Structure types">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Structure types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <div className="row tabs-mini">
              {(['column', 'beam', 'strip', 'pad'] as const).map((k) => (
                <button type="button" key={k} className={`tree-item${kind === k ? ' on' : ''}`} onClick={() => switchKind(k)}>{{ column: 'Columns', beam: 'Beams', strip: 'Strip', pad: 'Pad' }[k]}</button>
              ))}
            </div>
            <ul className="type-list">
              {list.map((t) => (
                <li key={t.id} className={`tree-item${editing === t.id ? ' on' : ''}`} title="Click to change the type; its elements follow." onClick={() => pick(t)}>{t.name}<span className="muted small"> · {describeType(t)}</span>
                  <button type="button" className="icon mini" title="Delete the type (refused while elements use it)" onClick={(e) => { e.stopPropagation(); void deleteType(t.id).then((ok) => { if (ok && editing === t.id) fresh(); }); }}><Trash2 size={11} /></button>
                </li>
              ))}
            </ul>
          </div>
          <div className="type-edit">
            <div className="row">
              <p className="eyebrow grow">{edited ? <span data-no-i18n>{edited.name}</span> : `New ${kind} type`}</p>
              {edited && <button type="button" className="btn small" onClick={fresh}>New type</button>}
            </div>
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its size)" onChange={(e) => setName(e.target.value)} /></label>
            {(kind === 'column' || kind === 'beam') && <>
              <label className="field-line"><span className="eyebrow">Section</span>
                <select value={shape} onChange={(e) => setShape(e.target.value as 'rectangle' | 'circle')}><option value="rectangle">Rectangle</option><option value="circle">Circle</option></select>
              </label>
              <Len label={shape === 'circle' ? 'Diameter' : 'Width'} value={w} min={0.01} onChange={setW} />
              {shape === 'rectangle' && <Len label={kind === 'beam' ? 'Depth' : 'Length'} value={d} min={0.01} onChange={setD} />}
            </>}
            {edited && (kind === 'column' || kind === 'beam') && structureHooks.typeExtras.map((Extra, i) => <Extra key={i} type={edited} kind={kind} />)}
            {kind === 'strip' && <>
              <Len label="Core" value={core} min={0.01} onChange={setCore} />
              <Len label="Stem height" value={stem} min={0} onChange={setStem} />
              <Len label="Footing width" value={fw} min={0.01} onChange={setFw} />
              <Len label="Footing height" value={fh} min={0.01} onChange={setFh} />
            </>}
            {kind === 'pad' && <label className="field-line"><span className="eyebrow">Steps L × W × H</span>
              <Cell value={formatSteps(steps, units)} title="Each step length × width × height, bottom up; a semicolon between steps"
                onCommit={(t) => { const s = parseSteps(t, units); if (!s?.length) return false; setSteps(s); }} /></label>}
            {(kind === 'strip' || kind === 'pad') && <Len label="Bottom" value={bottom} onChange={setBottom} />}
            <div className="row"><span className="grow" />{edited
              ? <button type="button" className="btn primary" title="Its elements follow the type" onClick={() => void save()}>Save type</button>
              : <button type="button" className="btn primary" onClick={() => void create()}>Create type</button>}</div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
