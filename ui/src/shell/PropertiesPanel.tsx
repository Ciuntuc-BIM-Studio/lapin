/**
 * The selected element in design terms: its type, its size, its level; the
 * modules add their sections (layers, phase, quantities). IFC data is there,
 * folded away, for whoever needs it.
 */

import { ChevronRight } from 'lucide-react';
import { useUi } from '../app/store';
import { propertySections } from '../platform/modules';

export function PropertiesPanel() {
  const kind = useUi((s) => s.selectedKind);
  if (!kind) return <aside className="panel props"><div className="panel-head"><span className="eyebrow">Properties</span></div><p className="muted small pad">Select an element to see it here.</p></aside>;
  return (
    <aside className="panel props" aria-label="Properties">
      <div className="panel-head"><span className="eyebrow">Wall</span><span className="panel-title">W-07</span></div>
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <select defaultValue="b30"><option value="b30">Brick 30 + EPS 10, plastered</option><option>AAC 25, plastered</option><option>Partition 10</option></select>
      </label>
      <section className="sect">
        <h3 className="eyebrow">Dimensions</h3>
        <dl className="fields">
          <dt>Length</dt><dd className="num">6.40 m</dd>
          <dt>Height</dt><dd><input defaultValue="3.00" /> m</dd>
          <dt>Base</dt><dd><select defaultValue="g"><option value="g">Ground floor</option></select></dd>
          <dt>Top</dt><dd><select defaultValue="l1"><option value="l1">Up to Level 1</option><option>Unconnected</option></select></dd>
          <dt>Location line</dt><dd><select defaultValue="core"><option value="core">Core centre</option><option>Exterior face</option></select></dd>
        </dl>
      </section>
      {propertySections(kind).map((s) => (
        <section className="sect" key={s.id}>
          <h3 className="eyebrow">{s.label}</h3>
          <s.component kind={kind} />
        </section>
      ))}
      <details className="sect advanced">
        <summary><ChevronRight size={12} /> <span className="eyebrow">Advanced · IFC data</span></summary>
        <dl className="fields">
          <dt>Class</dt><dd>IfcWall · STANDARD</dd>
          <dt>GlobalId</dt><dd className="mono">2xkyS…R5rUb</dd>
          <dt>Type</dt><dd>IfcWallType · Brick 30</dd>
        </dl>
      </details>
    </aside>
  );
}
