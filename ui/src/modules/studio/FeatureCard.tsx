/** A Studio solid in the inspector. */

import { useEffect, useState } from 'react';
import { PenTool } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { relatingOf } from '../../engine/model';
import { useUi } from '../../app/store';
import { editSketch, type Feature } from './sketch';

interface Offered { class: string; label: string; predefined: string[] }
let offered: Promise<Offered[] | null> | null = null;

/** What the card calls each operation. */
const OPS: Record<string, string> = { ring: 'Ring', box: 'Box', wall: 'Wall strip', boxSlab: 'Box bottom or top' };

/** The selected Studio solids (a change goes to all of them). */
const solidsSelected = (node: NodeDto): string[] => {
  const nodes = useSession.getState().nodes;
  const sel = useUi.getState().selected.filter((id) => nodes.get(id)?.props.operation);
  return sel.includes(node.id) ? sel : [node.id];
};

/** A Studio solid in the inspector: its operation, sketch, net face, guide; Edit sketch. */
export function FeatureCard({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const [f, setF] = useState<Feature | null>(null);
  const [classes, setClasses] = useState<Offered[]>([]);
  useEffect(() => { void query<Feature>('studio.feature', { id: node.id }).then(setF); }, [node.id, nodes]);
  useEffect(() => { offered ??= query<Offered[]>('studio.classes'); void offered.then((c) => setClasses(c ?? [])); }, []);
  if (!f) return null;
  const op = OPS[f.operation] ?? f.operation[0].toUpperCase() + f.operation.slice(1);
  const thick = ['ring', 'box', 'wall', 'boxSlab'].includes(f.operation);
  const box = f.operation === 'boxSlab' && typeof node.props.box === 'string' ? nodes.get(node.props.box) : undefined;
  const cls = typeof node.props.exportAs === 'string' ? node.props.exportAs : node.type;
  const pre = typeof node.props.predefinedType === 'string' ? node.props.predefinedType : '';
  const preList = classes.find((c) => c.class === cls)?.predefined ?? [];
  const type = relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType');
  const types = [...nodes.values()].filter((n) => n.type === 'LapinSolidType');
  return (
    <dl className="fields">
      <dt>Studio</dt><dd>{op}</dd>
      <dt title="The IFC class it is written as: quantities, property sets and its cut priority follow it">IFC class</dt>
      <dd><select className="cell" value={cls} onChange={(e) => void execute('studio.setClass', { ids: solidsSelected(node), class: e.target.value })}>
        {!classes.some((c) => c.class === cls) && <option value={cls}>{cls}</option>}
        {classes.map((c) => <option key={c.class} value={c.class}>{c.label}</option>)}
      </select></dd>
      <dt>Predefined type</dt>
      <dd><select className="cell" value={pre} onChange={(e) => {
        let v: string | null = e.target.value || null;
        if (v === '__own') { v = window.prompt('The predefined type (capitals, e.g. SIDEWALK)')?.trim().toUpperCase() || null; if (!v) return; }
        void execute('studio.setClass', { ids: solidsSelected(node), predefinedType: v });
      }}>
        <option value="">—</option>
        {pre && !preList.includes(pre) && <option value={pre}>{pre}</option>}
        {preList.map((p) => <option key={p} value={p}>{p.toLowerCase().replace(/_/g, ' ')}</option>)}
        <option value="__own">other…</option>
      </select></dd>
      <dt>Type</dt>
      <dd><select className="cell" value={type?.id ?? ''} onChange={(e) => {
        const v = e.target.value;
        if (v === '__new') { const name = window.prompt('The new type\'s name'); if (name?.trim()) void execute('studio.setType', { ids: solidsSelected(node), name: name.trim() }); return; }
        void execute('studio.setType', v ? { ids: solidsSelected(node), typeId: v } : { ids: solidsSelected(node) });
      }}>
        <option value="">none</option>
        {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        <option value="__new">new type…</option>
      </select></dd>
      {box && <><dt>Part of</dt><dd><button type="button" className="link" onClick={() => useUi.setState({ selection: box.id, selected: [box.id] })}>{box.name}</button></dd></>}
      {thick ? <><dt>Sketch</dt><dd>{f.view ?? '—'}</dd></> : <><dt>Sketch</dt><dd>{f.view ?? '—'} · outline, {f.holes} hole(s)</dd></>}
      {f.area != null && <><dt>Face</dt><dd>{f.area.toFixed(2)} m² net</dd></>}
      {f.guide != null && <><dt>{f.operation === 'sweep' ? 'Path' : 'Axis'}</dt><dd className={f.guide === 'missing' ? 'error-text' : ''}>{f.guide === 'missing' ? 'missing: redraw it to rebuild' : f.guide}</dd></>}
      <dt />
      <dd><button type="button" onClick={() => void editSketch(node.id)}><PenTool size={13} /> Edit sketch</button></dd>
      <dd className="muted small">Every step stays editable: change the sketch, its holes or the path, and the solid follows.</dd>
    </dl>
  );
}
