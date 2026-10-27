/**
 * Scaffolds and formwork in the inspector. A scaffold: its type, the walls it
 * follows or the line it was drawn on (which side), its work top, its feet.
 * A formwork: what it forms (the host and which faces), its type, a floor's
 * primary beams' direction. Both open their configurator.
 */

import { useMemo } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { ofClass, typeOf } from '../../engine/model';
import { useUi } from '../../app/store';
import { Mm } from './fields';
import { setSiteWorks } from './prefs';

function TypeField({ node, cls, kind, command }: { node: NodeDto; cls: string; kind?: string; command: string }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, cls).filter((t) => kind == null || (t.props.kind ?? 'slab') === kind);
  const current = typeOf(model, node.id);
  const open = () => {
    if (cls === 'LapinScaffoldType') { setSiteWorks({ scaffoldTypeId: current?.id ?? null }); useUi.setState({ dialog: 'scaffoldTypes' }); }
    else { setSiteWorks({ formworkTypeId: current?.id ?? null, formworkKind: (kind as 'slab' | 'beam') ?? 'slab' }); useUi.setState({ dialog: 'formworkTypes' }); }
  };
  return <>
    <dt>Type</dt>
    <dd>
      <select value={current?.id ?? ''} onChange={(e) => void execute(command, { id: node.id, typeId: e.target.value })}>
        {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
      </select>
    </dd>
    <dt /><dd><button type="button" className="btn compact" onClick={open}>Configurator…</button></dd>
  </>;
}

export function ScaffoldSection({ node }: { node: NodeDto }) {
  const set = (args: Record<string, unknown>) => void execute('sitework.updateScaffold', { id: node.id, ...args });
  const walls = Array.isArray(node.props.walls) ? node.props.walls.length : 0;
  const fixedTop = typeof node.props.top === 'number';
  return (
    <dl className="fields">
      <TypeField node={node} cls="LapinScaffoldType" command="sitework.updateScaffold" />
      {walls > 0
        ? <><dt>Follows</dt><dd>{walls} wall(s), round their outer faces <button type="button" className="link small" title="Keep the line as it is now, no longer following the walls" onClick={() => set({ detach: true })}>detach</button></dd></>
        : <>
          <dt>Scaffold on the</dt>
          <dd><select value={(node.props.side as string | undefined) ?? 'left'} onChange={(e) => set({ side: e.target.value })}><option value="left">left</option><option value="right">right</option></select> <span className="muted small">of the way it was drawn</span></dd>
          <dt>Closed</dt><dd><input type="checkbox" checked={node.props.closed === true} onChange={(e) => set({ closed: e.target.checked })} /></dd>
        </>}
      <dt>Work top</dt>
      <dd><Mm value={fixedTop ? (node.props.top as number) : null} blank={walls > 0 ? 'the walls\' top' : 'base + height'} onCommit={(v) => set({ top: v })} /></dd>
      {!fixedTop && walls === 0 && <><dt>Height</dt><dd><Mm value={typeof node.props.height === 'number' ? node.props.height : 6} min={1000} onCommit={(v) => v && set({ height: v })} /></dd></>}
      <dt>Base offset</dt><dd><Mm value={typeof node.props.baseOffset === 'number' ? node.props.baseOffset : 0} onCommit={(v) => set({ baseOffset: v ?? 0 })} /></dd>
      <dt>Feet</dt>
      <dd><select value={node.props.onStorey === true ? 'storey' : 'ground'} onChange={(e) => set({ onStorey: e.target.value === 'storey' })}>
        <option value="ground">on the terrain (else the storey)</option><option value="storey">on the storey</option></select></dd>
    </dl>
  );
}

const SLAB_FACES: [string, string][] = [['Soffit', 'Soffit'], ['Edges', 'Free edges']];
const BEAM_FACES: [string, string][] = [['Bottom', 'Bottom'], ['Side.Left', 'Left side'], ['Side.Right', 'Right side'], ['Edge.BottomLeft', 'Chamfer, bottom left'], ['Edge.BottomRight', 'Chamfer, bottom right']];

export function FormworkSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const beam = node.props.kind === 'beam';
  const host = typeof node.props.host === 'string' ? nodes.get(node.props.host) : undefined;
  const all = beam ? BEAM_FACES.map(([k]) => k) : ['Soffit', 'Edges'];
  const faces = Array.isArray(node.props.faces) && node.props.faces.length > 0 ? (node.props.faces as string[]) : all;
  const set = (args: Record<string, unknown>) => void execute('sitework.updateFormwork', { id: node.id, ...args });
  const toggle = (k: string, on: boolean) => {
    const next = on ? [...new Set([...faces, k])] : faces.filter((f) => f !== k);
    if (next.length > 0) set({ faces: next });
  };
  const single = faces.filter((f) => f.startsWith('Edge[')).length;
  return (
    <dl className="fields">
      <dt>Forms</dt><dd>{host ? host.name : <span className="error-text">its host is gone</span>}</dd>
      <TypeField node={node} cls="LapinFormworkType" kind={beam ? 'beam' : 'slab'} command="sitework.updateFormwork" />
      <dt>Faces</dt>
      <dd>
        {(beam ? BEAM_FACES : SLAB_FACES).map(([k, label]) => (
          <label key={k} className="check-line"><input type="checkbox" checked={faces.includes(k)} onChange={(e) => toggle(k, e.target.checked)} /> {label}</label>
        ))}
        {single > 0 && <span className="muted small">and {single} edge(s) picked</span>}
      </dd>
      {!beam && <>
        <dt>Primary beams</dt>
        <dd>
          <input className="num" type="number" step={15} value={typeof node.props.direction === 'number' ? node.props.direction : ''} placeholder="along the longest side"
            onChange={(e) => set({ direction: e.target.value === '' ? null : Number(e.target.value) })} /> <span className="muted">°</span>
        </dd>
      </>}
    </dl>
  );
}
