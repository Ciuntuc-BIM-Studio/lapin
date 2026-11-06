/**
 * The gate configurator (report #57): the project's gate types (its own styles) and Lapin's built-in styles, a new type
 * of a kind or from a style, a copy; the type edited by groups — the gate (width, height, gap, a pedestrian door's width,
 * folding panels, the opening angle), its leaves (frame, infill, spacing, spears, top and rise), its piers (masonry or
 * steel, size, height, cap), its hardware (motor, lock) and its materials by role — beside the engine's preview of it
 * (the plan with its swing or parking zone, and its elevation). Every gate of a type follows it.
 */

import { useMemo, useState } from 'react';
import { Copy, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import type { NodeDto } from '../../../engine/protocol';
import { relatedOf } from '../../../engine/model';
import { deleteType } from '../../core/deleteType';
import { ParamField, TypePreview, applies, type Spec } from '../../../shell/typeConfigurator';
import { GATE_ROLES, TYPE_CLASS, chooseGateType, gateSummary, kindOf, specOf, useGateTypesDialog, type GateKind, type GateStyle, type ParamDef } from './gates';

export function useGateCatalogs() {
  const info = useSession((s) => s.info?.catalogs);
  return {
    kinds: (info?.['architecture.gateKinds'] ?? []) as unknown as GateKind[],
    params: (info?.['architecture.gateParams'] ?? []) as unknown as ParamDef[],
    styles: (info?.['architecture.gateStyles'] ?? []) as unknown as GateStyle[],
  };
}

export function GateTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const { kinds, params, styles } = useGateCatalogs();
  const selected = useGateTypesDialog((s) => s.typeId);
  const [newKind, setNewKind] = useState('doubleSwing');
  const types = useMemo(() => [...nodes.values()].filter((n) => n.type === TYPE_CLASS).sort((a, b) => a.name.localeCompare(b.name)), [nodes]);
  const materials = useMemo(() => [...nodes.values()].filter((n) => n.type === 'IfcMaterial').map((n) => n.name).sort(), [nodes]);
  const type = types.find((t) => t.id === selected) ?? types[0];
  const spec = specOf(type);
  const kind = kinds.find((k) => k.key === spec.kind);
  const select = (id: string) => useGateTypesDialog.setState({ typeId: id });
  const update = (patch: Record<string, unknown>, name?: string) => type && void execute('architecture.updateGateType', { id: type.id, ...(name ? { name } : {}), spec: patch });
  const made = async (args: Record<string, unknown>, k: string) => {
    const r = (await execute('architecture.createGateType', args)) as { id?: string } | null;
    if (r?.id) { select(r.id); chooseGateType(k, r.id); }
  };
  const duplicate = async () => {
    if (!type) return;
    const r = (await execute('architecture.duplicateGateType', { id: type.id })) as { id?: string } | null;
    if (r?.id) select(r.id);
  };
  const full: Spec = { ...(kind?.defaults ?? {}), kind: kind?.key, ...spec };
  const groups = [...new Set(params.map((p) => p.group ?? 'Gate'))];

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Gate types">
      <div className="dialog wide" data-dialog="gate-types" data-i18n-ctx="gates">
        <header className="dialog-head">
          <span className="panel-title">Gate types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <div className="row" style={{ gap: 6, flexWrap: 'wrap' }}>
              <select style={{ flex: 1, minWidth: 0 }} value={newKind} data-field="gate-new-kind" onChange={(e) => setNewKind(e.target.value)}>
                {kinds.map((k) => <option key={k.key} value={k.key}>{k.label}</option>)}
              </select>
              <button type="button" className="btn small" data-action="gate-new-type" onClick={() => void made({ kind: newKind }, newKind)}><Plus size={13} /> New type</button>
            </div>
            <p className="eyebrow">Project styles</p>
            <ul className="type-list">
              {types.map((t) => (
                <li key={t.id} className={`tree-item${t.id === type?.id ? ' on' : ''}`} data-gate-type={t.name} onClick={() => select(t.id)}>
                  <span data-no-i18n>{t.name}</span>
                  <span className="muted small" data-no-i18n> · {relatedOf({ nodes, relations }, t.id, 'IfcRelDefinesByType').length}</span>
                  <button type="button" className="icon mini" title="Delete the type (refused while gates use it)" onClick={(e) => { e.stopPropagation(); void deleteType(t.id); }}><Trash2 size={11} /></button>
                </li>
              ))}
            </ul>
            {!types.length && <p className="muted small">No gate types yet: add a built-in style, make one, or place a gate and its kind's first type is made.</p>}
            <p className="eyebrow">Built-in styles</p>
            <ul className="type-list">
              {styles.map((st) => (
                <li key={st.key} className="tree-item" data-gate-style={st.key} title="Add it to the project's styles" onClick={() => void made({ style: st.key }, st.kind)}>
                  <Plus size={11} /> <span>{st.name}</span>
                </li>
              ))}
            </ul>
          </div>
          {type && kind ? (
            <div className="type-edit configurator-edit" data-testid="gate-type-edit">
              <div className="row" style={{ gap: 6, alignItems: 'center' }}>
                <span className="eyebrow">{kind.label}</span>
                <span className="grow" />
                <button type="button" className="btn small" onClick={() => void duplicate()} title="A copy of this type, to change"><Copy size={13} /> Duplicate</button>
              </div>
              <div className="configurator-grid">
                <dl className="fields">
                  <dt>Name</dt>
                  <dd><input key={type.id + type.name} data-field="gate-type-name" defaultValue={type.name} onBlur={(e) => { const n = e.target.value.trim(); if (n && n !== type.name) update({}, n); }} /></dd>
                  {groups.map((g) => {
                    const shown = params.filter((p) => (p.group ?? 'Gate') === g && applies(p, full));
                    if (!shown.length) return null;
                    return (
                      <div key={g} style={{ display: 'contents' }}>
                        <dt className="section-dt">{g}</dt><dd />
                        {shown.map((p) => (
                          <div key={p.key} data-param={p.key} style={{ display: 'contents' }}>
                            <dt>{p.label}</dt>
                            <dd><ParamField p={p} value={full[p.key]} onCommit={(v) => update({ [p.key]: v })} /></dd>
                          </div>
                        ))}
                      </div>
                    );
                  })}
                  <dt className="section-dt">Materials</dt><dd />
                  {Object.entries(GATE_ROLES).filter(([role]) => (role !== 'cap' || full.piers === 'masonry') && (role !== 'pier' || full.piers !== 'none') && (role !== 'motor' || full.motor === true)).map(([role, label]) => (
                    <div key={role} style={{ display: 'contents' }}>
                      <dt>{label}</dt>
                      <dd>
                        <input list="gate-materials" data-role={role} key={`${type.id}${role}${spec.materials?.[role] ?? ''}`} defaultValue={spec.materials?.[role] ?? ''}
                          onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== spec.materials?.[role]) update({ materials: { [role]: v } }); }} />
                      </dd>
                    </div>
                  ))}
                </dl>
                <TypePreview queryName="architecture.gatePreview" args={{ kind: kind.key, spec }} testId="gate-preview"
                  caption={(p) => `${Math.round(Number(p.overall ?? p.width) * 1000)} × ${Math.round(p.height * 1000)} mm${p.parking ? ` · parks in ${Math.round(Number(p.parking) * 1000)} mm` : ''}`} />
              </div>
              <datalist id="gate-materials">{materials.map((m) => <option key={m} value={m} />)}</datalist>
              <p className="muted small">{gateSummary(type, kinds)} · Every gate of this type follows a change. A material named here that the project has not is made.</p>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          ) : <div className="type-edit" />}
        </div>
      </div>
    </div>
  );
}

/** Opens the configurator on a type. */
export function editGateType(type: NodeDto | string | undefined) {
  const id = typeof type === 'string' ? type : type?.id;
  if (id) useGateTypesDialog.setState({ typeId: id });
  useUi.setState({ dialog: 'gateTypes' });
}

export { kindOf };
