/**
 * The selected entity in design terms: its name, its type, its dimensions;
 * the modules add their sections (layers, phase, quantities). IFC data is
 * there, folded away, for whoever needs it. Every change is one engine
 * command (element.rename, element.update, or the module's own).
 */

import { Fragment, useEffect, useRef, useState, type ReactNode } from 'react';
import { AlignHorizontalJustifyCenter, ChevronRight, Search, Trash2 } from 'lucide-react';
import { Cell, LengthCell } from './cells';
import { panelFocus, panelKeys, parseNumber } from './cellKeys';
import { useUi } from '../app/store';
import { setPanelWidth, usePanels } from '../app/panels';
import { activeStorey, execute, useSession } from '../engine/session';
import { frontView } from '../viewport/frontView';
import { view3dNode } from '../viewport/storeys3d';
import { relatedOf, relatingOf, storeys, typeOf } from '../engine/model';
import { fieldShown, type FieldDescriptor, type NodeDto } from '../engine/protocol';
import type { PropertySectionDef } from '../platform/module';
import { findTool, propertySections, typeFieldsOf } from '../platform/modules';
import { QuantitiesSection } from './QuantitiesSection';
import { DependenciesSection } from './DependenciesSection';
import { deleteElements } from '../engine/dependents';
import { ParamsSection } from './ParamsSection';
import { AxisFields } from './AxisFields';
import { paramKey, sameKind, type Param } from './params';
import { filterSets, importedSets } from './ifcProperties';
import { MaterialPicker } from '../modules/core/MaterialPicker';

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


/**
 * The class's fields this node shows: all, unless a section names the keys its kind of entity has; of those, the ones that
 * apply to it or to another of the selection (a roof's deck only without framing, decision 333).
 */
function fieldsShown(fields: FieldDescriptor[], sections: PropertySectionDef[], node: NodeDto, others: NodeDto[]): FieldDescriptor[] {
  const keys = sections.map((s) => s.fieldKeys?.(node)).find((k) => k != null);
  return (keys ? fields.filter((f) => keys.includes(f.key)) : fields).filter((f) => [node, ...others].some((n) => fieldShown(f, n.props)));
}

export function FieldRow({ node, field }: { node: NodeDto; field: FieldDescriptor }) {
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
    input = <select className="cell" value={!varies && typeof v === 'string' ? v : ''} onChange={(e) => update(e.target.value)}>{varies && <option value="">varies</option>}{!varies && typeof v !== 'string' && <option value="" disabled>—</option>}{(field.choices ?? []).map((c) => <option key={c} value={c}>{c}</option>)}</select>;
  } else if (field.kind === 'material') {
    // A project material by name (a roof's covering, decision 328): picked from the project's, or a new one made.
    input = <MaterialPicker value={!varies && typeof v === 'string' ? v : ''} placeholder={varies ? 'varies' : 'none'} allowEmpty onChange={(name) => update(name)} />;
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

// ─── What an imported element brought from its IFC file ─────────────────────

/**
 * The property sets and quantities an element brought from an IFC file, as
 * the file had them, read-only: a set per fold (open while there are few), a
 * filter when there are many (ArchiCAD writes hundreds for a zone).
 */
function IfcPropertiesSection({ node }: { node: NodeDto }) {
  const [filter, setFilter] = useState('');
  const sets = importedSets(node);
  if (sets.length === 0) return null;
  const total = sets.reduce((n, s) => n + s.props.length, 0);
  const shown = filterSets(sets, filter);
  return (
    <Group id="ifcProperties" label={`IFC properties · ${total}`}>
      {total > 20 && <input className="filter ifc-props-filter" placeholder="Filter by set, name or value" value={filter} onChange={(e) => setFilter(e.target.value)} />}
      {shown.map((s) => (
        <details key={s.name} className="advanced ifc-set" open={sets.length <= 3 || filter.trim() !== ''}>
          <summary><ChevronRight size={12} /> <span>{s.name}</span> <span className="muted small">{s.props.length}</span></summary>
          <dl className="fields ifc-fields">
            {s.props.map(([k, v]) => <Fragment key={k}><dt title={k}>{k}</dt><dd title={v}>{v || '—'}</dd></Fragment>)}
          </dl>
        </details>
      ))}
      {shown.length === 0 && <p className="muted small pad">Nothing matches.</p>}
    </Group>
  );
}

// ─── The panel's width, dragged by its left edge (the panels' own width, app/panels.ts, as App's separator) ───

function startResize(e: React.PointerEvent): void {
  e.preventDefault();
  const x0 = e.clientX, w0 = usePanels.getState().propsWidth;
  const move = (ev: PointerEvent) => setPanelWidth('props', w0 - (ev.clientX - x0));
  const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
  window.addEventListener('pointermove', move); window.addEventListener('pointerup', up);
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
  const toolId = useUi((s) => s.tool);
  // A placing tool on: what it will put down (its type), before the first click.
  const placing = findTool(toolId);
  if (placing?.properties) {
    const Section = placing.properties;
    return (
      <aside className="panel props" aria-label="Properties">
        <div className="panel-head"><span className="eyebrow">New element</span><span className="panel-title">{placing.label}</span></div>
        <Section />
      </aside>
    );
  }
  return <SelectionPanel selection={selection} />;
}

function SelectionPanel({ selection }: { selection: string | null }) {
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
        <AxisFields gridId={axis.gridId} tag={axis.tag} />
        <div className="pad"><button type="button" className="btn" title="Parallel axes start and end on the same lines; with several axes selected, only those"
          onClick={() => {
            const tags = useUi.getState().selected.filter((s) => s.startsWith(`${axis.gridId}#`)).map((s) => s.slice(axis.gridId.length + 1));
            void execute('drafting.alignAxes', { gridId: axis.gridId, ...(tags.length > 1 ? { tags } : {}) });
          }}><AlignHorizontalJustifyCenter size={14} />Line up the axes</button></div>
        <div className="pad"><button type="button" className="btn" onClick={() => void execute('element.delete', { ids: [selection] }).then(() => useUi.setState({ selection: null, selected: [] }))}><Trash2 size={14} />Delete axis</button></div>
      </aside>
    );
  }
  if (!node) return <FrontViewPanel />;
  return <ElementPanel node={node} />;
}

/**
 * Nothing selected: the view in front, with its own settings (discipline,
 * style, range, the storeys it shows, its sheet's title block). A level's
 * plan without a view of its own draws with the defaults, and offers to make one.
 */
function FrontViewPanel() {
  const ui = useUi();
  const nodes = useSession((s) => s.nodes);
  const storey = activeStorey();
  const id = ui.view === 'plan' ? (ui.planViewId && nodes.get(ui.planViewId)?.props.storeyId === storey?.id ? ui.planViewId : null)
    : ui.view === 'drawing' ? ui.drawingViewId : ui.view === 'sheet' ? ui.sheetId : ui.view === '3d' ? ui.view3dId : null;
  const front = id ? nodes.get(id) : undefined;
  if (front) return <ElementPanel node={front} inFront />;
  // {3D}: no entity of its own (its settings are the project's), so its view settings only (discipline, style, layers, the storeys it shows), as a saved 3D view's; the other sections address entities.
  if (ui.view === '3d') {
    const view3d = view3dNode(nodes);
    return (
      <aside className="panel props" aria-label="Properties">
        <div className="panel-head"><span className="eyebrow">3D view · in front</span><span className="panel-title" data-no-i18n>3D</span></div>
        {propertySections('LapinView').filter((s) => s.id === 'viewSettings').map((s) => <Group key={s.id} id={s.id} label={s.label}><s.component node={view3d} /></Group>)}
      </aside>
    );
  }
  if (ui.view === 'plan' && storey) {
    const own = [...nodes.values()].find((n) => n.type === 'LapinView' && n.props.kind === 'plan' && n.props.storeyId === storey.id);
    return (
      <aside className="panel props" aria-label="Properties">
        <div className="panel-head"><span className="eyebrow">Floor plan · in front</span><span className="panel-title">{storey.name}</span></div>
        <p className="muted small pad">{own ? 'This plan is the level\'s default one; its own views are in the project browser.' : 'This level\'s plan draws with the defaults (Architecture, cut at 1.20 m). Give it a view of its own to set its discipline, style, range and the storeys it shows.'}</p>
        {!own && <div className="pad"><button type="button" className="btn" onClick={() => void frontView()}>Make its own view</button></div>}
      </aside>
    );
  }
  return (
    <aside className="panel props">
      <div className="panel-head"><span className="eyebrow">Properties</span></div>
      <p className="muted small pad">Select an element to see it here.</p>
    </aside>
  );
}

function ElementPanel({ node, inFront }: { node: NodeDto; inFront?: boolean }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const selectedCount = useUi((s) => s.selected.length);
  const [filter, setFilter] = useState('');
  const body = useRef<HTMLElement>(null);
  useFilter(body, filter, [node, nodes]);
  const cls = info?.nodeTypes.find((t) => t.type === node.type);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const storey = relatingOf(model, node.id, 'IfcRelContainedInSpatialStructure');
  const sections = propertySections(node.type).filter((s) => !s.shows || s.shows(node));
  const TypeFields = type ? typeFieldsOf(type.type) : null;
  return (
    <aside className="panel props" aria-label="Properties" ref={body} onKeyDown={panelKeys} onFocus={panelFocus}>
      <div className="props-resize" onPointerDown={startResize} title="Drag to widen the panel" />
      <div className="panel-head">
        <span className="eyebrow">{cls?.label ?? node.type}{inFront ? ' · in front' : selectedCount > 1 ? ` · ${selectedCount} selected` : ''}</span>
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
          <dl className="fields">{fieldsShown(cls.fields, sections, node, sameKind(node).map((id) => nodes.get(id)).filter((n): n is NodeDto => !!n)).map((f) => <FieldRow key={f.key} node={node} field={f} />)}</dl>
        </Group>
      )}
      {node.editable && <Group id="params" label="Parameters"><ParamsSection node={node} /></Group>}
      {node.editable && type && (
        <Group id="typeParams" label={`Type parameters · ${type.name}`}>
          <p className="muted small pad">Every element of the type has these; a change here changes them all ({relatedOf(model, type.id, 'IfcRelDefinesByType').length}).</p>
          {TypeFields && <TypeFields type={type} node={node} />}
          <ParamsSection node={type} ids={() => [type.id]} shadowed={new Set(((Array.isArray(node.props.params) ? node.props.params : []) as unknown as Param[]).map(paramKey))} />
        </Group>
      )}
      <QuantitiesSection node={node} />
      <DependenciesSection node={node} />
      <IfcPropertiesSection node={node} />
      {node.editable && node.type !== 'IfcProject' && node.type !== 'IfcSite' && node.type !== 'IfcBuilding' && (
        <section className="sect actions">
          <button type="button" className="btn" onClick={() => void deleteElements([node.id])}><Trash2 size={14} />Delete</button>
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
