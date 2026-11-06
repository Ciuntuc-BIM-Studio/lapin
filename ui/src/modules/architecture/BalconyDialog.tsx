/**
 * The balcony configurator (a tester's report, #96), as the roof's: the styles on the left (the project's own, then the
 * built-in ones: Juliet, cantilevered, stacked on posts, on brackets, hung, rounded, curved, loggia, corner, mezzanine), and
 * on the right the balcony being configured — its shape, its slab and finish, its railing, what carries it, a loggia's
 * recess — over its plan and a section through its middle as the engine would build the draft. A style picked fills every
 * field in; any field may then be changed by hand. Apply is one step; Save as style keeps it as the project's own; Stack
 * on storeys above copies it onto every storey over its own.
 */

import { useEffect, useMemo, useState } from 'react';
import { Layers, Pencil, RotateCcw, Save, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { fieldShown, type FieldDescriptor } from '../../engine/protocol';
import { t, useLang } from '../../i18n';
import { LengthField, NumberField } from '../../shell/cells';
import { PendingDialog } from '../core/PendingDialog';
import { MaterialPicker } from '../core/MaterialPicker';
import { BALCONY, styleOf, useBalconyStyles } from './Balconies';

type Item = { role: string; pts: number[] };
interface Preview {
  problem?: string | null;
  attrs?: Record<string, unknown>;
  outline?: number[];
  roles?: string[];
  rails?: number[][];
  posts?: number[];
  hangers?: number[];
  brackets?: number[];
  wall?: number | null;
  notes?: string[];
  section?: { items: Item[]; railHeight: number };
  summary?: [string, string][];
}
interface Draft { style?: string; attrs: Record<string, unknown> }

const TABS = [['shape', 'Shape'], ['slab', 'Slab and finish'], ['railing', 'Railing'], ['support', 'Supports'], ['loggia', 'Loggia']] as const;
type Tab = (typeof TABS)[number][0];

const FIELDS: Record<Tab, string[]> = {
  shape: ['kind', 'outline', 'width', 'depth', 'cornerRadius', 'bulge', 'returnLength', 'cornerSide', 'gallerySlab', 'fascia'],
  slab: ['thickness', 'edge', 'edgeThickness', 'material', 'finishThickness', 'finishMaterial', 'fall', 'soffitThickness', 'soffitMaterial', 'thermalBreak'],
  railing: ['railStyle', 'railHeight', 'railSides', 'railInset', 'railProfile', 'barSize', 'postSize', 'postSpacing', 'balusterSpacing', 'parapetThickness', 'parapetMaterial', 'railColor'],
  support: ['support', 'supportShape', 'supportSize', 'supportSpacing', 'bracketDepth', 'bracketHeight', 'hangerHeight', 'supportMaterial'],
  loggia: ['recessHeight', 'cheekThickness', 'projection', 'ceiling', 'backWall'],
};

const ROLE_FILL: Record<string, string> = {
  wall: '#d9d4c9', slab: '#b8b4ac', finish: '#d8cfc0', soffit: '#efece6', break: '#e3b23c', drip: '#a9adb2', railing: '#3a3d42', glass: '#9cc8e8',
  parapet: '#c9c4bb', posts: '#5d6168', brackets: '#5d6168', hangers: '#5d6168', ceiling: '#b8b4ac', fascia: '#f1efea',
};

/** A field as the engine describes it: a choice, a tick, a material, a colour, a length in mm, a number. */
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
  if (f.kind === 'color') return <input type="color" value={typeof value === 'string' ? value : '#3a3d42'} onChange={(e) => onChange(e.target.value)} />;
  const n = typeof value === 'number' ? value : null;
  return f.kind === 'length'
    ? <LengthField metres={n} onCommit={(m) => { if (m != null) onChange(m); }} />
    : <NumberField value={n} onCommit={(v) => { if (v != null) onChange(v); }} />;
}

const pairs = (a: number[] | undefined): [number, number][] => Array.from({ length: Math.floor((a?.length ?? 0) / 2) }, (_, i) => [a![2 * i], a![2 * i + 1]]);

/** The plan in the balcony's frame: the wall's face along the bottom, the outline (thick against the building), the railing, what carries it. */
function PlanPlot({ p }: { p: Preview }) {
  const W = 380, H = 230, pad = 24;
  const outline = pairs(p.outline);
  if (outline.length < 3) return <svg width={W} height={H} />;
  const wall = p.wall ?? 0.25;
  const all = [...outline, [outline[0][0], -wall] as [number, number]];
  const [x0, x1] = [Math.min(...all.map((q) => q[0])) - 0.3, Math.max(...all.map((q) => q[0])) + 0.3];
  const [y0, y1] = [Math.min(...all.map((q) => q[1]), -wall), Math.max(...all.map((q) => q[1]))];
  const k = Math.min((W - 2 * pad) / Math.max(1e-6, x1 - x0), (H - 2 * pad) / Math.max(1e-6, y1 - y0));
  // The view from above, looking at the building: out from the wall is down the page.
  const X = (x: number) => (W - (x1 - x0) * k) / 2 + (x - x0) * k;
  const Y = (y: number) => (H - (y1 - y0) * k) / 2 + (y - y0) * k;
  const path = (ps: [number, number][], close = true) => ps.map((q, i) => `${i ? 'L' : 'M'}${X(q[0])},${Y(q[1])}`).join('') + (close ? 'Z' : '');
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Plan" data-testid="balcony-plan">
      {p.wall != null && <rect x={X(x0)} y={Y(-wall)} width={(x1 - x0) * k} height={wall * k} fill="#d9d4c9" stroke="#8a8378" strokeWidth={0.6} />}
      <path d={path(outline)} fill="#f3ece2" stroke="#1e2a3a" strokeWidth={1} />
      {outline.map((a, i) => {
        const b = outline[(i + 1) % outline.length];
        const role = p.roles?.[i] ?? 'front';
        return role === 'wall' ? <line key={i} x1={X(a[0])} y1={Y(a[1])} x2={X(b[0])} y2={Y(b[1])} stroke="#8a8378" strokeWidth={2.2} /> : null;
      })}
      {(p.rails ?? []).map((r, i) => <path key={`r${i}`} d={path(pairs(r), false)} fill="none" stroke="#2b5f9e" strokeWidth={2} />)}
      {pairs(p.posts).map((q, i) => <circle key={`p${i}`} cx={X(q[0])} cy={Y(q[1])} r={3.2} fill="#5d6168"><title>Post</title></circle>)}
      {pairs(p.hangers).map((q, i) => <line key={`h${i}`} x1={X(q[0])} y1={Y(q[1])} x2={X(q[0])} y2={Y(0)} stroke="#5d6168" strokeDasharray="3 2" />)}
      {(p.brackets ?? []).map((u, i) => <rect key={`b${i}`} x={X(u) - 2} y={Y(0)} width={4} height={18} fill="#5d6168"><title>Bracket</title></rect>)}
    </svg>
  );
}

/** The section through its middle: the wall, the slab under its finish, the soffit, the railing, what carries it; heights from the balcony's floor. */
function SectionPlot({ s }: { s: NonNullable<Preview['section']> }) {
  const W = 380, H = 230, pad = 18;
  const vs = s.items.flatMap((it) => it.pts.filter((_, j) => j % 2 === 0));
  const zs = s.items.flatMap((it) => it.pts.filter((_, j) => j % 2 === 1));
  if (!vs.length) return <svg width={W} height={H} />;
  const [x0, x1, z0, z1] = [Math.min(...vs), Math.max(...vs), Math.max(Math.min(...zs), -3.2), Math.max(...zs)];
  const k = Math.min((W - 2 * pad) / Math.max(1e-6, x1 - x0), (H - 2 * pad) / Math.max(1e-6, z1 - z0));
  const X = (x: number) => (W - (x1 - x0) * k) / 2 + (x - x0) * k;
  const Z = (z: number) => H - (H - (z1 - z0) * k) / 2 - (Math.max(z, z0) - z0) * k;
  const order = (r: string) => (r === 'wall' ? 0 : r === 'slab' || r === 'ceiling' ? 1 : 2);
  const items = [...s.items].sort((a, b) => order(a.role) - order(b.role));
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Section" data-testid="balcony-section-plot">
      {items.map((it, i) => (
        <polygon key={i} points={pairs(it.pts).map(([v, z]) => `${X(v)},${Z(z)}`).join(' ')} fill={ROLE_FILL[it.role] ?? '#c9c4bb'} stroke="#2a2a2a" strokeWidth={0.5}
          opacity={it.role === 'glass' ? 0.7 : 1}><title>{it.role}</title></polygon>
      ))}
      <line x1={pad / 2} x2={W - pad / 2} y1={Z(0)} y2={Z(0)} stroke="#b4502a" strokeWidth={0.6} strokeDasharray="5 3" />
    </svg>
  );
}

export function BalconyDialog() {
  useLang((s) => s.lang);
  const id = useUi((s) => s.balconyId);
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const { builtIn, own } = useBalconyStyles();
  const fields = useMemo(() => new Map((info?.nodeTypes.find((c) => c.type === BALCONY)?.fields ?? []).map((f) => [f.key, f])), [info]);
  const [tab, setTab] = useState<Tab>('shape');
  const [draft, setDraft] = useState<Draft>({ attrs: {} });
  const [preview, setPreview] = useState<Preview | null>(null);

  useEffect(() => {
    if (!id) return;
    let live = true;
    void query<Preview>('architecture.balconyPreview', { id, ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs }).then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [id, draft, node]);
  useEffect(() => { if (!node) useUi.setState({ dialog: null }); }, [node]);

  if (!node) return <PendingDialog title="Balcony" />;
  const close = () => useUi.setState({ dialog: null });
  const dirty = Boolean(draft.style) || Object.keys(draft.attrs).length > 0;
  const value = (k: string): unknown => (k in draft.attrs ? draft.attrs[k] : preview?.attrs?.[k] ?? node.props[k]);
  const values: Record<string, unknown> = { ...(preview?.attrs ?? node.props), ...draft.attrs };
  const set = (k: string, v: unknown) => setDraft((d) => ({ ...d, attrs: { ...d.attrs, [k]: v } }));
  const current = draft.style ?? (dirty ? undefined : styleOf(node));
  const args = () => ({ id: node.id, ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs });
  const done = () => { if (!useSession.getState().error) setDraft({ attrs: {} }); };
  const apply = async () => { await execute('architecture.updateBalcony', args()); done(); };
  const saveAs = async () => {
    const name = window.prompt(t('Name of the balcony style'), node.name);
    if (!name?.trim()) return;
    await execute('architecture.saveBalconyStyle', { ...args(), name });
    done();
  };
  const ownStyle = own.find((s) => s.id === current);
  const update = async () => { if (ownStyle) { await execute('architecture.saveBalconyStyle', { ...args(), name: ownStyle.name, styleId: ownStyle.id }); done(); } };
  const stack = async () => {
    if (dirty) await execute('architecture.updateBalcony', args());
    if (useSession.getState().error) return;
    setDraft({ attrs: {} });
    await execute('architecture.stackBalcony', { ids: [node.id] });
  };
  const loggia = values.kind === 'loggia';
  const tabs = TABS.filter(([k]) => k !== 'loggia' || loggia);
  const rows = FIELDS[tab].filter((k) => fields.has(k) && fieldShown(fields.get(k)!, values)).map((k) => {
    const f = fields.get(k)!;
    return (
      <label key={k} className="field-line roof-field" data-field={k}><span className="eyebrow">{f.label}</span>
        <FieldInput f={f} value={value(k)} onChange={(v) => set(k, v)} />
      </label>
    );
  });

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Balcony configurator">
      <div className="dialog wide roof-dialog balcony-dialog" data-testid="balcony-dialog">
        <header className="dialog-head">
          <span className="panel-title">Balcony configurator <span className="muted small" data-no-i18n>· {node.name}</span></span>
          <button type="button" className="icon" onClick={() => { if (!dirty || window.confirm(t('Close without applying the changes?'))) close(); }} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div className="type-list roof-styles">
            <button type="button" className={`tree-item${!current ? ' on' : ''}`} onClick={() => setDraft({ attrs: {} })} title="The balcony as it is">As it is</button>
            <p className="eyebrow">Project</p>
            {own.length === 0 && <p className="muted small">Configure a balcony, then Save as style.</p>}
            {own.map((s) => (
              <div key={s.id} className="row roof-own">
                <button type="button" className={`tree-item grow${current === s.id ? ' on' : ''}`} data-balcony-style={s.id} onClick={() => setDraft({ style: s.id, attrs: {} })}>
                  <span className="grow" data-no-i18n>{s.name}</span><span className="muted small">{String(s.props.kind ?? '')}</span>
                </button>
                <button type="button" className="icon mini" title="Rename" onClick={() => { const n = window.prompt(t('Name of the balcony style'), s.name); if (n?.trim()) void execute('architecture.renameBalconyStyle', { styleId: s.id, name: n }); }}><Pencil size={12} /></button>
                <button type="button" className="icon mini" title="Delete the style (its balconies keep how they are)" onClick={() => { if (window.confirm(t('Delete this balcony style? Its balconies keep how they are.'))) void execute('architecture.deleteBalconyStyle', { styleId: s.id }); }}><Trash2 size={12} /></button>
              </div>
            ))}
            <p className="eyebrow">Built-in</p>
            {builtIn.map((s) => (
              <button key={s.id} type="button" className={`tree-item${current === s.id ? ' on' : ''}`} title={s.description} data-balcony-style={s.id} onClick={() => setDraft({ style: s.id, attrs: {} })}>
                <span className="grow">{s.label}</span>
              </button>
            ))}
          </div>
          <div className="roof-edit">
            <div className="tabs-inline" role="tablist">
              {tabs.map(([k, l]) => <button key={k} type="button" role="tab" aria-selected={tab === k} className={tab === k ? 'on' : ''} onClick={() => setTab(k)}>{l}</button>)}
            </div>
            <div className="joinery-edit">
              <div className="joinery-form">
                {rows}
                {tab === 'slab' && values.thermalBreak === true && !['loggia', 'mezzanine'].includes(String(values.kind)) && (
                  <p className="muted small">A thermal break (80 mm) between the slab and the floor inside: it says so in IFC and the schedule.</p>
                )}
                {tab === 'loggia' && <p className="muted small">The recess is cut through the wall as wide as the loggia; its back wall, of the wall's type, can hold the door.</p>}
              </div>
              <div className="joinery-preview">
                {preview && (tab === 'shape' || tab === 'loggia' ? <PlanPlot p={preview} /> : preview.section && <SectionPlot s={preview.section} />)}
                <p className="muted small">{tab === 'shape' || tab === 'loggia' ? 'Plan: the wall along the top, the railing in blue.' : 'Section through its middle; the dashed line is its floor.'}</p>
                {preview?.problem && <p className="error-text">{preview.problem}</p>}
                {(preview?.notes ?? []).filter((n) => n !== preview?.problem).map((n) => <p key={n} className="muted small">{n}</p>)}
                {preview?.summary && (
                  <dl className="fields roof-summary">
                    {preview.summary.map(([k, v]) => <div key={k} className="contents"><dt>{k}</dt><dd className="num">{v}</dd></div>)}
                  </dl>
                )}
              </div>
              <div className="row joinery-actions">
                <span className="muted small grow">{dirty ? 'Changed, not applied yet' : ''}</span>
                <button type="button" className="btn" disabled={!dirty} onClick={() => setDraft({ attrs: {} })}><RotateCcw size={14} />Reset</button>
                <button type="button" className="btn" title="Apply, then copy it onto every storey above its own" disabled={Boolean(preview?.problem)} onClick={() => void stack()}><Layers size={14} />Stack on storeys above</button>
                {ownStyle && <button type="button" className="btn" disabled={Boolean(preview?.problem)} onClick={() => void update()}><Save size={14} />Update style</button>}
                <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save the balcony as it is configured as a style of the project" onClick={() => void saveAs()}><Save size={14} />Save as style…</button>
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
