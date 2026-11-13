/**
 * The part's parameters (docs/plan-studio-parametric.md §3.2 and §3.3, stage P1): the table beside the tree in edit mode (name, default
 * or formula, this object's own value, type or instance, published, and a row menu), Flex (values changed for a preview drawn as a ghost
 * in 3D, then Keep or Put back), and the published parameters in Properties in model mode. The logic is in partParamModel.ts.
 */

import { useEffect, useMemo, useState } from 'react';
import * as THREE from 'three';
import { ArrowDown, ArrowUp, ChevronDown, ChevronRight, MoreHorizontal, PencilRuler, Plus, Trash2, X } from 'lucide-react';
import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import type { Overlay3DProps } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { Cell } from '../../shell/cells';
import { formatLength, parseLength, unitLabel } from '../../engine/unitsStore';
import { useCamera } from '../../viewport/camera';
import { runSteps } from '../../viewport/grips';
import { useScope } from '../../platform/scope';
import { setLooks } from '../../platform/looks';
import { t as tr } from '../../i18n';
import { editedPartId, editPart } from './editMode';
import { usePartTree } from './partTreeStore';
import {
  FLEX_OFF, PARAM_KINDS, cellsOf, defaultChanges, flexChanged, flexKeepPlan, flexPreviewValues, flexSet, flexShown, groupParams, isDerived, moveTarget,
  newParam, parseCell, plainValue, previewVolume, publishedRows, unitOf, type CellInput, type FlexState, type ParamDto, type ParamKind, type PreviewDto, type PublishedRow, type ValueDto,
} from './partParamModel';

type GoodInput = Exclude<CellInput, 'bad'>;
interface ParamsUi { open: boolean; flex: FlexState; preview: PreviewDto | null; errors: Record<string, string>; menu: string | null }
export const useParamsUi = create<ParamsUi>(() => ({ open: false, flex: FLEX_OFF, preview: null, errors: {}, menu: null }));
const closeParams = () => useParamsUi.setState({ open: false, flex: FLEX_OFF, preview: null, errors: {}, menu: null });

/** The button in the tree's Parameters section that opens the table. */
export function ParamsButton() {
  const open = useParamsUi((s) => s.open);
  return (
    <button type="button" className={`btn small${open ? ' on' : ''}`} data-testid="part-params-open" title="The parameter table: values, formulas, type or instance, published, Flex"
      onClick={() => (open ? closeParams() : useParamsUi.setState({ open: true }))}><PencilRuler size={12} />Parameters…</button>
  );
}

/** A value of a kind as a cell shows it: a length in the project's unit, a bool as yes/no. */
const cellText = (v: number | string | undefined, kind: ParamKind): string => (kind === 'length' && typeof v === 'number' ? formatLength(v) : plainValue(v, kind));

/** Runs a command; the engine's refusal is put under the row it belongs to, and the result is null. */
async function run(row: string, command: string, args: Record<string, unknown>): Promise<Record<string, unknown> | null> {
  useParamsUi.setState((s) => ({ errors: { ...s.errors, [row]: '' } }));
  const r = (await execute(command, args)) as Record<string, unknown> | null;
  if (r === null) {
    const message = useSession.getState().error ?? tr('The engine refused it.');
    useParamsUi.setState((s) => ({ errors: { ...s.errors, [row]: message } }));
  }
  return r;
}

/** A cell for a parameter's value: a check box for a bool, else a text cell that reads a formula or a value. */
function ValueCell({ p, value, readOnly, onInput, testId, title }: { p: ParamDto; value: number | string; readOnly?: boolean; onInput: (i: GoodInput) => void; testId: string; title?: string }) {
  if (p.kind === 'bool') {
    return <input type="checkbox" aria-label={p.label} disabled={readOnly} checked={Number(value) !== 0} data-field={testId} onChange={(e) => onInput({ value: e.target.checked ? 1 : 0 })} />;
  }
  const suffix = p.kind === 'length' ? unitLabel() : unitOf(p);
  return (
    <Cell value={cellText(value, p.kind)} suffix={suffix || undefined} readOnly={readOnly} title={title} testId={testId} style={{ width: 82 }}
      onCommit={(text) => {
        const i = parseCell(text, p.kind, (t) => parseLength(t));
        if (i === 'bad') return false;
        if ('clear' in i) return false;
        onInput(i);
      }} />
  );
}

function RowDetails({ id, p, params }: { id: string; p: ParamDto; params: ParamDto[] }) {
  const send = (changes: Record<string, unknown>) => run(p.key, 'studio.updatePartParam', { partId: id, key: p.key, changes });
  const text = (field: 'label' | 'description' | 'group') => (v: string) => { void send({ [field]: field === 'group' && !v.trim() ? null : v }); return true; };
  const limit = (field: 'min' | 'max') => (v: string) => {
    if (!v.trim()) { void send({ [field]: null }); return true; }
    const n = p.kind === 'length' ? parseLength(v) : Number(v.replace(',', '.'));
    if (n == null || !Number.isFinite(n)) return false;
    void send({ [field]: n });
    return true;
  };
  const limitText = (n: number | undefined) => (n == null ? '' : cellText(n, p.kind));
  const up = moveTarget(params, p.key, -1), down = moveTarget(params, p.key, 1);
  return (
    <tr className="part-param-menu" data-part-param-menu={p.key}>
      <td colSpan={7}>
        <div className="part-param-detail">
          <label>Name<Cell value={p.key} testId="param-key" onCommit={(v) => { if (!v.trim()) return false; void send({ newKey: v.trim() }); return true; }} /></label>
          <label>Label<Cell value={p.label} testId="param-label" onCommit={text('label')} /></label>
          <label>Description<Cell value={p.description ?? ''} testId="param-description" onCommit={text('description')} /></label>
          <label>Group<Cell value={p.group ?? ''} testId="param-group" onCommit={text('group')} /></label>
          <label>Minimum<Cell value={limitText(p.min)} testId="param-min" onCommit={limit('min')} /></label>
          <label>Maximum<Cell value={limitText(p.max)} testId="param-max" onCommit={limit('max')} /></label>
          <label>Kind<select value={p.kind} data-field="param-kind" onChange={(e) => void send({ kind: e.target.value })}>{PARAM_KINDS.map((k) => <option key={k} value={k}>{KIND_LABELS[k]}</option>)}</select></label>
          <label>Unit<Cell value={p.unit ?? ''} testId="param-unit" onCommit={(v) => { void send({ unit: v.trim() }); return true; }} /></label>
          <div className="part-param-actions">
            <button type="button" className="btn small" disabled={up == null} onClick={() => up != null && void run(p.key, 'studio.movePartParam', { partId: id, key: p.key, to: up })}><ArrowUp size={12} />Move up</button>
            <button type="button" className="btn small" disabled={down == null} onClick={() => down != null && void run(p.key, 'studio.movePartParam', { partId: id, key: p.key, to: down })}><ArrowDown size={12} />Move down</button>
            <button type="button" className="btn small" data-testid="param-delete" onClick={() => void run(p.key, 'studio.removePartParam', { partId: id, key: p.key })}><Trash2 size={12} />Delete</button>
          </div>
        </div>
      </td>
    </tr>
  );
}

const KIND_LABELS: Record<ParamKind, string> = { length: 'Length', number: 'Number', angle: 'Angle', bool: 'Yes/no', text: 'Text', integer: 'Whole number' };

function ParamRowView({ id, p, params, flex }: { id: string; p: ParamDto; params: ParamDto[]; flex: boolean }) {
  const state = useParamsUi();
  const cells = cellsOf(p);
  const derived = isDerived(p);
  const error = state.errors[p.key];
  const send = (changes: Record<string, unknown>) => run(p.key, 'studio.updatePartParam', { partId: id, key: p.key, changes });
  const flexed = p.key in state.flex.values;
  const setFlex = (v: number | string) => useParamsUi.setState((s) => ({ flex: flexSet(params, s.flex, p.key, v) }));
  const onDefault = async (i: GoodInput) => {
    if (flex) { if ('value' in i) setFlex(i.value); return; }
    const changes = defaultChanges(i, derived);
    if (!changes) return;
    const r = await send(changes);
    if (r && r.droppedValues) useParamsUi.setState((s) => ({ errors: { ...s.errors, [p.key]: tr('Some objects\' own values were dropped.') } }));
  };
  const onOwn = (i: GoodInput) => {
    if (!('value' in i)) return;
    if (flex) setFlex(i.value);
    else void run(p.key, 'studio.setPartValues', { ids: [id], values: { [p.key]: i.value } });
  };
  // In flex a type parameter flexes in its default's cell, an instance one in the object's cell.
  const defaultFlexes = flex && !p.instance && !derived;
  const defaultShown = defaultFlexes ? flexShown(p, state.flex) : derived ? `=${p.formula ?? ''}` : p.default;
  const menuOpen = state.menu === p.key;
  return (
    <>
      <tr data-part-param-row={p.key} className={`${flexed ? 'flexed' : ''}${derived ? ' derived' : ''}`}>
        <td title={p.description || p.key}><span data-no-i18n>{p.label}</span>{p.label !== p.key && <span className="muted small"> <span data-no-i18n>{p.key}</span></span>}</td>
        <td>
          {cells.defaultCell === 'formula'
            ? <Cell value={defaultShown as string} className="mono" testId="param-default" readOnly={flex} style={{ width: 110 }} title={tr('A formula of the other parameters; a value replaces it')}
                onCommit={(text) => { const i = parseCell(text, p.kind, (t) => parseLength(t)); if (i === 'bad' || 'clear' in i) return false; void onDefault(i); return true; }} />
            : <ValueCell p={p} value={defaultShown} readOnly={flex && !defaultFlexes} testId="param-default" onInput={(i) => void onDefault(i)} />}
        </td>
        <td>
          {cells.showsOwn
            ? <ValueCell p={p} value={flex ? flexShown(p, state.flex) : p.value} testId="param-own" onInput={onOwn} />
            : derived
              ? <span className="muted" title={`= ${p.formula ?? ''}`} data-testid="param-derived" data-no-i18n>= {cellText(p.value, p.kind)}</span>
              : <span className="muted small">type</span>}
        </td>
        <td><input type="checkbox" aria-label="Instance: each object has its own value" title="Instance: each object has its own value; off: the part's value for all (type)" disabled={flex || derived} checked={p.instance} data-field="param-instance" onChange={(e) => void send({ instance: e.target.checked })} /></td>
        <td><input type="checkbox" aria-label="Published: shown in the object's Properties" title="Published: shown in the object's Properties" disabled={flex} checked={p.published} data-field="param-published" onChange={(e) => void send({ published: e.target.checked })} /></td>
        <td><button type="button" className="icon mini" aria-label="Row menu" title="Rename, description, limits, group, order, delete" data-field="param-menu" onClick={() => useParamsUi.setState((s) => ({ menu: s.menu === p.key ? null : p.key }))}><MoreHorizontal size={12} /></button></td>
      </tr>
      {menuOpen && !flex && <RowDetails id={id} p={p} params={params} />}
      {error && <tr><td colSpan={6}><p className="part-param-error" role="alert" data-testid="param-error" data-no-i18n>{error}</p></td></tr>}
    </>
  );
}

/** The part's parameters in the table beside the tree. */
export function PartParamsPanel() {
  const scope = useScope((s) => s.scope);
  const id = editedPartId(scope);
  const open = useParamsUi((s) => s.open);
  const tree = usePartTree(open ? id : null);
  const { flex, preview } = useParamsUi();
  const [kind, setKind] = useState<ParamKind>('length');
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});
  useEffect(() => { if (!id) closeParams(); }, [id]);
  const params = useMemo(() => (tree?.params ?? []) as unknown as ParamDto[], [tree]);
  const groups = useMemo(() => groupParams(params), [params]);
  // The preview, asked a moment after the last change.
  const wanted = flexPreviewValues(params, flex);
  const wantedKey = JSON.stringify(wanted);
  useEffect(() => {
    if (!id || !flex.on) return;
    if (!wanted) { useParamsUi.setState({ preview: null }); return; }
    let live = true;
    const timer = setTimeout(() => {
      void query<PreviewDto>('studio.partPreview', { id, values: wanted }).then((r) => { if (live) useParamsUi.setState({ preview: r ?? { ok: false, reason: useSession.getState().error ?? tr('The engine could not preview it.') } }); });
    }, 250);
    return () => { live = false; clearTimeout(timer); };
  }, [id, flex.on, wantedKey]); // eslint-disable-line react-hooks/exhaustive-deps
  // While the ghost shows, the real part is drawn faded (as the scope fades the rest); restored on Keep, Put back or leaving.
  const ghostShown = !!id && flex.on && !!preview?.ok;
  useEffect(() => {
    if (!ghostShown || !id) return;
    setLooks('part-flex', { [id]: { opacity: 0.25 } });
    return () => setLooks('part-flex', {});
  }, [ghostShown, id]);
  if (!id || !open) return null;
  const changed = flexChanged(params, flex);
  const volume = previewVolume(preview);
  const keep = async () => {
    const plan = flexKeepPlan(params, flex);
    const steps = [
      ...(Object.keys(plan.instance).length ? [{ command: 'studio.setPartValues', args: { ids: [id], values: plan.instance } }] : []),
      ...plan.defaults.map((d) => ({ command: 'studio.updatePartParam', args: { partId: id, key: d.key, changes: { default: d.value } } })),
    ];
    const r = await runSteps(steps);
    if (r === null && steps.length) { useParamsUi.setState((s) => ({ errors: { ...s.errors, '': useSession.getState().error ?? tr('The engine refused it.') } })); return; }
    useParamsUi.setState({ flex: { on: true, values: {} }, preview: null });
  };
  return (
    <div className="sel-card part-params-panel" role="region" aria-label="Parameters" data-testid="part-params">
      <div className="row between">
        <b>Parameters</b>
        <span className="row">
          <label className={`cad-toggle${flex.on ? ' on' : ''}`} title="Flex: change values to see the part change as a ghost, then keep them or put them back"><input type="checkbox" checked={flex.on} data-testid="part-flex"
            onChange={(e) => useParamsUi.setState({ flex: e.target.checked ? { on: true, values: {} } : FLEX_OFF, preview: null, menu: null })} /> Flex</label>
          <button type="button" className="icon mini" aria-label="Close" title="Close" onClick={closeParams}><X size={13} /></button>
        </span>
      </div>
      {!tree && <p className="muted small">Reading the part…</p>}
      {tree && (
        <table className="part-params wide">
          <thead><tr><th>Name</th><th>Default or formula</th><th>This object</th><th title="Instance: each object has its own value">Inst.</th><th title="Published: shown in the object's Properties">Pub.</th><th /></tr></thead>
          <tbody>
            {groups.map((g) => (
              <ParamGroupRows key={g.group} name={g.group} folded={!!collapsed[g.group]} onFold={() => setCollapsed((c) => ({ ...c, [g.group]: !c[g.group] }))}
                rows={g.rows.map((p) => <ParamRowView key={p.key} id={id} p={p} params={params} flex={flex.on} />)} />
            ))}
          </tbody>
        </table>
      )}
      {tree && !params.length && <p className="muted small">None.</p>}
      {tree && !flex.on && (
        <div className="row part-param-add">
          <select value={kind} aria-label="Kind" data-field="param-new-kind" onChange={(e) => setKind(e.target.value as ParamKind)}>{PARAM_KINDS.map((k) => <option key={k} value={k}>{KIND_LABELS[k]}</option>)}</select>
          <button type="button" className="btn small" data-testid="param-add" onClick={() => void run('', 'studio.addPartParam', { partId: id, param: newParam(params, kind) })}><Plus size={12} />Parameter</button>
        </div>
      )}
      {useParamsUi.getState().errors[''] && <p className="part-param-error" role="alert" data-no-i18n>{useParamsUi.getState().errors['']}</p>}
      {flex.on && (
        <div className="part-flex" data-testid="part-flex-bar">
          <span className="muted small">{changed.length ? tr('The ghost shows the part with the changed values.') : tr('Change a value to see the part flex.')}</span>
          {preview && !preview.ok && <p className="part-param-error" role="alert" data-testid="flex-reason" data-no-i18n>{preview.reason ?? tr('That does not give a part.')}</p>}
          {volume != null && <span className="small" data-testid="flex-volume">{tr('Volume')} <span data-no-i18n>{formatVolume(volume)}</span></span>}
          <span className="row">
            <button type="button" className="btn small primary" data-testid="flex-keep" disabled={!changed.length || (preview != null && !preview.ok)} onClick={() => void keep()}>Keep</button>
            <button type="button" className="btn small" data-testid="flex-put-back" disabled={!changed.length} onClick={() => useParamsUi.setState({ flex: { on: true, values: {} }, preview: null })}>Put back</button>
          </span>
        </div>
      )}
    </div>
  );
}

const formatVolume = (m3: number): string => `${Math.round(m3 * 1e4) / 1e4} m³`;

function ParamGroupRows({ name, folded, onFold, rows }: { name: string; folded: boolean; onFold: () => void; rows: React.ReactNode[] }) {
  return (
    <>
      {name !== '' && <tr className="part-param-group"><td colSpan={6}><button type="button" className="link" onClick={onFold}>{folded ? <ChevronRight size={12} /> : <ChevronDown size={12} />}<span data-no-i18n>{name}</span></button></td></tr>}
      {!folded && rows}
    </>
  );
}

/** The flexed part as a ghost in 3D: the preview's edges, in the model's coordinates, drawn dashed over the part. */
export function PartFlex3D({ camera, canvas }: Overlay3DProps) {
  useCamera();
  const preview = useParamsUi((s) => s.preview);
  const flexOn = useParamsUi((s) => s.flex.on && s.open);
  const cam = camera.current, r = canvas.current?.getBoundingClientRect();
  if (!flexOn || !preview?.ok || !preview.bodies || !cam || !r) return null;
  cam.updateMatrixWorld();
  const project = (x: number, y: number, z: number): [number, number] | null => {
    const v = new THREE.Vector3(x, y, z).project(cam);
    return v.z > 1 || v.z < -1 ? null : [((v.x + 1) / 2) * r.width, ((1 - v.y) / 2) * r.height];
  };
  const lines: string[] = [];
  for (const b of preview.bodies) for (const e of b.edges) {
    if (e.length < 6) continue;
    const a = project(e[0]!, e[1]!, e[2]!), c = project(e[3]!, e[4]!, e[5]!);
    if (a && c) lines.push(`M${a[0].toFixed(1)} ${a[1].toFixed(1)}L${c[0].toFixed(1)} ${c[1].toFixed(1)}`);
  }
  return (
    <svg className="grip-overlay" data-sketch3d="part-flex" style={{ pointerEvents: 'none' }}>
      <path d={lines.join('')} className="part-flex-ghost" />
    </svg>
  );
}

// ---------------------------------------------------------------- Properties, model mode

/** The part of a placed part's Properties: its published parameters (instance ones editable per object), and the way into the edit mode. */
export function PublishedParamsSection({ node }: { node: NodeDto }) {
  const tree = usePartTree(node.id);
  const version = useSession((s) => s.version);
  const [values, setValues] = useState<ValueDto[]>([]);
  const [error, setError] = useState('');
  useEffect(() => {
    let live = true;
    void query<{ values: ValueDto[] }>('studio.partValues', { id: node.id }).then((r) => { if (live) setValues(r?.values ?? []); });
    return () => { live = false; };
  }, [node.id, version]);
  const rows = publishedRows((tree?.params ?? []) as unknown as ParamDto[], values);
  // The objects of the part selected with this one are changed together.
  const ids = (): string[] => {
    const sel = useUi.getState().selected, nodes = useSession.getState().nodes;
    return sel.includes(node.id) ? sel.filter((i) => nodes.get(i)?.props.operation === 'part') : [node.id];
  };
  const set = async (key: string, value: number | string | null) => {
    setError('');
    // Shown at once (a checkbox would spring back until the engine answers), put right by the refetch if refused.
    if (value != null) setValues((vs) => (vs.some((v) => v.key === key) ? vs.map((v) => (v.key === key ? { ...v, value } : v)) : [...vs, { key, value } as ValueDto]));
    const r = await execute('studio.setPartValues', { ids: ids(), values: { [key]: value } });
    if (r === null) {
      setError(useSession.getState().error ?? '');
      void query<{ values: ValueDto[] }>('studio.partValues', { id: node.id }).then((q) => setValues(q?.values ?? []));
    }
  };
  return (
    <dl className="fields" data-section="part-params">
      {tree && !rows.length && <dd className="muted small">The part publishes no parameters.</dd>}
      {rows.map((r) => <PublishedRowView key={r.key} row={r} onSet={set} />)}
      {error && <dd><p className="part-param-error" role="alert" data-no-i18n>{error}</p></dd>}
      <dt />
      <dd><button type="button" className="btn small" data-field="edit-part-link" onClick={() => editPart(node.id)}><PencilRuler size={12} />Edit part (Tab)</button></dd>
    </dl>
  );
}

function PublishedRowView({ row, onSet }: { row: PublishedRow; onSet: (key: string, v: number | string | null) => Promise<void> }) {
  const p = { key: row.key, label: row.label, kind: row.kind, unit: row.unit || undefined } as ParamDto;
  const title = row.mode === 'derived' ? `= ${row.formula ?? ''}${row.problem ? ` (${row.problem})` : ''}` : row.description || undefined;
  return (
    <>
      <dt title={row.description || undefined} data-no-i18n>{row.label}</dt>
      <dd className="row" data-published-param={row.key}>
        {row.mode === 'own'
          ? <ValueCell p={p} value={row.value} testId={`published-${row.key}`} title={title}
              onInput={(i) => { if ('value' in i) void onSet(row.key, i.value); }} />
          : <span className="muted" title={title} data-no-i18n>{cellText(row.value, row.kind)}{row.unit && row.kind !== 'length' ? ` ${row.unit}` : ''}</span>}
        {row.mode === 'type' && <span className="badge small" title="The part's value, the same for every object">type</span>}
        {row.mode === 'derived' && <span className="badge small" title={title}>formula</span>}
      </dd>
    </>
  );
}
