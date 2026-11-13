/**
 * Families in use (docs/plan-user-extensibility.md §5.6): the Family place tool (pick a family, click on the plan, R turns it a
 * quarter, M mirrors it, the values asked for in the bar), the Properties of an object of a family (its family, each parameter's
 * own value, Edit family…), and Make family from sketch… (closed outlines drafted on a view, by the ribbon or a right-click).
 */

import { useEffect, useState } from 'react';
import { Component, FlipHorizontal2, Pencil, RotateCw, X } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import type { ToolDef } from '../../../platform/module';
import { useToolTick } from '../../../platform/modules';
import { notify, select, useUi } from '../../../app/store';
import { activeStorey, execute, useSession } from '../../../engine/session';
import { formatLength, parseLength } from '../../../engine/unitsStore';
import { t as tr } from '../../../i18n';
import { registerDraftMenu } from '../../../viewport/draftMenu';
import { familiesDialog, listFamilies, previewFamily, setFamilyValues, useFamilies, useFromSketch, usePlaceFamily } from './familyApi';
import { IFC_CLASSES, className, parseValue, placedEdges, planEdges, sketchSources, valuesOf, type FamilyDto } from './familyModel';

export const FAMILY_CLASS = 'LapinFamilyInstance';

// ------------------------------------------------------------ the place tool

const redraw = () => useToolTick.setState((s) => ({ n: s.n + 1 }));
const turn = () => { usePlaceFamily.setState((s) => ({ rotation: (s.rotation + 90) % 360 })); redraw(); };
const flip = () => { usePlaceFamily.setState((s) => ({ mirror: !s.mirror })); redraw(); };

/** The ghost: the family's outline seen from above in its own frame, asked of the engine when the family or its values change. */
let ghostEdges: [number, number, number, number][] = [];

export const placeFamilyTool: ToolDef = {
  id: 'studio.family', label: 'Family', icon: Component, gesture: {
    kind: 'point',
    ghost: (at) => { const s = usePlaceFamily.getState(); return placedEdges(ghostEdges, at, s.rotation, s.mirror); },
  }, options: FamilyPlaceOptions, space: turn,
  complete: async ({ points }, ctx) => {
    const at = points[0];
    if (!at) return;
    const families = await listFamilies();
    const s = usePlaceFamily.getState();
    const family = families.find((f) => f.id === s.familyId) ?? families[0];
    if (!family) { notify('There is no family yet: make one in Studio → Families…, or from a sketch.'); useUi.setState({ tool: null }); return; }
    if (family.id !== s.familyId) usePlaceFamily.setState({ familyId: family.id, values: {} });
    const values = valuesOf(family.def, s.values, (t) => parseLength(t));
    const r = await ctx.execute('studio.placeFamily', {
      familyId: family.id, storeyId: ctx.storeyId || activeStorey()?.id, at: [at.x, at.y], ...(s.rotation ? { rotation: s.rotation } : {}),
      ...(s.mirror ? { mirror: true } : {}), ...(Object.keys(values).length ? { values } : {}),
    }) as { id?: string } | null;
    if (r?.id) select(r.id); else if (useSession.getState().error) notify(useSession.getState().error);
  },
};

/** The bar while placing: the family, its quarter turn and mirror (R, M), the values (empty: the default). */
function FamilyPlaceOptions() {
  const s = usePlaceFamily();
  const { families } = useFamilies();
  const family = families.find((f) => f.id === s.familyId);
  useEffect(() => { if (families.length && !family) usePlaceFamily.setState({ familyId: families[0].id, values: {} }); }, [families, family]);
  // The ghost's edges: asked again when the family, its values or the model change (typed values wait a moment), never on a mouse move.
  const version = useSession((x) => x.version);
  const typedKey = JSON.stringify(s.values);
  useEffect(() => {
    if (!family) { ghostEdges = []; redraw(); return; }
    let live = true;
    const timer = setTimeout(() => {
      const values = valuesOf(family.def, s.values, (t) => parseLength(t));
      void previewFamily({ id: family.id }, values).then((r) => { if (live) { ghostEdges = 'bodies' in r ? planEdges(r.bodies) : []; redraw(); } });
    }, 250);
    return () => { live = false; clearTimeout(timer); };
  }, [family?.id, typedKey, version]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => () => { ghostEdges = []; }, []);
  // R and M while placing, unless a field has the key (taken before the command keys do).
  useEffect(() => {
    const on = (e: KeyboardEvent) => {
      if (e.ctrlKey || e.metaKey || e.altKey || (e.target as HTMLElement | null)?.closest?.('input, select, textarea, [role="dialog"]')) return;
      const k = e.key.toLowerCase();
      if (k !== 'r' && k !== 'm') return;
      e.preventDefault(); e.stopPropagation();
      if (k === 'r') turn(); else flip();
    };
    window.addEventListener('keydown', on, true);
    return () => window.removeEventListener('keydown', on, true);
  }, []);
  if (!families.length) return <span className="muted small">No family yet: Studio → Families…</span>;
  return (
    <>
      <select className="cad-toggle" value={s.familyId ?? ''} data-family-pick title="The family to place" onChange={(e) => usePlaceFamily.setState({ familyId: e.target.value, values: {} })}>
        {families.map((f) => <option key={f.id} value={f.id} data-no-i18n>{f.name}</option>)}
      </select>
      <button type="button" className="cad-toggle" title="Turn a quarter (R or Space)" data-family-rotate onClick={turn}><RotateCw size={12} /> {s.rotation}°</button>
      <button type="button" className={`cad-toggle${s.mirror ? ' on' : ''}`} title="Mirror it (M)" data-family-mirror onClick={flip}><FlipHorizontal2 size={12} /></button>
      {family?.def.params.map((p) => (
        <label key={p.key} className="cad-toggle" title={p.label}>
          <span data-no-i18n>{p.label}</span>{' '}
          <input value={s.values[p.key] ?? ''} size={6} data-no-i18n data-family-value={p.key} aria-invalid={parseValue(s.values[p.key] ?? '', p.kind, (t) => parseLength(t)) === 'bad'}
            placeholder={p.kind === 'bool' ? (p.default ? 'yes' : 'no') : p.kind === 'length' ? formatLength(p.default) : String(p.default)}
            onKeyDown={(e) => e.stopPropagation()} onChange={(e) => { const v = e.target.value; usePlaceFamily.setState((x) => ({ values: { ...x.values, [p.key]: v } })); }} />
        </label>
      ))}
      <button type="button" className="cad-toggle" data-family-edit title="Edit the family" onClick={() => editFamily(s.familyId)}><Pencil size={12} /> Edit…</button>
    </>
  );
}

/** The Families dialog, open on the family. */
export function editFamily(id: string | null | undefined): void {
  familiesDialog.open = id ?? null;
  useUi.setState({ dialog: 'families' });
}

// ------------------------------------------------------------ the object's Properties

const shownValue = (v: unknown, kind: string): string => (typeof v === 'number' ? (kind === 'bool' ? (v ? 'yes' : 'no') : String(Math.round(v * 1e6) / 1e6)) : '');

export function FamilySection({ node }: { node: NodeDto }) {
  const { families } = useFamilies();
  const family: FamilyDto | undefined = families.find((f) => f.id === node.props.familyId);
  const own = (node.props.values ?? {}) as Record<string, unknown>;
  // The objects of the family selected with this one are changed together.
  const ids = (): string[] => {
    const sel = useUi.getState().selected;
    const nodes = useSession.getState().nodes;
    return sel.includes(node.id) ? sel.filter((id) => nodes.get(id)?.type === FAMILY_CLASS) : [node.id];
  };
  const commit = async (key: string, kind: 'length' | 'number' | 'angle' | 'bool', text: string) => {
    const v = parseValue(text, kind, (t) => parseLength(t));
    if (v === 'bad') { notify(kind === 'bool' ? 'Yes or no, or empty for the default.' : 'A parameter is a number, or empty for the default.'); return; }
    await setFamilyValues(ids(), { [key]: v });
    if (useSession.getState().error) notify(useSession.getState().error);
  };
  if (!family) return <p className="muted small">Its family is not in the project.</p>;
  return (
    <dl className="fields" data-section="family">
      <dt>Family</dt>
      <dd className="row"><strong data-no-i18n>{family.name}</strong><span className="grow" />
        <button type="button" className="btn small" data-field="family-edit" onClick={() => editFamily(family.id)}><Pencil size={12} />Edit family…</button></dd>
      <dt>IFC class</dt><dd className="muted" data-no-i18n>{className(family.def.ifcClass)}</dd>
      {family.def.params.map((p) => (
        <FamilyValue key={`${p.key}:${shownValue(own[p.key], p.kind)}`} label={p.label} kind={p.kind} pkey={p.key} initial={own[p.key]} def={p.default} onCommit={commit} />
      ))}
      {family.def.params.length > 0 && <dd className="muted small fam-note">An empty value is the family's own.</dd>}
    </dl>
  );
}

function FamilyValue({ label, kind, pkey, initial, def, onCommit }: { label: string; kind: 'length' | 'number' | 'angle' | 'bool'; pkey: string; initial: unknown; def: number; onCommit: (k: string, kind: 'length' | 'number' | 'angle' | 'bool', text: string) => Promise<void> }) {
  const shown = typeof initial === 'number' ? (kind === 'length' ? formatLength(initial) : shownValue(initial, kind)) : '';
  const [text, setText] = useState(shown);
  const go = () => { if (text.trim() !== shown.trim()) void onCommit(pkey, kind, text); };
  const placeholder = kind === 'bool' ? (def ? 'yes' : 'no') : kind === 'length' ? formatLength(def) : String(def);
  return (
    <>
      <dt data-no-i18n>{label}</dt>
      <dd>{kind === 'bool'
        ? <select value={text} data-family-param={pkey} onChange={(e) => { setText(e.target.value); void onCommit(pkey, kind, e.target.value); }}>
          <option value="">{tr(def ? 'Yes (the family\'s)' : 'No (the family\'s)')}</option><option value="yes">Yes</option><option value="no">No</option></select>
        : <input className="mono" value={text} placeholder={placeholder} data-no-i18n data-family-param={pkey} onChange={(e) => setText(e.target.value)} onBlur={go} onKeyDown={(e) => { if (e.key === 'Enter') go(); }} />}</dd>
    </>
  );
}

// ------------------------------------------------------------ Make family from sketch…

/** Make family from sketch…: the closed outlines selected on a drafting view (else it says what to select), then the dialog. */
export function startFamilyFromSketch(): void {
  const src = sketchSources(useUi.getState().selected, useSession.getState().nodes);
  if (!src) { notify('Select closed drafting first: closed polylines, rectangles or circles drawn on a plan, a section or an elevation.'); return; }
  useFromSketch.setState({ request: src });
  useUi.setState({ dialog: 'familyFromSketch' });
}

registerDraftMenu((ids, nodes) => (sketchSources(ids, nodes) ? [{ label: 'Make family from sketch…', onSelect: startFamilyFromSketch }] : []));

export function FamilyFromSketchDialog() {
  const request = useFromSketch((s) => s.request);
  const { families } = useFamilies();
  const [name, setName] = useState('');
  const [depth, setDepth] = useState(formatLength(0.5));
  const [cls, setCls] = useState('IfcBuildingElementProxy');
  const [parameterize, setParameterize] = useState(true);
  const [busy, setBusy] = useState(false);
  const close = () => { useFromSketch.setState({ request: null }); useUi.setState({ dialog: null }); };
  useEffect(() => { if (!name && families) setName(freeFamilyName(families.map((f) => f.name))); }, [families]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!request) return null;
  const d = parseLength(depth);
  const go = async () => {
    if (busy || d == null || d <= 0) return;
    setBusy(true);
    const r = await execute('studio.familyFromSketch', { viewId: request.viewId, ids: request.ids, name: name.trim() || tr('Family'), depth: d, parameterize, ifcClass: cls }) as { id?: string } | null;
    setBusy(false);
    if (!r?.id) { notify(useSession.getState().error ?? 'The family could not be made.'); return; }
    notify('Family made: its parameters are W, H and D. Edit it in Studio → Families….');
    close();
    editFamily(r.id);
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog tpl-save" role="dialog" aria-label="Make family from sketch" data-dialog="family-from-sketch" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Make family from sketch</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <label className="doc-info-row"><span className="muted small">Name</span>
            <input autoFocus value={name} onChange={(e) => setName(e.target.value)} data-field="fs-name" data-no-i18n onKeyDown={(e) => { if (e.key === 'Enter') void go(); }} /></label>
          <label className="doc-info-row"><span className="muted small">Depth</span>
            <input value={depth} onChange={(e) => setDepth(e.target.value)} data-field="fs-depth" data-no-i18n aria-invalid={d == null || d <= 0} onKeyDown={(e) => { if (e.key === 'Enter') void go(); }} /></label>
          <label className="doc-info-row"><span className="muted small">IFC class</span>
            <select value={cls} onChange={(e) => setCls(e.target.value)} data-field="fs-class">{IFC_CLASSES.map((c) => <option key={c} value={c} data-no-i18n>{className(c)} ({c})</option>)}</select></label>
          <label className="row"><input type="checkbox" checked={parameterize} onChange={(e) => setParameterize(e.target.checked)} data-field="fs-parameterize" />Width, height and depth as parameters (W, H, D)</label>
          <p className="muted small">{`${request.ids.length} ${tr(request.ids.length === 1 ? 'outline' : 'outlines')}`}</p>
          <p className="muted small">A plan's outlines lie flat, a section's or an elevation's stand. The drafting stays.</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={busy || d == null || d <= 0} onClick={() => void go()} data-field="fs-go">Make family</button>
        </footer>
      </div>
    </div>
  );
}

const freeFamilyName = (taken: string[]): string => {
  const base = tr('Family');
  if (!taken.includes(base)) return base;
  for (let i = 2; ; i++) if (!taken.includes(`${base} ${i}`)) return `${base} ${i}`;
};
