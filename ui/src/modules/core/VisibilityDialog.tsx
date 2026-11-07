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
import { ChevronDown, ChevronRight, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { frontView } from '../../viewport/frontView';
import { PendingDialog } from './PendingDialog';

export interface Row { hidden?: boolean; lineColor?: string; lineWeight?: string; fillColor?: string; cutHatch?: string; sub?: Record<string, Row> }
interface Rules { categories?: Record<string, Row>; classes?: Record<string, Row> }
export interface LinkRow extends Rules { display?: 'host' | 'custom'; hidden?: boolean; halftone?: boolean }
interface Graphics extends Rules { links?: Record<string, LinkRow> }
interface LinkInfo { id: string; name: string; kind: string; visible: boolean; halftone: boolean; classes: string[] }
export interface Visibility {
  graphics?: Graphics | null; graphicsSet?: string | null; fromStyle?: boolean; style?: string;
  sets: { id: string; name: string }[]; categories: string[]; subcategories?: Record<string, string[]>;
  /** Hidden until the graphics show them (rooms, in 3D). */ hiddenByDefault?: string[];
  links?: LinkInfo[];
}

/** The ISO 128 pen series (mm), as the engine reads a category's weight; the older names still show when a project has them. */
const WEIGHTS = ['', '0.13', '0.18', '0.25', '0.35', '0.5', '0.7', '1.0'];
const LEGACY: Record<string, string> = { heavy: '0.7', medium: '0.5', light: '0.25', hairline: '0.13' };

/** A category's or a subcategory's name as the dialog shows it. */
export const CATEGORY_NAMES: Record<string, string> = {
  walls: 'Walls', slabs: 'Slabs', roofs: 'Roofs', columns: 'Columns', beams: 'Beams', doors: 'Doors', windows: 'Windows', stairs: 'Stairs',
  railings: 'Railings', spaces: 'Spaces', furniture: 'Furniture', topography: 'Topography', other: 'Other', linework: 'Linework', voids: 'Voids',
  entourage: 'Entourage', coverings: 'Coverings', rebar: 'Rebar', formwork: 'Formwork', scaffolds: 'Scaffolds', mep: 'MEP',
  steelConnections: 'Steel connections', transport: 'Transport', shading: 'Shading',
  core: 'Core', finishes: 'Finishes', insulation: 'Insulation', membranes: 'Membranes', otherLayers: 'Other layers', reveals: 'Reveals',
};
export const categoryName = (c: string) => CATEGORY_NAMES[c] ?? (c ? c[0].toUpperCase() + c.slice(1) : c);

type Tab = 'categories' | 'links' | 'classes';

/** A row cleared of what says nothing (an empty field, shown when shown is the default). */
export function clean(next: Row, keepsShown: boolean): Row {
  for (const k of Object.keys(next) as (keyof Row)[])
    if (next[k] === '' || (next[k] === false && !(k === 'hidden' && keepsShown)) || next[k] == null || (k === 'sub' && !Object.keys(next.sub ?? {}).length)) delete next[k];
  return next;
}

export function VisibilityDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [target, setTarget] = useState<string | null>(null);
  const [v, setV] = useState<Visibility | null>(null);
  const [hatches, setHatches] = useState<string[]>([]);
  const [tab, setTab] = useState<Tab>('categories');
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
    const keepsShown = !sub && !link && (v.hiddenByDefault ?? []).includes(c);
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
      <thead><tr><th>Shown</th><th>Category</th>{!link && <><th>Line colour</th><th>Line weight</th><th>Fill</th><th>Cut hatch</th></>}</tr></thead>
      <tbody>
        {v.categories.map((c) => {
          const r = rulesOf(link).categories?.[c] ?? {};
          const subs = v.subcategories?.[c] ?? [];
          const key = `${link ?? ''}:${c}`;
          const shown = !(r.hidden ?? (!link && (v.hiddenByDefault ?? []).includes(c)));
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
                    <td><input type="color" value={r.fillColor ?? '#ffffff'} onChange={(e) => setCategory(c, { fillColor: e.target.value })} />{r.fillColor && <button type="button" className="icon mini" title="No fill of its own" onClick={() => setCategory(c, { fillColor: '' })}><X size={10} /></button>}</td>
                    <td><select value={r.cutHatch ?? ''} onChange={(e) => setCategory(c, { cutHatch: e.target.value })}><option value="">the materials'</option>{hatches.map((h) => <option key={h} value={h}>{h}</option>)}</select></td>
                  </>
                )}
              </tr>
              {open[key] && subs.map((s) => {
                const sr = r.sub?.[s] ?? {};
                return (
                  <tr key={s} className="vv-sub" data-sub={`${c}.${s}`}>
                    <td><input type="checkbox" aria-label={`${categoryName(c)} ▸ ${categoryName(s)}`} checked={shown && !sr.hidden} disabled={!shown} onChange={(e) => setCategory(c, { hidden: !e.target.checked }, link, s)} /></td>
                    <td className="vv-sub-name">{categoryName(s)}</td>
                    {!link && <td colSpan={4} />}
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
            {view && (
              <label className="row"><span className="muted small">Hidden lines</span>
                <select value={String(view.props.hiddenLines ?? 'auto')} onChange={(e) => void execute('element.update', { id: target, attrs: { hiddenLines: e.target.value } })}>
                  <option value="auto">by the view's kind</option><option value="show">shown</option><option value="hide">hidden</option>
                </select>
              </label>
            )}
          </div>
          <div className="tabs-inline" role="tablist">
            <button type="button" role="tab" className={tab === 'categories' ? 'on' : ''} onClick={() => setTab('categories')}>Model categories</button>
            <button type="button" role="tab" className={tab === 'links' ? 'on' : ''} onClick={() => setTab('links')}>Links</button>
            <button type="button" role="tab" className={tab === 'classes' ? 'on' : ''} onClick={() => setTab('classes')}>IFC classes</button>
          </div>
          {tab === 'categories' && categoryTable()}
          {tab === 'classes' && classTable(classes)}
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
