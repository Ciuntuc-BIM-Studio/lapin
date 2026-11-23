/**
 * The tunnel lining configurator (docs/plan-wing-walls.md, S4): a preset (a horseshoe for a mined tunnel, a TBM's circular ring,
 * a cut-and-cover box, a vault on walls) or a section drawn and saved, its numbers by group — the section, the lining's
 * thicknesses and outer shell, the blocks or rings, the output — each a value or a formula, beside the engine's preview of it
 * (plan, section, 3D), built by the same code as the element. A new one is placed on an axis picked between two stations, or
 * along an axis drawn; one made before is rebuilt in place (same element, same GlobalIds).
 */

import { backdropClose } from '../../shell/Dialog';
import { useEffect, useMemo, useState } from 'react';
import { X, Route, Spline } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { TypePreview } from '../core/TypePreview';
import { choiceLabel } from '../../shell/typeConfigurator';
import { activate } from '../../platform/modules';
import type { ToolDef } from '../../platform/module';
import { groupsOf, specOut, withPreset, type PortalCatalog, type PortalSpec } from './portal';
import { FormulaField } from './PortalDialog';
import { TUNNEL_PRESET_HINTS, TUNNEL_PRESET_LABELS, fullTunnelSpec, isRings, useTunnelDialog } from './tunnel';
import { drawTunnelAxis, placeTunnelOnAxis } from './tunnelTools';

export function useTunnelCatalog(): PortalCatalog {
  const info = useSession((s) => s.info?.catalogs);
  return (info?.['civil.tunnel'] as unknown as PortalCatalog | undefined) ?? { params: [], presets: [] };
}

interface Values { values: Record<string, number>; problems: string[]; sectionArea: number; clearArea: number }

export function TunnelDialog() {
  const catalog = useTunnelCatalog();
  const nodes = useSession((s) => s.nodes);
  const { id, spec } = useTunnelDialog();
  const editing = id ? nodes.get(id) : undefined;
  const full = useMemo(() => fullTunnelSpec(catalog, spec), [catalog, spec]);
  const groups = useMemo(() => groupsOf(catalog, full), [catalog, full]);
  const profiles = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinSectionProfile').sort((a, b) => a.name.localeCompare(b.name)), [nodes]);
  const [values, setValues] = useState<Values | null>(null);
  const specJson = JSON.stringify(specOut(spec));
  useEffect(() => {
    let live = true;
    const t = setTimeout(() => { void query<Values>('civil.tunnelValues', { spec: JSON.parse(specJson) }).then((r) => { if (live) setValues(r); }); }, 150);
    return () => { live = false; clearTimeout(t); };
  }, [specJson]);
  const set = (patch: PortalSpec) => useTunnelDialog.setState({ spec: { ...spec, ...patch } });
  const close = () => useUi.setState({ dialog: null });
  const place = (tool: ToolDef) => { close(); activate(tool); };
  const apply = async () => { if (id) await execute('civil.updateTunnelLining', { id, spec: specOut(spec) }); };
  const previewSpec = useMemo(() => specOut(spec), [spec]);
  const drawn = typeof spec.profileId === 'string' && !!spec.profileId;

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Tunnel lining" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" data-dialog="tunnel" data-i18n-ctx="tunnel">
        <header className="dialog-head">
          <span className="panel-title">Tunnel lining{editing && <span data-no-i18n> · {editing.name}</span>}</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <p className="eyebrow">Presets</p>
            <ul className="type-list">
              {catalog.presets.map((p) => (
                <li key={p.key} className={`tree-item${full.preset === p.key && !drawn ? ' on' : ''}`} data-tunnel-preset={p.key} title={TUNNEL_PRESET_HINTS[p.key]}
                  onClick={() => useTunnelDialog.setState({ spec: { ...withPreset(catalog, spec, p.key), profileId: null } })}>
                  <span>{TUNNEL_PRESET_LABELS[p.key] ?? p.key}</span>
                </li>
              ))}
            </ul>
            <p className="eyebrow">Section</p>
            <select style={{ width: '100%' }} data-field="tunnel-profile" value={drawn ? (spec.profileId as string) : ''}
              onChange={(e) => set({ profileId: e.target.value || null })}>
              <option value="">Coded: the preset's</option>
              {profiles.map((p) => <option key={p.id} value={p.id} data-no-i18n>{p.name}</option>)}
            </select>
            <p className="muted small">A drawn profile: its outside and the clear opening inside it, the opening's floor under the road by the invert depth.</p>
            {values && (
              <p className="muted small" data-testid="tunnel-areas">
                <span>Concrete in the section</span> <span data-no-i18n>{values.sectionArea.toFixed(2)} m²</span>
                {' · '}<span>Clear opening</span> <span data-no-i18n>{values.clearArea.toFixed(2)} m²</span>
              </p>
            )}
            <p className="eyebrow">Axis</p>
            <p className="muted small">{editing ? 'Its axis, stations and red line are the corridor configurator\'s.' : 'On an axis picked (a road, a path, a wall, a track, a drafted line) between two stations, or along an axis drawn.'}</p>
          </div>
          <div className="type-edit configurator-edit" data-testid="tunnel-edit">
            <div className="configurator-grid">
              <dl className="fields">
                {groups.map((g) => (
                  <div key={g.group} style={{ display: 'contents' }}>
                    <dt className="section-dt">{g.group}</dt><dd />
                    {g.params.filter((p) => !(drawn && g.group === 'Section' && p.key !== 'invertDepth')).map((p) => (
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
              <TypePreview typeClass="LapinTunnel" type={previewSpec} caption={isRings(full) ? 'Drawn by the engine, as the drawings draw it: a straight stretch of the lining in its rings.' : 'Drawn by the engine, as the drawings draw it: a straight stretch of the lining in its blocks.'} />
            </div>
            {values && values.problems.length > 0 && (
              <ul className="muted small" data-testid="tunnel-problems">{values.problems.map((p) => <li key={p} data-no-i18n>{p}</li>)}</ul>
            )}
            <div className="row" style={{ gap: 6, justifyContent: 'flex-end', marginTop: 8 }}>
              {editing ? (
                <button type="button" className="btn primary" data-action="tunnel-apply" onClick={() => void apply()}>Apply</button>
              ) : (
                <>
                  <button type="button" className="btn" data-action="tunnel-draw-axis" onClick={() => place(drawTunnelAxis)} title="Draw its axis as a polyline; round its corners in the corridor configurator"><Spline size={13} /> Draw the axis…</button>
                  <button type="button" className="btn primary" data-action="tunnel-place-axis" onClick={() => place(placeTunnelOnAxis)} title="Click a road, a path, a wall or a line where the lining starts, then where it ends"><Route size={13} /> On an axis…</button>
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
