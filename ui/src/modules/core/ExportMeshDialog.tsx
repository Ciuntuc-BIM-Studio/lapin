/**
 * File → Export 3D… (docs/plan-mesh-export.md): the model as a mesh for
 * rendering, visualisation and AR, not as a model (IFC is that). GLB (one
 * glTF file, the default), glTF, USDZ (OpenUSD packed, for Apple's AR Quick
 * Look and the USD tools), USDA (OpenUSD text) or DAE (COLLADA 1.4.1, which
 * SketchUp imports) or OBJ with its MTL in a zip (every renderer; report #76). Every
 * material goes out once, named as in the project, with its look. Everything, or the model as the 3D view in front shows
 * it (the default when the view leaves anything out): its hidden elements and
 * categories (the engine reads them off the view), and what only the
 * interface knows: the elements it does not draw (the storeys it does not
 * show, by storeys3d.ts, and what is hidden for now, all through the looks)
 * and the planes it cuts by (the cut above the storeys shown, a section, the
 * section box), cut in the export too and closed by caps in the materials.
 * Fragments (.frag) is not here: it exchanges the model, with IFC (File → Export IFC or Fragments…).
 * The layers the view shows (without finishes, the cores only) the engine reads
 * off the view too, so the export has the same layers as the screen.
 * At the end of a phase, with or without edges, from the project's base
 * point, its internal origin or the survey point's coordinates.
 */

import { useState } from 'react';
import { isCloudLink } from '../../viewport/pointClouds';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { exportFile, projectTitle } from '../../engine/files';
import { useLooks } from '../../platform/looks';
import { useClip3d } from '../../viewport/section3d';
import { view3dProps } from '../../viewport/storeys3d';
import { layers3dOf } from '../../viewport/layers3d';
import { ORIGINS, usePhaseList } from './format';

const FORMATS: [string, string][] = [
  ['glb', 'GLB: glTF in one binary file (Blender, three.js, viewers)'],
  ['gltf', 'glTF: the same as text, its data inside'],
  ['usdz', 'USDZ: OpenUSD packed (AR Quick Look, Omniverse, USD tools)'],
  ['usda', 'USDA: OpenUSD as text'],
  ['dae', 'DAE: COLLADA 1.4.1 (SketchUp, Rhino, 3ds Max, Blender)'],
  ['obj', 'OBJ + MTL in a .zip (Twinmotion, Lumion, 3ds Max, any renderer)'],
];
/** The file a format goes out as: OBJ zipped with its MTL and images. */
const extOf = (format: string) => (format === 'obj' ? 'zip' : format);

export function ExportMeshDialog() {
  const path = useSession((s) => s.doc?.path ?? null);
  const view3dId = useUi((s) => s.view3dId);
  // What the 3D view leaves out: what it does not draw (by the looks: the storeys not shown, what is hidden for now),
  // what its own settings hide, and the planes it cuts by.
  const [hidden] = useState(() => Object.entries(useLooks.getState().looks).filter(([, l]) => l.hidden).map(([id]) => id));
  const [planes] = useState(() => useClip3d.getState().planes);
  const [hidesAny] = useState(() => {
    const own = view3dProps(useSession.getState().nodes, view3dId)?.hiddenElements;
    const nodes = useSession.getState().nodes;
    return hidden.length > 0 || planes.length > 0 || (Array.isArray(own) && own.length > 0) || layers3dOf(nodes, view3dId).mode !== 'all';
  });
  const [format, setFormat] = useState('glb');
  const [what, setWhat] = useState<'model' | 'view'>(hidesAny ? 'view' : 'model');
  const [cuts, setCuts] = useState(true);
  const [phase, setPhase] = useState('');
  const phases = usePhaseList();
  const [edges, setEdges] = useState(false);
  const [origin, setOrigin] = useState('base');
  const [voids, setVoids] = useState(false);
  // The point clouds linked go out only when ticked, and only in GLB and glTF (decision 396).
  const hasClouds = useSession((s) => [...s.nodes.values()].some(isCloudLink));
  const [clouds, setClouds] = useState(false);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  const run = async () => {
    setBusy(true);
    const view = what === 'view' ? { viewId: view3dId ?? '3d', hidden, ...(cuts && planes.length ? { planes } : {}) } : {};
    await exportFile('project.exportMesh', { format, edges, origin, ...view, ...(phase ? { phase } : {}), ...(voids ? { voids: true } : {}), ...(clouds && hasClouds ? { pointClouds: true } : {}) }, `${projectTitle(path)}.${extOf(format)}`, extOf(format));
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
            <dd><select className="export-what" value={what} onChange={(e) => setWhat(e.target.value as 'model' | 'view')}
              title="As the view shows it: its storeys shown, its hidden elements and categories left out, the layers it shows, cut as it is cut">
              <option value="model">everything</option>
              <option value="view">as the view shows it</option>
            </select></dd>
            {what === 'view' && planes.length > 0 && (
              <>
                <dt title="The cut above the storeys shown, a section shown in 3D, the section box: the model cut there and closed in its materials, as on the screen">Cut</dt>
                <dd><label><input type="checkbox" className="export-cuts" style={{ width: 'auto', marginRight: 6 }} checked={cuts} onChange={(e) => setCuts(e.target.checked)} /> cut as the view is cut, closed in the materials</label></dd>
              </>
            )}
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
            <dt title="Each opening in a wall, floor or roof also as a solid of its own, in a group named Opening voids, for clash tools; the kinds and offset of Manage → Openings vs MEP">Opening voids</dt>
            <dd><input type="checkbox" data-testid="export-mesh-voids" checked={voids} onChange={(e) => setVoids(e.target.checked)} /></dd>
            {hasClouds && <>
              <dt title="A sample of each point cloud shown (at most 5 million points in all), as points with their colours; GLB and glTF only">Point clouds</dt>
              <dd><label><input type="checkbox" data-testid="export-mesh-clouds" style={{ width: 'auto', marginRight: 6 }} checked={clouds} disabled={format !== 'glb' && format !== 'gltf'} onChange={(e) => setClouds(e.target.checked)} />{format === 'glb' || format === 'gltf' ? 'a sample, as points' : 'GLB and glTF only'}</label></dd>
            </>}
          </dl>
          <p className="muted small">Surfaces for rendering, visualisation and AR: each element a node with its GlobalId, IFC class and type, a mesh per material. To exchange the model itself, use Export IFC.</p>
          <p className="muted small">Each material goes out once under its name, with its colour, texture, roughness, metalness and glass, so a renderer such as Twinmotion can swap it by name (Manage → Materials).</p>
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
