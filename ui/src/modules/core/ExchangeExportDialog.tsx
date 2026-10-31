/**
 * File → Export IFC or Fragments… (docs/fragments.md, Exchange): the model itself, IFC or That Open's Fragments,
 * chosen as an import is (SurveyChoice: the project's elements by discipline and kind, its storeys), at the end of
 * a phase, everything or only what the 3D view shows. IFC in IFC4 or IFC4X3, its parts grouped under their element
 * or one body each, its bodies as faceted B-reps (exact) or face sets (a much smaller file), simplified on request
 * within a tolerance where a body is meshed (Simplifier in the engine); Fragments from the project base point, the
 * internal origin or the survey point.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { engine, query, useSession } from '../../engine/session';
import { exportFile, projectTitle } from '../../engine/files';
import { choiceOf, type ExchangeFormat, type Survey } from '../../engine/ifcImport';
import { useLooks } from '../../platform/looks';
import { nothingChosen, SurveyChoice } from './SurveyChoice';

const ORIGINS: [string, string][] = [['base', 'the project base point'], ['internal', 'the internal origin'], ['survey', 'the survey point (real coordinates)']];

export function ExchangeExportDialog() {
  const path = useSession((s) => s.doc?.path ?? null);
  const view3dId = useUi((s) => s.view3dId);
  const [format, setFormat] = useState<ExchangeFormat>(useUi.getState().dialog === 'exportFrag' ? 'frag' : 'ifc');
  const [survey, setSurvey] = useState<Survey | null>(null);
  const [offKinds, setOffKinds] = useState<Set<string>>(new Set());
  const [offStoreys, setOffStoreys] = useState<Set<string>>(new Set());
  const [phases, setPhases] = useState<{ id: string; name: string }[]>([]);
  const [phase, setPhase] = useState('');
  // What the 3D view does not draw (the storeys it does not show, what is hidden for now), by the looks.
  const [hidden] = useState(() => Object.entries(useLooks.getState().looks).filter(([, l]) => l.hidden).map(([id]) => id));
  const [onlyShown, setOnlyShown] = useState(false);
  const [schema, setSchema] = useState('IFC4');
  const [detail, setDetail] = useState('group');
  const [geometry, setGeometry] = useState('brep');
  const [simplify, setSimplify] = useState(false);
  const [tolerance, setTolerance] = useState('2');
  const [origin, setOrigin] = useState('base');
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    void engine.call<Survey>('project.exportSurvey', {}).then(setSurvey).catch(() => setSurvey(null));
    void query<{ phases: { id: string; name: string }[] }>('phases.settings').then((r) => setPhases(r?.phases ?? []));
  }, []);
  const choice = useMemo(() => (survey ? choiceOf(survey, offKinds, offStoreys) : { kinds: null, storeys: null }), [survey, offKinds, offStoreys]);
  const mm = Number(tolerance.replace(',', '.'));
  const valid = format !== 'ifc' || !simplify || (Number.isFinite(mm) && mm > 0 && mm <= 100);
  const run = async () => {
    setBusy(true);
    const common = {
      ...(choice.kinds ? { kinds: choice.kinds } : {}), ...(choice.storeys ? { storeys: choice.storeys } : {}),
      ...(phase ? { phase } : {}), ...(onlyShown ? { hidden } : {}),
    };
    if (format === 'ifc') await exportFile('project.exportIfc', { ...common, schema, detail, geometry, ...(simplify ? { tolerance: mm / 1000 } : {}) }, `${projectTitle(path)}.ifc`, 'ifc');
    else await exportFile('project.exportMesh', { ...common, format: 'frag', origin, ...(onlyShown ? { viewId: view3dId ?? '3d' } : {}) }, `${projectTitle(path)}.frag`, 'frag');
    setBusy(false);
    close();
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog import-ifc" role="dialog" aria-label="Export IFC or Fragments" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Export IFC or Fragments</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body exchange-options">
          <div className="row">
            <label className="check-line"><input type="radio" name="exchange-format" checked={format === 'ifc'} onChange={() => setFormat('ifc')} /> <strong>IFC</strong> <span className="muted small">the model for every BIM program</span></label>
            <label className="check-line"><input type="radio" name="exchange-format" checked={format === 'frag'} onChange={() => setFormat('frag')} /> <strong>Fragments</strong> <span className="muted small">That Open's format, for BIM web viewers</span></label>
          </div>
        </div>
        <div className="dialog-body split">
          {survey ? <SurveyChoice survey={survey} whatLabel="What to export" offKinds={offKinds} setOffKinds={setOffKinds} offStoreys={offStoreys} setOffStoreys={setOffStoreys} />
            : <p className="muted">Reading the project…</p>}
        </div>
        <div className="dialog-body exchange-options">
          <dl className="fields">
            {phases.length > 0 && (
              <>
                <dt title="The elements that stand at the end of this phase">Phase</dt>
                <dd><select value={phase} onChange={(e) => setPhase(e.target.value)}><option value="">every phase</option>{phases.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></dd>
              </>
            )}
            {hidden.length > 0 && (
              <>
                <dt title="What the 3D view does not draw now (the storeys it does not show, what is hidden for now) left out">3D view</dt>
                <dd><label><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={onlyShown} onChange={(e) => setOnlyShown(e.target.checked)} /> only what it shows</label></dd>
              </>
            )}
            {format === 'ifc' ? (
              <>
                <dt>Schema</dt>
                <dd><select value={schema} onChange={(e) => setSchema(e.target.value)}>
                  <option value="IFC4">IFC4: read everywhere</option>
                  <option value="IFC4X3">IFC4X3: terrain and infrastructure as IFC 4.3 names them</option>
                </select></dd>
                <dt title="A layered wall's layers, a window's frame and glass: parts under their element, or one body each">Parts</dt>
                <dd><select value={detail} onChange={(e) => setDetail(e.target.value)}><option value="group">grouped under their element</option><option value="simple">one body each</option></select></dd>
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
              </>
            ) : (
              <>
                <dt>Origin</dt>
                <dd><select value={origin} onChange={(e) => setOrigin(e.target.value)}>{ORIGINS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></dd>
              </>
            )}
          </dl>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={busy || !valid || !survey || nothingChosen(survey, offKinds, offStoreys)} onClick={() => void run()}>{busy ? 'Exporting…' : 'Export'}</button>
        </footer>
      </div>
    </div>
  );
}
