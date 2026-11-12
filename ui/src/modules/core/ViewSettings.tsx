/**
 * A view's drawing settings in one place (docs/view-styles.md): its style
 * (Architectural, Structural, MEP or one of the project's own), and over it
 * what the view sets itself: its partial structure display (decision 500: the
 * entire model, without the finishes, the cores only, the load-bearing
 * elements' cores only; a style says it for its plans, its sections and
 * elevations, and its 3D views), whether one material goes on from
 * element to element without a line, whether a plan draws what is above its
 * cut, whether a plan draws the 2D an IFC file gave its elements instead of
 * their cut, whether the openings' voids are drawn (decision 290), how people, cars and plants draw (decision 341); with a plan's cut plane and range (View range) and the storeys it
 * shows. A 3D view has its discipline, style and layers too (the walls and
 * floors without their finishes, or their cores alone) and the storeys it
 * shows. In the view's properties and as View → View settings for the view in
 * front. Manage → View styles edits the styles themselves.
 */

import { setDisplay, useDisplay } from '../../app/display';
import { Fragment, useEffect, useState } from 'react';
import { Copy, Plus, RotateCcw, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { DisciplinePicker } from './DisciplinePicker';
import { t } from '../../i18n';
import { ENTOURAGE_FILLS, ENTOURAGE_MODES, FINISH_FIELDS, LOD_HELP, LODS, STRUCTURE_MODES, structureLabel, styleStructure, useViewStyles, type EntourageMode, type LayerMode, type ViewStyle } from './viewStyles';
import { frontView } from '../../viewport/frontView';
import { storeys } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { PlanStoreysEditor } from './PlanStoreys';
import { Storeys3DEditor } from './Storeys3D';
import { view3dNode } from '../../viewport/storeys3d';
import { SitePlanFields } from './SitePlans';
import { SectionFields } from './sections';
import { shadowsAllowed, ViewShadowFields } from './ViewShadowFields';

interface ViewStyleState { style: ViewStyle; named: boolean; resolved: ViewStyle; overrides: string[] }

/** A partial structure display choice: the presets, each with what it draws (decision 500). */
function StructureSelect({ value, onPick, testId }: { value: LayerMode; onPick: (m: LayerMode) => void; testId?: string }) {
  return (
    <select data-testid={testId} value={value} onChange={(e) => onPick(e.target.value as LayerMode)}>
      {STRUCTURE_MODES.map(([k, l, d]) => <option key={k} value={k} title={d}>{l}</option>)}
    </select>
  );
}

const STRUCTURE_HELP = 'Which layers of walls, floors and roofs the view draws, as its style says for its kind of view (plans; sections and elevations; 3D) unless it sets its own; also in the view\'s corner, beside its scale. A plan rules what its cut plane cuts: the floors seen under it show as they are. A layer is a finish, insulation or membrane by its own category in its type, else its material\'s.';

/** The editor of one view's settings. */
function ViewSettingsEditor({ view }: { view: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const styles = useViewStyles();
  // The settings read for a view, kept with its id: another view's (the panel moved on, an answer late) are never shown or sent.
  const [read, setRead] = useState<{ id: string; state: ViewStyleState } | null>(null);
  useEffect(() => {
    let live = true;
    let again: ReturnType<typeof setTimeout> | undefined;
    // No answer (the engine busy past the wait): what was read stays and it asks again, so the panel never stays at "…".
    const ask = () => void query<ViewStyleState>('view.style', { id: view.id }).then((r) => {
      if (!live) return;
      if (r) setRead({ id: view.id, state: r });
      else again = setTimeout(ask, 1000);
    });
    ask();
    return () => { live = false; clearTimeout(again); };
  }, [view.id, nodes]);
  const state = read?.id === view.id ? read.state : null;
  if (!state) return <p className="muted">…</p>;
  const set = (patch: Record<string, unknown>) => void execute('view.setStyle', { id: view.id, ...patch });
  const own = (f: string) => state.overrides.includes(f);
  // A site plan sees the whole model from above: no cut, range, reference level or storeys of its own; its north arrow, grid and scale bar instead.
  const site = view.props.kind === 'plan' && typeof view.props.site === 'string';
  const plan = view.props.kind === 'plan' && !site;
  const threeD = view.props.kind === '3d';
  const section = view.props.kind === 'section';
  const Reset = ({ field }: { field: string }) => own(field)
    ? <button type="button" className="icon mini" title="Back to the style's" onClick={() => set({ [field]: null })}><RotateCcw size={11} /></button>
    : <span className="muted small"> style</span>;
  return (
    <div className="view-settings">
      <dl className="fields">
        <dt title="The browser files the view under this discipline's set; without a style of its own the view draws as the discipline's style says">Discipline</dt>
        <dd><DisciplinePicker node={view} onPick={view.id === '3d' ? (discipline) => set({ discipline }) : undefined} /></dd>
        <dt title="The style gives every setting below; what the view sets itself wins over it">Style</dt>
        <dd>
          <select value={state.named ? state.style.id : ''} onChange={(e) => set({ style: e.target.value || null })}>
            <option value="">by the discipline: {state.style.name}</option>
            {styles.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
          <button type="button" className="link small" onClick={() => useUi.setState({ dialog: 'viewStyles' })}>Edit styles…</button>
        </dd>
        <dt title={LOD_HELP}>Level of development</dt>
        <dd>
          <select data-testid="view-detail" value={state.resolved.detail ?? 350} onChange={(e) => set({ detail: Number(e.target.value) })}>
            {LODS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
          </select><Reset field="detail" />
        </dd>
        <dt title={STRUCTURE_HELP}>Partial structure display</dt>
        <dd>
          <StructureSelect testId="view-structure-setting" value={state.resolved.layers} onPick={(m) => set({ layers: m === styleStructure(state.style, view.props.kind as string | undefined) ? null : m })} /><Reset field="layers" />
        </dd>
        {(plan || section || view.props.kind === 'elevation') && (
          <>
            <dt className="muted small" title="Over the partial structure display, element kind by element kind: whether this view draws the walls' and the floors' finish layers (and the rooms' wall and floor finishes)">By element kind</dt>
            <dd className="muted small">advanced</dd>
          </>
        )}
        {(plan || section || view.props.kind === 'elevation') && FINISH_FIELDS.filter((f) => f.plan === plan).map((f) => (
          <Fragment key={f.key}>
            <dt title={f.title}>{f.label}</dt>
            <dd><input type="checkbox" data-testid={`view-${f.key}`} checked={state.resolved[f.key] !== false} onChange={(e) => set({ [f.key]: e.target.checked })} /><Reset field={f.key} /></dd>
          </Fragment>
        ))}
        {!threeD && (
          <>
            <dt title="One material goes on from one element into the next without a line (walls, beams, footings): on by default">Join materials</dt>
            <dd><input type="checkbox" checked={state.resolved.joins} onChange={(e) => set({ joins: e.target.checked })} /><Reset field="joins" /></dd>
          </>
        )}
        {!threeD && (
          <>
            <dt title="How people, cars and plants draw in plans, elevations and sections: outlines (simple symbols and silhouettes), abstract (circles and boxes), detailed (every line of their symbols and bodies) or not at all; their bodies stay in 3D">Entourage in 2D</dt>
            <dd>
              <select data-testid="view-entourage" value={state.resolved.entourage ?? 'outline'} onChange={(e) => set({ entourage: e.target.value })}>
                {ENTOURAGE_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
              </select><Reset field="entourage" />
            </dd>
          </>
        )}
        {(view.props.kind === 'elevation' || section) && (state.resolved.entourage ?? 'outline') !== 'none' && (state.resolved.entourage ?? 'outline') !== 'detailed' && (
          <>
            <dt title="The fill of the silhouettes elevations and sections draw for people, cars and plants">Silhouette fill</dt>
            <dd>
              <select data-testid="view-entourage-fill" value={state.resolved.entourageFill ?? ''} onChange={(e) => set({ entourageFill: e.target.value || 'none' })}>
                {ENTOURAGE_FILLS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
                {state.resolved.entourageFill && !ENTOURAGE_FILLS.some(([k]) => k === state.resolved.entourageFill) && <option value={state.resolved.entourageFill}>{state.resolved.entourageFill}</option>}
              </select><Reset field="entourageFill" />
            </dd>
          </>
        )}
        {(view.props.kind === 'elevation' || section) && (
          <>
            <dt title="Elevations and sections hatch the faces they see with their materials' elevation hatch (Manage → Materials), where nothing nearer hides them">Surface hatches</dt>
            <dd><input type="checkbox" data-testid="view-surface-hatch" checked={state.resolved.surfaceHatch !== false} onChange={(e) => set({ surfaceHatch: e.target.checked })} /><Reset field="surfaceHatch" /></dd>
          </>
        )}
        {plan && (
          <>
            <dt title="What lies above the cut plane, up to the range's top, drawn dashed">Overhead</dt>
            <dd><input type="checkbox" checked={state.resolved.overhead} onChange={(e) => set({ overhead: e.target.checked })} /><Reset field="overhead" /></dd>
            <dt title="Elements from an IFC file that bring their own plan drawing (a footprint, a door's swing, a fixture's symbol) draw it instead of their cut; off: their 3D cut">IFC 2D symbols</dt>
            <dd><input type="checkbox" checked={state.resolved.ifc2d !== false} onChange={(e) => set({ ifc2d: e.target.checked })} /><Reset field="ifc2d" /></dd>
            <dt title="Plans hatch the rooms' floor finishes as their types say (a plan seen from below, their ceilings)">Floor finish hatches</dt>
            <dd><input type="checkbox" data-testid="view-finish-hatch" checked={state.resolved.finishHatch !== false} onChange={(e) => set({ finishHatch: e.target.checked })} /><Reset field="finishHatch" /></dd>
            <dt title="From above: a plan. From below: a reflected ceiling plan, what lies above the cut up to the range's top (soffits, beams, formwork), drawn the way the plan lies">Seen from</dt>
            <dd>
              <select value={view.props.look === 'up' ? 'up' : 'down'} onChange={(e) => void execute('view.setRange', { id: view.id, look: e.target.value })}>
                <option value="down">above (plan)</option><option value="up">below (ceiling plan)</option>
              </select>
            </dd>
            <dt>Cut plane and depth</dt>
            <dd><button type="button" className="btn small" onClick={() => useUi.setState({ dialog: 'viewRange' })}>View range…</button></dd>
            <dt title="Another level's plan drawn under this one on screen, faded, never picked but snapped to; it does not print">Reference level</dt>
            <dd>
              <select value={typeof view.props.underlay === 'string' ? view.props.underlay : ''} onChange={(e) => void execute('view.setUnderlay', { id: view.id, storeyId: e.target.value || null })}>
                <option value="">none</option>
                {storeys(useSession.getState()).filter((st) => st.id !== view.props.storeyId).reverse().map((st) => {
                  const dz = Number(st.props.elevation ?? 0) - Number(nodes.get(String(view.props.storeyId))?.props.elevation ?? 0);
                  return <option key={st.id} value={st.id}>{st.name} ({dz > 0 ? 'above' : 'below'})</option>;
                })}
              </select>
            </dd>
          </>
        )}
        {!threeD && !site && (
          <>
            <dt title="Each opening's void drawn where the view cuts or sees it, crossed, with its mark (V1, V2…), as a coordination drawing shows provisions for voids; sheets print it">Opening voids</dt>
            <dd><input type="checkbox" data-testid="view-voids" checked={state.resolved.voids === true} onChange={(e) => set({ voids: e.target.checked })} /><Reset field="voids" /></dd>
            {state.resolved.voids && (
              <>
                <dt title="Under each void's mark its width × height (a round one's diameter) and a wall opening's bottom level">Void sizes</dt>
                <dd><input type="checkbox" checked={state.resolved.voidSizes !== false} onChange={(e) => set({ voidSizes: e.target.checked })} /><Reset field="voidSizes" /></dd>
              </>
            )}
          </>
        )}
        {threeD && <Drafts3dField />}
        {threeD && <Dxf3dField />}
        {threeD && <Navigation3dFields />}
        <DrillField />
        {site && <SitePlanFields view={view} />}
        {shadowsAllowed(view) && <ViewShadowFields view={view} />}
        {section && <SectionFields view={view} />}
        <dt title="What this view shows and how, by category, class and element (VV)">Graphics</dt>
        <dd><button type="button" className="btn small" onClick={() => useUi.setState({ dialog: 'visibility' })}>Visibility / Graphics…</button></dd>
      </dl>
      {plan && (
        <>
          <p className="eyebrow">Storeys shown</p>
          <PlanStoreysEditor view={view} />
        </>
      )}
      {threeD && (
        <>
          <p className="eyebrow">Storeys shown</p>
          <Storeys3DEditor viewId={view.id} />
        </>
      )}
    </div>
  );
}

/** In a view's properties (a plan, a section, an elevation, a 3D view: with its layers and the storeys it shows). */
export function ViewSettingsSection({ node }: { node: NodeDto }) {
  return <ViewSettingsEditor view={node} />;
}

/** View → View settings: the view in front (a plan's own view is made when its storey has none). */
export function ViewSettingsDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [id, setId] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    void (async () => {
      const ui = useUi.getState();
      // A 3D view in front: its own settings ({3D}'s on the project).
      if (ui.view === '3d') { setId(ui.view3dId ?? '3d'); return; }
      setId(await frontView());
    })();
  }, []);
  const view = id === '3d' ? view3dNode(nodes) : id ? nodes.get(id) : undefined;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="View settings" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">View settings{view ? ` · ${view.name}` : ''}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">{view ? <ViewSettingsEditor view={view} /> : <p className="muted">…</p>}</div>
        <footer className="dialog-foot">{error && <p className="error-text" role="alert">{error}</p>}<span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** Manage → View styles: the project's styles, each a row; a view takes one by name or by its discipline. */
export function ViewStylesDialog() {
  const styles = useViewStyles();
  // The project's graphics sets, which a style can name as its views' (decision 391).
  const nodes = useSession((s) => s.nodes);
  const [sets, setSets] = useState<{ id: string; name: string }[]>([]);
  useEffect(() => { void query<{ sets: { id: string; name: string }[] }>('view.visibility', { id: '3d' }).then((r) => r && setSets(r.sets)); }, [nodes]);
  const error = useSession((s) => s.error);
  const close = () => useUi.setState({ dialog: null });
  const update = (id: string, patch: Partial<ViewStyle>) => void execute('viewStyle.update', { id, ...patch });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="View styles" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">View styles</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <table className="layer-table view-styles">
            <thead><tr><th>Name</th><th title="The views of this discipline take this style unless they name another">Discipline</th><th title={LOD_HELP}>Level of development</th><th title="Partial structure display of the style's plans: which layers of walls, floors and roofs they draw">Structure in plans</th><th title="Partial structure display of the style's sections and elevations">Structure in sections</th><th title="Partial structure display of the style's 3D views and other views">Structure in 3D</th><th title="One material goes on from element to element without a line">Join materials</th><th title="Plans draw what lies above their cut">Overhead</th><th title="Plans draw the 2D an IFC file gave its elements instead of their cut">IFC 2D symbols</th><th title="Plans, sections and elevations draw the openings' voids, crossed, with their marks">Opening voids</th><th title="Under each void's mark its width × height (a round one's diameter) and a wall opening's bottom level">Void sizes</th><th title="Plans hatch the rooms' floor finishes as their types say (a plan seen from below, their ceilings)">Floor finish hatches</th><th title="Elevations and sections hatch the faces they see with their materials' elevation hatch (Manage → Materials), where nothing nearer hides them">Surface hatches</th>{FINISH_FIELDS.map((f) => <th key={f.key} title={f.title}>{f.column}</th>)}<th title="How people, cars and plants draw in plans, elevations and sections: outlines (simple symbols and silhouettes), abstract (circles and boxes), detailed (every line of their symbols and bodies) or not at all; their bodies stay in 3D">Entourage in 2D</th><th title="The fill of the silhouettes elevations and sections draw for people, cars and plants">Silhouette fill</th><th title="The Visibility / Graphics set its views take when they have none of their own, as a view template">Graphics set</th><th /></tr></thead>
            <tbody>
              {styles.map((s) => (
                <tr key={s.id}>
                  <td><input key={s.name} defaultValue={s.name} onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== s.name) update(s.id, { name: v }); }} /></td>
                  <td><input key={s.discipline ?? ''} defaultValue={s.discipline ?? ''} placeholder="none" onBlur={(e) => { const v = e.target.value.trim(); if (v !== (s.discipline ?? '')) update(s.id, { discipline: v || null }); }} /></td>
                  <td><select data-testid="style-detail" value={s.detail ?? 350} onChange={(e) => update(s.id, { detail: Number(e.target.value) })}>{LODS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                  <td><StructureSelect testId="style-structure-plan" value={styleStructure(s, 'plan')} onPick={(m) => update(s.id, { layersPlan: m })} /></td>
                  <td><StructureSelect testId="style-structure-section" value={styleStructure(s, 'section')} onPick={(m) => update(s.id, { layersSection: m })} /></td>
                  <td><StructureSelect testId="style-structure-3d" value={s.layers} onPick={(m) => update(s.id, { layers: m })} /></td>
                  <td><input type="checkbox" checked={s.joins} onChange={(e) => update(s.id, { joins: e.target.checked })} /></td>
                  <td><input type="checkbox" checked={s.overhead} onChange={(e) => update(s.id, { overhead: e.target.checked })} /></td>
                  <td><input type="checkbox" checked={s.ifc2d !== false} onChange={(e) => update(s.id, { ifc2d: e.target.checked })} /></td>
                  <td><input type="checkbox" checked={s.voids === true} onChange={(e) => update(s.id, { voids: e.target.checked })} /></td>
                  <td><input type="checkbox" checked={s.voidSizes !== false} disabled={!s.voids} onChange={(e) => update(s.id, { voidSizes: e.target.checked })} /></td>
                  <td><input type="checkbox" checked={s.finishHatch !== false} onChange={(e) => update(s.id, { finishHatch: e.target.checked })} /></td>
                  <td><input type="checkbox" checked={s.surfaceHatch !== false} onChange={(e) => update(s.id, { surfaceHatch: e.target.checked })} /></td>
                  {FINISH_FIELDS.map((f) => <td key={f.key}><input type="checkbox" data-testid={`style-${f.key}`} checked={s[f.key] !== false} onChange={(e) => update(s.id, { [f.key]: e.target.checked })} /></td>)}
                  <td><select value={s.entourage ?? 'outline'} onChange={(e) => update(s.id, { entourage: e.target.value as EntourageMode })}>{ENTOURAGE_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                  <td><select value={s.entourageFill ?? ''} onChange={(e) => update(s.id, { entourageFill: e.target.value || null })}>{ENTOURAGE_FILLS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                  <td><select data-testid="style-graphics-set" value={s.graphicsSet ?? ''} onChange={(e) => update(s.id, { graphicsSet: e.target.value || null })}><option value="">none</option>{sets.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select></td>
                  <td className="row-tools">
                    <button type="button" className="icon mini" title="Duplicate" onClick={() => void execute('viewStyle.create', { from: s.id })}><Copy size={12} /></button>
                    <button type="button" className="icon mini" title="Delete: its views take their discipline's style" disabled={styles.length < 2}
                      onClick={() => { if (window.confirm(`Delete the view style ${s.name}? Its views take their discipline's style.`)) void execute('viewStyle.delete', { id: s.id }); }}><Trash2 size={12} /></button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="btn" onClick={() => void execute('viewStyle.create', { name: 'New style' })}><Plus size={14} /> New style</button>
          <p className="muted small">
            {styles.length ? `${t('Partial structure display')}: ${styles.map((s) => `${s.name}: ${t(structureLabel(styleStructure(s, 'plan')))} · ${t(structureLabel(styleStructure(s, 'section')))} · ${t(structureLabel(s.layers))}`).join('; ')}. ` : ''}
            Finishes are the layers whose material's category is Finish (Manage → Materials); without a category, plaster, render, paint, tiles, boards and floor coverings count as finishes.
          </p>
          <p className="muted small">A layer's own category in its type (Wall types, Floor types) wins over its material's. Plans, and sections with elevations, each say whether they draw the walls' finishes and the floors'.</p>
          <p className="muted small">The partial structure display is said for plans, for sections and elevations, and for 3D views; a view sets its own in its settings or from its corner, beside its scale. The finish columns are its advanced detail, element kind by element kind.</p>
        </div>
        <footer className="dialog-foot">{error && <p className="error-text" role="alert">{error}</p>}<span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** The shapes drafted on plans and reference planes, drawn in 3D (View → Drafts in 3D; for the eyes, every 3D view). */
function Drafts3dField() {
  const on = useDisplay((s) => s.drafts3d);
  return (
    <>
      <dt title="The lines, polylines, rectangles, circles and arcs drafted on plans and reference planes, on their planes in 3D (View → Drafts in 3D)">Drafts in 3D</dt>
      <dd><input type="checkbox" data-testid="view-drafts3d" checked={on} onChange={(e) => setDisplay({ drafts3d: e.target.checked })} /></dd>
    </>
  );
}

/** An imported DXF's drawing on its view's plane in 3D (View → DXF in 3D; decision 382). */
function Dxf3dField() {
  const on = useDisplay((s) => s.dxf3d);
  return (
    <>
      <dt title="The drawing a DXF brought into a plan, section or elevation, on that view's plane in 3D, in its layers' colours (View → DXF in 3D)">DXF in 3D</dt>
      <dd><input type="checkbox" data-testid="view-dxf3d" checked={on} onChange={(e) => setDisplay({ dxf3d: e.target.checked })} /></dd>
    </>
  );
}

/** Drill-down picking (platform/assemblies.ts), in every view; for this computer (View → Drill into assemblies). */
function DrillField() {
  const drill = useDisplay((s) => s.drillSelect);
  return (
    <>
      <dt title="A click on a part picks the whole it belongs to first (a group, a steel hall, an assembly), each further click one level deeper; Esc steps back up, Ctrl/Cmd+click picks the part itself (View → Drill into assemblies)">Drill into assemblies</dt>
      <dd><input type="checkbox" data-testid="view-drill-select" checked={drill} onChange={(e) => setDisplay({ drillSelect: e.target.checked })} /></dd>
    </>
  );
}

/** Navigating in 3D (report #89): the wheel to the cursor, the orbit round the selection; for this computer, every 3D view (View → Zoom to cursor, Orbit around selection). */
function Navigation3dFields() {
  const zoom = useDisplay((s) => s.zoomToCursor);
  const orbit = useDisplay((s) => s.orbitSelection);
  return (
    <>
      <dt title="The mouse wheel zooms to the point of the model under the cursor, which stays where it is (View → Zoom to cursor); off, to the middle of the view">Zoom to cursor</dt>
      <dd><input type="checkbox" data-testid="view-zoom-cursor" checked={zoom} onChange={(e) => setDisplay({ zoomToCursor: e.target.checked })} /></dd>
      <dt title="A drag turns the view round the selection's middle, else round the point of the model under the cursor (View → Orbit around selection); off, round the middle of the view">Orbit around selection</dt>
      <dd><input type="checkbox" data-testid="view-orbit-selection" checked={orbit} onChange={(e) => setDisplay({ orbitSelection: e.target.checked })} /></dd>
    </>
  );
}
