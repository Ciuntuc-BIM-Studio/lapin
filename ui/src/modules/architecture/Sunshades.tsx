/**
 * Sunshades (report #56): canopies, vertical fins, louvres (brise-soleil), egg-crates, perforated screens, awnings,
 * pergolas and external venetian blinds, as IfcShadingDevice of a type. A click on a door or window puts one on it,
 * sized to its opening; two points along a wall (or a facade line anywhere) put one there. The types dialog edits a
 * type's kind and every parameter of it beside the engine's preview (front, section, plan); styles start a type
 * (Lapin's, or the project's own, saved there). A sunshade's Properties show its type, where it stands, its own
 * projection and the noon helper: the projection that shades its window at noon on 21 June at the project's place.
 */

import { useEffect, useMemo, useState } from 'react';
import { Copy, Pencil, Plus, Save, Settings2, Sun, Table2, Trash2, Umbrella, X } from 'lucide-react';
import { create } from 'zustand';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { relatedOf, relatingOf } from '../../engine/model';
import type { Grip, ToolDef, Vec2 } from '../../platform/module';
import { LengthField, NumberField } from '../../shell/cells';
import { MaterialPicker } from '../core/MaterialPicker';
import { deleteType } from '../core/deleteType';
import { t } from '../../i18n';
import { Strokes, type Drawing } from './JoineryDialog';

export const SHADE = 'IfcShadingDevice';
export const SHADE_TYPE = 'IfcShadingDeviceType';

export interface ShadeParam { key: string; label: string; kind: 'length' | 'number' | 'angle' | 'choice' | 'bool' | 'material'; min?: number | null; max?: number | null; choices?: string[] | null; showIf?: Record<string, string[]> | null }
export interface ShadeKind { key: string; label: string; predefined: string; objectType?: string | null; over: boolean; params: ShadeParam[]; defaults: Record<string, unknown> }
interface ShadeStyle { id: string; label: string; kind?: string; spec: Record<string, unknown> }
type Spec = Record<string, unknown> & { kind?: string };

/** What the choices read as. */
const CHOICES: Record<string, string> = {
  solid: 'Solid', louvred: 'Louvred', glass: 'Glass', none: 'None', brackets: 'Brackets', tieRods: 'Tie rods', posts: 'Posts',
  flat: 'Flat', aerofoil: 'Aerofoil', box: 'Box', round: 'Round holes', square: 'Square holes', slots: 'Slots',
};

export function useShadeKinds(): ShadeKind[] {
  return (useSession((s) => s.info?.catalogs['architecture.sunshadeKinds']) ?? []) as unknown as ShadeKind[];
}
const specOf = (n: NodeDto | undefined): Spec => (n?.props.spec as Spec | undefined) ?? {};
const valueOf = (spec: Spec, k: ShadeKind | undefined, key: string): unknown => spec[key] ?? k?.defaults[key];
/** Whether a parameter applies, by the one it depends on. */
export function shown(p: ShadeParam, spec: Spec, k: ShadeKind | undefined): boolean {
  if (!p.showIf) return true;
  return Object.entries(p.showIf).every(([key, values]) => values.includes(String(valueOf(spec, k, key) ?? '')));
}

/** What the tools put down: a type, or a style a type is made from. */
export const useShadePrefs = create<{ typeId: string | null; style: string }>(() => ({ typeId: null, style: 'concrete-canopy' }));
/** The types dialog's selection, set from outside (a sunshade's Edit type…). */
const useShadeDialog = create<{ typeId: string | null }>(() => ({ typeId: null }));

function typeArgs(): Record<string, unknown> {
  const { typeId, style } = useShadePrefs.getState();
  return typeId && useSession.getState().nodes.has(typeId) ? { typeId } : { style };
}

/** After a sunshade is made from a style, its type is the one the tools put down from then on. */
async function remember(id: string | undefined) {
  if (!id) return;
  const { nodes, relations } = useSession.getState();
  const type = relatingOf({ nodes, relations }, id, 'IfcRelDefinesByType');
  if (type) useShadePrefs.setState({ typeId: type.id });
}

/** The bar of the sunshade tools: the type (or a style to make one from). */
function ShadeOptions() {
  const nodes = useSession((s) => s.nodes);
  const styles = (useSession((s) => s.info?.catalogs['architecture.sunshadeStyles']) as unknown as ShadeStyle[] | undefined) ?? [];
  const prefs = useShadePrefs();
  const types = [...nodes.values()].filter((n) => n.type === SHADE_TYPE).sort((a, b) => a.name.localeCompare(b.name));
  const value = prefs.typeId && nodes.has(prefs.typeId) ? prefs.typeId : `style:${prefs.style}`;
  return (
    <span className="cad-layer on">
      <select value={value} data-testid="sunshade-type" title="The type it puts down, or a style a new type is made from" onChange={(e) => {
        const v = e.target.value;
        if (v.startsWith('style:')) useShadePrefs.setState({ typeId: null, style: v.slice(6) });
        else useShadePrefs.setState({ typeId: v });
      }}>
        {types.length > 0 && <optgroup label={t('Types')}>{types.map((x) => <option key={x.id} value={x.id} data-no-i18n>{x.name}</option>)}</optgroup>}
        <optgroup label={t('New from a style')}>{styles.map((s) => <option key={s.id} value={`style:${s.id}`}>{s.label}</option>)}</optgroup>
      </select>
    </span>
  );
}

/** A sunshade on a door or window: click it. */
export const sunshade: ToolDef = {
  id: 'architecture.sunshade', label: 'Sunshade', icon: Umbrella, shortcut: 'SN',
  gesture: { kind: 'pick', types: ['IfcWindow', 'IfcDoor'] },
  options: ShadeOptions,
  complete: async ({ entity }, ctx) => {
    if (!entity) return;
    const r = (await ctx.execute('architecture.addSunshade', { hostId: entity.id, ...typeArgs() })) as { id?: string } | null;
    await remember(r?.id);
  },
};
/** A sunshade along a wall (two points on it) or free on a facade line (out to its right). */
export const sunshadeAlong: ToolDef = {
  id: 'architecture.sunshadeAlong', label: 'Sunshade along a facade', icon: Umbrella,
  gesture: { kind: 'twoPoints' },
  options: ShadeOptions,
  complete: async ({ points: [a, b] }, ctx) => {
    const r = (await ctx.execute('architecture.addSunshade', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, ...typeArgs() })) as { id?: string } | null;
    await remember(r?.id);
  },
};
export const sunshadeTypes: ToolDef = { id: 'architecture.sunshadeTypes', label: 'Sunshade types', icon: Settings2, run: () => editShadeType(useShadePrefs.getState().typeId) };
export const sunshadeSchedule: ToolDef = {
  id: 'architecture.sunshadeSchedule', label: 'Sunshade schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'sunshades' }).then((r) => { if (r) notify('Sunshade schedule made: it is in the project browser, under Schedules.'); }),
};

export function editShadeType(id: string | null | undefined) {
  if (id) useShadeDialog.setState({ typeId: id });
  useUi.setState({ dialog: 'sunshades' });
}

/** One parameter, as its kind says it is edited. */
function ParamField({ p, value, onCommit }: { p: ShadeParam; value: unknown; onCommit: (v: unknown) => void }) {
  switch (p.kind) {
    case 'length': return <LengthField metres={Number(value ?? 0)} min={p.min ?? undefined} max={p.max ?? undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'number': case 'angle': return <NumberField value={Number(value ?? 0)} min={p.min ?? undefined} max={p.max ?? undefined} suffix={p.kind === 'angle' ? '°' : undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'choice': return <select value={String(value ?? '')} onChange={(e) => onCommit(e.target.value)}>{(p.choices ?? []).map((c) => <option key={c} value={c}>{CHOICES[c] ?? c}</option>)}</select>;
    case 'bool': return <input type="checkbox" checked={value === true} onChange={(e) => onCommit(e.target.checked)} />;
    case 'material': return <MaterialPicker value={String(value ?? '')} allowEmpty onChange={(m) => onCommit(m || null)} />;
    default: return null;
  }
}

interface Preview { front: Drawing; section: Drawing; plan: Drawing; width: number; depth: number; height: number; over: boolean }

/** The engine's picture of a spec: from outside, in section, in plan. */
function ShadePreview({ spec }: { spec: Spec }) {
  const [p, setP] = useState<Preview | null>(null);
  const key = JSON.stringify(spec);
  useEffect(() => {
    let live = true;
    void query<Preview>('architecture.sunshadePreview', { spec }).then((r) => { if (live) setP(r); });
    return () => { live = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  if (!p) return <div className="joinery-preview muted small">…</div>;
  return (
    <div className="joinery-preview" data-testid="sunshade-preview">
      <Strokes drawing={p.front} w={300} h={220} label="From outside" />
      <div className="joinery-small">
        <figure><Strokes drawing={p.section} w={130} h={150} label="Section" /><figcaption className="muted small">Section</figcaption></figure>
        <figure><Strokes drawing={p.plan} w={150} h={110} label="Plan" /><figcaption className="muted small">Plan</figcaption></figure>
      </div>
      <p className="muted small" data-no-i18n>{Math.round(p.width * 1000)} × {Math.round(p.depth * 1000)} × {Math.round(p.height * 1000)} mm</p>
    </div>
  );
}

export function SunshadeTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const kinds = useShadeKinds();
  const styles = (useSession((s) => s.info?.catalogs['architecture.sunshadeStyles']) as unknown as ShadeStyle[] | undefined) ?? [];
  const project = useMemo(() => [...nodes.values()].find((n) => n.type === 'IfcProject'), [nodes]);
  const own = (project?.props.sunshadeStyles as ShadeStyle[] | undefined) ?? [];
  const selected = useShadeDialog((s) => s.typeId);
  const [newStyle, setNewStyle] = useState('concrete-canopy');
  const types = useMemo(() => [...nodes.values()].filter((n) => n.type === SHADE_TYPE).sort((a, b) => a.name.localeCompare(b.name)), [nodes]);
  const type = types.find((x) => x.id === selected) ?? types[0];
  const spec = specOf(type);
  const kind = kinds.find((k) => k.key === spec.kind) ?? kinds[0];
  const select = (id: string) => useShadeDialog.setState({ typeId: id });
  const update = (patch: Spec, name?: string) => type && void execute('architecture.updateSunshadeType', { id: type.id, ...(name ? { name } : {}), spec: patch });
  const create = async () => {
    const r = (await execute('architecture.createSunshadeType', { style: newStyle })) as { id?: string } | null;
    if (r?.id) { select(r.id); useShadePrefs.setState({ typeId: r.id }); }
  };
  const duplicate = async () => {
    if (!type) return;
    const r = (await execute('architecture.duplicateSunshadeType', { id: type.id })) as { id?: string } | null;
    if (r?.id) select(r.id);
  };
  const saveStyle = async () => {
    if (!type) return;
    const name = window.prompt(t('Name of the sunshade style'), type.name);
    if (!name?.trim()) return;
    const r = await execute('architecture.saveSunshadeStyle', { typeId: type.id, name });
    if (r) notify(t('Saved as a style of the project.'));
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Sunshade types">
      <div className="dialog wide" data-dialog="sunshade-types">
        <header className="dialog-head">
          <span className="panel-title">Sunshade types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <div style={{ display: 'grid', gap: 6 }}>
              <select value={newStyle} data-field="sunshade-new-style" onChange={(e) => setNewStyle(e.target.value)}>
                <optgroup label={t('Lapin')}>{styles.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}</optgroup>
                {own.length > 0 && <optgroup label={t('This project')}>{own.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.label}</option>)}</optgroup>}
              </select>
              <button type="button" className="btn small" data-action="sunshade-new-type" onClick={() => void create()}><Plus size={13} /> New type</button>
            </div>
            <ul className="type-list">
              {types.map((x) => (
                <li key={x.id} className={`tree-item${x.id === type?.id ? ' on' : ''}`} data-sunshade-type={x.name} onClick={() => select(x.id)}>
                  <span data-no-i18n>{x.name}</span>
                  <span className="muted small" data-no-i18n> · {relatedOf({ nodes, relations }, x.id, 'IfcRelDefinesByType').length}</span>
                  <button type="button" className="icon mini" title="Delete the type (refused while sunshades use it)" onClick={(e) => { e.stopPropagation(); void deleteType(x.id); }}><Trash2 size={11} /></button>
                </li>
              ))}
            </ul>
            {!types.length && <p className="muted small">No sunshade types yet: make one from a style, or put a sunshade on a window and its style's type is made.</p>}
            {own.length > 0 && <>
              <p className="eyebrow">The project's styles</p>
              <ul className="type-list compact">
                {own.map((s) => (
                  <li key={s.id} className="row" style={{ gap: 4 }}>
                    <span className="grow small" data-no-i18n>{s.label}</span>
                    <button type="button" className="icon mini" title="Rename" onClick={() => { const n = window.prompt(t('Name of the sunshade style'), s.label); if (n?.trim()) void execute('architecture.renameSunshadeStyle', { styleId: s.id, name: n }); }}><Pencil size={11} /></button>
                    <button type="button" className="icon mini" title="Delete the style (the types made with it stay as they are)" onClick={() => { if (window.confirm(t('Delete this sunshade style?'))) void execute('architecture.deleteSunshadeStyle', { styleId: s.id }); }}><Trash2 size={11} /></button>
                  </li>
                ))}
              </ul>
            </>}
          </div>
          {type && kind ? (
            <div className="type-edit joinery-edit" data-testid="sunshade-type-edit">
              <div className="joinery-form">
                <label className="field-line"><span className="eyebrow">Name</span>
                  <input key={type.id + type.name} data-field="sunshade-type-name" defaultValue={type.name} onBlur={(e) => { const n = e.target.value.trim(); if (n && n !== type.name) update({}, n); }} /></label>
                <label className="field-line"><span className="eyebrow">Kind</span>
                  <select data-field="sunshade-kind" value={kind.key} onChange={(e) => update({ kind: e.target.value })}>
                    {kinds.map((k) => <option key={k.key} value={k.key}>{k.label}</option>)}
                  </select></label>
                {kind.params.filter((p) => shown(p, spec, kind)).map((p) => (
                  <label key={p.key} className="field-line" data-param={p.key}><span className="eyebrow">{p.label}</span>
                    <ParamField p={p} value={valueOf(spec, kind, p.key)} onCommit={(v) => update({ [p.key]: v })} />
                  </label>
                ))}
                <div className="row wrap" style={{ gap: 6 }}>
                  <button type="button" className="btn small" onClick={() => void duplicate()} title="A copy of this type, to change"><Copy size={13} /> Duplicate</button>
                  <button type="button" className="btn small" data-action="sunshade-save-style" onClick={() => void saveStyle()} title="Keep this type's shape as a style of the project"><Save size={13} /> Save as style…</button>
                </div>
              </div>
              <ShadePreview spec={{ ...kind.defaults, ...spec, kind: kind.key }} />
              <p className="muted small" data-no-i18n>IFC: IfcShadingDevice · {kind.predefined}{kind.objectType ? ` (${kind.objectType})` : ''}</p>
              <p className="muted small">Every sunshade of this type follows a change.</p>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          ) : <div className="type-edit" />}
        </div>
      </div>
    </div>
  );
}

/** A sunshade in Properties: its type, kind, what it stands on, its own projection, the noon helper and its type's dialog. */
export function SunshadeSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const kinds = useShadeKinds();
  const type = relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType') ?? undefined;
  const spec = specOf(type);
  const kind = kinds.find((k) => k.key === spec.kind);
  const types = [...nodes.values()].filter((n) => n.type === SHADE_TYPE).sort((a, b) => a.name.localeCompare(b.name));
  const update = (attrs: Record<string, unknown>) => void execute('element.update', { ids: [node.id], attrs });
  const host = typeof node.props.host === 'string' ? nodes.get(node.props.host) : undefined;
  const wall = typeof node.props.wall === 'string' ? nodes.get(node.props.wall) : undefined;
  const own = typeof node.props.projection === 'number' ? node.props.projection : null;
  const typeProjection = Number(valueOf(spec, kind, 'projection') ?? 0);
  const noon = async () => {
    const r = (await execute('architecture.shadeAtNoon', { ids: [node.id] })) as { results?: { depth: number; altitude: number; shaded: boolean; latitude: number }[] } | null;
    const x = r?.results?.[0];
    if (!x) return;
    notify(x.shaded
      ? t('The sun stands behind this facade at noon on 21 June: it needs no shade then.')
      : `${t('Full shade at noon on 21 June')}: ${Math.round(x.depth * 1000)} mm (${t('sun')} ${x.altitude.toFixed(1)}°, ${x.latitude.toFixed(2)}° N).`);
  };
  if (node.type !== SHADE) return null;
  return (
    <div data-testid="sunshade-section">
      <dl className="fields">
        <dt>Type</dt>
        <dd>
          <select value={type?.id ?? ''} data-field="sunshade-type" onChange={(e) => void execute('architecture.setSunshadeType', { ids: [node.id], typeId: e.target.value })}>
            {types.map((x) => <option key={x.id} value={x.id} data-no-i18n>{x.name}</option>)}
          </select>
        </dd>
        <dt>Kind</dt><dd>{kind?.label ?? '—'}</dd>
        <dt>On</dt><dd data-no-i18n>{host ? host.name : wall ? `${t('Wall')} ${wall.name}` : t('A facade line')}</dd>
        {kind && (kind.over || kind.params.some((p) => p.key === 'projection')) && <>
          <dt title="Its own projection; empty: its type's">Projection</dt>
          <dd><LengthField testId="sunshade-projection" metres={own ?? typeProjection} min={0.05} max={8} onCommit={(v) => update({ projection: v && v > 0 ? v : null })} /></dd>
        </>}
        {!host && <><dt>Height</dt><dd><LengthField metres={typeof node.props.z === 'number' ? node.props.z : kind?.over ? 2.4 : 0.9} onCommit={(v) => update({ z: v ?? null })} /></dd></>}
        <dt>On the other face</dt><dd><input type="checkbox" checked={node.props.flip === true} onChange={(e) => update({ flip: e.target.checked || null })} /></dd>
      </dl>
      <div className="pad" style={{ display: 'grid', gap: 6 }}>
        {host && kind?.over && <button type="button" className="btn small" data-action="sunshade-noon" title="The projection that keeps the whole window in shade at noon on 21 June, at the project's place (Manage → Location); set as its own" onClick={() => void noon()}><Sun size={13} /> Full shade at noon, 21 June</button>}
        {own != null && <button type="button" className="btn small" onClick={() => update({ projection: null })}>Type's projection</button>}
        <button type="button" className="btn small" data-action="sunshade-edit-type" onClick={() => editShadeType(type?.id)}><Settings2 size={13} /> Edit type…</button>
      </div>
    </div>
  );
}

interface ShadeFrame { o: number[]; u: number[]; n: number[]; length: number; projection: number; over: boolean; kind: string }

/**
 * A sunshade's grips (its frame from the engine, shadeFrame): its ends along a wall or a facade line, and, for one that
 * projects (a canopy, an awning, a pergola), the middle of its front edge to drag its own projection out or in.
 */
export function sunshadeGrips(node: NodeDto): Grip[] {
  const f = node.props.shadeFrame as unknown as ShadeFrame | undefined;
  if (!f) return [];
  const at = (x: number, y: number): Vec2 => ({ x: f.o[0] + f.u[0] * x + f.n[0] * y, y: f.o[1] + f.u[1] * x + f.n[1] * y });
  const along = (p: Vec2) => (p.x - f.o[0]) * f.u[0] + (p.y - f.o[1]) * f.u[1];
  const out = (p: Vec2) => (p.x - f.o[0]) * f.n[0] + (p.y - f.o[1]) * f.n[1];
  const grips: Grip[] = [];
  if (f.over) {
    grips.push({ key: 'projection', kind: 'edgeMid', at: at(f.length / 2, f.projection), title: 'Drag: its own projection',
      step: (to) => { const p = Math.round(out(to) * 1000) / 1000; return p > 0.05 ? { command: 'architecture.updateSunshade', args: { id: node.id, projection: p } } : null; } });
  }
  const from = typeof node.props.from === 'number' ? node.props.from : 0;
  if (typeof node.props.wall === 'string') {
    grips.push(
      { key: 'from', kind: 'end', at: at(0, 0), title: 'Drag: where it starts along the wall', step: (to) => ({ command: 'architecture.updateSunshade', args: { id: node.id, from: Math.round((from + along(to)) * 1000) / 1000 } }) },
      { key: 'to', kind: 'end', at: at(f.length, 0), title: 'Drag: where it ends along the wall', step: (to) => ({ command: 'architecture.updateSunshade', args: { id: node.id, to: Math.round((from + along(to)) * 1000) / 1000 } }) },
    );
  } else if (Array.isArray(node.props.start) && Array.isArray(node.props.end)) {
    grips.push(
      { key: 'start', kind: 'end', at: { x: Number(node.props.start[0]), y: Number(node.props.start[1]) }, title: 'Drag: its start', step: (to) => ({ command: 'architecture.updateSunshade', args: { id: node.id, start: [to.x, to.y] } }) },
      { key: 'end', kind: 'end', at: { x: Number(node.props.end[0]), y: Number(node.props.end[1]) }, title: 'Drag: its end', step: (to) => ({ command: 'architecture.updateSunshade', args: { id: node.id, end: [to.x, to.y] } }) },
    );
  }
  return grips;
}
