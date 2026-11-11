/**
 * Furniture types (docs/plan-furniture.md, Tipurile): the project's types by kind, a new one of a kind or a copy, and the
 * type edited — its name, size, its kind's parameters and its materials by role — beside the engine's preview of it (the
 * plan symbol and the front), so what is changed is seen before it is placed. Every piece of a type follows it.
 */

import { useEffect, useMemo, useState } from 'react';
import { Copy, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { relatedOf } from '../../engine/model';
import { deleteType } from '../core/deleteType';
import { LengthField, NumberField } from '../../shell/cells';
import {
  CHOICE_LABELS, ROLE_LABELS, TYPE_CLASS, appliesTo, chooseType, kindOf, planBox, planPaths, sizeOf, specOf, useTypesDialog,
  type FurnitureKind, type FurnitureSpec, type KindParam, type KitchenLayout, type PreviewDto,
} from './furniture';

export function useKinds(): FurnitureKind[] {
  return ((useSession((s) => s.info?.catalogs['furniture.kinds']) ?? []) as unknown as FurnitureKind[]);
}

/** The engine's picture of a spec: the plan symbol above, the front below. */
export function FurniturePreview({ kind, spec, length }: { kind: string; spec: FurnitureSpec; length?: number }) {
  const nodes = useSession((s) => s.nodes);
  const [p, setP] = useState<PreviewDto | null>(null);
  const key = JSON.stringify(spec);
  useEffect(() => {
    let live = true;
    void query<PreviewDto>('furniture.preview', { kind, spec, length }).then((r) => { if (live) setP(r); });
    return () => { live = false; };
  }, [kind, key, length, nodes]);
  if (!p) return <div className="furniture-preview muted small">…</div>;
  const [bx, by, bw, bh] = planBox(p);
  const fx0 = Math.min(...p.front.map((f) => f.x0), -0.1) - 0.08, fx1 = Math.max(...p.front.map((f) => f.x1), 0.1) + 0.08;
  const fz1 = Math.max(...p.front.map((f) => f.z1), 0.1) + 0.08;
  const stroke = Math.max(bw, bh) / 260;
  return (
    <div className="furniture-preview" data-testid="furniture-preview">
      <svg viewBox={`${bx} ${by} ${bw} ${bh}`} className="furniture-plan" role="img" aria-label="Plan">
        {planPaths(p).map((x, i) => (
          <path key={i} d={x.d} fill="none" stroke="currentColor" strokeWidth={stroke} strokeDasharray={x.kind === 'dashed' || x.kind === 'overhead' ? `${stroke * 6} ${stroke * 4}` : undefined} />
        ))}
      </svg>
      <svg viewBox={`${fx0} ${-fz1} ${fx1 - fx0} ${fz1 + 0.04}`} className="furniture-front" role="img" aria-label="Front">
        {p.front.map((f, i) => (
          <rect key={i} x={f.x0} y={-f.z1} width={f.x1 - f.x0} height={f.z1 - f.z0} fill={f.color} stroke="rgba(0,0,0,.35)" strokeWidth={(fx1 - fx0) / 500} />
        ))}
        <line x1={fx0} x2={fx1} y1={0} y2={0} stroke="currentColor" strokeWidth={(fx1 - fx0) / 300} />
      </svg>
      <p className="muted small" data-no-i18n>{Math.round(p.width * 1000)} × {Math.round(p.depth * 1000)} × {Math.round(p.height * 1000)} mm</p>
    </div>
  );
}

/** One of a kind's parameters, as its kind says it is edited. */
function ParamField({ p, value, onCommit }: { p: KindParam; value: unknown; onCommit: (v: unknown) => void }) {
  switch (p.kind) {
    case 'length': return <LengthField metres={Number(value ?? 0)} min={p.min ?? undefined} max={p.max ?? undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'number': return <NumberField value={Number(value ?? 0)} min={p.min ?? undefined} max={p.max ?? undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'choice': return <select value={String(value ?? '')} onChange={(e) => onCommit(e.target.value)}>{(p.choices ?? []).map((c) => <option key={c} value={c}>{CHOICE_LABELS[c] ?? c}</option>)}</select>;
    case 'bool': return <input type="checkbox" checked={value === true} onChange={(e) => onCommit(e.target.checked)} />;
    default: return null;
  }
}

export function FurnitureTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const kinds = useKinds();
  const model = { nodes, relations };
  const selected = useTypesDialog((s) => s.typeId);
  const [newKind, setNewKind] = useState('sofa');
  const [newLayout, setNewLayout] = useState('one-wall');
  const layouts = ((useSession((s) => s.info?.catalogs['furniture.kitchenLayouts']) ?? []) as unknown as KitchenLayout[]);
  const types = useMemo(() => [...nodes.values()].filter((n) => n.type === TYPE_CLASS), [nodes]);
  const type = types.find((t) => t.id === selected) ?? types[0];
  const spec = specOf(type);
  const kind = kinds.find((k) => k.key === spec.kind);
  const materials = useMemo(() => [...nodes.values()].filter((n) => n.type === 'IfcMaterial').map((n) => n.name).sort(), [nodes]);
  const select = (id: string) => useTypesDialog.setState({ typeId: id });
  const update = (patch: Record<string, unknown>, name?: string) => type && void execute('furniture.updateType', { id: type.id, ...(name ? { name } : {}), spec: patch });

  const create = async () => {
    const r = (await execute('furniture.createType', { kind: newKind, ...(newKind === 'kitchen' ? { layout: newLayout } : {}) })) as { id?: string } | null;
    if (r?.id) { select(r.id); chooseType(newKind, r.id); }
  };
  const duplicate = async () => {
    if (!type) return;
    const r = (await execute('furniture.duplicateType', { id: type.id })) as { id?: string } | null;
    if (r?.id) select(r.id);
  };
  const groups = [...new Set(kinds.map((k) => k.group))];

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Furniture types">
      <div className="dialog wide" data-dialog="furniture-types" data-i18n-ctx="furniture">
        <header className="dialog-head">
          <span className="panel-title">Furniture types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <div className="row" style={{ gap: 6 }}>
              <select value={newKind} data-field="furniture-new-kind" onChange={(e) => setNewKind(e.target.value)}>
                {groups.map((g) => <optgroup key={g} label={g}>{kinds.filter((k) => k.group === g).map((k) => <option key={k.key} value={k.key}>{k.label}</option>)}</optgroup>)}
              </select>
              {newKind === 'kitchen' && (
                <select value={newLayout} data-field="furniture-new-layout" title="The kitchen’s layout" onChange={(e) => setNewLayout(e.target.value)}>
                  {layouts.map((l) => <option key={l.key} value={l.key}>{CHOICE_LABELS[l.key] ?? l.label}</option>)}
                </select>
              )}
              <button type="button" className="btn small" data-action="furniture-new-type" onClick={() => void create()}><Plus size={13} /> New type</button>
            </div>
            {groups.map((g) => {
              const of = types.filter((t) => kinds.find((k) => k.key === kindOf(t))?.group === g).sort((a, b) => a.name.localeCompare(b.name));
              if (!of.length) return null;
              return (
                <div key={g}>
                  <p className="eyebrow">{g}</p>
                  <ul className="type-list">
                    {of.map((t) => (
                      <li key={t.id} className={`tree-item${t.id === type?.id ? ' on' : ''}`} data-furniture-type={t.name} onClick={() => select(t.id)}>
                        <span data-no-i18n>{t.name}</span>
                        <span className="muted small" data-no-i18n> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span>
                        <button type="button" className="icon mini" title="Delete the type (refused while pieces use it)" onClick={(e) => { e.stopPropagation(); void deleteType(t.id); }}><Trash2 size={11} /></button>
                      </li>
                    ))}
                  </ul>
                </div>
              );
            })}
            {!types.length && <p className="muted small">No furniture types yet: make one, or place a piece and its kind's first type is made.</p>}
          </div>
          {type && kind ? (
            <div className="type-edit furniture-edit" data-testid="furniture-type-edit">
              <div className="row" style={{ gap: 6, alignItems: 'center' }}>
                <span className="eyebrow">{kind.label}</span>
                <span className="grow" />
                <button type="button" className="btn small" onClick={() => void duplicate()} title="A copy of this type, to change"><Copy size={13} /> Duplicate</button>
              </div>
              <div className="furniture-edit-grid">
                <dl className="fields">
                  <dt>Name</dt>
                  <dd><input key={type.id + type.name} data-field="furniture-type-name" defaultValue={type.name} onBlur={(e) => { const n = e.target.value.trim(); if (n && n !== type.name) update({}, n); }} /></dd>
                  {spec.kind !== 'kitchen' && <><dt>Width</dt><dd><LengthField testId="furniture-width" metres={Number(spec.width ?? kind.width)} min={0.1} max={12} onCommit={(v) => { if (v != null) update({ width: v }); }} /></dd></>}
                  <dt>Depth</dt><dd><LengthField testId="furniture-depth" metres={Number(spec.depth ?? kind.depth)} min={0.1} max={12} onCommit={(v) => { if (v != null) update({ depth: v }); }} /></dd>
                  <dt>{spec.kind === 'kitchen' ? 'Worktop height' : 'Height'}</dt><dd><LengthField testId="furniture-height" metres={Number(spec.height ?? kind.height)} min={0.1} max={12} onCommit={(v) => { if (v != null) update({ height: v }); }} /></dd>
                  {kind.params.filter((p) => appliesTo(p, spec, kind.defaults)).map((p) => (
                    <div key={p.key} className="fields-row" data-param={p.key} style={{ display: 'contents' }}>
                      <dt>{p.label}</dt>
                      {/* A layout starts the kitchen from its sizes (a galley's aisle, an L's leg…); each stays editable. */}
                      <dd><ParamField p={p} value={spec[p.key] ?? kind.defaults[p.key]} onCommit={(v) => update(p.key === 'layout' ? (layouts.find((l) => l.key === v)?.preset ?? { layout: v }) : { [p.key]: v })} /></dd>
                    </div>
                  ))}
                  <dt className="section-dt">Materials</dt><dd />
                  {kind.roles.map((role) => (
                    <div key={role} style={{ display: 'contents' }}>
                      <dt>{ROLE_LABELS[role] ?? role}</dt>
                      <dd>
                        <input list="furniture-materials" data-role={role} key={`${type.id}${role}${spec.materials?.[role] ?? ''}`} defaultValue={spec.materials?.[role] ?? ''}
                          onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== spec.materials?.[role]) update({ materials: { [role]: v } }); }} />
                      </dd>
                    </div>
                  ))}
                </dl>
                <FurniturePreview kind={kind.key} spec={spec} />
              </div>
              <datalist id="furniture-materials">{materials.map((m) => <option key={m} value={m} />)}</datalist>
              <p className="muted small">{sizeOf(type, kinds)} · Every piece of this type follows a change. A material named here that the project has not is made.</p>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          ) : <div className="type-edit" />}
        </div>
      </div>
    </div>
  );
}

/** Opens the types dialog on a type. */
export function editType(type: NodeDto | string | undefined) {
  const id = typeof type === 'string' ? type : type?.id;
  if (id) useTypesDialog.setState({ typeId: id });
  useUi.setState({ dialog: 'furnitureTypes' });
}
