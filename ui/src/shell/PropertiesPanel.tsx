/**
 * The selected entity in design terms: its name, its type, its dimensions;
 * the modules add their sections (layers, phase, quantities). IFC data is
 * there, folded away, for whoever needs it. Every change is one engine
 * command (element.rename, element.update, or the module's own).
 */

import { useEffect, useRef, useState, type ReactNode } from 'react';
import { AlignHorizontalJustifyCenter, ChevronRight, Plus, Search, Trash2, X } from 'lucide-react';
import { Cell, LengthCell } from './cells';
import { panelFocus, panelKeys, parseNumber } from './cellKeys';
import { useUi } from '../app/store';
import { execute, useSession } from '../engine/session';
import { relatedOf, relatingOf, storeys, typeOf } from '../engine/model';
import type { FieldDescriptor, NodeDto } from '../engine/protocol';
import type { PropertySectionDef } from '../platform/module';
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

/** The selected elements a change goes to: every selected element of this one's kind. */
const sameKind = (node: NodeDto): string[] => {
  const { selected } = useUi.getState();
  const nodes = useSession.getState().nodes;
  return selected.includes(node.id) ? selected.filter((id) => nodes.get(id)?.type === node.type) : [node.id];
};

/** The class's fields this node shows: all, unless a section names the keys its kind of entity has. */
function fieldsShown(fields: FieldDescriptor[], sections: PropertySectionDef[], node: NodeDto): FieldDescriptor[] {
  const keys = sections.map((s) => s.fieldKeys?.(node)).find((k) => k != null);
  return keys ? fields.filter((f) => keys.includes(f.key)) : fields;
}

function FieldRow({ node, field }: { node: NodeDto; field: FieldDescriptor }) {
  const nodes = useSession((s) => s.nodes);
  useUi((s) => s.selected);
  const v = node.props[field.key];
  // Several selected: a value they share is shown, otherwise "varies"; a change goes to all of them.
  const others = sameKind(node).map((id) => nodes.get(id)?.props[field.key]);
  const varies = others.some((x) => JSON.stringify(x ?? null) !== JSON.stringify(v ?? null));
  const update = (value: unknown) => void execute('element.update', { ids: sameKind(node), attrs: { [field.key]: value } });
  const ph = varies ? 'varies' : undefined;
  let input;
  if (field.kind === 'length') {
    input = <LengthCell metres={varies ? null : (v as number | undefined)} placeholder={ph} onCommit={update} />;
  } else if (field.kind === 'number' || field.kind === 'angle') {
    input = <Cell className="num" value={!varies && typeof v === 'number' ? String(v) : ''} placeholder={ph} suffix={field.kind === 'angle' ? '°' : undefined}
      onCommit={(t) => { if (!t.trim()) { update(null); return; } const n = parseNumber(t); if (n != null) update(n); }} />;
  } else if (field.kind === 'bool') {
    input = <input type="checkbox" checked={v === true} ref={(el) => { if (el) el.indeterminate = varies; }} onChange={(e) => update(e.target.checked)} />;
  } else if (field.kind === 'choice') {
    input = <select className="cell" value={!varies && typeof v === 'string' ? v : ''} onChange={(e) => update(e.target.value)}>{varies && <option value="">varies</option>}{!varies && typeof v !== 'string' && <option value="" disabled>—</option>}{(field.choices ?? []).map((c) => <option key={c}>{c}</option>)}</select>;
  } else if (field.kind === 'color') {
    input = <input type="color" value={typeof v === 'string' ? v : '#c9ced6'} onChange={(e) => update(e.target.value)} />;
  } else {
    input = <Cell value={!varies && typeof v === 'string' ? v : ''} placeholder={ph} onCommit={(t) => update(t)} />;
  }
  return <><dt title={field.key}>{field.label}</dt><dd className={field.readOnly ? 'ro' : ''}>{field.readOnly ? <span className="num">{String(v ?? '—')}</span> : input}</dd></>;
}

// ─── Groups that fold, and remember it ──────────────────────────────────────

const CLOSED_KEY = 'lapin.props.closed';
const readClosed = (): Set<string> => { try { return new Set(JSON.parse(localStorage.getItem(CLOSED_KEY) ?? '[]') as string[]); } catch { return new Set(); } };
function Group({ id, label, children }: { id: string; label: string; children: ReactNode }) {
  const [open, setOpen] = useState(() => !readClosed().has(id));
  const toggle = () => {
    const closed = readClosed();
    if (open) closed.add(id); else closed.delete(id);
    try { localStorage.setItem(CLOSED_KEY, JSON.stringify([...closed])); } catch { /* remembered for this session only */ }
    setOpen(!open);
  };
  return (
    <section className={`sect group-sect${open ? '' : ' closed'}`} data-group={id}>
      <button type="button" className="sect-head" onClick={toggle} aria-expanded={open}><ChevronRight size={12} className="chev" /><span className="eyebrow">{label}</span></button>
      {open && <div className="sect-body">{children}</div>}
    </section>
  );
}

// ─── The user's own parameters ──────────────────────────────────────────────

interface Param { name: string; kind: string; value: unknown; options?: string[] }
const KINDS = ['text', 'number', 'length', 'area', 'angle', 'bool', 'choice'] as const;

/**
 * The user's parameters of `node` (an element, or the type it shows the
 * parameters of: `ids` then names who is written). Parameters the element
 * has of its own shadow its type's of the same name.
 */
function ParamsSection({ node, ids: who, shadowed }: { node: NodeDto; ids?: () => string[]; shadowed?: Set<string> }) {
  const params = (Array.isArray(node.props.params) ? node.props.params : []) as unknown as Param[];
  const [name, setName] = useState('');
  const [kind, setKind] = useState<string>('text');
  const ids = who ?? (() => sameKind(node));
  const set = (p: Param, value: unknown) => void execute('element.setParam', { ids: ids(), name: p.name, value });
  const add = () => {
    const n = name.trim();
    if (!n) return;
    const options = kind === 'choice' ? (window.prompt('The choices, separated by commas') ?? '').split(',').map((x) => x.trim()).filter(Boolean) : undefined;
    void execute('element.setParam', { ids: ids(), name: n, kind, value: kind === 'bool' ? false : kind === 'text' || kind === 'choice' ? (options?.[0] ?? '') : 0, ...(options ? { options } : {}) }).then(() => setName(''));
  };
  return (
    <>
      {params.length > 0 && (
        <dl className="fields">
          {params.map((p) => (
            <span key={p.name} className="qrow">
              <dt title={shadowed?.has(p.name.toLowerCase()) ? `${p.kind} · this element has its own value` : p.kind} className={shadowed?.has(p.name.toLowerCase()) ? 'shadowed' : undefined}>{p.name}<button type="button" className="icon mini row-x" title="Remove the parameter" onClick={() => void execute('element.removeParam', { ids: ids(), name: p.name })}><X size={10} /></button></dt>
              <dd>
                {p.kind === 'length' ? <LengthCell metres={p.value as number} onCommit={(m) => set(p, m)} />
                  : p.kind === 'bool' ? <input type="checkbox" checked={p.value === true} onChange={(e) => set(p, e.target.checked)} />
                  : p.kind === 'choice' ? <select className="cell" value={String(p.value ?? '')} onChange={(e) => set(p, e.target.value)}>{(p.options ?? []).map((o) => <option key={o}>{o}</option>)}</select>
                  : p.kind === 'text' ? <Cell value={String(p.value ?? '')} onCommit={(t) => set(p, t)} />
                  : <Cell className="num" value={p.value == null ? '' : String(p.value)} suffix={p.kind === 'area' ? 'm²' : p.kind === 'angle' ? '°' : undefined}
                      onCommit={(t) => { const n = parseNumber(t); if (n != null) set(p, n); }} />}
              </dd>
            </span>
          ))}
        </dl>
      )}
      <div className="param-add">
        <input placeholder="New parameter" value={name} onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') add(); }} />
        <select value={kind} onChange={(e) => setKind(e.target.value)} title="Kind">{KINDS.map((k) => <option key={k}>{k}</option>)}</select>
        <button type="button" className="icon" title="Add the parameter" onClick={add} disabled={!name.trim()}><Plus size={14} /></button>
      </div>
    </>
  );
}

// ─── The panel's width, dragged and remembered ──────────────────────────────

const WIDTH_KEY = 'lapin.props.w';
function useWidth(): [number, (e: React.PointerEvent) => void] {
  const [w, setW] = useState(() => { try { return Number(localStorage.getItem(WIDTH_KEY)) || 300; } catch { return 300; } });
  useEffect(() => { document.documentElement.style.setProperty('--props-w', `${w}px`); }, [w]);
  const start = (e: React.PointerEvent) => {
    e.preventDefault();
    const x0 = e.clientX, w0 = w;
    const move = (ev: PointerEvent) => setW(Math.max(240, Math.min(560, w0 - (ev.clientX - x0))));
    const up = () => {
      window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up);
      setW((cur) => { try { localStorage.setItem(WIDTH_KEY, String(cur)); } catch { /* this session */ } return cur; });
    };
    window.addEventListener('pointermove', move); window.addEventListener('pointerup', up);
  };
  return [w, start];
}

/** Rows whose label does not match the filter are hidden (in every section, the modules' too). */
function useFilter(root: React.RefObject<HTMLElement | null>, text: string, deps: unknown[]) {
  useEffect(() => {
    const el = root.current;
    if (!el) return;
    const q = text.trim().toLowerCase();
    for (const dt of el.querySelectorAll('dl dt')) {
      const hide = !!q && !(dt.textContent ?? '').toLowerCase().includes(q);
      dt.classList.toggle('filtered', hide);
      const dd = dt.nextElementSibling;
      if (dd?.tagName === 'DD') dd.classList.toggle('filtered', hide);
    }
    // A section none of whose rows match goes too, while the filter is on.
    for (const sect of el.querySelectorAll('.sect')) {
      const rows = sect.querySelectorAll('dl dt');
      sect.classList.toggle('filtered', !!q && rows.length > 0 && [...rows].every((r) => r.classList.contains('filtered')));
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [text, ...deps]);
}

export function PropertiesPanel() {
  const selection = useUi((s) => s.selection);
  const nodes = useSession((s) => s.nodes);
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
        <div className="pad"><button type="button" className="btn" title="Parallel axes start and end on the same lines; with several axes selected, only those"
          onClick={() => {
            const tags = useUi.getState().selected.filter((s) => s.startsWith(`${axis.gridId}#`)).map((s) => s.slice(axis.gridId.length + 1));
            void execute('drafting.alignAxes', { gridId: axis.gridId, ...(tags.length > 1 ? { tags } : {}) });
          }}><AlignHorizontalJustifyCenter size={14} />Line up the axes</button></div>
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
  return <ElementPanel node={node} />;
}

function ElementPanel({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const selectedCount = useUi((s) => s.selected.length);
  const [filter, setFilter] = useState('');
  const [, startResize] = useWidth();
  const body = useRef<HTMLElement>(null);
  useFilter(body, filter, [node, nodes]);
  const cls = info?.nodeTypes.find((t) => t.type === node.type);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const storey = relatingOf(model, node.id, 'IfcRelContainedInSpatialStructure');
  const sections = propertySections(node.type);
  return (
    <aside className="panel props" aria-label="Properties" ref={body} onKeyDown={panelKeys} onFocus={panelFocus}>
      <div className="props-resize" onPointerDown={startResize} title="Drag to widen the panel" />
      <div className="panel-head">
        <span className="eyebrow">{cls?.label ?? node.type}{selectedCount > 1 ? ` · ${selectedCount} selected` : ''}</span>
        {node.editable
          ? <Commit className="panel-title-input" value={node.name} onCommit={(name) => void execute('element.rename', { id: node.id, name })} />
          : <span className="panel-title">{node.name}</span>}
      </div>
      <label className="props-filter"><Search size={12} /><input placeholder="Filter properties" value={filter} onChange={(e) => setFilter(e.target.value)} onKeyDown={(e) => { if (e.key === 'Escape') setFilter(''); }} /></label>
      {!node.editable && <p className="muted small pad">This element belongs to a module this edition does not have: it is shown and kept, not edited.</p>}
      {sections.map((s) => (
        <Group key={s.id} id={s.id} label={s.label}><s.component node={node} /></Group>
      ))}
      {node.editable && storey && node.type !== 'IfcBuildingStorey' && (
        <Group id="placement" label="Placement">
          <dl className="fields">
            <dt title="Moves the selection there, with the level (Modify › Move to storey keeps it in place)">Storey</dt>
            <dd>
              <select className="cell" value={storey.id} onChange={(e) => void execute('element.moveToStorey', { ids: useUi.getState().selected.includes(node.id) ? useUi.getState().selected : [node.id], storeyId: e.target.value })}>
                {storeys(model).map((st) => <option key={st.id} value={st.id}>{st.name}</option>)}
              </select>
            </dd>
          </dl>
        </Group>
      )}
      {cls && cls.fields.length > 0 && node.editable && !sections.some((s) => s.replacesFields?.(node)) && (
        <Group id="dimensions" label="Dimensions">
          <dl className="fields">{fieldsShown(cls.fields, sections, node).map((f) => <FieldRow key={f.key} node={node} field={f} />)}</dl>
        </Group>
      )}
      {node.editable && <Group id="params" label="Parameters"><ParamsSection node={node} /></Group>}
      {node.editable && type && (
        <Group id="typeParams" label={`Type parameters · ${type.name}`}>
          <p className="muted small pad">Every element of the type has these; a change here changes them all ({relatedOf(model, type.id, 'IfcRelDefinesByType').length}).</p>
          <ParamsSection node={type} ids={() => [type.id]} shadowed={new Set(((Array.isArray(node.props.params) ? node.props.params : []) as unknown as Param[]).map((p) => p.name.toLowerCase()))} />
        </Group>
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
