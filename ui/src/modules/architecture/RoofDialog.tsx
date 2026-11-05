/**
 * The roof configurator (decision 231): the catalogue's styles on the left, and on the right the roof being configured —
 * its shape and edges, its covering and drainage, its timber framing, its openings — over a plan of it (the edges clicked
 * through auto, eave, gable) and a section across its ridge through the framing, both as the engine would build the draft.
 * A style picked fills every field in; any field may then be changed by hand. Apply is one engine command, one undo step.
 */

import { useEffect, useMemo, useState } from 'react';
import { lengthText } from '../../engine/unitsStore';
import { LengthField, NumberField } from '../../shell/cells';
import { PendingDialog } from '../core/PendingDialog';
import { MaterialPicker } from '../core/MaterialPicker';
import { mergeEdge, type Edge } from './roofEdges';
import { Pencil, RotateCcw, Save, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { FieldDescriptor } from '../../engine/protocol';
import { t, useLang } from '../../i18n';

type Style = { id: string; label: string; family: string; region: string; shape: string; pitch: number; covering: string; description?: string; structure?: string | null };
type Covering = { id: string; color: string; minPitch: number };
/** A project's own style (decision 233), kept on the project. */
type OwnStyle = { id: string; label: string; shape: string; pitch: number; structure?: string };

type Item = { role: string; pts: number[] };
interface Preview {
  problem?: string | null;
  attrs?: Record<string, unknown>;
  outline?: [number, number][];
  eaves?: [number, number][];
  kinds?: ('eave' | 'gable')[];
  lines?: { kind: string; a: [number, number]; b: [number, number] }[];
  section?: { items: Item[]; box: [number, number, number, number]; span?: number | null; plate: number; top: number };
  /** Where the downpipes leave the gutters (decision 235), and whether they are the roof's automatic ones. */
  outlets?: [number, number][];
  downpipes?: string;
  summary?: [string, string][];
}
interface Draft { style?: string; attrs: Record<string, unknown>; edges?: Edge[] }

const TABS = [['shape', 'Shape'], ['covering', 'Covering and drainage'], ['framing', 'Framing'], ['openings', 'Openings']] as const;
type Tab = (typeof TABS)[number][0];

/** Which fields each tab shows, and when (a mansard's upper pitch only on a mansard…). */
const FIELDS: Record<Tab, [string, ((v: (k: string) => unknown) => boolean)?][]> = {
  shape: [['shape'], ['pitch', (v) => v('shape') !== 'barrel'], ['overhang'], ['gableOverhang'], ['ridgeDirection', (v) => ['gable', 'shed', 'barrel'].includes(String(v('shape')))],
    ['shedFlip', (v) => v('shape') === 'shed'], ['upperPitch', (v) => v('shape') === 'mansard'], ['mansardInset', (v) => v('shape') === 'mansard'], ['rise', (v) => v('shape') === 'barrel'], ['eaveOffset']],
  covering: [['covering'], ['coveringMaterial'], ['gutterProfile'], ['gutterMaterial', (v) => v('gutterProfile') !== 'none'],
    ['gutterWidth', (v) => v('gutterProfile') !== 'none'], ['gutterOffset', (v) => v('gutterProfile') !== 'none'], ['gutterDrop', (v) => v('gutterProfile') !== 'none'],
    ['gutterFall', (v) => v('gutterProfile') !== 'none'], ['gutterEnds', (v) => v('gutterProfile') !== 'none'], ['downpipes', (v) => v('gutterProfile') !== 'none'], ['snowGuards']],
  framing: [['structure', (v) => !['flat', 'barrel'].includes(String(v('shape')))],
    ['rafterSection', (v) => !['none'].includes(String(v('structure')))], ['rafterSpacing', (v) => v('structure') !== 'none'],
    ['purlins', (v) => (v('structure') ?? 'rafters') === 'rafters'], ['webSection', (v) => v('structure') === 'trusses'], ['collarHeight', (v) => v('structure') === 'collar'],
    ['timberGrade', (v) => v('structure') !== 'none'], ['rafterTail', (v) => v('structure') !== 'none'], ['rafterSeat', (v) => ['rafters', 'collar', undefined].includes(v('structure') as string | undefined)]],
  openings: [],
};

/** What the engine takes for a field the roof does not have yet (a roof made before the field was). */
const DEFAULTS: Record<string, unknown> = { rafterTail: 'plumb', rafterSeat: 'birdsmouth', collarHeight: 0.66, timberGrade: 'C24', webSection: '45x95 mm', ridgeDirection: 'auto', shedFlip: false,
  gutterWidth: 0.125, gutterOffset: 0, gutterDrop: 0, gutterFall: 0, gutterEnds: 'True', downpipes: 'auto' };

const ROLE_FILL: Record<string, string> = {
  covering: '#b0563d', deck: '#c9b79c', wall: '#d9d4c9', plate: '#8a6a48', rafter: '#c99a64', topChord: '#c99a64', bottomChord: '#b98a5a',
  web: '#d8b07a', collar: '#a87a46', purlin: '#8a6a48', ridge: '#8a6a48', trimmer: '#b98a5a',
};

/** A field as the engine describes it: a choice, a tick, an angle, a length in mm, a number. */
function FieldInput({ f, value, onChange }: { f: FieldDescriptor; value: unknown; onChange: (v: unknown) => void }) {
  if (f.kind === 'choice') {
    return (
      <select value={typeof value === 'string' ? value : ''} onChange={(e) => onChange(e.target.value)}>
        {typeof value !== 'string' && <option value="" disabled>—</option>}
        {(f.choices ?? []).map((c) => <option key={c} value={c}>{c}</option>)}
      </select>
    );
  }
  if (f.kind === 'material') return <MaterialPicker value={typeof value === 'string' ? value : ''} placeholder="none" allowEmpty onChange={(name) => onChange(name)} />;
  if (f.kind === 'bool') return <input type="checkbox" checked={value === true} onChange={(e) => onChange(e.target.checked)} />;
  const n = typeof value === 'number' ? value : null;
  return f.kind === 'length'
    ? <LengthField metres={n} onCommit={(m) => { if (m != null) onChange(m); }} />
    : <NumberField value={n} suffix={f.kind === 'angle' ? '°' : undefined} onCommit={(v) => { if (v != null) onChange(v); }} />;
}

/** The plan: the wall line with its edges (thick: eave, dashed: gable; click to go auto → eave → gable), the eaves, ridges, hips and valleys. */
function PlanPlot({ p, edges, onEdge }: { p: Preview; edges: Edge[]; onEdge: (i: number) => void }) {
  const W = 360, H = 240, pad = 22;
  const all = [...(p.outline ?? []), ...(p.eaves ?? [])];
  if (all.length < 3) return <svg width={W} height={H} />;
  const [x0, x1] = [Math.min(...all.map((q) => q[0])), Math.max(...all.map((q) => q[0]))];
  const [y0, y1] = [Math.min(...all.map((q) => q[1])), Math.max(...all.map((q) => q[1]))];
  const k = Math.min((W - 2 * pad) / Math.max(1e-6, x1 - x0), (H - 2 * pad) / Math.max(1e-6, y1 - y0));
  const X = (x: number) => (W - (x1 - x0) * k) / 2 + (x - x0) * k;
  const Y = (y: number) => (H + (y1 - y0) * k) / 2 - (y - y0) * k;
  const path = (ps: [number, number][]) => ps.map((q, i) => `${i ? 'L' : 'M'}${X(q[0])},${Y(q[1])}`).join('') + 'Z';
  const outline = p.outline ?? [];
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Plan">
      {p.eaves && <path d={path(p.eaves)} fill="#f3ece2" stroke="#8a8378" strokeDasharray="4 3" strokeWidth={0.8} />}
      <path d={path(outline)} fill="#ffffff" stroke="none" />
      {(p.outlets ?? []).map((o, i) => <circle key={`o${i}`} cx={X(o[0])} cy={Y(o[1])} r={3} fill="#2b5f9e"><title>Downpipe</title></circle>)}
      {(p.lines ?? []).map((l, i) => (
        <line key={i} x1={X(l.a[0])} y1={Y(l.a[1])} x2={X(l.b[0])} y2={Y(l.b[1])} stroke={l.kind === 'valley' ? '#2b5f9e' : '#5a3a22'} strokeWidth={l.kind === 'ridge' ? 1.6 : 1} />
      ))}
      {outline.map((a, i) => {
        const b = outline[(i + 1) % outline.length];
        const kind = p.kinds?.[i] ?? 'eave';
        const own = edges[i]?.kind;
        const [mx, my] = [(X(a[0]) + X(b[0])) / 2, (Y(a[1]) + Y(b[1])) / 2];
        return (
          <g key={i} className="roof-edge" onClick={() => onEdge(i)} style={{ cursor: 'pointer' }}>
            <title>{`${i + 1}: ${own ?? `auto (${kind})`}`}</title>
            <line x1={X(a[0])} y1={Y(a[1])} x2={X(b[0])} y2={Y(b[1])} stroke="transparent" strokeWidth={12} />
            <line x1={X(a[0])} y1={Y(a[1])} x2={X(b[0])} y2={Y(b[1])} stroke={own ? '#b4502a' : '#1e2a3a'} strokeWidth={kind === 'eave' ? 2.6 : 1.4} strokeDasharray={kind === 'gable' ? '6 3' : undefined} />
            <circle cx={mx} cy={my} r={8} fill="#ffffff" stroke={own ? '#b4502a' : '#1e2a3a'} strokeWidth={0.8} />
            <text x={mx} y={my + 3.5} textAnchor="middle" fontSize={9.5}>{i + 1}</text>
          </g>
        );
      })}
    </svg>
  );
}

/** The section across the ridge: the covering, the walls and plates, the truss or couple nearest the middle, the members cut. */
function SectionPlot({ s }: { s: NonNullable<Preview['section']> }) {
  const W = 360, H = 220, pad = 16;
  const [x0, z0, x1, z1] = s.box;
  const k = Math.min((W - 2 * pad) / Math.max(1e-6, x1 - x0), (H - 2 * pad - 14) / Math.max(1e-6, z1 - z0));
  const X = (x: number) => (W - (x1 - x0) * k) / 2 + (x - x0) * k;
  const Z = (z: number) => H - pad - 14 - (z - z0) * k;
  const order = (r: string) => (r === 'wall' ? 0 : r === 'covering' || r === 'deck' ? 1 : r === 'plate' ? 2 : 3);
  const items = [...s.items].sort((a, b) => order(a.role) - order(b.role));
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Section">
      {items.map((it, i) => (
        <polygon key={i} points={it.pts.reduce<string[]>((acc, v, j) => (j % 2 ? [...acc.slice(0, -1), `${acc[acc.length - 1]},${Z(v)}`] : [...acc, `${X(v)}`]), []).join(' ')}
          fill={ROLE_FILL[it.role] ?? '#c99a64'} stroke="#3a2a1a" strokeWidth={0.5} opacity={it.role === 'covering' ? 0.85 : 1}><title>{it.role}</title></polygon>
      ))}
      {s.span != null && <text x={W / 2} y={H - 4} textAnchor="middle" fontSize={10}>{`${lengthText(s.span)} · ${lengthText(s.top - s.plate)}`}</text>}
    </svg>
  );
}

export function RoofDialog() {
  useLang((s) => s.lang);
  const roofId = useUi((s) => s.roofId);
  const node = useSession((s) => (roofId ? s.nodes.get(roofId) : undefined));
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const styles = (info?.catalogs['architecture.roofStyles'] as unknown as Style[] | undefined) ?? [];
  const project = useSession((s) => [...s.nodes.values()].find((n) => n.type === 'IfcProject'));
  const own = (project?.props.roofStyles as OwnStyle[] | undefined) ?? [];
  const coverings = (info?.catalogs['architecture.roofCoverings'] as unknown as Covering[] | undefined) ?? [];
  const fields = useMemo(() => new Map((info?.nodeTypes.find((c) => c.type === 'IfcRoof')?.fields ?? []).map((f) => [f.key, f])), [info]);
  const [tab, setTab] = useState<Tab>('shape');
  const [draft, setDraft] = useState<Draft>({ attrs: {} });
  const [preview, setPreview] = useState<Preview | null>(null);

  // The preview follows the draft and the roof as it stands (an undo, a grip moved).
  useEffect(() => {
    if (!roofId) return;
    let live = true;
    void query<Preview>('architecture.roofPreview', { id: roofId, ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs, ...(draft.edges ? { edges: draft.edges } : {}) })
      .then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [roofId, draft, node]);
  // The roof went (an undo, deleted by someone in the session): the configurator closes.
  useEffect(() => { if (!node) useUi.setState({ dialog: null }); }, [node]);

  if (!node) return <PendingDialog title="Roof" />;
  const close = () => useUi.setState({ dialog: null });
  const dirty = Boolean(draft.style) || Object.keys(draft.attrs).length > 0 || draft.edges !== undefined;
  const value = (k: string): unknown => (k in draft.attrs ? draft.attrs[k] : preview?.attrs?.[k] ?? node.props[k] ?? DEFAULTS[k]);
  const set = (k: string, v: unknown) => setDraft((d) => ({ ...d, attrs: { ...d.attrs, [k]: v } }));
  const contour = (preview?.outline ?? (node.props.contour as [number, number][] | undefined) ?? []);
  const edges: Edge[] = draft.edges ?? ((preview?.attrs?.edges as Edge[] | undefined) ?? (node.props.edges as Edge[] | undefined) ?? []);
  const setEdge = (i: number, patch: Partial<NonNullable<Edge>> | null) => setDraft((d) => ({ ...d, edges: mergeEdge(edges, contour.length, i, patch) }));
  const cycleEdge = (i: number) => setEdge(i, { kind: edges[i]?.kind === undefined ? 'eave' : edges[i]?.kind === 'eave' ? 'gable' : undefined });
  const pickStyle = (id: string) => setDraft({ style: id, attrs: {} });
  const apply = async () => {
    await execute('architecture.updateRoof', draftArgs());
    if (!useSession.getState().error) setDraft({ attrs: {} });
  };
  const current = draft.style ?? (dirty ? undefined : (node.props.style as string | undefined));
  const draftArgs = () => ({ id: node.id, ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs, ...(draft.edges ? { edges: draft.edges } : {}) });
  // Saved as the project's own style: a new one under a name, or the one it has saved again (its roofs follow).
  const saveAs = async () => {
    const name = window.prompt(t('Name of the roof style'), node.props.style && own.some((s) => s.id === node.props.style) ? '' : `${node.name}`);
    if (!name?.trim()) return;
    await execute('architecture.saveRoofStyle', { ...draftArgs(), name });
    if (!useSession.getState().error) setDraft({ attrs: {} });
  };
  const ownStyle = own.find((s) => s.id === (draft.style ?? node.props.style));
  const update = async () => {
    if (!ownStyle) return;
    await execute('architecture.saveRoofStyle', { ...draftArgs(), name: ownStyle.label, styleId: ownStyle.id });
    if (!useSession.getState().error) setDraft({ attrs: {} });
  };
  const families = [...new Set(styles.map((s) => s.family))];
  const covering = coverings.find((c) => c.id === value('covering'));
  const pitch = Number(value('pitch'));
  const openings = (node.props.openings as { id: string; name: string; kind: string; width?: number; length?: number }[] | undefined) ?? [];

  const rows = FIELDS[tab].filter(([k, when]) => fields.has(k) && (!when || when(value))).map(([k]) => {
    const f = fields.get(k)!;
    return (
      <label key={k} className="field-line roof-field"><span className="eyebrow">{f.label}</span>
        <FieldInput f={f} value={value(k)} onChange={(v) => set(k, v)} />
      </label>
    );
  });

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Roof configurator">
      <div className="dialog wide roof-dialog">
        <header className="dialog-head">
          <span className="panel-title">Roof configurator <span className="muted small">· {node.name}</span></span>
          <button type="button" className="icon" onClick={() => { if (!dirty || window.confirm('Close without applying the changes?')) close(); }} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div className="type-list roof-styles">
            <button type="button" className={`tree-item${!current ? ' on' : ''}`} onClick={() => setDraft({ attrs: {} })} title="The roof as it is">{node.props.style ? 'As it is' : 'Shaped by hand'}</button>
            <p className="eyebrow">Project</p>
            {own.length === 0 && <p className="muted small">Configure a roof, then Save as style.</p>}
            {own.map((s) => (
              <div key={s.id} className="row roof-own">
                <button type="button" className={`tree-item grow${current === s.id ? ' on' : ''}`} onClick={() => pickStyle(s.id)}>
                  <span className="grow">{s.label}</span>
                  <span className="muted small">{t(s.shape)}{s.shape === 'barrel' ? '' : ` ${s.pitch}°`}{s.structure && s.structure !== 'rafters' ? ` · ${s.structure}` : ''}</span>
                </button>
                <button type="button" className="icon mini" title="Rename" onClick={() => { const n = window.prompt(t('Name of the roof style'), s.label); if (n?.trim()) void execute('architecture.renameRoofStyle', { styleId: s.id, name: n }); }}><Pencil size={12} /></button>
                <button type="button" className="icon mini" title="Delete the style (its roofs keep how they look)" onClick={() => { if (window.confirm(t('Delete this roof style? Its roofs keep how they look.'))) void execute('architecture.deleteRoofStyle', { styleId: s.id }); }}><Trash2 size={12} /></button>
              </div>
            ))}
            {families.map((fam) => (
              <div key={fam}>
                <p className="eyebrow">{fam}</p>
                {styles.filter((s) => s.family === fam).map((s) => (
                  <button key={s.id} type="button" className={`tree-item${current === s.id ? ' on' : ''}`} title={s.description || undefined} onClick={() => pickStyle(s.id)}>
                    <span className="grow">{s.label}</span>
                    <span className="muted small">{t(s.shape)}{s.shape === 'barrel' ? '' : ` ${s.pitch}°`}{s.structure ? ` · ${s.structure}` : ''}</span>
                  </button>
                ))}
              </div>
            ))}
          </div>
          <div className="roof-edit">
            <div className="tabs-inline" role="tablist">
              {TABS.map(([k, l]) => <button key={k} type="button" role="tab" aria-selected={tab === k} className={tab === k ? 'on' : ''} onClick={() => setTab(k)}>{l}</button>)}
            </div>
            <div className="joinery-edit">
              <div className="joinery-form">
                {rows}
                {tab === 'shape' && contour.length > 0 && (
                  <table className="roof-edges">
                    <thead><tr><th>Edge</th><th>Kind</th><th>°</th><th>Overhang</th><th title="Gutter">⌣</th></tr></thead>
                    <tbody>
                      {contour.map((_, i) => {
                        const e = edges[i] ?? null;
                        return (
                          <tr key={i}>
                            <td className="num">{i + 1}</td>
                            <td>
                              <select value={e?.kind ?? ''} data-i18n-ctx="edge" onChange={(ev) => setEdge(i, { kind: (ev.target.value || undefined) as 'eave' | 'gable' | undefined })}>
                                <option value="">auto</option><option value="eave">eave</option><option value="gable">gable</option>
                              </select>
                            </td>
                            <td><NumberField value={e?.pitch} blank placeholder={String(value('pitch') ?? '')} onCommit={(v) => setEdge(i, { pitch: v ?? undefined })} /></td>
                            <td><LengthField metres={e?.overhang} blank noUnit onCommit={(v) => setEdge(i, { overhang: v ?? undefined })} /></td>
                            <td>{(preview?.kinds?.[i] ?? 'eave') === 'eave' && <input type="checkbox" title="This eave's gutter" checked={e?.gutter !== false} onChange={(ev) => setEdge(i, { gutter: ev.target.checked ? undefined : false })} />}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                )}
                {tab === 'covering' && (
                  <>
                    {value('gutterProfile') !== 'none' && (
                      <label className="field-line roof-field"><span className="eyebrow">Gutter colour</span>
                        <input type="color" value={typeof value('gutterColor') === 'string' && value('gutterColor') ? String(value('gutterColor')) : '#8a9096'} onChange={(e) => set('gutterColor', e.target.value)} />
                      </label>
                    )}
                    {value('gutterProfile') !== 'none' && preview?.downpipes === 'auto' && (
                      <p className="muted small">Downpipes: one every 10 m, set by the roof. <button type="button" className="link" title="Each downpipe an element of its own: slid along its gutter, its route, diameter and shoe set in its properties"
                        onClick={() => void execute('architecture.makeDownpipes', { id: node.id })}>Make them editable</button></p>
                    )}
                    {covering && (
                      <p className="muted small"><span className="swatch" style={{ background: covering.color }} /> {covering.id}{Number.isFinite(pitch) && pitch < covering.minPitch ? ` · below its ${covering.minPitch}° the covering does not shed water` : ` · from ${covering.minPitch}°`}</p>
                    )}
                  </>
                )}
                {tab === 'openings' && (
                  openings.length === 0
                    ? <p className="muted small">No roof windows or dormers yet: Architecture → Roof window or Dormer, then a click on a slope.</p>
                    : (
                      <table className="roof-openings">
                        <tbody>
                          {openings.map((o) => (
                            <tr key={o.id}>
                              <td>{o.name}</td>
                              <td><LengthField metres={o.width ?? 0.78} min={0.01} noUnit title="Width" onCommit={(v) => { if (v != null) void execute('architecture.updateRoofOpening', { roofId: node.id, id: o.id, width: v }); }} /></td>
                              <td><LengthField metres={o.length ?? 1.18} min={0.01} title="Length up the slope" onCommit={(v) => { if (v != null) void execute('architecture.updateRoofOpening', { roofId: node.id, id: o.id, length: v }); }} /></td>
                              <td className="muted small">{o.kind}</td>
                              <td><button type="button" className="icon mini" title="Remove" onClick={() => void execute('architecture.removeRoofOpening', { roofId: node.id, id: o.id })}>×</button></td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )
                )}
              </div>
              <div className="joinery-preview">
                {preview && (tab === 'framing' && preview.section ? <SectionPlot s={preview.section} /> : <PlanPlot p={preview} edges={edges} onEdge={cycleEdge} />)}
                {tab !== 'framing' && <p className="muted small">Click an edge: auto, eave, gable.</p>}
                {tab === 'framing' && preview?.section && <p className="muted small">Section across the ridge, at the middle.</p>}
                {preview?.problem && <p className="error-text">{preview.problem}</p>}
                {preview?.summary && (
                  <dl className="fields roof-summary">
                    {preview.summary.map(([k, v]) => <div key={k} className="contents"><dt>{k}</dt><dd className="num">{v}</dd></div>)}
                  </dl>
                )}
              </div>
              <div className="row joinery-actions">
                <span className="muted small grow">{dirty ? (draft.style ? `${styles.find((s) => s.id === draft.style)?.label ?? draft.style}${Object.keys(draft.attrs).length || draft.edges ? ', changed by hand' : ''}` : 'Changed, not applied yet') : ''}</span>
                <button type="button" className="btn" disabled={!dirty} onClick={() => setDraft({ attrs: {} })}><RotateCcw size={14} />Reset</button>
                {ownStyle && <button type="button" className="btn" disabled={Boolean(preview?.problem)} title={`Save the roof as it is configured into ${ownStyle.label}; every roof of the style follows`} onClick={() => void update()}><Save size={14} />Update style</button>}
                <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save the roof as it is configured as a style of the project" onClick={() => void saveAs()}><Save size={14} />Save as style…</button>
                <button type="button" className="btn primary" disabled={!dirty || Boolean(preview?.problem)} onClick={() => void apply()}>Apply</button>
              </div>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
