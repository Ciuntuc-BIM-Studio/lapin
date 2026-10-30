/**
 * File → Export IFC… (docs/spec-ifc.md): the model as IFC, IFC4 (the default)
 * or IFC4X3; parts as elements of their own (the default) or one body per
 * element; at the end of a phase or whole. And how bodies are written: as
 * faceted B-reps (exact, a face entity per face: as before, the default), or
 * as face sets (the same faces over one point list, a much smaller file), and
 * simplified on request: fewer triangles where a body is meshed (a reference
 * read from another program's file), never further from it than the
 * tolerance (Simplifier in the engine).
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { query } from '../../engine/session';
import { exportIfc } from '../../engine/files';

export function ExportIfcDialog() {
  const [schema, setSchema] = useState('IFC4');
  const [detail, setDetail] = useState('group');
  const [phase, setPhase] = useState('');
  const [phases, setPhases] = useState<{ id: string; name: string }[]>([]);
  const [geometry, setGeometry] = useState('brep');
  const [simplify, setSimplify] = useState(false);
  const [tolerance, setTolerance] = useState('2');
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { void query<{ phases: { id: string; name: string }[] }>('phases.settings').then((r) => setPhases(r?.phases ?? [])); }, []);
  const mm = Number(tolerance.replace(',', '.'));
  const valid = !simplify || (Number.isFinite(mm) && mm > 0 && mm <= 100);
  const run = async () => {
    setBusy(true);
    await exportIfc({ schema, detail, geometry, ...(phase ? { phase } : {}), ...(simplify ? { tolerance: mm / 1000 } : {}) });
    setBusy(false);
    close();
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Export IFC" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Export IFC</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <dl className="fields">
            <dt>Schema</dt>
            <dd><select value={schema} onChange={(e) => setSchema(e.target.value)}>
              <option value="IFC4">IFC4: read everywhere</option>
              <option value="IFC4X3">IFC4X3: terrain and infrastructure as IFC 4.3 names them</option>
            </select></dd>
            <dt>Detail</dt>
            <dd><select value={detail} onChange={(e) => setDetail(e.target.value)}>
              <option value="group">parts as elements of their own</option>
              <option value="simple">one body per element</option>
            </select></dd>
            {phases.length > 0 && (
              <>
                <dt title="The elements that stand at the end of this phase">Phase</dt>
                <dd><select value={phase} onChange={(e) => setPhase(e.target.value)}><option value="">every phase</option>{phases.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></dd>
              </>
            )}
            <dt>Geometry</dt>
            <dd><select className="export-geometry" value={geometry} onChange={(e) => setGeometry(e.target.value)}>
              <option value="brep">faceted B-rep: exact, a face entity per face</option>
              <option value="faceset">face sets: the same faces, a much smaller file</option>
            </select></dd>
            <dt>Simplify</dt>
            <dd><label><input type="checkbox" className="export-simplify" style={{ width: 'auto', marginRight: 6 }} checked={simplify} onChange={(e) => setSimplify(e.target.checked)} /> fewer triangles where a body is meshed, never further from it than the tolerance</label></dd>
            {simplify && (
              <>
                <dt>Tolerance (mm)</dt>
                <dd><input className="export-tolerance" inputMode="decimal" value={tolerance} onChange={(e) => setTolerance(e.target.value)} /></dd>
              </>
            )}
          </dl>
          <p className="muted small">The model itself, for other BIM tools: every element with its class, type, properties and GlobalId. Simplifying changes only how finely a body is meshed, within the tolerance; nothing else.</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={busy || !valid} onClick={() => void run()}>{busy ? 'Exporting…' : 'Export'}</button>
        </footer>
      </div>
    </div>
  );
}
