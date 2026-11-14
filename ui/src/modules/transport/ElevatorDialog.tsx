/**
 * The elevator configurator (docs/plan-elevators.md, reports #54 and #97): the catalogue's twelve kinds and the project's
 * own styles on the left; on the right the elevator being configured — its car and load, its doors and stops, its shaft —
 * over a plan of it at its lowest stop and a section through the car from the pit to the top of the shaft, both as the
 * engine would build the draft, with what EN 81-20 and EN 81-70 say of it. A style picked fills every field in; any
 * field may then be changed by hand. Apply is one engine command, one undo step. A circular vacuum lift (report #149)
 * shows its tube, cabin, frame and doors instead: tube presets, and each landing's door facing its own angle.
 */

import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Pencil, RotateCcw, Save, Scale, Trash2, X } from 'lucide-react';
import { LengthField, NumberField } from '../../shell/cells';
import { PendingDialog } from '../core/PendingDialog';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { fieldShown, type FieldDescriptor } from '../../engine/protocol';
import { relatingOf } from '../../engine/model';
import { t, useLang } from '../../i18n';
import { CLASS, DOOR_ANGLES, TUBE_PRESETS, groupStyles, isVacuum, itemPath, landingAngle, planBox, setLandingAngle, stopsBetween, styleAbout, toggleStop, useElevatorDialog, type ElevatorStyle, type OwnStyle, type Preview } from './elevator';

interface Draft { style?: string; attrs: Record<string, unknown> }

const TABS = [['car', 'Car and load'], ['doors', 'Doors and stops'], ['shaft', 'Shaft']] as const;
type Tab = (typeof TABS)[number][0];

// Each tab's fields; those that do not apply to the drive hide (the engine's ShowIf): a vacuum lift's tube, cabin, frame and doors.
const FIELDS: Record<Tab, string[]> = {
  car: ['drive', 'load', 'persons', 'speed', 'accessible', 'tubeDiameter', 'cabinDiameter', 'cabinWidth', 'cabinDepth', 'cabinHeight', 'glass', 'tubeMaterial', 'tubeColor', 'cabinColor', 'fireFighting', 'fireExit'],
  doors: ['entrances', 'doorKind', 'doorWidth', 'doorHeight', 'doorOpening', 'doorPanels', 'doorFrame', 'cabinDoor', 'doorAngle'],
  shaft: ['counterweight', 'jackSide', 'pit', 'headroom', 'machineRoom', 'shaftGlass', 'shaftThickness', 'columns', 'columnSize', 'frameColor', 'ringBeamHeight', 'headUnitHeight', 'baseRingHeight', 'rotation'],
};

const FILL: Record<string, string> = { shaft: '#b8b4ad', machine: '#c9c4bb', cabin: '#e4e8ec', door: '#8d959d', weight: '#5d646c', tube: '#9cc8e8', frame: '#2b2f33' };

function FieldInput({ f, value, onChange }: { f: FieldDescriptor; value: unknown; onChange: (v: unknown) => void }) {
  if (f.kind === 'choice') {
    return (
      <select value={typeof value === 'string' ? value : ''} onChange={(e) => onChange(e.target.value)}>
        {typeof value !== 'string' && <option value="" disabled>—</option>}
        {(f.choices ?? []).map((c) => <option key={c} value={c}>{c}</option>)}
      </select>
    );
  }
  if (f.kind === 'bool') return <input type="checkbox" checked={value === true} onChange={(e) => onChange(e.target.checked)} />;
  if (f.kind === 'color') return <input type="color" value={typeof value === 'string' ? value : '#2b2f33'} onChange={(e) => onChange(e.target.value)} />;
  const n = typeof value === 'number' ? value : null;
  return f.kind === 'length'
    ? <LengthField metres={n} min={f.min ?? undefined} max={f.max ?? undefined} onCommit={(m) => { if (m != null) onChange(m); }} />
    : <NumberField value={n} suffix={f.kind === 'angle' ? '°' : undefined} onCommit={(v) => { if (v != null) onChange(v); }} />;
}

/** The plan at the lowest stop: the shaft's walls with the landing door's opening, the car with its cross, the counterweight, the doors. */
function PlanPlot({ p }: { p: Preview }) {
  const items = p.plan ?? [];
  const [x, y, w, h] = planBox(items);
  return (
    <svg viewBox={`${x} ${y} ${w} ${h}`} width={300} height={Math.min(300, (300 * h) / Math.max(w, 1e-6))} aria-label="Plan" className="elevator-plan">
      {items.map((i, k) => (
        <path key={k} d={itemPath(i)} vectorEffect="non-scaling-stroke"
          fill={i.kind === 'wall' ? '#b8b4ad' : i.kind === 'room' ? '#f1eee8' : 'none'}
          stroke={i.kind === 'wall' ? '#3a3a3a' : i.kind === 'dashed' ? '#6c7680' : '#1e2a3a'}
          strokeDasharray={i.kind === 'dashed' ? '3 2' : undefined} strokeWidth={i.kind === 'wall' ? 1 : 0.8} />
      ))}
    </svg>
  );
}

/** The section through the car, front (left) to back: the pit floor, the walls with the landings' openings, the cap, the machine room, the stops. */
function SectionPlot({ s }: { s: NonNullable<Preview['section']> }) {
  const W = 300, H = 360, pad = 14, label = 70;
  const ys = s.items.flatMap((i) => [i.y0, i.y1]);
  const [y0, y1] = [Math.min(...ys), Math.max(...ys)];
  const [z0, z1] = [s.bottom, s.top];
  const k = Math.min((W - 2 * pad - label) / Math.max(1e-6, y1 - y0), (H - 2 * pad) / Math.max(1e-6, z1 - z0));
  const X = (y: number) => pad + (y - y0) * k;
  const Z = (z: number) => H - pad - (z - z0) * k;
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Section" className="elevator-section">
      {s.items.map((i, n) => (
        <rect key={n} x={X(i.y0)} y={Z(i.z1)} width={(i.y1 - i.y0) * k} height={(i.z1 - i.z0) * k}
          fill={FILL[i.role] ?? '#ccc'} fillOpacity={i.role === 'cabin' ? 0.6 : 1} stroke="#3a3a3a" strokeWidth={0.5}><title>{i.role}</title></rect>
      ))}
      {s.levels.map((l) => (
        <g key={l.id}>
          <line x1={pad} x2={X(y1) + 6} y1={Z(l.z)} y2={Z(l.z)} stroke={l.served ? '#b4502a' : '#8a8378'} strokeDasharray={l.served ? undefined : '3 2'} strokeWidth={0.8} />
          <text x={X(y1) + 8} y={Z(l.z) + 3} fontSize={9} data-no-i18n>{`${l.z >= 0 ? '+' : ''}${l.z.toFixed(2)} ${l.name}`}</text>
        </g>
      ))}
    </svg>
  );
}

export function ElevatorDialog() {
  useLang((s) => s.lang);
  const id = useElevatorDialog((s) => s.id);
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const styles = (info?.catalogs['transport.styles'] as unknown as ElevatorStyle[] | undefined) ?? [];
  const project = [...nodes.values()].find((n) => n.type === 'IfcProject');
  const own = (project?.props.elevatorStyles as OwnStyle[] | undefined) ?? [];
  const fields = useMemo(() => new Map((info?.nodeTypes.find((c) => c.type === CLASS)?.fields ?? []).map((f) => [f.key, f])), [info]);
  const [tab, setTab] = useState<Tab>('car');
  const [draft, setDraft] = useState<Draft>({ attrs: {} });
  const [preview, setPreview] = useState<Preview | null>(null);

  useEffect(() => {
    if (!id) return;
    let live = true;
    void query<Preview>('transport.preview', { id, ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs }).then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [id, draft, node]);
  useEffect(() => { if (!node) useUi.setState({ dialog: null }); }, [node]);

  if (!node) return <PendingDialog title="Elevator" />;
  const close = () => useUi.setState({ dialog: null });
  const dirty = Boolean(draft.style) || Object.keys(draft.attrs).length > 0;
  const props = { ...node.props, ...(preview?.attrs ?? {}), ...draft.attrs };
  const value = (k: string): unknown => props[k];
  const set = (k: string, v: unknown) => setDraft((d) => ({ ...d, attrs: { ...d.attrs, [k]: v } }));
  const pickStyle = (sid: string) => setDraft({ style: sid, attrs: {} });
  const args = () => ({ id: node.id, ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs });
  const apply = async () => { await execute('transport.update', args()); if (!useSession.getState().error) setDraft({ attrs: {} }); };
  const current = draft.style ?? (dirty ? undefined : (node.props.style as string | undefined));
  const ownStyle = own.find((s) => s.id === (draft.style ?? node.props.style));
  const saveAs = async () => {
    const name = window.prompt(t('Name of the elevator style'), '');
    if (!name?.trim()) return;
    await execute('transport.saveStyle', { ...args(), name });
    if (!useSession.getState().error) setDraft({ attrs: {} });
  };
  const updateStyle = async () => {
    if (!ownStyle) return;
    await execute('transport.saveStyle', { ...args(), name: ownStyle.label, styleId: ownStyle.id });
    if (!useSession.getState().error) setDraft({ attrs: {} });
  };

  const storeys = [...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey').sort((a, b) => Number(a.props.elevation ?? 0) - Number(b.props.elevation ?? 0));
  const bottom = relatingOf({ nodes, relations }, node.id, 'IfcRelContainedInSpatialStructure')?.id;
  const top = (value('topStoreyId') as string | undefined) ?? bottom;
  const stops = stopsBetween(storeys, bottom, top);
  const unserved = (value('unserved') as string[] | undefined) ?? [];
  const wallTypes = [...nodes.values()].filter((n) => n.type === 'IfcWallType').sort((a, b) => a.name.localeCompare(b.name));
  const vacuum = isVacuum(props);
  const doorAngles = value('doorAngles') as Record<string, number> | undefined;

  const rows = FIELDS[tab].filter((k) => fields.has(k) && fieldShown(fields.get(k)!, props)).map((k) => {
    const f = fields.get(k)!;
    return (
      <label key={k} className="field-line roof-field"><span className="eyebrow">{f.label}</span>
        <FieldInput f={f} value={value(k)} onChange={(v) => set(k, v)} />
        {k === 'tubeDiameter' && (
          <span className="row" style={{ gap: 4 }} data-testid="vacuum-tube-presets">
            {TUBE_PRESETS.map((d) => (
              <button key={d} type="button" className={`btn small${Math.abs(Number(value(k)) - d) < 1e-6 ? ' on' : ''}`} data-no-i18n onClick={() => set(k, d)}>Ø {Math.round(d * 1000)}</button>
            ))}
          </span>
        )}
      </label>
    );
  });

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Elevator configurator" data-i18n-ctx="transport">
      <div className="dialog wide roof-dialog elevator-dialog">
        <header className="dialog-head">
          <span className="panel-title">Elevator configurator <span className="muted small" data-no-i18n>· {node.name}</span></span>
          <button type="button" className="icon" onClick={() => { if (!dirty || window.confirm(t('Close without applying the changes?'))) close(); }} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div className="type-list roof-styles">
            <button type="button" className={`tree-item${!current ? ' on' : ''}`} onClick={() => setDraft({ attrs: {} })} title="The elevator as it is">As it is</button>
            <p className="eyebrow">Project</p>
            {own.length === 0 && <p className="muted small">Configure an elevator, then save it as a style.</p>}
            {own.map((s) => (
              <div key={s.id} className="row roof-own">
                <button type="button" className={`tree-item grow${current === s.id ? ' on' : ''}`} onClick={() => pickStyle(s.id)}>
                  <span className="grow" data-no-i18n>{s.label}</span>
                  <span className="muted small" data-no-i18n>{styleAbout(s.attrs)}</span>
                </button>
                <button type="button" className="icon mini" title="Rename" onClick={() => { const n = window.prompt(t('Name of the elevator style'), s.label); if (n?.trim()) void execute('transport.renameStyle', { styleId: s.id, name: n }); }}><Pencil size={12} /></button>
                <button type="button" className="icon mini" title="Delete the style (its elevators keep how they are)" onClick={() => { if (window.confirm(t('Delete this elevator style? Its elevators keep how they are.'))) void execute('transport.deleteStyle', { styleId: s.id }); }}><Trash2 size={12} /></button>
              </div>
            ))}
            {groupStyles(styles).map(([group, list]) => (
              <div key={group}>
                <p className="eyebrow">{group}</p>
                {list.map((s) => (
                  <button key={s.id} type="button" className={`tree-item${current === s.id ? ' on' : ''}`} title={s.description} data-style={s.id} onClick={() => pickStyle(s.id)}>
                    <span className="grow">{s.label}</span>
                    <span className="muted small" data-no-i18n>{styleAbout(s.values)}</span>
                  </button>
                ))}
              </div>
            ))}
          </div>
          <div className="roof-edit">
            <div className="tabs-inline" role="tablist">
              {TABS.map(([k, l]) => <button key={k} type="button" role="tab" aria-selected={tab === k} className={tab === k ? 'on' : ''} onClick={() => setTab(k)}>{vacuum && k === 'shaft' ? 'Tube and frame' : l}</button>)}
            </div>
            <div className="joinery-edit">
              <div className="joinery-form">
                {rows}
                {tab === 'car' && !vacuum && preview?.suggested && (
                  <button type="button" className="btn small" title="The least rated load EN 81-20 gives this car, and the persons it carries"
                    onClick={() => setDraft((d) => ({ ...d, attrs: { ...d.attrs, load: preview.suggested!.load, persons: preview.suggested!.persons } }))}>
                    <Scale size={13} /> Load and persons from the car
                  </button>
                )}
                {tab === 'doors' && (
                  <>
                    <label className="field-line roof-field"><span className="eyebrow">Highest stop</span>
                      <select value={top ?? ''} onChange={(e) => set('topStoreyId', e.target.value)}>
                        {storeys.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
                      </select>
                    </label>
                    <div className="field-line roof-field"><span className="eyebrow">Stops on</span>
                      <div className="elevator-stops">
                        {[...stops].reverse().map((s) => (
                          <div key={s.id} className="row small" style={{ gap: 4 }}>
                            <label className="row small" style={{ gap: 4 }}>
                              <input type="checkbox" checked={!unserved.includes(s.id)} disabled={s.id === stops[0]?.id || s.id === stops[stops.length - 1]?.id}
                                onChange={(e) => set('unserved', toggleStop(unserved, s.id, e.target.checked))} />
                              <span data-no-i18n>{s.name}</span>
                            </label>
                            {vacuum && !unserved.includes(s.id) && (
                              <select value={doorAngles?.[s.id] ?? ''} title="Where the door faces at this landing" data-field="vacuum-landing-angle"
                                onChange={(e) => set('doorAngles', setLandingAngle(doorAngles, s.id, e.target.value === '' ? null : Number(e.target.value)))}>
                                <option value="">{`${t('As the doors')} (${landingAngle({ doorAngle: value('doorAngle') }, s.id)}°)`}</option>
                                {DOOR_ANGLES.map((a) => <option key={a} value={a} data-no-i18n>{a}°</option>)}
                              </select>
                            )}
                          </div>
                        ))}
                      </div>
                    </div>
                  </>
                )}
                {tab === 'shaft' && !vacuum && (
                  <label className="field-line roof-field"><span className="eyebrow">Shaft walls</span>
                    <select value={(value('shaftWallTypeId') as string | undefined) ?? ''} onChange={(e) => set('shaftWallTypeId', e.target.value || null)}>
                      <option value="">Concrete, the thickness below</option>
                      {wallTypes.map((w) => <option key={w.id} value={w.id} data-no-i18n>{w.name}</option>)}
                    </select>
                  </label>
                )}
              </div>
              <div className="joinery-preview">
                {preview && !preview.problem && (tab === 'shaft' || tab === 'doors') && preview.section ? <SectionPlot s={preview.section} /> : preview && !preview.problem && <PlanPlot p={preview} />}
                <p className="muted small">{tab === 'car' ? 'Plan at the lowest stop, the doors at the bottom.' : vacuum ? 'Section through the tube, its front on the left.' : 'Section through the car, its front on the left.'}</p>
                {preview?.problem && <p className="error-text" data-no-i18n>{preview.problem}</p>}
                {preview?.checks && preview.checks.length > 0 && (
                  <ul className="elevator-checks small" data-no-i18n>
                    {preview.checks.map((c) => <li key={c}><AlertTriangle size={12} /> {c}</li>)}
                  </ul>
                )}
                {preview?.checks && preview.checks.length === 0 && !preview.indicative && <p className="small ok-text">Meets EN 81-20 and EN 81-70 as configured.</p>}
                {preview?.indicative && <p className="muted small">Indicative values: the maker's data and EN 81-41 decide.</p>}
                {preview?.summary && (
                  <dl className="fields roof-summary">
                    {preview.summary.map(([k, v]) => <div key={k} className="contents"><dt>{k}</dt><dd className="num" data-no-i18n>{v}</dd></div>)}
                  </dl>
                )}
              </div>
              <div className="row joinery-actions">
                <span className="muted small grow">{dirty ? (draft.style ? `${styles.find((s) => s.id === draft.style)?.label ?? own.find((s) => s.id === draft.style)?.label ?? draft.style}${Object.keys(draft.attrs).length ? ', changed by hand' : ''}` : 'Changed, not applied yet') : ''}</span>
                <button type="button" className="btn" disabled={!dirty} onClick={() => setDraft({ attrs: {} })}><RotateCcw size={14} />Reset</button>
                {ownStyle && <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save the elevator as it is configured into its style; every elevator of the style follows" onClick={() => void updateStyle()}><Save size={14} />Update style</button>}
                <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save the elevator as it is configured as a style of the project" onClick={() => void saveAs()}><Save size={14} />Save as style…</button>
                <button type="button" className="btn primary" disabled={!dirty || Boolean(preview?.problem)} data-action="elevator-apply" onClick={() => void apply()}>Apply</button>
              </div>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
