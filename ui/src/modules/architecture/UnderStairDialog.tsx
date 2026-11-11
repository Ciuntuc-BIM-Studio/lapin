/**
 * The space under a stair (a tester's report, #71), configured like a roof or a hall: the styles on the left (the project's
 * own, then the ready-made ones: storage cupboard, closet office, reading nook, entry and mudroom, toddler playhouse, dog bed
 * house), the fields in the middle (where it stands under the flight, the front, the bookcase, the feature, the materials),
 * and on the right the engine's preview of the draft: the space seen from its open side under the flight's soffit and from
 * above, its numbers, its parts and what did not fit. Apply is one engine command, one undo step: a new space, or the one
 * selected changed.
 */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { Pencil, RotateCcw, Save, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { lengthText } from '../../engine/unitsStore';
import { LengthField } from '../../shell/cells';
import { t, useLang } from '../../i18n';
import { draftArgs, emptyDraft, featureFields, fit, isDirty, partLines, specValue, type Draft, type PreviewItem, type Role, type UnderStairPreview, type UnderStairSpec } from './underStair';

/** The stair the configurator is open on and the space it edits (null: a new one under that stair). */
export const useUnderStair = create<{ stairId: string | null; id: string | null }>(() => ({ stairId: null, id: null }));

/** Opens the configurator on a space, or on a stair for a new one. */
export function openUnderStair(stairId: string, id: string | null = null) {
  useUnderStair.setState({ stairId, id });
  useUi.setState({ dialog: 'underStair' });
}

type Preset = { id: string; label: string; description: string; spec: UnderStairSpec };
type OwnStyle = { id: string; label: string; spec: UnderStairSpec };

const FILL: Record<Role, string> = { stair: '#e4dfd5', carcass: '#c99a5b', front: '#a3ae8c', fabric: '#ddd0b8', metal: '#3d3d3d', light: '#ffe7a3' };

const LABELS: Partial<Record<keyof UnderStairSpec, string>> = {
  featureWidth: 'Feature width', deskHeight: 'Desk height', benchHeight: 'Bench height', cushion: 'Cushions', light: 'Light fixture',
  hookHeight: 'Coat hook height', hookSpacing: 'Coat hook spacing', openingWidth: 'Opening width', openingHeight: 'Opening height', window: 'Window beside the door',
};

/** A drawing of the preview: the stair's flight behind, the parts' pieces over it. */
function Plot({ items, label, colors, w = 380, h = 200 }: { items: PreviewItem[]; label: string; colors: (role: Role) => string; w?: number; h?: number }) {
  const f = fit(items, w, h, 12);
  if (!f) return <svg width={w} height={h} />;
  return (
    <svg viewBox={`0 0 ${w} ${h}`} width={w} height={h} aria-label={label} data-field={`under-stair-${label === 'Plan' ? 'plan' : 'elevation'}`}>
      {items.map((it, i) => (
        <polygon key={i} points={Array.from({ length: it.pts.length / 2 }, (_, j) => `${f.X(it.pts[2 * j])},${f.Y(it.pts[2 * j + 1])}`).join(' ')}
          fill={colors(it.role)} stroke={it.role === 'stair' ? '#8a8378' : '#3a2a1a'} strokeWidth={it.role === 'stair' ? 0.8 : 0.4} data-role={it.role} />
      ))}
    </svg>
  );
}

export function UnderStairDialog() {
  useLang((s) => s.lang);
  const { stairId, id } = useUnderStair();
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const stair = useSession((s) => (stairId ? s.nodes.get(stairId) : undefined));
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const nodes = useSession((s) => s.nodes);
  const project = [...nodes.values()].find((n) => n.type === 'IfcProject');
  const presets = ((info?.catalogs['architecture.underStair'] as unknown as { presets?: Preset[] } | undefined)?.presets) ?? [];
  const own = (project?.props.underStairStyles as OwnStyle[] | undefined) ?? [];
  const materials = [...nodes.values()].filter((n) => n.type === 'IfcMaterial');
  const [draft, setDraft] = useState<Draft>(() => (id ? emptyDraft() : { style: 'storage', spec: {}, place: {} }));
  const [preview, setPreview] = useState<UnderStairPreview | null>(null);

  // The preview follows the draft, the space and its stair as they stand (an undo, the stair changed).
  useEffect(() => {
    if (!stairId) return;
    let live = true;
    void query<UnderStairPreview>('architecture.underStairPreview', { stairId, ...(id ? { id } : {}), ...draftArgs(draft, (node?.props.spec as UnderStairSpec | undefined)) })
      .then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [stairId, id, draft, node, stair]);
  // The space went (an undo of its making): a new one under the stair; the stair went: the configurator closes.
  useEffect(() => { if (id && !node) useUnderStair.setState({ id: null }); }, [id, node]);
  useEffect(() => { if (stairId && !stair) useUi.setState({ dialog: null }); }, [stairId, stair]);

  const close = () => useUi.setState({ dialog: null });
  const dirty = isDirty(draft);
  const value = <K extends keyof UnderStairSpec>(k: K) => specValue(draft, preview, k);
  const set = <K extends keyof UnderStairSpec>(k: K, v: UnderStairSpec[K]) => setDraft((d) => ({ ...d, spec: { ...d.spec, [k]: v } }));
  const place = <K extends keyof Draft['place']>(k: K, v: Draft['place'][K]) => setDraft((d) => ({ ...d, place: { ...d.place, [k]: v } }));
  const placed = (k: 'start' | 'end' | 'depth'): number | null => (k in draft.place ? draft.place[k] ?? null : typeof node?.props[k] === 'number' ? (node.props[k] as number) : null);
  const current = draft.style ?? (dirty ? undefined : (node?.props.style as string | undefined));
  const ownStyle = own.find((s) => s.id === current);
  const pick = (style: string) => setDraft((d) => ({ style, spec: {}, place: d.place }));
  const colorOf = (role: Role) => {
    const name = role === 'carcass' ? value('carcass') : role === 'front' ? value('fronts') : role === 'fabric' ? value('fabric') : null;
    const c = name ? materials.find((m) => m.name.toLowerCase() === String(name).toLowerCase())?.props.color : null;
    return typeof c === 'string' ? c : FILL[role];
  };
  const apply = async () => {
    const args = draftArgs(draft, preview?.spec);
    if (id) await execute('architecture.updateUnderStair', { id, ...args });
    else {
      const r = await execute('architecture.addUnderStair', { stairId, ...args }) as { id?: string } | null;
      if (r?.id) { useUnderStair.setState({ id: r.id }); useUi.setState({ selection: r.id, selected: [r.id] }); }
    }
    if (!useSession.getState().error) setDraft(emptyDraft());
  };
  const save = async (styleId?: string, label?: string) => {
    const name = label ?? window.prompt(t('Name of the under-stair style'), String(value('name') ?? ''));
    if (!name?.trim() || !preview?.spec) return;
    await execute('architecture.saveUnderStairStyle', { ...(id ? { id } : {}), name, spec: preview.spec, ...(styleId ? { styleId } : {}) });
    if (!useSession.getState().error && id) setDraft(emptyDraft());
  };
  if (!stair) return null;

  const feature = (value('feature') ?? 'none') as UnderStairSpec['feature'];
  const front = (value('front') ?? 'doors') as UnderStairSpec['front'];
  const len = (k: keyof UnderStairSpec, label: string, min = 0.01) => (
    <label key={k} className="field-line roof-field"><span className="eyebrow">{label}</span>
      <LengthField metres={value(k) as number | undefined} min={min} testId={`under-stair-${k}`} onCommit={(v) => { if (v != null) set(k, v as never); }} />
    </label>
  );
  const tick = (k: keyof UnderStairSpec, label: string) => (
    <label key={k} className="field-line roof-field"><span className="eyebrow">{label}</span>
      <input type="checkbox" checked={value(k) === true} data-field={`under-stair-${k}`} onChange={(e) => set(k, e.target.checked as never)} />
    </label>
  );
  const material = (k: 'carcass' | 'fronts' | 'fabric', label: string) => {
    const v = String(value(k) ?? '');
    const names = [...new Set([v, ...materials.map((m) => m.name)].filter(Boolean))].sort((a, b) => a.localeCompare(b));
    return (
      <label key={k} className="field-line roof-field"><span className="eyebrow">{label}</span>
        <select value={v} data-field={`under-stair-${k}`} onChange={(e) => set(k, e.target.value)}>{names.map((n) => <option key={n} value={n} data-no-i18n>{n}</option>)}</select>
      </label>
    );
  };
  const sp = preview?.space;

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Under-stair space" data-dialog="under-stair">
      <div className="dialog wide roof-dialog under-stair-dialog">
        <header className="dialog-head">
          <span className="panel-title">Under-stair space <span className="muted small" data-no-i18n>· {node?.name ?? stair.name}</span></span>
          <button type="button" className="icon" onClick={() => { if (!dirty || window.confirm(t('Close without applying the changes?'))) close(); }} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div className="type-list roof-styles">
            {node && <button type="button" className={`tree-item${!current ? ' on' : ''}`} onClick={() => setDraft(emptyDraft())}>The space as it is</button>}
            <p className="eyebrow">Project</p>
            {own.length === 0 && <p className="muted small">Configure a space, then Save as style.</p>}
            {own.map((s) => (
              <div key={s.id} className="row roof-own">
                <button type="button" className={`tree-item grow${current === s.id ? ' on' : ''}`} data-style={s.id} onClick={() => pick(s.id)}><span className="grow" data-no-i18n>{s.label}</span></button>
                <button type="button" className="icon mini" title="Rename" onClick={() => { const n = window.prompt(t('Name of the under-stair style'), s.label); if (n?.trim()) void execute('architecture.renameUnderStairStyle', { styleId: s.id, name: n }); }}><Pencil size={12} /></button>
                <button type="button" className="icon mini" title="Delete the style (its spaces keep what fills them)" onClick={() => { if (window.confirm(t('Delete this under-stair style? Its spaces keep what fills them.'))) void execute('architecture.deleteUnderStairStyle', { styleId: s.id }); }}><Trash2 size={12} /></button>
              </div>
            ))}
            <p className="eyebrow">Catalogue</p>
            {presets.map((p) => (
              <button key={p.id} type="button" className={`tree-item${current === p.id ? ' on' : ''}`} data-style={p.id} title={p.description} onClick={() => pick(p.id)}>
                <span className="grow">{p.label}</span>
              </button>
            ))}
          </div>
          <div className="roof-edit">
            <div className="joinery-edit">
              <div className="joinery-form">
                <p className="eyebrow">Placement</p>
                {(preview?.flights ?? 1) > 1 && (
                  <label className="field-line roof-field"><span className="eyebrow">Under flight</span>
                    <select value={draft.place.flight ?? (node?.props.flight as number | undefined) ?? 1} data-field="under-stair-flight" onChange={(e) => place('flight', Number(e.target.value))}>
                      {Array.from({ length: preview?.flights ?? 1 }, (_, i) => <option key={i} value={i + 1} data-no-i18n>{i + 1}</option>)}
                    </select>
                  </label>
                )}
                <label className="field-line roof-field"><span className="eyebrow">Open side</span>
                  <select value={draft.place.side ?? (node?.props.side as string | undefined) ?? ''} data-field="under-stair-side" onChange={(e) => place('side', (e.target.value || null) as 'left' | 'right' | null)}>
                    <option value="">away from the turn</option><option value="left">left</option><option value="right">right</option>
                  </select>
                </label>
                <label className="field-line roof-field" title="Along the flight from its first riser; empty: where the soffit is 300 mm over the floor"><span className="eyebrow">Starts at</span>
                  <LengthField metres={placed('start')} blank placeholder={sp ? lengthText(sp.start) : ''} onCommit={(v) => place('start', v)} />
                </label>
                <label className="field-line roof-field" title="Along the flight from its first riser; empty: at its last riser"><span className="eyebrow">Ends at</span>
                  <LengthField metres={placed('end')} blank placeholder={sp ? lengthText(sp.end) : ''} onCommit={(v) => place('end', v)} />
                </label>
                <label className="field-line roof-field" title="In from the open side; empty: as wide as the stair"><span className="eyebrow">Depth</span>
                  <LengthField metres={placed('depth')} blank placeholder={sp ? lengthText(sp.depth) : ''} onCommit={(v) => place('depth', v)} />
                </label>
                <p className="eyebrow">The front</p>
                <label className="field-line roof-field"><span className="eyebrow">Low part</span>
                  <select value={front} data-field="under-stair-front" onChange={(e) => set('front', e.target.value as UnderStairSpec['front'])}>
                    <option value="doors">Cupboard doors</option><option value="panel">Closed panel</option><option value="open">Left open</option>
                  </select>
                </label>
                {front === 'doors' && len('doorWidth', 'Door width', 0.25)}
                {front === 'doors' && tick('shelves', 'Shelves behind the doors')}
                {len('shelfSpacing', 'Shelf spacing', 0.15)}
                {tick('bookcase', 'Bookcase at the high end')}
                {value('bookcase') === true && len('bookcaseWidth', 'Bookcase width', 0.25)}
                <p className="eyebrow">Feature</p>
                <label className="field-line roof-field"><span className="eyebrow">Feature</span>
                  <select value={feature} data-field="under-stair-feature" onChange={(e) => set('feature', e.target.value as UnderStairSpec['feature'])}>
                    <option value="none">No feature</option><option value="desk">Desk with a shelf</option><option value="bench">Bench with cushions</option>
                    <option value="mudroom">Mudroom bench and coat hooks</option><option value="playhouse">Playhouse door</option><option value="dogbed">Dog bed</option>
                  </select>
                </label>
                {featureFields(feature).map((k) => (k === 'cushion' || k === 'light' || k === 'window' ? tick(k, LABELS[k]!) : len(k, LABELS[k]!, 0.1)))}
                <p className="eyebrow">Materials</p>
                {len('thickness', 'Board thickness', 0.008)}
                {material('carcass', 'Carcass')}
                {material('fronts', 'Fronts')}
                {material('fabric', 'Fabric')}
              </div>
              <div className="joinery-preview">
                {preview?.elevation && <Plot items={preview.elevation} label="Seen from the open side" colors={colorOf} />}
                {preview?.elevation && <p className="muted small">Seen from the open side, under the flight.</p>}
                {preview?.plan && <Plot items={preview.plan} label="Plan" colors={colorOf} h={120} />}
                {preview?.plan && <p className="muted small">From above, the open side at the bottom.</p>}
                {preview?.problem && <p className="error-text">{preview.problem}</p>}
                {(preview?.notes ?? []).map((n) => <p key={n} className="error-text small">{n}</p>)}
                {sp && (
                  <dl className="fields roof-summary">
                    <div className="contents"><dt>Length</dt><dd className="num">{lengthText(sp.end - sp.start)}</dd></div>
                    <div className="contents"><dt>Depth</dt><dd className="num">{lengthText(sp.depth)}</dd></div>
                    <div className="contents"><dt>Height at the low end</dt><dd className="num">{lengthText(sp.low)}</dd></div>
                    <div className="contents"><dt>Height at the high end</dt><dd className="num">{lengthText(sp.high)}</dd></div>
                    {partLines(preview?.parts ?? []).map(([name, n]) => <div key={name} className="contents"><dt>{name}</dt><dd className="num" data-no-i18n>{n}</dd></div>)}
                  </dl>
                )}
              </div>
              <div className="row joinery-actions">
                <span className="muted small grow">{dirty ? 'Changed, not applied yet' : ''}</span>
                <button type="button" className="btn" disabled={!dirty} onClick={() => setDraft(emptyDraft())}><RotateCcw size={14} />Reset</button>
                {ownStyle && <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save what fills this space into its style; every space of the style follows" onClick={() => void save(ownStyle.id, ownStyle.label)}><Save size={14} />Update style</button>}
                <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save what fills this space as a style of the project" onClick={() => void save()}><Save size={14} />Save as style…</button>
                <button type="button" className="btn primary" data-field="under-stair-apply" disabled={(Boolean(id) && !dirty) || Boolean(preview?.problem)} onClick={() => void apply()}>{id ? 'Apply' : 'Create'}</button>
              </div>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
