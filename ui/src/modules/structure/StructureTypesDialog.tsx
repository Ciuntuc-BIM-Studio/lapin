/**
 * Structure types: columns and beams (their section), strip foundations
 * (the stem's layers, the footing's size, its level) and pad footings (the
 * steps, bottom up). A new type is made here; the inspector gives an element
 * another one.
 */

import { useState } from 'react';
import { Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import { describeType } from './StructureSection';

const mmToM = (t: string) => Number(t.replace(',', '.')) / 1000;

export function StructureTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = { nodes, relations };
  const [kind, setKind] = useState<'column' | 'beam' | 'strip' | 'pad'>('column');
  const [name, setName] = useState('');
  const [shape, setShape] = useState<'rectangle' | 'circle'>('rectangle');
  const [w, setW] = useState('300');
  const [d, setD] = useState('300');
  const [stem, setStem] = useState('1400');
  const [fw, setFw] = useState('600');
  const [fh, setFh] = useState('400');
  const [bottom, setBottom] = useState('-1800');
  const [core, setCore] = useState('300');
  const [steps, setSteps] = useState('1800 × 1800 × 500, 1000 × 1000 × 400');
  const list = kind === 'column' ? ofClass(model, 'IfcColumnType') : kind === 'beam' ? ofClass(model, 'IfcBeamType')
    : ofClass(model, 'IfcFootingType').filter((t) => t.props.kind === kind);

  const create = async () => {
    const section = { kind: shape, width: mmToM(w), depth: shape === 'circle' ? mmToM(w) : mmToM(d) };
    if (kind === 'column') await execute('structure.createColumnType', { name, section });
    else if (kind === 'beam') await execute('structure.createBeamType', { name, section });
    else if (kind === 'strip') {
      await execute('structure.createStripType', {
        name, stemHeight: mmToM(stem), footingWidth: mmToM(fw), footingHeight: mmToM(fh), bottom: mmToM(bottom),
        layers: [
          { material: 'Protection board', thickness: 0.02 }, { material: 'XPS insulation', thickness: 0.1 },
          { material: 'Waterproofing', thickness: 0.005 }, { material: 'Reinforced concrete', thickness: mmToM(core), core: true },
        ],
      });
    } else {
      const parsed = steps.split(',').map((s) => s.match(/[\d.]+/g)?.map((n) => Number(n) / 1000) ?? []).filter((n) => n.length === 3);
      await execute('structure.createPadType', { name, bottom: mmToM(bottom), steps: parsed.map(([length, width, height]) => ({ length, width, height })) });
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
                <button type="button" key={k} className={`tree-item${kind === k ? ' on' : ''}`} onClick={() => setKind(k)}>{{ column: 'Columns', beam: 'Beams', strip: 'Strip', pad: 'Pad' }[k]}</button>
              ))}
            </div>
            <ul className="type-list">
              {list.map((t) => (
                <li key={t.id} className="tree-item">{t.name}<span className="muted small"> · {describeType(t)}</span>
                  <button type="button" className="icon mini" title="Delete the type (refused while elements use it)" onClick={() => void execute('element.delete', { id: t.id })}><Trash2 size={11} /></button>
                </li>
              ))}
            </ul>
          </div>
          <div className="type-edit">
            <p className="eyebrow">New {kind} type</p>
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its size)" onChange={(e) => setName(e.target.value)} /></label>
            {(kind === 'column' || kind === 'beam') && <>
              <label className="field-line"><span className="eyebrow">Section</span>
                <select value={shape} onChange={(e) => setShape(e.target.value as 'rectangle' | 'circle')}><option value="rectangle">Rectangle</option><option value="circle">Circle</option></select>
              </label>
              <label className="field-line"><span className="eyebrow">{shape === 'circle' ? 'Diameter' : 'Width'} mm</span><input value={w} onChange={(e) => setW(e.target.value)} /></label>
              {shape === 'rectangle' && <label className="field-line"><span className="eyebrow">{kind === 'beam' ? 'Depth' : 'Length'} mm</span><input value={d} onChange={(e) => setD(e.target.value)} /></label>}
            </>}
            {kind === 'strip' && <>
              <label className="field-line"><span className="eyebrow">Core mm</span><input value={core} onChange={(e) => setCore(e.target.value)} /></label>
              <label className="field-line"><span className="eyebrow">Stem height</span><input value={stem} onChange={(e) => setStem(e.target.value)} /></label>
              <label className="field-line"><span className="eyebrow">Footing width</span><input value={fw} onChange={(e) => setFw(e.target.value)} /></label>
              <label className="field-line"><span className="eyebrow">Footing height</span><input value={fh} onChange={(e) => setFh(e.target.value)} /></label>
            </>}
            {kind === 'pad' && <label className="field-line"><span className="eyebrow">Steps L × W × H</span><input value={steps} onChange={(e) => setSteps(e.target.value)} /></label>}
            {(kind === 'strip' || kind === 'pad') && <label className="field-line"><span className="eyebrow">Bottom mm</span><input value={bottom} onChange={(e) => setBottom(e.target.value)} /></label>}
            <div className="row"><span className="grow" /><button type="button" className="btn primary" onClick={() => void create()}>Create type</button></div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
