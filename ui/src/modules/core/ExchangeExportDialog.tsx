/**
 * File → Export IFC, Fragments or BOS… (docs/fragments.md, Exchange; docs/bos.md): the model itself, IFC, That Open's Fragments or BIM Open Schema,
 * chosen as an import is (SurveyChoice: the project's elements by discipline and kind, its storeys), at the end of
 * a phase, everything or only what the 3D view shows. IFC in IFC4 or IFC4X3, its parts grouped under their element
 * or one body each, its bodies as faceted B-reps (exact) or face sets (a much smaller file), simplified on request
 * within a tolerance where a body is meshed (Simplifier in the engine); Fragments from the project base point, the
 * internal origin or the survey point. On the desktop, an IFC can take Bonsai's drawing styles beside it (a stylesheet and its hatch patterns, decision 495). SDNF 3.0 (docs/sdnf.md, decision 463) where the product has the steel module: the columns, beams
 * and steel members of the storeys and kinds chosen, their connections' plates, the grids, for steel detailing programs.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { engine, useSession } from '../../engine/session';
import { LONG_TIMEOUT_MS } from '../../engine/client';
import { exportFile, isDesktop, projectTitle, saveFile } from '../../engine/files';
import { hasModule } from '../../platform/brand';
import { choiceOf, type ExchangeFormat, type Survey } from '../../engine/ifcImport';
import { useLooks } from '../../platform/looks';
import { nothingChosen, SurveyChoice } from './SurveyChoice';
import { ORIGINS, usePhaseList } from './format';


export function ExchangeExportDialog() {
  const path = useSession((s) => s.doc?.path ?? null);
  const view3dId = useUi((s) => s.view3dId);
  const [format, setFormat] = useState<ExchangeFormat | 'sdnf'>('ifc');
  const [survey, setSurvey] = useState<Survey | null>(null);
  const [offKinds, setOffKinds] = useState<Set<string>>(new Set());
  const [offStoreys, setOffStoreys] = useState<Set<string>>(new Set());
  const phases = usePhaseList();
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
  // The openings also as solid voids for clash tools (decision 289): off, the export is as it always was.
  const [voids, setVoids] = useState(false);
  // Bonsai's drawing styles beside the IFC (docs/spec-ifc.md, decision 495): a stylesheet and its hatches, on the desktop.
  const [bonsai, setBonsai] = useState(false);
  // The analytical model and the loads as an IFC Structural Analysis View (docs/plan-fem.md §8.7).
  const [structural, setStructural] = useState(false);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    void engine.call<Survey>('project.exportSurvey', {}).then((s) => {
      setSurvey(s);
      // What is left out unless chosen (entourage) starts unticked.
      setOffKinds(new Set(s.groups.flatMap((g) => g.kinds.filter((k) => k.off).map((k) => k.key))));
    }).catch(() => setSurvey(null));
  }, []);
  const choice = useMemo(() => (survey ? choiceOf(survey, offKinds, offStoreys) : { kinds: null, storeys: null }), [survey, offKinds, offStoreys]);
  const mm = Number(tolerance.replace(',', '.'));
  const valid = format !== 'ifc' || !simplify || (Number.isFinite(mm) && mm > 0 && mm <= 100);
  const run = async () => {
    setBusy(true);
    const common = {
      ...(choice.kinds ? { kinds: choice.kinds } : {}), ...(choice.storeys ? { storeys: choice.storeys } : {}),
      ...(phase ? { phase } : {}), ...(onlyShown ? { hidden } : {}), ...(voids ? { voids: true } : {}),
    };
    // The kinds ticked decide; all of them ticked takes the entourage too (the engine leaves it out when not told).
    if (format === 'sdnf') {
      // The steel module's query (docs/sdnf.md): an ASCII file, saved where the user says.
      try {
        const r = await engine.call<{ data: string }>('doc.query', { name: 'steel.sdnfExport', args: { ...(choice.kinds ? { kinds: choice.kinds } : {}), ...(choice.storeys ? { storeys: choice.storeys } : {}), ...(onlyShown ? { hidden } : {}) } }, { timeoutMs: LONG_TIMEOUT_MS });
        const saved = await saveFile(Uint8Array.from(atob(r.data), (c) => c.charCodeAt(0)), `${projectTitle(path)}.sdnf`, 'text/plain');
        if (saved) notify(`Exported ${saved}.`);
      } catch (e) {
        notify(e instanceof Error ? e.message : String(e));
      }
    } else if (format === 'ifc') await exportFile('project.exportIfc', { ...common, entourage: true, schema, detail, geometry, ...(simplify ? { tolerance: mm / 1000 } : {}), ...(bonsai && isDesktop() ? { bonsaiStyles: true } : {}), ...(structural ? { structural: true } : {}) }, `${projectTitle(path)}.ifc`, 'ifc');
    else await exportFile('project.exportMesh', { ...common, format, origin, ...(onlyShown ? { viewId: view3dId ?? '3d' } : {}) }, `${projectTitle(path)}.${format}`, format);
    setBusy(false);
    close();
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog import-ifc" role="dialog" aria-label="Export IFC, Fragments or BOS" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Export IFC, Fragments or BOS</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body exchange-options">
          <div className="row">
            <label className="check-line"><input type="radio" name="exchange-format" checked={format === 'ifc'} onChange={() => setFormat('ifc')} /> <strong>IFC</strong> <span className="muted small">the model for every BIM program</span></label>
            <label className="check-line"><input type="radio" name="exchange-format" checked={format === 'frag'} onChange={() => setFormat('frag')} /> <strong>Fragments</strong> <span className="muted small">That Open's format, for BIM web viewers</span></label>
            <label className="check-line"><input type="radio" name="exchange-format" checked={format === 'bos'} onChange={() => setFormat('bos')} /> <strong>BIM Open Schema</strong> <span className="muted small">Parquet tables, for data analysis and viewers</span></label>
            {hasModule('steel') && <label className="check-line"><input type="radio" name="exchange-format" data-testid="export-sdnf" checked={format === 'sdnf'} onChange={() => setFormat('sdnf')} /> <strong>SDNF</strong> <span className="muted small">columns, beams, steel members and plates, for steel detailing programs</span></label>}
          </div>
        </div>
        <div className="dialog-body split">
          {survey ? <SurveyChoice survey={survey} whatLabel="What to export" offKinds={offKinds} setOffKinds={setOffKinds} offStoreys={offStoreys} setOffStoreys={setOffStoreys} />
            : <p className="muted">Reading the project…</p>}
        </div>
        <div className="dialog-body exchange-options">
          <dl className="fields">
            {phases.length > 0 && format !== 'sdnf' && (
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
            {format !== 'sdnf' && (
              <>
                <dt title="Each opening in a wall, floor or roof also as a solid of its own (IfcBuildingElementProxy PROVISIONFORVOID with Pset_ProvisionForVoid), for clash tools; the kinds and offset of Manage → Openings vs MEP">Opening voids</dt>
                <dd><label><input type="checkbox" data-testid="export-voids" style={{ width: 'auto', marginRight: 6 }} checked={voids} onChange={(e) => setVoids(e.target.checked)} /> as solids, for clash tools</label></dd>
              </>
            )}
            {format === 'sdnf' ? null : format === 'ifc' ? (
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
                {isDesktop() && (
                  <>
                    <dt title="Bonsai (BlenderBIM) draws its drawings with a CSS stylesheet: the project's materials (cut hatches, fills), pens and text written beside the IFC as name.css and name-patterns.svg, and named in the file so Bonsai's new drawings take them">Bonsai drawing styles</dt>
                    <dd><label><input type="checkbox" data-testid="export-bonsai" style={{ width: 'auto', marginRight: 6 }} checked={bonsai} onChange={(e) => setBonsai(e.target.checked)} /> a stylesheet and its hatches beside the file</label></dd>
                  </>
                )}
                <dt title="The analytical model (bearing walls, floors, columns and beams as structural members) with the load cases, the loads and the combinations, as an IFC Structural Analysis View">Structural analysis</dt>
                <dd><label><input type="checkbox" data-testid="export-structural" style={{ width: 'auto', marginRight: 6 }} checked={structural} onChange={(e) => setStructural(e.target.checked)} /> Structural analysis view (analytical model and loads)</label></dd>
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
