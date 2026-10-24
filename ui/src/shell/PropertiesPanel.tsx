/**
 * The selected entity in design terms: its name, its type, its dimensions;
 * the modules add their sections (layers, phase, quantities). IFC data is
 * there, folded away, for whoever needs it. Every change is one engine
 * command (element.rename, element.update, or the module's own).
 */

import { useEffect, useState } from 'react';
import { ChevronRight, Trash2 } from 'lucide-react';
import { useUi } from '../app/store';
import { execute, useSession } from '../engine/session';
import { relatingOf, typeOf } from '../engine/model';
import type { FieldDescriptor, NodeDto } from '../engine/protocol';
import { propertySections } from '../platform/modules';
import { QuantitiesSection } from './QuantitiesSection';

/** An input that commits on Enter or when it loses focus, not on every key. */
function Commit({ value, onCommit, suffix, className }: { value: string; onCommit: (v: string) => void; suffix?: string; className?: string }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const done = () => { if (text !== value) onCommit(text); };
  return (
    <span className="commit">
      <input className={className} value={text} onChange={(e) => setText(e.target.value)} onBlur={done}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); if (e.key === 'Escape') setText(value); }} />
      {suffix && <span className="muted"> {suffix}</span>}
    </span>
  );
}

function FieldRow({ node, field }: { node: NodeDto; field: FieldDescriptor }) {
  const v = node.props[field.key];
  const update = (value: unknown) => void execute('element.update', { id: node.id, attrs: { [field.key]: value } });
  let input;
  if (field.kind === 'length') {
    input = <Commit value={typeof v === 'number' ? String(Math.round(v * 1000)) : ''} suffix="mm"
      onCommit={(t) => update(t.trim() === '' ? null : Number(t.replace(',', '.')) / 1000)} />;
  } else if (field.kind === 'number' || field.kind === 'angle') {
    input = <Commit value={typeof v === 'number' ? String(v) : ''} onCommit={(t) => update(t.trim() === '' ? null : Number(t.replace(',', '.')))} />;
  } else if (field.kind === 'bool') {
    input = <input type="checkbox" checked={v === true} onChange={(e) => update(e.target.checked)} />;
  } else if (field.kind === 'choice') {
    input = <select value={typeof v === 'string' ? v : ''} onChange={(e) => update(e.target.value)}>{(field.choices ?? []).map((c) => <option key={c}>{c}</option>)}</select>;
  } else if (field.kind === 'color') {
    input = <input type="color" value={typeof v === 'string' ? v : '#c9ced6'} onChange={(e) => update(e.target.value)} />;
  } else {
    input = <Commit value={typeof v === 'string' ? v : ''} onCommit={(t) => update(t)} />;
  }
  return <><dt>{field.label}</dt><dd>{field.readOnly ? <span className="num">{String(v ?? '—')}</span> : input}</dd></>;
}

export function PropertiesPanel() {
  const selection = useUi((s) => s.selection);
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const node = selection ? nodes.get(selection) : undefined;
  // One grid axis (grid#tag): its label and which ends carry a bubble.
  const axis = !node && selection?.includes('#') ? (() => {
    const [gridId, tag] = [selection.slice(0, selection.indexOf('#')), selection.slice(selection.indexOf('#') + 1)];
    const grid = nodes.get(gridId);
    const a = (grid?.props.axes as { tag: string; ends?: string }[] | undefined)?.find((x) => x.tag === tag);
    return grid && a ? { gridId, tag, ends: a.ends ?? '' } : null;
  })() : null;
  if (axis) {
    return (
      <aside className="panel props" aria-label="Properties">
        <div className="panel-head"><span className="eyebrow">Grid axis</span>
          <Commit className="panel-title-input" value={axis.tag} onCommit={(t) => void execute('drafting.updateAxis', { gridId: axis.gridId, tag: axis.tag, newTag: t }).then((r) => { if (r !== null) useUi.setState({ selection: `${axis.gridId}#${t}`, selected: [`${axis.gridId}#${t}`] }); })} />
        </div>
        <dl className="fields pad">
          <dt>Bubbles</dt>
          <dd><select value={axis.ends} onChange={(e) => void execute('drafting.updateAxis', { gridId: axis.gridId, tag: axis.tag, ends: e.target.value || null })}>
            <option value="">as its axis type</option><option value="both">both ends</option><option value="start">start</option><option value="end">end</option><option value="none">none</option>
          </select></dd>
        </dl>
        <div className="pad"><button type="button" className="btn" onClick={() => void execute('element.delete', { ids: [selection] }).then(() => useUi.setState({ selection: null, selected: [] }))}><Trash2 size={14} />Delete axis</button></div>
      </aside>
    );
  }
  if (!node) {
    return (
      <aside className="panel props">
        <div className="panel-head"><span className="eyebrow">Properties</span></div>
        <p className="muted small pad">Select an element to see it here.</p>
      </aside>
    );
  }
  const cls = info?.nodeTypes.find((t) => t.type === node.type);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const storey = relatingOf(model, node.id, 'IfcRelContainedInSpatialStructure');
  return (
    <aside className="panel props" aria-label="Properties">
      <div className="panel-head">
        <span className="eyebrow">{cls?.label ?? node.type}</span>
        {node.editable
          ? <Commit className="panel-title-input" value={node.name} onCommit={(name) => void execute('element.rename', { id: node.id, name })} />
          : <span className="panel-title">{node.name}</span>}
      </div>
      {!node.editable && <p className="muted small pad">This element belongs to a module this edition does not have: it is shown and kept, not edited.</p>}
      {propertySections(node.type).map((s) => (
        <section className="sect" key={s.id}>
          <h3 className="eyebrow">{s.label}</h3>
          <s.component node={node} />
        </section>
      ))}
      {cls && cls.fields.length > 0 && node.editable && (
        <section className="sect">
          <h3 className="eyebrow">Dimensions</h3>
          <dl className="fields">{cls.fields.map((f) => <FieldRow key={f.key} node={node} field={f} />)}</dl>
        </section>
      )}
      <QuantitiesSection node={node} />
      {node.editable && node.type !== 'IfcProject' && node.type !== 'IfcSite' && node.type !== 'IfcBuilding' && (
        <section className="sect actions">
          <button type="button" className="btn" onClick={() => void execute('element.delete', { id: node.id })}><Trash2 size={14} />Delete</button>
        </section>
      )}
      <details className="sect advanced">
        <summary><ChevronRight size={12} /> <span className="eyebrow">Advanced · IFC data</span></summary>
        <dl className="fields">
          <dt>Class</dt><dd>{node.type}{typeof node.props.predefinedType === 'string' ? ` · ${node.props.predefinedType}` : ''}</dd>
          <dt>GlobalId</dt><dd className="mono">{node.id}</dd>
          {type && <><dt>Type</dt><dd>{type.type} · {type.name}</dd></>}
          {storey && <><dt>Storey</dt><dd>{storey.name}</dd></>}
          <dt>Module</dt><dd>{node.module ?? '—'}</dd>
        </dl>
      </details>
    </aside>
  );
}
