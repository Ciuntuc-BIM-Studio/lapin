/**
 * A view's drawing settings in one place (docs/view-styles.md): its style
 * (Architectural, Structural, MEP or one of the project's own), and over it
 * what the view sets itself: which layers of layered elements draw (all,
 * all but the finishes, the cores), whether one material goes on from
 * element to element without a line, whether a plan draws what is above its
 * cut; with a plan's cut plane and range (View range) and the storeys it
 * shows. In the view's properties and as View → View settings for the view in
 * front. Manage → View styles edits the styles themselves.
 */

import { useCallback, useEffect, useState } from 'react';
import { Copy, Plus, RotateCcw, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { DisciplinePicker } from './DisciplinePicker';
import { useViewStyles, type LayerMode, type ViewStyle } from './viewStyles';
import { frontView } from '../../viewport/frontView';
import { storeys } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { PlanStoreysEditor } from './PlanStoreys';

interface ViewStyleState { style: ViewStyle; named: boolean; resolved: ViewStyle; overrides: string[] }

const LAYER_MODES: [LayerMode, string][] = [['all', 'every layer'], ['noFinishes', 'without finishes'], ['core', 'the core only']];
const layerLabel = (m: LayerMode) => LAYER_MODES.find(([k]) => k === m)?.[1] ?? m;

/** The editor of one view's settings. */
export function ViewSettingsEditor({ view }: { view: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const styles = useViewStyles();
  const [state, setState] = useState<ViewStyleState | null>(null);
  const read = useCallback(() => void query<ViewStyleState>('view.style', { id: view.id }).then(setState), [view.id]);
  useEffect(read, [read, nodes]);
  if (!state) return <p className="muted">…</p>;
  const set = (patch: Record<string, unknown>) => void execute('view.setStyle', { id: view.id, ...patch });
  const own = (f: string) => state.overrides.includes(f);
  const plan = view.props.kind === 'plan';
  const Reset = ({ field }: { field: string }) => own(field)
    ? <button type="button" className="icon mini" title="Back to the style's" onClick={() => set({ [field]: null })}><RotateCcw size={11} /></button>
    : <span className="muted small"> style</span>;
  return (
    <div className="view-settings">
      <dl className="fields">
        <dt title="The browser files the view under this discipline's set; without a style of its own the view draws as the discipline's style says">Discipline</dt>
        <dd><DisciplinePicker node={view} /></dd>
        <dt title="The style gives every setting below; what the view sets itself wins over it">Style</dt>
        <dd>
          <select value={state.named ? state.style.id : ''} onChange={(e) => set({ style: e.target.value || null })}>
            <option value="">by the discipline: {state.style.name}</option>
            {styles.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
          <button type="button" className="link small" onClick={() => useUi.setState({ dialog: 'viewStyles' })}>Edit styles…</button>
        </dd>
        <dt title="Which layers of walls, floors and footings draw: the finishes are the layers of materials whose category is Finish">Layers</dt>
        <dd>
          <select value={state.resolved.layers} onChange={(e) => set({ layers: e.target.value })}>
            {LAYER_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
          </select><Reset field="layers" />
        </dd>
        <dt title="One material goes on from one element into the next without a line (walls, beams, footings): on by default">Join materials</dt>
        <dd><input type="checkbox" checked={state.resolved.joins} onChange={(e) => set({ joins: e.target.checked })} /><Reset field="joins" /></dd>
        {plan && (
          <>
            <dt title="What lies above the cut plane, up to the range's top, drawn dashed">Overhead</dt>
            <dd><input type="checkbox" checked={state.resolved.overhead} onChange={(e) => set({ overhead: e.target.checked })} /><Reset field="overhead" /></dd>
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
        <dt title="What this view shows and how, by category, class and element (VV)">Graphics</dt>
        <dd><button type="button" className="btn small" onClick={() => useUi.setState({ dialog: 'visibility' })}>Visibility / Graphics…</button></dd>
      </dl>
      {plan && (
        <>
          <p className="eyebrow">Storeys shown</p>
          <PlanStoreysEditor view={view} />
        </>
      )}
    </div>
  );
}

/** In a view's properties (a plan, a section, an elevation). */
export function ViewSettingsSection({ node }: { node: NodeDto }) {
  if (node.props.kind === '3d') return null;
  return <ViewSettingsEditor view={node} />;
}

/** View → View settings: the view in front (a plan's own view is made when its storey has none). */
export function ViewSettingsDialog() {
  const nodes = useSession((s) => s.nodes);
  const [id, setId] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    void (async () => {
      const ui = useUi.getState();
      if (ui.view === '3d') { useUi.setState({ notice: 'View settings are a drawing\'s: open a plan, a section or an elevation.' }); close(); return; }
      setId(await frontView());
    })();
  }, []);
  const view = id ? nodes.get(id) : undefined;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="View settings" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">View settings{view ? ` · ${view.name}` : ''}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">{view ? <ViewSettingsEditor view={view} /> : <p className="muted">…</p>}</div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** Manage → View styles: the project's styles, each a row; a view takes one by name or by its discipline. */
export function ViewStylesDialog() {
  const styles = useViewStyles();
  const close = () => useUi.setState({ dialog: null });
  const update = (id: string, patch: Partial<ViewStyle>) => void execute('viewStyle.update', { id, ...patch });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="View styles" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">View styles</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <table className="layer-table view-styles">
            <thead><tr><th>Name</th><th title="The views of this discipline take this style unless they name another">Discipline</th><th>Layers</th><th title="One material goes on from element to element without a line">Join materials</th><th title="Plans draw what lies above their cut">Overhead</th><th /></tr></thead>
            <tbody>
              {styles.map((s) => (
                <tr key={s.id}>
                  <td><input key={s.name} defaultValue={s.name} onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== s.name) update(s.id, { name: v }); }} /></td>
                  <td><input key={s.discipline ?? ''} defaultValue={s.discipline ?? ''} placeholder="none" onBlur={(e) => { const v = e.target.value.trim(); if (v !== (s.discipline ?? '')) update(s.id, { discipline: v || null }); }} /></td>
                  <td><select value={s.layers} onChange={(e) => update(s.id, { layers: e.target.value as LayerMode })}>{LAYER_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                  <td><input type="checkbox" checked={s.joins} onChange={(e) => update(s.id, { joins: e.target.checked })} /></td>
                  <td><input type="checkbox" checked={s.overhead} onChange={(e) => update(s.id, { overhead: e.target.checked })} /></td>
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
            {styles.length ? `Layers: ${styles.map((s) => `${s.name} draws ${layerLabel(s.layers)}`).join('; ')}. ` : ''}
            Finishes are the layers whose material's category is Finish (Manage → Materials); without a category, plaster, render, paint, tiles, boards and floor coverings count as finishes.
          </p>
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
