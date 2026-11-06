/**
 * Exhaust fan types (report #55): the project's types by kind and the built-in ones, a new type of a kind or from a
 * built-in one, a copy; the type edited — its name and every parameter its kind takes (duct size, grille shape, size,
 * pattern and finish, airflow, power, mounting, control, shutter, light) — beside the engine's preview (the plan symbol
 * and the face). Every fan of a type follows it.
 */

import { useMemo, useState } from 'react';
import { Copy, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import type { NodeDto } from '../../../engine/protocol';
import { relatedOf } from '../../../engine/model';
import { deleteType } from '../../core/deleteType';
import { ParamField, TypePreview, applies, type Spec } from '../../../shell/typeConfigurator';
import { TYPE_CLASS, chooseFanType, fanSummary, kindOf, specOf, useFanTypesDialog, type FanKind, type FanPreset, type ParamDef } from './fans';

export function useFanCatalogs() {
  const info = useSession((s) => s.info?.catalogs);
  return {
    kinds: (info?.['mep.fanKinds'] ?? []) as unknown as FanKind[],
    params: (info?.['mep.fanParams'] ?? []) as unknown as ParamDef[],
    presets: (info?.['mep.fanPresets'] ?? []) as unknown as FanPreset[],
  };
}

export function FanTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const { kinds, params, presets } = useFanCatalogs();
  const selected = useFanTypesDialog((s) => s.typeId);
  const [newKind, setNewKind] = useState('wallAxial');
  const types = useMemo(() => [...nodes.values()].filter((n) => n.type === TYPE_CLASS).sort((a, b) => a.name.localeCompare(b.name)), [nodes]);
  const type = types.find((t) => t.id === selected) ?? types[0];
  const spec = specOf(type);
  const kind = kinds.find((k) => k.key === spec.kind);
  const select = (id: string) => useFanTypesDialog.setState({ typeId: id });
  const update = (patch: Record<string, unknown>, name?: string) => type && void execute('mep.updateFanType', { id: type.id, ...(name ? { name } : {}), spec: patch });
  const made = async (args: Record<string, unknown>, k: string) => {
    const r = (await execute('mep.createFanType', args)) as { id?: string } | null;
    if (r?.id) { select(r.id); chooseFanType(k, r.id); }
  };
  const duplicate = async () => {
    if (!type) return;
    const r = (await execute('mep.duplicateFanType', { id: type.id })) as { id?: string } | null;
    if (r?.id) select(r.id);
  };
  const defaults = { ...(kind?.defaults ?? {}), kind: kind?.key };
  const full: Spec = { ...defaults, ...spec };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Exhaust fan types">
      <div className="dialog wide" data-dialog="fan-types" data-i18n-ctx="fans">
        <header className="dialog-head">
          <span className="panel-title">Exhaust fan types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <div className="row" style={{ gap: 6, flexWrap: 'wrap' }}>
              <select style={{ flex: 1, minWidth: 0 }} value={newKind} data-field="fan-new-kind" onChange={(e) => setNewKind(e.target.value)}>
                {kinds.map((k) => <option key={k.key} value={k.key}>{k.label}</option>)}
              </select>
              <button type="button" className="btn small" data-action="fan-new-type" onClick={() => void made({ kind: newKind }, newKind)}><Plus size={13} /> New type</button>
            </div>
            <p className="eyebrow">Project types</p>
            <ul className="type-list">
              {types.map((t) => (
                <li key={t.id} className={`tree-item${t.id === type?.id ? ' on' : ''}`} data-fan-type={t.name} onClick={() => select(t.id)}>
                  <span data-no-i18n>{t.name}</span>
                  <span className="muted small" data-no-i18n> · {relatedOf({ nodes, relations }, t.id, 'IfcRelDefinesByType').length}</span>
                  <button type="button" className="icon mini" title="Delete the type (refused while fans use it)" onClick={(e) => { e.stopPropagation(); void deleteType(t.id); }}><Trash2 size={11} /></button>
                </li>
              ))}
            </ul>
            {!types.length && <p className="muted small">No fan types yet: add a built-in one, make one, or place a fan and its kind's first type is made.</p>}
            <p className="eyebrow">Built-in types</p>
            <ul className="type-list">
              {presets.map((p) => (
                <li key={p.key} className="tree-item" data-fan-preset={p.key} title="Add it to the project's types" onClick={() => void made({ preset: p.key }, p.kind)}>
                  <Plus size={11} /> <span>{p.name}</span>
                </li>
              ))}
            </ul>
          </div>
          {type && kind ? (
            <div className="type-edit configurator-edit" data-testid="fan-type-edit">
              <div className="row" style={{ gap: 6, alignItems: 'center' }}>
                <span className="eyebrow">{kind.label}</span>
                <span className="grow" />
                <button type="button" className="btn small" onClick={() => void duplicate()} title="A copy of this type, to change"><Copy size={13} /> Duplicate</button>
              </div>
              <div className="configurator-grid">
                <dl className="fields">
                  <dt>Name</dt>
                  <dd><input key={type.id + type.name} data-field="fan-type-name" defaultValue={type.name} onBlur={(e) => { const n = e.target.value.trim(); if (n && n !== type.name) update({}, n); }} /></dd>
                  {params.filter((p) => applies(p, full)).map((p) => (
                    <div key={p.key} data-param={p.key} style={{ display: 'contents' }}>
                      <dt>{p.label}</dt>
                      <dd><ParamField p={p.key === 'mounting' ? { ...p, choices: kind.mountings } : p} value={full[p.key]} onCommit={(v) => update({ [p.key]: v })} /></dd>
                    </div>
                  ))}
                </dl>
                <TypePreview queryName="mep.fanPreview" args={{ kind: kind.key, spec }} testId="fan-preview" caption={(p) => `${Math.round(p.width * 1000)} × ${Math.round(p.height * 1000)} mm · ${String(p.airflow)} m³/h`} />
              </div>
              <p className="muted small">{fanSummary(type, kinds)} · Every fan of this type follows a change.</p>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          ) : <div className="type-edit" />}
        </div>
      </div>
    </div>
  );
}

/** Opens the types dialog on a type. */
export function editFanType(type: NodeDto | string | undefined) {
  const id = typeof type === 'string' ? type : type?.id;
  if (id) useFanTypesDialog.setState({ typeId: id });
  useUi.setState({ dialog: 'fanTypes' });
}

export { kindOf };
