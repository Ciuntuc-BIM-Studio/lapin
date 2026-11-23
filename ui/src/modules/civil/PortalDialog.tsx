/**
 * The tunnel-portal and bridge-abutment configurator (docs/plan-wing-walls.md, S3): a preset (straight, U, flared or warped
 * wings, an abutment with wings), its numbers by group — the layout, the wings, the section, the warp, the free end, the output —
 * each a value or a formula (`{= embankment + 0.5}`, `= (crown - embankment) * slope`), beside the engine's preview of it (plan,
 * section across the left wing, 3D), built by the same code as the element. A new one is placed by two points (the face wall's
 * middle on the road's axis, then the way the wings go) or on a road picked; one made before is rebuilt in place (same element,
 * same GlobalIds). The path may be a polyline drawn instead, the section a profile drawn and saved.
 */

import { backdropClose } from '../../shell/Dialog';
import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { TypePreview } from '../core/TypePreview';
import { choiceLabel } from '../../shell/typeConfigurator';
import { activate } from '../../platform/modules';
import type { ToolDef } from '../../platform/module';
import {
  PRESET_HINTS, PRESET_LABELS, fieldText, fullSpec, groupsOf, isFormula, readField, specOut, usePortalDialog, withPreset,
  type PortalCatalog, type PortalParam, type PortalSpec,
} from './portal';
import { drawPortalPath, placePortal, placePortalOnRoad } from './portalTools';
import { Landmark, Spline, MousePointer2 } from 'lucide-react';

export function usePortalCatalog(): PortalCatalog {
  const info = useSession((s) => s.info?.catalogs);
  return (info?.['civil.portal'] as unknown as PortalCatalog | undefined) ?? { params: [], presets: [] };
}

interface Values { values: Record<string, number>; problems: string[]; path: [number, number][] }

/** One number (or formula): its text, read back on Enter or blur; the value a formula gives beside it. */
function FormulaField({ p, value, worked, onCommit }: { p: PortalParam; value: unknown; worked?: number; onCommit: (v: number | string | null) => void }) {
  const [text, setText] = useState(fieldText(value));
  useEffect(() => setText(fieldText(value)), [value]);
  const unit = p.kind === 'length' ? 'm' : p.kind === 'angle' ? '°' : '';
  const commit = () => {
    const v = readField(text);
    if (v === undefined) { setText(fieldText(value)); return; }
    if (v !== value) onCommit(v);
  };
  return (
    <span className="row" style={{ gap: 4, alignItems: 'center' }}>
      <input data-param-input={p.key} value={text} placeholder={p.default == null ? 'auto' : fieldText(p.default)} title="A number, or a formula: = embankment + 0.5"
        style={{ flex: 1, minWidth: 0, fontFamily: isFormula(text) ? 'var(--mono, monospace)' : undefined }}
        onChange={(e) => setText(e.target.value)} onBlur={commit} onKeyDown={(e) => { if (e.key === 'Enter') commit(); }} />
      {unit && <span className="muted small" data-no-i18n>{unit}</span>}
      {isFormula(value) && worked != null && <span className="muted small" data-no-i18n data-worked={p.key}>= {fieldText(worked, 2)}</span>}
    </span>
  );
}

export function PortalDialog() {
  const catalog = usePortalCatalog();
  const nodes = useSession((s) => s.nodes);
  const { id, spec } = usePortalDialog();
  const editing = id ? nodes.get(id) : undefined;
  const full = useMemo(() => fullSpec(catalog, spec), [catalog, spec]);
  const groups = useMemo(() => groupsOf(catalog, full), [catalog, full]);
  const profiles = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinSectionProfile').sort((a, b) => a.name.localeCompare(b.name)), [nodes]);
  const [values, setValues] = useState<Values | null>(null);
  const specJson = JSON.stringify(specOut(spec));
  useEffect(() => {
    let live = true;
    const t = setTimeout(() => { void query<Values>('civil.portalValues', { spec: JSON.parse(specJson) }).then((r) => { if (live) setValues(r); }); }, 150);
    return () => { live = false; clearTimeout(t); };
  }, [specJson]);
  const set = (patch: PortalSpec) => usePortalDialog.setState({ spec: { ...spec, ...patch } });
  const close = () => useUi.setState({ dialog: null });
  const place = (tool: ToolDef) => { close(); activate(tool); };
  const apply = async () => { if (id) await execute('civil.updatePortal', { id, spec: specOut(spec) }); };
  // The preview: about the origin, without the placement (a drawn path is moved there by the engine).
  const previewSpec = useMemo(() => { const o = specOut(spec); delete o.origin; delete o.direction; return o; }, [spec]);
  const hasPath = Array.isArray(spec.path);

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Portal or abutment" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" data-dialog="portal" data-i18n-ctx="portal">
        <header className="dialog-head">
          <span className="panel-title">Portal or abutment{editing && <span data-no-i18n> · {editing.name}</span>}</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <p className="eyebrow">Presets</p>
            <ul className="type-list">
              {catalog.presets.map((p) => (
                <li key={p.key} className={`tree-item${full.preset === p.key ? ' on' : ''}`} data-portal-preset={p.key} title={PRESET_HINTS[p.key]}
                  onClick={() => usePortalDialog.setState({ spec: withPreset(catalog, spec, p.key) })}>
                  <span>{PRESET_LABELS[p.key] ?? p.key}</span>
                </li>
              ))}
            </ul>
            <p className="eyebrow">Path</p>
            <p className="muted small">{hasPath ? 'A path drawn: its first and last segments are the wings.' : 'Laid out from the numbers, placed by two points or on a road.'}</p>
            <div className="row" style={{ gap: 6, flexWrap: 'wrap' }}>
              <button type="button" className="btn small" data-action="portal-draw-path" onClick={() => place(drawPortalPath)}><Spline size={13} /> Draw the path</button>
              {hasPath && <button type="button" className="btn small" data-action="portal-clear-path" onClick={() => { const o = { ...spec }; delete o.path; usePortalDialog.setState({ spec: o }); }}>From the numbers</button>}
            </div>
            <p className="eyebrow">Section</p>
            <select style={{ width: '100%' }} data-field="portal-profile" value={typeof spec.profileId === 'string' ? spec.profileId : ''}
              onChange={(e) => set({ profileId: e.target.value || null })}>
              <option value="">Coded: crown, batters, footing</option>
              {profiles.map((p) => <option key={p.id} value={p.id} data-no-i18n>{p.name}</option>)}
            </select>
            <p className="muted small">A drawn profile is the stem's section, stretched to the wall's height.</p>
          </div>
          <div className="type-edit configurator-edit" data-testid="portal-edit">
            <div className="configurator-grid">
              <dl className="fields">
                {groups.map((g) => (
                  <div key={g.group} style={{ display: 'contents' }}>
                    <dt className="section-dt">{g.group}</dt><dd />
                    {g.params.map((p) => (
                      <div key={p.key} data-param={p.key} style={{ display: 'contents' }}>
                        <dt>{p.label}</dt>
                        <dd>
                          {p.kind === 'choice' ? (
                            <select value={String(full[p.key] ?? '')} onChange={(e) => set({ [p.key]: e.target.value })}>
                              {(p.choices ?? []).map((c) => <option key={c} value={c}>{choiceLabel(c)}</option>)}
                            </select>
                          ) : p.kind === 'bool' ? (
                            <input type="checkbox" checked={full[p.key] === true} onChange={(e) => set({ [p.key]: e.target.checked })} />
                          ) : p.kind === 'text' ? (
                            <input defaultValue={String(full[p.key] ?? '')} key={String(full[p.key] ?? '')} onBlur={(e) => set({ [p.key]: e.target.value.trim() || null })} />
                          ) : (
                            <FormulaField p={p} value={full[p.key]} worked={values?.values[p.key]} onCommit={(v) => set({ [p.key]: v })} />
                          )}
                        </dd>
                      </div>
                    ))}
                  </div>
                ))}
              </dl>
              <TypePreview typeClass="LapinPortal" type={previewSpec} caption="Drawn by the engine, as the drawings draw it: the portal or abutment as these values build it." />
            </div>
            {values && values.problems.length > 0 && (
              <ul className="muted small" data-testid="portal-problems">{values.problems.map((p) => <li key={p} data-no-i18n>{p}</li>)}</ul>
            )}
            <div className="row" style={{ gap: 6, justifyContent: 'flex-end', marginTop: 8 }}>
              {editing ? (
                <button type="button" className="btn primary" data-action="portal-apply" onClick={() => void apply()}>Apply</button>
              ) : hasPath ? (
                <button type="button" className="btn primary" data-action="portal-add" onClick={() => { close(); void execute('civil.addPortal', { spec: specOut(spec) }); }}>Add</button>
              ) : (
                <>
                  <button type="button" className="btn" data-action="portal-place-road" onClick={() => place(placePortalOnRoad)} title="Click a road or path: the portal stands across it there"><MousePointer2 size={13} /> On a road…</button>
                  <button type="button" className="btn primary" data-action="portal-place" onClick={() => place(placePortal)} title="The face wall's middle on the road's axis, then a point the way the wings go"><Landmark size={13} /> Place…</button>
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

