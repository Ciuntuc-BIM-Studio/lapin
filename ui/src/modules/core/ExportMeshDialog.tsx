/**
 * File → Export 3D… (docs/plan-mesh-export.md): the model as a mesh for
 * rendering, visualisation and AR, not as a model (IFC is that). GLB (one
 * glTF file, the default), glTF, USDZ (OpenUSD packed, for Apple's AR Quick
 * Look and the USD tools) or USDA (OpenUSD text). The whole model or what the
 * 3D view shows, at the end of a phase, with or without edges, from the
 * project's base point, its internal origin or the survey point's coordinates.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { query, useSession } from '../../engine/session';
import { exportFile, projectTitle } from '../../engine/files';

const FORMATS: [string, string][] = [
  ['glb', 'GLB: glTF in one binary file (Blender, three.js, viewers)'],
  ['gltf', 'glTF: the same as text, its data inside'],
  ['usdz', 'USDZ: OpenUSD packed (AR Quick Look, Omniverse, USD tools)'],
  ['usda', 'USDA: OpenUSD as text'],
];
const ORIGINS: [string, string][] = [['base', 'the project base point'], ['internal', 'the internal origin'], ['survey', 'the survey point (real coordinates)']];

export function ExportMeshDialog() {
  const path = useSession((s) => s.doc?.path ?? null);
  const view3dId = useUi((s) => s.view3dId);
  const [format, setFormat] = useState('glb');
  const [what, setWhat] = useState<'model' | 'view'>('model');
  const [phase, setPhase] = useState('');
  const [phases, setPhases] = useState<{ id: string; name: string }[]>([]);
  const [edges, setEdges] = useState(false);
  const [origin, setOrigin] = useState('base');
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { void query<{ phases: { id: string; name: string }[] }>('phases.settings').then((r) => setPhases(r?.phases ?? [])); }, []);
  const run = async () => {
    setBusy(true);
    await exportFile('project.exportMesh', { format, edges, origin, ...(what === 'view' ? { viewId: view3dId ?? '3d' } : {}), ...(phase ? { phase } : {}) }, `${projectTitle(path)}.${format}`, format);
    setBusy(false);
    close();
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Export 3D" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Export 3D</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <dl className="fields">
            <dt>Format</dt>
            <dd><select value={format} onChange={(e) => setFormat(e.target.value)}>{FORMATS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></dd>
            <dt>What</dt>
            <dd><select value={what} onChange={(e) => setWhat(e.target.value as 'model' | 'view')}>
              <option value="model">the whole model</option>
              <option value="view">what the 3D view shows (its hidden elements and categories left out)</option>
            </select></dd>
            {phases.length > 0 && (
              <>
                <dt title="The elements that stand at the end of this phase">Phase</dt>
                <dd><select value={phase} onChange={(e) => setPhase(e.target.value)}><option value="">every phase</option>{phases.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></dd>
              </>
            )}
            <dt>Origin</dt>
            <dd><select value={origin} onChange={(e) => setOrigin(e.target.value)}>{ORIGINS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></dd>
            <dt title="The elements' edges as lines, besides their faces; most programs do not use them">Edges</dt>
            <dd><input type="checkbox" checked={edges} onChange={(e) => setEdges(e.target.checked)} /></dd>
          </dl>
          <p className="muted small">Surfaces for rendering, visualisation and AR: each element a node with its GlobalId, IFC class and type, a mesh per material. To exchange the model itself, use Export IFC.</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={busy} onClick={() => void run()}>{busy ? 'Exporting…' : 'Export'}</button>
        </footer>
      </div>
    </div>
  );
}
