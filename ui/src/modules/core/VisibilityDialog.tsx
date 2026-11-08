/**
 * Visibility / Graphics (VV, VG) for the view in front, 2D or 3D, as Revit's
 * (docs/plan-visibility-graphics.md): Model categories with their
 * subcategories (a wall's core, finishes, insulation…: shown, line colour and
 * weight, fill, cut hatch), Links (each link by the host view or with its own
 * categories and classes in this view, shown and halftone here) and IFC
 * classes; the view's own settings, a named set of the project's shared by
 * every view that takes it, or its view style's set; a 2D view's hidden lines.
 */

import { Fragment, useEffect, useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, ChevronDown, ChevronRight, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { frontView } from '../../viewport/frontView';
import { PendingDialog } from './PendingDialog';
import { LOD_HELP, LODS, lodLabel } from './viewStyles';
import { CLASS_COLORS, CLASS_NAMES } from '../../viewport/pointClouds3d';

export interface Row { hidden?: boolean; lineColor?: string; lineWeight?: string; fillColor?: string; cutHatch?: string; halftone?: boolean; transparency?: number; lines?: Record<string, { pattern?: string; pen?: number }>; sub?: Record<string, Row> }
interface Rules { categories?: Record<string, Row>; classes?: Record<string, Row> }
export interface LinkRow extends Rules { display?: 'host' | 'custom'; hidden?: boolean; halftone?: boolean }
export interface FilterRow { id: string; hidden?: boolean; color?: string; halftone?: boolean; transparency?: number; pattern?: string }
interface Graphics extends Rules {
  links?: Record<string, LinkRow>; annotations?: Record<string, { hidden?: boolean; color?: string; halftone?: boolean }>; detailLevel?: string;
  imports?: Record<string, { hidden?: boolean; halftone?: boolean; layers?: Record<string, { hidden?: boolean }> }>; filters?: FilterRow[];
  pointClouds?: Record<string, CloudRow>;
}
interface ImportInfo { id: string; kind: 'dxf' | 'image'; name: string; layers: string[] }
export interface ViewFilter { id: string; name: string; match: 'all' | 'any'; rules: FilterRule[] }
export interface FilterRule { field: string; op: string; value?: string; name?: string }
interface LinkInfo { id: string; name: string; kind: string; visible: boolean; halftone: boolean; classes: string[] }
/** A point cloud as the tab lists it (decision 396): its link's own settings, what its points carry, the ASPRS classes they have. */
interface CloudInfo { id: string; name: string; visible: boolean; halftone: boolean; colorMode: string; color: boolean; intensity: boolean; classification: boolean; classes: number[] }
export interface CloudRow { hidden?: boolean; halftone?: boolean; colorMode?: string; hiddenClasses?: number[]; slice?: number }
export interface Visibility {
  graphics?: Graphics | null; graphicsSet?: string | null; fromStyle?: boolean; style?: string;
  /** Its level of development (decision 404): drawn at, its own (null: its style's), its style's. */ detail?: number; ownDetail?: number | null; styleDetail?: number;
  sets: { id: string; name: string }[]; categories: string[]; subcategories?: Record<string, string[]>;
  /** Hidden until the graphics show them (rooms, in 3D). */ hiddenByDefault?: string[];
  links?: LinkInfo[]; annotations?: string[]; imports?: ImportInfo[]; filters?: { id: string; name: string }[];
  pointClouds?: CloudInfo[];
}

/** The ISO 128 pen series (mm), as the engine reads a category's weight; the older names still show when a project has them. */
const WEIGHTS = ['', '0.13', '0.18', '0.25', '0.35', '0.5', '0.7', '1.0'];
const LEGACY: Record<string, string> = { heavy: '0.7', medium: '0.5', light: '0.25', hairline: '0.13' };

/** The line patterns a category or a filter can draw its cut and projection lines in. */
export const PATTERNS: [string, string][] = [['', 'as styled'], ['solid', 'solid'], ['dashed', 'dashed'], ['hidden', 'hidden'], ['center', 'center'], ['dotted', 'dotted']];

/** A category's lines drawn in a pattern (cut and projection), or as styled again. */
export function withPattern(lines: Row['lines'], pattern: string): Row['lines'] {
  const next = { ...(lines ?? {}) };
  for (const role of ['cut', 'projection']) {
    const spec = { ...(next[role] ?? {}) };
    if (pattern) spec.pattern = pattern; else delete spec.pattern;
    if (Object.keys(spec).length) next[role] = spec; else delete next[role];
  }
  return next;
}

/** A category's or a subcategory's name as the dialog shows it. */
export const CATEGORY_NAMES: Record<string, string> = {
  walls: 'Walls', slabs: 'Slabs', roofs: 'Roofs', columns: 'Columns', beams: 'Beams', doors: 'Doors', windows: 'Windows', stairs: 'Stairs',
  railings: 'Railings', spaces: 'Spaces', furniture: 'Furniture', topography: 'Topography', other: 'Other', linework: 'Linework', voids: 'Voids',
  entourage: 'Entourage', coverings: 'Coverings', rebar: 'Rebar', formwork: 'Formwork', scaffolds: 'Scaffolds', mep: 'MEP',
  steelConnections: 'Steel connections', transport: 'Transport', shading: 'Shading',
  core: 'Core', finishes: 'Finishes', insulation: 'Insulation', membranes: 'Membranes', otherLayers: 'Other layers', reveals: 'Reveals',
  dimensions: 'Dimensions', texts: 'Texts', tags: 'Tags', roomTags: 'Room tags', grids: 'Grids', levels: 'Levels', sections: 'Section marks',
  elevations: 'Elevation marks', callouts: 'Callouts', refPlanes: 'Reference planes', viewBoundaries: 'View boundaries', legends: 'Legends',
};

/** What a filter's rule can read, and how it compares, as the editor names them. */
export const FILTER_FIELDS: [string, string][] = [['class', 'IFC class'], ['category', 'Category'], ['type', 'Type'], ['name', 'Name'], ['material', 'Material'], ['storey', 'Storey'], ['group', 'Group'], ['link', 'Link'], ['phase', 'Phase created'], ['predefinedType', 'Predefined type'], ['param', 'Parameter']];
export const FILTER_OPS: [string, string][] = [['equals', 'equals'], ['notEquals', 'does not equal'], ['contains', 'contains'], ['notContains', 'does not contain'], ['beginsWith', 'begins with'], ['greater', 'is greater than'], ['less', 'is less than'], ['hasValue', 'has a value'], ['hasNoValue', 'has no value']];

/** The tab the dialog opens on next (Manage → View filters opens it on Filters). */
let nextTab: Tab | null = null;
export function openVisibility(tab?: Tab) { nextTab = tab ?? null; useUi.setState({ dialog: 'visibility' }); }

/** A view's filters reordered: the row at <i>i</i> moved by <i>by</i> (−1 up, +1 down), the list as it was when it cannot move. */
export function moveRow<T>(list: T[], i: number, by: number): T[] {
  const j = i + by;
  if (j < 0 || j >= list.length) return list;
  const next = [...list];
  [next[i], next[j]] = [next[j], next[i]];
  return next;
}
export const categoryName = (c: string) => CATEGORY_NAMES[c] ?? (c ? c[0].toUpperCase() + c.slice(1) : c);

type Tab = 'categories' | 'annotations' | 'imports' | 'filters' | 'links' | 'pointClouds' | 'classes';

/** A row cleared of what says nothing (an empty field, shown when shown is the default). */
export function clean(next: Row, keepsShown: boolean): Row {
  for (const k of Object.keys(next) as (keyof Row)[])
    if (next[k] === '' || next[k] === 0 || (next[k] === false && !(k === 'hidden' && keepsShown)) || next[k] == null || ((k === 'sub' || k === 'lines') && !Object.keys((next[k] as object | undefined) ?? {}).length)) delete next[k];
  return next;
}

export function VisibilityDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [target, setTarget] = useState<string | null>(null);
  const [v, setV] = useState<Visibility | null>(null);
  const [hatches, setHatches] = useState<string[]>([]);
  // Read once (an initializer may run twice in development), forgotten once the dialog is up.
  const [tab, setTab] = useState<Tab>(() => nextTab ?? 'categories');
  useEffect(() => { nextTab = null; }, []);
  const [importOpen, setImportOpen] = useState<Record<string, boolean>>({});
  const [defs, setDefs] = useState<ViewFilter[]>([]);
  const [editing, setEditing] = useState<string | null>(null);
  const [counts, setCounts] = useState<Record<string, number>>({});
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const [linkOpen, setLinkOpen] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => { void frontView().then((id) => { if (!id) { notify('Open a plan, a section, an elevation or the 3D first.'); close(); } else setTarget(id); }); }, []);
  useEffect(() => {
    if (!target) return;
    let live = true;
    void query<Visibility>('view.visibility', { id: target }).then((x) => { if (live) setV(x); });
    return () => { live = false; };
  }, [target, nodes]);
  useEffect(() => { void query<{ name: string }[]>('hatch.list').then((r) => r && setHatches(r.map((h) => h.name))); }, []);
  // The project's filters with their rules, and how many elements each takes now.
  useEffect(() => {
    let live = true;
    void query<ViewFilter[]>('viewFilter.list').then(async (list) => {
      if (!live || !list) return;
      setDefs(list);
      const n: Record<string, number> = {};
      for (const f of list) n[f.id] = (await query<number>('viewFilter.count', { id: f.id })) ?? 0;
      if (live) setCounts(n);
    });
    return () => { live = false; };
  }, [nodes]);

  // The IFC classes of the host's elements on storeys (a link's are on its own row).
  const classes = useMemo(() => [...new Set([...nodes.values()].filter((n) => n.storeyId && typeof n.props.linkId !== 'string').map((n) => n.type))].sort(), [nodes]);
  if (!target || !v) return <PendingDialog title="Visibility / Graphics" />;
  const view = target === '3d' ? null : nodes.get(target);
  const links = v.links ?? [];
  const setName = v.sets.find((s) => s.id === v.graphicsSet)?.name;

  /** The rules a row is read from: the view's, or a link's own. */
  const rulesOf = (link?: string): Rules => (link ? v.graphics?.links?.[link] : v.graphics) ?? {};
  const setCategory = (c: string, patch: Row, link?: string, sub?: string) => {
    const cur = rulesOf(link).categories?.[c] ?? {};
    const keepsShown = !sub && (v.hiddenByDefault ?? []).includes(c);
    const base = sub ? { ...(cur.sub?.[sub] ?? {}) } : { ...cur };
    delete base.sub;
    const next = clean({ ...base, ...patch }, keepsShown);
    void execute('view.graphics', { id: target, category: c, ...(sub ? { sub } : {}), ...(link ? { link } : {}), value: Object.keys(next).length ? next : null });
  };
  const setClass = (c: string, hidden: boolean, link?: string) =>
    void execute('view.graphics', { id: target, class: c, ...(link ? { link } : {}), value: hidden ? { hidden: true } : null });
  const setLink = (id: string, patch: Partial<LinkRow>) => {
    const cur = v.graphics?.links?.[id] ?? {};
    const next: Record<string, unknown> = { display: cur.display, hidden: cur.hidden, halftone: cur.halftone, ...patch };
    for (const k of Object.keys(next)) if (next[k] == null || next[k] === false || next[k] === 'host') delete next[k];
    void execute('view.graphics', { id: target, link: id, value: Object.keys(next).length ? next : null });
  };

  /** The categories, each unfolding into its subcategories; full columns for the view's own, Shown alone inside a link. */
  const categoryTable = (link?: string) => (
    <table className="layer-table vv-categories">
      <thead><tr><th>Shown</th><th>Category</th>{!link && <><th>Line colour</th><th>Line weight</th><th>Pattern</th><th>Fill</th><th>Cut hatch</th><th>Halftone</th><th title="In 3D: 0 opaque, 100 clear">Transparency</th></>}</tr></thead>
      <tbody>
        {v.categories.map((c) => {
          const r = rulesOf(link).categories?.[c] ?? {};
          const subs = v.subcategories?.[c] ?? [];
          const key = `${link ?? ''}:${c}`;
          const shown = !(r.hidden ?? (v.hiddenByDefault ?? []).includes(c));
          return (
            <Fragment key={c}>
              <tr data-category={c}>
                <td><input type="checkbox" aria-label={categoryName(c)} checked={shown} onChange={(e) => setCategory(c, { hidden: !e.target.checked }, link)} /></td>
                <td>
                  <span className="vv-name">
                    {subs.length > 0
                      ? <button type="button" className="icon mini" aria-label="Subcategories" title="Subcategories" onClick={() => setOpen((o) => ({ ...o, [key]: !o[key] }))}>{open[key] ? <ChevronDown size={11} /> : <ChevronRight size={11} />}</button>
                      : <span className="vv-indent" />}
                    {categoryName(c)}
                  </span>
                </td>
                {!link && (
                  <>
                    <td><input type="color" value={r.lineColor ?? '#000000'} onChange={(e) => setCategory(c, { lineColor: e.target.value })} />{r.lineColor && <button type="button" className="icon mini" title="As the object styles" onClick={() => setCategory(c, { lineColor: '' })}><X size={10} /></button>}</td>
                    <td><select value={r.lineWeight ?? ''} onChange={(e) => setCategory(c, { lineWeight: e.target.value })}>{(r.lineWeight && !WEIGHTS.includes(r.lineWeight) ? [...WEIGHTS, r.lineWeight] : WEIGHTS).map((w) => <option key={w} value={w}>{w === '' ? 'as styled' : LEGACY[w] ? `${w} (${LEGACY[w]} mm)` : `${w} mm`}</option>)}</select></td>
                    <td><select aria-label="Pattern" value={r.lines?.projection?.pattern ?? r.lines?.cut?.pattern ?? ''} onChange={(e) => setCategory(c, { lines: withPattern(r.lines, e.target.value) })}>{PATTERNS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                    <td><input type="color" value={r.fillColor ?? '#ffffff'} onChange={(e) => setCategory(c, { fillColor: e.target.value })} />{r.fillColor && <button type="button" className="icon mini" title="No fill of its own" onClick={() => setCategory(c, { fillColor: '' })}><X size={10} /></button>}</td>
                    <td><select value={r.cutHatch ?? ''} onChange={(e) => setCategory(c, { cutHatch: e.target.value })}><option value="">the materials'</option>{hatches.map((h) => <option key={h} value={h}>{h}</option>)}</select></td>
                    <td><input type="checkbox" aria-label="Halftone" checked={!!r.halftone} onChange={(e) => setCategory(c, { halftone: e.target.checked })} /></td>
                    <td><input type="number" className="vv-number" aria-label="Transparency" min={0} max={95} step={5} value={r.transparency ?? 0} onChange={(e) => setCategory(c, { transparency: Math.max(0, Math.min(95, Number(e.target.value) || 0)) })} /></td>
                  </>
                )}
              </tr>
              {open[key] && subs.map((s) => {
                const sr = r.sub?.[s] ?? {};
                return (
                  <tr key={s} className="vv-sub" data-sub={`${c}.${s}`}>
                    <td><input type="checkbox" aria-label={`${categoryName(c)} ▸ ${categoryName(s)}`} checked={shown && !sr.hidden} disabled={!shown} onChange={(e) => setCategory(c, { hidden: !e.target.checked }, link, s)} /></td>
                    <td className="vv-sub-name">{categoryName(s)}</td>
                    {!link && <td colSpan={7} />}
                  </tr>
                );
              })}
            </Fragment>
          );
        })}
      </tbody>
    </table>
  );

  const classTable = (list: string[], link?: string) => (
    <table className="layer-table">
      <thead><tr><th>Shown</th><th>IFC class</th></tr></thead>
      <tbody>
        {list.map((c) => (
          <tr key={c}>
            <td><input type="checkbox" aria-label={c} checked={!rulesOf(link).classes?.[c]?.hidden} onChange={(e) => setClass(c, !e.target.checked, link)} /></td>
            <td>{c}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );

  const custom = links.find((l) => l.id === linkOpen && v.graphics?.links?.[l.id]?.display === 'custom');

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Visibility / Graphics" data-testid="vv-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Visibility / Graphics · {target === '3d' ? '3D' : view?.name ?? 'View'}</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="row vv-set">
            <span className="eyebrow">Graphics</span>
            <select value={v.fromStyle ? '' : v.graphicsSet ?? ''} onChange={(e) => void execute('view.useGraphicsSet', { id: target, setId: e.target.value || null })}>
              <option value="">{v.fromStyle ? "the style's set" : "this view's own"}</option>
              {v.sets.map((s) => <option key={s.id} value={s.id}>set: {s.name}</option>)}
            </select>
            <button type="button" className="btn" onClick={() => { const name = window.prompt('The new set\'s name'); if (name?.trim()) void execute('view.saveGraphicsSet', { id: target, name: name.trim() }); }}>Save as a set…</button>
            {v.graphicsSet && !v.fromStyle && <button type="button" className="btn" title="Its views keep its settings as their own" onClick={() => void execute('view.removeGraphicsSet', { setId: v.graphicsSet })}><Trash2 size={13} />Remove the set</button>}
            {v.fromStyle && <button type="button" className="btn" title="Graphics of this view's own, starting from the style's set" onClick={() => void execute('view.useGraphicsSet', { id: target, setId: null })}>Make them this view's own</button>}
            {setName && <span className="muted small">Changes here change „{setName}” in every view that takes it.</span>}
            {(view || target === '3d') && (
              <label className="row" title={LOD_HELP}><span className="muted small">Level of development</span>
                <select data-testid="vv-detail" value={v.ownDetail != null ? String(v.ownDetail) : ''} onChange={(e) => {
                  // The view's own level (a set's old coarse, medium or fine taken out, so the choice rules).
                  if (v.graphics?.detailLevel != null) void execute('view.graphics', { id: target, detailLevel: null });
                  void execute('view.setStyle', { id: target, detail: e.target.value ? Number(e.target.value) : null });
                }}>
                  <option value="">as its style: {lodLabel(v.styleDetail)}</option>
                  {LODS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
                </select>
              </label>
            )}
            {view && (
              <label className="row"><span className="muted small">Hidden lines</span>
                <select value={String(view.props.hiddenLines ?? 'auto')} onChange={(e) => void execute('element.update', { id: target, attrs: { hiddenLines: e.target.value } })}>
                  <option value="auto">by the view's kind</option><option value="show">shown</option><option value="hide">hidden</option>
                </select>
              </label>
            )}
          </div>
          <div className="tabs-inline" role="tablist">
            <button type="button" role="tab" data-tab="categories" className={tab === 'categories' ? 'on' : ''} onClick={() => setTab('categories')}>Model categories</button>
            <button type="button" role="tab" data-tab="annotations" className={tab === 'annotations' ? 'on' : ''} onClick={() => setTab('annotations')}>Annotation categories</button>
            <button type="button" role="tab" data-tab="imports" className={tab === 'imports' ? 'on' : ''} onClick={() => setTab('imports')}>Imported categories</button>
            <button type="button" role="tab" data-tab="filters" className={tab === 'filters' ? 'on' : ''} onClick={() => setTab('filters')}>Filters</button>
            <button type="button" role="tab" data-tab="links" className={tab === 'links' ? 'on' : ''} onClick={() => setTab('links')}>Links</button>
            <button type="button" role="tab" data-tab="pointClouds" className={tab === 'pointClouds' ? 'on' : ''} onClick={() => setTab('pointClouds')}>Point clouds</button>
            <button type="button" role="tab" data-tab="classes" className={tab === 'classes' ? 'on' : ''} onClick={() => setTab('classes')}>IFC classes</button>
          </div>
          {tab === 'categories' && categoryTable()}
          {tab === 'classes' && classTable(classes)}
          {tab === 'annotations' && (
            <table className="layer-table vv-annotations">
              <thead><tr><th>Shown</th><th>Category</th><th>Colour</th><th>Halftone</th></tr></thead>
              <tbody>
                {(v.annotations ?? []).map((c) => {
                  const r = v.graphics?.annotations?.[c] ?? {};
                  const put = (patch: { hidden?: boolean; color?: string; halftone?: boolean }) => {
                    const next: Record<string, unknown> = { ...r, ...patch };
                    for (const k of Object.keys(next)) if (!next[k]) delete next[k];
                    void execute('view.graphics', { id: target, annotation: c, value: Object.keys(next).length ? next : null });
                  };
                  return (
                    <tr key={c} data-annotation={c}>
                      <td><input type="checkbox" aria-label={categoryName(c)} checked={!r.hidden} onChange={(e) => put({ hidden: !e.target.checked })} /></td>
                      <td>{categoryName(c)}</td>
                      <td><input type="color" aria-label="Colour" value={r.color ?? '#000000'} onChange={(e) => put({ color: e.target.value })} />{r.color && <button type="button" className="icon mini" title="Its own colour" onClick={() => put({ color: '' })}><X size={10} /></button>}</td>
                      <td><input type="checkbox" aria-label="Halftone" checked={!!r.halftone} onChange={(e) => put({ halftone: e.target.checked })} /></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
          {tab === 'imports' && (
            !(v.imports ?? []).length ? <p className="muted small">Nothing is imported into this view: Import DXF…, or a PDF or an image to trace over (Insert).</p> : (
              <table className="layer-table vv-imports">
                <thead><tr><th>Shown</th><th>Import</th><th>Halftone</th></tr></thead>
                <tbody>
                  {(v.imports ?? []).map((im) => {
                    const r = v.graphics?.imports?.[im.id] ?? {};
                    const set = (patch: { hidden?: boolean; halftone?: boolean }) => {
                      const next: Record<string, boolean> = {};
                      for (const [k, x] of Object.entries({ hidden: r.hidden, halftone: r.halftone, ...patch })) if (x) next[k] = true;
                      void execute('view.graphics', { id: target, import: im.id, value: Object.keys(next).length ? next : null });
                    };
                    return (
                      <Fragment key={im.id}>
                        <tr data-import={im.name}>
                          <td><input type="checkbox" aria-label={im.name} checked={!r.hidden} onChange={(e) => set({ hidden: !e.target.checked })} /></td>
                          <td><span className="vv-name">
                            {im.layers.length > 0
                              ? <button type="button" className="icon mini" aria-label="Layers" title="Layers" onClick={() => setImportOpen((o) => ({ ...o, [im.id]: !o[im.id] }))}>{importOpen[im.id] ? <ChevronDown size={11} /> : <ChevronRight size={11} />}</button>
                              : <span className="vv-indent" />}
                            {im.name} <span className="muted small">{im.kind === 'dxf' ? 'DXF' : 'PDF / image'}</span>
                          </span></td>
                          <td>{im.kind === 'dxf' && <input type="checkbox" aria-label="Halftone" checked={!!r.halftone} onChange={(e) => set({ halftone: e.target.checked })} />}</td>
                        </tr>
                        {importOpen[im.id] && im.layers.map((l) => (
                          <tr key={l} className="vv-sub" data-layer={l}>
                            <td><input type="checkbox" aria-label={l} checked={!r.hidden && !r.layers?.[l]?.hidden} disabled={!!r.hidden} onChange={(e) => void execute('view.graphics', { id: target, import: im.id, layer: l, value: e.target.checked ? null : { hidden: true } })} /></td>
                            <td className="vv-sub-name">{l}</td><td />
                          </tr>
                        ))}
                      </Fragment>
                    );
                  })}
                </tbody>
              </table>
            )
          )}
          {tab === 'pointClouds' && <PointCloudsTab target={target} clouds={v.pointClouds ?? []} rows={v.graphics?.pointClouds ?? {}} in3d={!view} />}
          {tab === 'filters' && <FiltersTab target={target} categories={v.categories} rows={v.graphics?.filters ?? []} defs={defs} counts={counts} editing={editing} setEditing={setEditing} />}
          {tab === 'links' && (
            links.length === 0 ? <p className="muted small">The project has no links: File → Link IFC…, or Import DXF… as a link.</p> : (
              <>
                <table className="layer-table vv-links">
                  <thead><tr><th>Shown</th><th>Link</th><th>Display</th><th>Halftone</th><th /></tr></thead>
                  <tbody>
                    {links.map((l) => {
                      const r = v.graphics?.links?.[l.id] ?? {};
                      return (
                        <tr key={l.id} data-link={l.name}>
                          <td><input type="checkbox" aria-label={l.name} checked={!r.hidden} onChange={(e) => setLink(l.id, { hidden: !e.target.checked })} /></td>
                          <td>{l.name} <span className="muted small">{l.kind.toUpperCase()}</span>{!l.visible && <span className="muted small"> · hidden everywhere (Manage → Links)</span>}</td>
                          <td>
                            <select value={r.display ?? 'host'} onChange={(e) => { setLink(l.id, { display: e.target.value as LinkRow['display'] }); if (e.target.value === 'custom') setLinkOpen(l.id); }}>
                              <option value="host">By host view</option><option value="custom">Custom</option>
                            </select>
                          </td>
                          <td><input type="checkbox" aria-label="Halftone" checked={!!r.halftone || l.halftone} disabled={l.halftone} title={l.halftone ? 'Halftone everywhere (Manage → Links)' : undefined} onChange={(e) => setLink(l.id, { halftone: e.target.checked })} /></td>
                          <td>{r.display === 'custom' && <button type="button" className="btn small" onClick={() => setLinkOpen(linkOpen === l.id ? null : l.id)}>{linkOpen === l.id ? 'Close' : 'Categories…'}</button>}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                <p className="muted small">By host view, a link takes this view's categories and classes. Custom gives it its own here: hide the rebar of a structural link, or the finishes of an architectural one, in this view alone.</p>
                {custom && (
                  <div className="vv-link-custom" data-testid="vv-link-custom">
                    <h4>{custom.name}<span className="muted small"> · its categories in this view</span></h4>
                    {categoryTable(custom.id)}
                    {custom.classes.length > 0 && <><h4>{custom.name}<span className="muted small"> · its IFC classes</span></h4>{classTable(custom.classes, custom.id)}</>}
                  </div>
                )}
              </>
            )
          )}
        </div>
        <footer className="dialog-foot">{error && <p className="error-text" role="alert">{error}</p>}<span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** The Filters tab: the view's filters in order (shown or hidden), and the project's filters with their rules. */
function FiltersTab({ target, categories, rows, defs, counts, editing, setEditing }: { target: string; categories: string[]; rows: FilterRow[]; defs: ViewFilter[]; counts: Record<string, number>; editing: string | null; setEditing: (id: string | null) => void }) {
  const save = (next: FilterRow[]) => void execute('view.graphics', { id: target, filters: next.map((r) => Object.fromEntries(Object.entries(r).filter(([, x]) => x !== undefined))) });
  const used = rows.filter((r) => defs.some((d) => d.id === r.id));
  const put = (id: string, patch: Partial<FilterRow>) => save(used.map((x) => (x.id === id ? { ...x, ...patch } : x)));
  const unused = defs.filter((d) => !used.some((r) => r.id === d.id));
  const edited = defs.find((d) => d.id === editing);
  const setRules = (f: ViewFilter, rules: FilterRule[]) => void execute('viewFilter.update', { id: f.id, rules });
  return (
    <div className="vv-filters">
      {used.length === 0 ? <p className="muted small">This view uses no filter. Add one of the project's below, or make a new one.</p> : (
        <table className="layer-table">
          <thead><tr><th>Shown</th><th>Filter</th><th>Elements</th><th>Colour</th><th>Pattern</th><th>Halftone</th><th title="In 3D: 0 opaque, 100 clear">Transparency</th><th /></tr></thead>
          <tbody>
            {used.map((r, i) => {
              const d = defs.find((x) => x.id === r.id)!;
              return (
                <tr key={r.id} data-filter={d.name}>
                  <td><input type="checkbox" aria-label={d.name} checked={!r.hidden} onChange={(e) => save(used.map((x) => (x.id === r.id ? { ...x, hidden: !e.target.checked } : x)))} /></td>
                  <td><button type="button" className="link-button" onClick={() => setEditing(editing === d.id ? null : d.id)}>{d.name}</button></td>
                  <td className="muted small">{counts[d.id] ?? '…'}</td>
                  <td><input type="color" aria-label="Colour" value={r.color ?? '#000000'} onChange={(e) => put(r.id, { color: e.target.value })} />{r.color && <button type="button" className="icon mini" title="Its own colour" onClick={() => put(r.id, { color: undefined })}><X size={10} /></button>}</td>
                  <td><select aria-label="Pattern" value={r.pattern ?? ''} onChange={(e) => put(r.id, { pattern: e.target.value || undefined })}>{PATTERNS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                  <td><input type="checkbox" aria-label="Halftone" checked={!!r.halftone} onChange={(e) => put(r.id, { halftone: e.target.checked || undefined })} /></td>
                  <td><input type="number" className="vv-number" aria-label="Transparency" min={0} max={95} step={5} value={r.transparency ?? 0} onChange={(e) => put(r.id, { transparency: Math.max(0, Math.min(95, Number(e.target.value) || 0)) || undefined })} /></td>
                  <td className="row-tools">
                    <button type="button" className="icon mini" title="Up: before the others" disabled={i === 0} onClick={() => save(moveRow(used, i, -1))}><ArrowUp size={12} /></button>
                    <button type="button" className="icon mini" title="Down" disabled={i === used.length - 1} onClick={() => save(moveRow(used, i, 1))}><ArrowDown size={12} /></button>
                    <button type="button" className="icon mini" title="Remove from this view" onClick={() => save(used.filter((x) => x.id !== r.id))}><X size={12} /></button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
      <div className="row vv-set">
        {unused.length > 0 && (
          <select aria-label="Add a filter" value="" onChange={(e) => { if (e.target.value) save([...used, { id: e.target.value, hidden: true }]); }}>
            <option value="">Add a filter…</option>
            {unused.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        )}
        <button type="button" className="btn" data-testid="vv-new-filter" onClick={() => void execute('viewFilter.create', { name: 'New filter', rules: [{ field: 'category', op: 'equals', value: 'walls' }] }).then((x) => { const r = x as { id?: string } | null; if (r?.id) { setEditing(r.id); save([...used, { id: r.id, hidden: true }]); } })}><Plus size={13} />New filter</button>
        <span className="muted small">The first filter in the list that takes an element decides whether it is shown.</span>
      </div>
      {edited && (
        <div className="vv-link-custom" data-testid="vv-filter-editor">
          <div className="row vv-set">
            <input aria-label="Filter name" key={edited.name} defaultValue={edited.name} onBlur={(e) => { const n = e.target.value.trim(); if (n && n !== edited.name) void execute('viewFilter.update', { id: edited.id, name: n }); }} />
            <select aria-label="Match" value={edited.match} onChange={(e) => void execute('viewFilter.update', { id: edited.id, match: e.target.value })}>
              <option value="all">all the rules</option><option value="any">any of the rules</option>
            </select>
            <span className="muted small">{counts[edited.id] ?? '…'} elements</span>
            <span className="grow" />
            <button type="button" className="btn" title="Delete: the views that use it forget it" onClick={() => { if (window.confirm(`Delete the filter ${edited.name}?`)) { void execute('viewFilter.delete', { id: edited.id }); setEditing(null); } }}><Trash2 size={13} />Delete</button>
          </div>
          <table className="layer-table vv-rules">
            <thead><tr><th>Field</th><th>Parameter</th><th>Operator</th><th>Value</th><th /></tr></thead>
            <tbody>
              {edited.rules.map((rule, i) => {
                const put = (patch: Partial<FilterRule>) => setRules(edited, edited.rules.map((x, j) => (j === i ? { ...x, ...patch } : x)));
                return (
                  <tr key={i}>
                    <td><select value={rule.field} onChange={(e) => put({ field: e.target.value, ...(e.target.value === 'param' ? { name: rule.name ?? 'Mark' } : { name: undefined }) })}>{FILTER_FIELDS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                    <td>{rule.field === 'param' && <input aria-label="Parameter" key={rule.name} defaultValue={rule.name ?? ''} placeholder="Fire rating, Pset_WallCommon.IsExternal…" onBlur={(e) => { const n = e.target.value.trim(); if (n && n !== rule.name) put({ name: n }); }} />}</td>
                    <td><select value={rule.op} onChange={(e) => put({ op: e.target.value })}>{FILTER_OPS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                    <td>{rule.op !== 'hasValue' && rule.op !== 'hasNoValue' && (rule.field === 'category'
                      ? <select aria-label="Value" value={rule.value ?? ''} onChange={(e) => put({ value: e.target.value })}>{categories.map((c) => <option key={c} value={c}>{categoryName(c)}</option>)}</select>
                      : <input aria-label="Value" key={rule.value} defaultValue={rule.value ?? ''} onBlur={(e) => { if (e.target.value !== (rule.value ?? '')) put({ value: e.target.value }); }} />)}</td>
                    <td><button type="button" className="icon mini" title="Remove the rule" onClick={() => setRules(edited, edited.rules.filter((_, j) => j !== i))}><X size={12} /></button></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <button type="button" className="btn small" onClick={() => setRules(edited, [...edited.rules, { field: 'type', op: 'contains', value: '' }])}><Plus size={12} />Add a rule</button>
        </div>
      )}
    </div>
  );
}

/** The 3D shows its Visibility / Graphics: what it hides, a link halftone in it, the parts its subcategories hide. */
export function Visibility3D() {
  const nodes = useSession((s) => s.nodes);
  const view3dId = useUi((s) => s.view3dId);
  useEffect(() => {
    let live = true;
    void import('../../platform/looks').then(({ setLooks }) =>
      query<Record<string, { hidden?: boolean; opacity?: number; parts?: Record<string, { hidden?: boolean }> }>>('view.looks3d', { id: view3dId ?? '3d' }).then((looks) => { if (live && looks) setLooks('visibility', looks); }));
    return () => { live = false; };
  }, [nodes, view3dId]);
  return null;
}

/** The colour modes a cloud offers, as the tab names them. */
const CLOUD_COLORS: [string, string][] = [['', 'as its link'], ['rgb', 'RGB'], ['intensity', 'Intensity'], ['elevation', 'Elevation'], ['classification', 'Classification']];

/** A row for a cloud cleared of what says nothing. */
export function cloudRow(cur: CloudRow, patch: Partial<CloudRow>): CloudRow | null {
  const next: CloudRow = { ...cur, ...patch };
  if (!next.hidden) delete next.hidden;
  if (!next.halftone) delete next.halftone;
  if (!next.colorMode) delete next.colorMode;
  if (!next.hiddenClasses?.length) delete next.hiddenClasses;
  if (!next.slice) delete next.slice;
  return Object.keys(next).length ? next : null;
}

/**
 * The Point clouds tab (decision 396): each cloud in this view, shown, halftone, its colours, the slice a plan or a section
 * draws (cm), and its ASPRS classes, each shown or hidden (ground, vegetation, building, noise…).
 */
function PointCloudsTab({ target, clouds, rows, in3d }: { target: string; clouds: CloudInfo[]; rows: Record<string, CloudRow>; in3d: boolean }) {
  const [open, setOpen] = useState<string | null>(null);
  const put = (id: string, patch: Partial<CloudRow>) => void execute('view.graphics', { id: target, pointCloud: id, value: cloudRow(rows[id] ?? {}, patch) });
  if (clouds.length === 0) return <p className="muted small">The project has no point cloud: File → Link point cloud….</p>;
  const shown = clouds.find((c) => c.id === open);
  return (
    <div className="vv-clouds" data-testid="vv-clouds">
      <table className="layer-table">
        <thead><tr><th>Shown</th><th>Point cloud</th><th>Halftone</th><th>Colours</th>{!in3d && <th title="How thick a slice a plan or a section draws round its cut">Slice (cm)</th>}<th /></tr></thead>
        <tbody>
          {clouds.map((c) => {
            const r = rows[c.id] ?? {};
            return (
              <tr key={c.id} data-cloud={c.name}>
                <td><input type="checkbox" aria-label={c.name} checked={!r.hidden} onChange={(e) => put(c.id, { hidden: !e.target.checked })} /></td>
                <td>{c.name}{!c.visible && <span className="muted small"> · hidden everywhere (Manage → Links)</span>}</td>
                <td><input type="checkbox" aria-label="Halftone" checked={!!r.halftone || c.halftone} disabled={c.halftone} title={c.halftone ? 'Halftone everywhere (Manage → Links)' : undefined} onChange={(e) => put(c.id, { halftone: e.target.checked })} /></td>
                <td>
                  <select aria-label="Colours" value={r.colorMode ?? ''} onChange={(e) => put(c.id, { colorMode: e.target.value || undefined })}>
                    {CLOUD_COLORS.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                  </select>
                </td>
                {!in3d && <td><input type="number" className="num" min={1} max={10000} step={1} style={{ width: 64 }} value={Math.round((r.slice ?? 0.1) * 100)} onChange={(e) => { const cm = Number(e.target.value); if (cm > 0) put(c.id, { slice: cm === 10 ? undefined : cm / 100 }); }} /></td>}
                <td>{c.classes.length > 1 && <button type="button" className="btn small" data-cloud-classes={c.name} onClick={() => setOpen(open === c.id ? null : c.id)}>{open === c.id ? 'Close' : 'Classes…'}</button>}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
      {shown && (
        <table className="layer-table vv-cloud-classes" data-testid="vv-cloud-classes">
          <thead><tr><th>Shown</th><th>Class</th><th /></tr></thead>
          <tbody>
            {shown.classes.map((k) => {
              const hidden = rows[shown.id]?.hiddenClasses ?? [];
              return (
                <tr key={k} data-class={k}>
                  <td><input type="checkbox" aria-label={CLASS_NAMES[k] ?? `Class ${k}`} checked={!hidden.includes(k)}
                    onChange={(e) => put(shown.id, { hiddenClasses: e.target.checked ? hidden.filter((x) => x !== k) : [...hidden, k].sort((a, b) => a - b) })} /></td>
                  <td>{CLASS_NAMES[k] ?? `Class ${k}`} <span className="muted small">({k})</span></td>
                  <td><span className="swatch" style={{ background: CLASS_COLORS[k] ?? '#9e9e9e', display: 'inline-block', width: 14, height: 14, borderRadius: 3 }} /></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
      <p className="muted small">A plan draws the slice round its cut, a section the slice just behind its plane, an elevation every point it faces, as dots you can trace over and snap to. Manage → Links hides a cloud everywhere.</p>
    </div>
  );
}
