/**
 * The portal frame hall configurator (decision 270), like the roof's: the styles on the left (the project's own, then the
 * catalogue's), the hall's fields in tabs in the middle (frame, haunches, gables, secondary steel, placement), and the
 * engine's preview of the draft on the right — a typical frame's elevation with its haunches and its connections (a
 * gable's on the Gables tab), the plan of the grid and the members, and the summary (frames, heights, steel by kind, the
 * connections' bolts and anchors). The Nodes tab sets the frames' connections (decision 273): on or off, the column bases
 * pinned or fixed, their anchors, the eaves' and the apex's end plates and bolts. The Secondary steel tab (decision 275):
 * purlins, side rails, bracing, the eaves beam and fly braces, on or off, their profiles and spacings; the preview shows
 * them from above. Apply is one engine command, one undo step: a new hall, or the hall built again over its members and
 * connections; Delete hall takes them all away.
 */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { Pencil, RotateCcw, Save, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { lengthText, unitLabel, useStandards } from '../../engine/unitsStore';
import { LengthField, NumberField } from '../../shell/cells';
import { t, useLang } from '../../i18n';
import { baysForLength, baysText, fit, parseBays, parseBracing, secondaryName, secondaryProfiles, secondaryValue, summaryRows, type HallAttrs, type HallPreview, type HaunchDto, type PreviewItem, type PreviewRole } from './hall';
import type { SectionDto } from './sections';
import { useBoltGrades } from './boltGrades';
import { anchorCounts, BOLT_SIZES, boltLabel, type NodeParamsDto } from './nodes';
import { ProfilePickerField } from '../core/profileLibraries/ProfilePickerField';

/** The hall the configurator is open on; null: a new one. */
export const useHall = create<{ id: string | null }>(() => ({ id: null }));

type Style = { id: string; label: string; description?: string; attrs: HallAttrs };
interface Draft { style?: string; attrs: HallAttrs }

const TABS = [['frame', 'Frame'], ['haunches', 'Haunches'], ['gables', 'Gables'], ['nodes', 'Nodes'], ['secondary', 'Secondary steel'], ['placement', 'Placement']] as const;
type Tab = (typeof TABS)[number][0];

const FILL: Record<PreviewRole, string> = {
  column: '#5d6b7a', rafter: '#7f8c99', haunch: '#b4502a', post: '#9aa6b2', node: '#1e2a3a',
  purlin: '#3f7cac', rail: '#5a9367', eaves: '#5a9367', brace: '#c08a1e', fly: '#8e5ea2',
};

/** A profile's name as the spec keeps it: a catalogue name, or a section of its own (its name or kind). */
const profileName = (v: unknown): string => (typeof v === 'string' ? v : (v as SectionDto | undefined)?.name ?? (v as SectionDto | undefined)?.kind ?? '');

/** An elevation or a plan: the members' outlines (and the grid's axes with their tags in plan). */
function Plot({ items, axes, label, w = 380, h = 220 }: { items: PreviewItem[]; axes?: HallPreview['axes']; label: string; w?: number; h?: number }) {
  const ends = (axes ?? []).flatMap((a) => [a.a, a.b]);
  const f = fit(items, ends, w, h, 18);
  if (!f) return <svg width={w} height={h} />;
  const order = (r: string) => (r === 'node' ? 4 : r === 'brace' || r === 'fly' ? 3 : r === 'haunch' ? 2 : r === 'rafter' || r === 'purlin' ? 1 : 0);
  return (
    <svg viewBox={`0 0 ${w} ${h}`} width={w} height={h} aria-label={label} data-field={`hall-${label.toLowerCase()}`}>
      {(axes ?? []).map((a) => (
        <g key={a.tag}>
          <line x1={f.X(a.a[0])} y1={f.Y(a.a[1])} x2={f.X(a.b[0])} y2={f.Y(a.b[1])} stroke="#b4502a" strokeWidth={0.6} strokeDasharray="8 2 2 2" />
          <circle cx={f.X(a.a[0])} cy={f.Y(a.a[1])} r={6} fill="#ffffff" stroke="#b4502a" strokeWidth={0.6} />
          <text x={f.X(a.a[0])} y={f.Y(a.a[1]) + 3} textAnchor="middle" fontSize={8} data-no-i18n>{a.tag}</text>
        </g>
      ))}
      {[...items].sort((a, b) => order(a.role) - order(b.role)).map((it, i) => (
        <polygon key={i} points={Array.from({ length: it.pts.length / 2 }, (_, j) => `${f.X(it.pts[2 * j])},${f.Y(it.pts[2 * j + 1])}`).join(' ')}
          fill={FILL[it.role]} stroke="#1e2a3a" strokeWidth={it.role === 'node' ? 0.2 : 0.4} data-role={it.role} />
      ))}
    </svg>
  );
}

/** A haunch's fields: on or off, its kind, length and depth, and the plates' thicknesses when welded from plates. */
function HaunchFields({ which, h, set }: { which: 'eaves' | 'apex'; h: HaunchDto | null | undefined; set: (h: HaunchDto | null) => void }) {
  const on = Boolean(h);
  const cur: HaunchDto = h ?? { length: which === 'eaves' ? 2 : 1, depth: which === 'eaves' ? 0.36 : 0.18, kind: 'cut' };
  const patch = (p: Partial<HaunchDto>) => set({ ...cur, ...p });
  return (
    <>
      <label className="field-line roof-field"><span className="eyebrow">{which === 'eaves' ? 'Haunch at the eaves' : 'Haunch at the apex'}</span>
        <input type="checkbox" checked={on} data-field={`hall-${which}-haunch`} onChange={(e) => set(e.target.checked ? cur : null)} />
      </label>
      {on && <>
        <label className="field-line roof-field"><span className="eyebrow">Made</span>
          <select value={cur.kind ?? 'cut'} data-field={`hall-${which}-kind`} onChange={(e) => patch({ kind: e.target.value as HaunchDto['kind'] })}>
            <option value="cut">cut from the rafter's section</option><option value="plates">welded from plates</option>
          </select>
        </label>
        <label className="field-line roof-field"><span className="eyebrow">Length</span><LengthField metres={cur.length} min={0.05} testId={`hall-${which}-length`} onCommit={(v) => { if (v != null) patch({ length: v }); }} /></label>
        <label className="field-line roof-field"><span className="eyebrow">Depth</span><LengthField metres={cur.depth} min={0.02} testId={`hall-${which}-depth`} onCommit={(v) => { if (v != null) patch({ depth: v }); }} /></label>
        {cur.kind === 'plates' && <>
          <label className="field-line roof-field"><span className="eyebrow">Web plate</span><LengthField metres={cur.web ?? 0.01} min={0.002} onCommit={(v) => { if (v != null) patch({ web: v }); }} /></label>
          <label className="field-line roof-field"><span className="eyebrow">Flange plate</span><LengthField metres={cur.flange ?? 0.012} min={0.002} onCommit={(v) => { if (v != null) patch({ flange: v }); }} /></label>
          <label className="field-line roof-field" title="The rafter's flange width when left empty"><span className="eyebrow">Flange width</span><LengthField metres={cur.width || null} min={0} placeholder="—" onCommit={(v) => patch({ width: v ?? 0 })} /></label>
        </>}
      </>}
    </>
  );
}

/** The bolts' (or anchors') diameter: by the profiles, or one of the sizes. */
function BoltPick({ value, set, testId }: { value: number | undefined; set: (d: number | null) => void; testId: string }) {
  return (
    <select value={value ?? ''} data-field={testId} onChange={(e) => set(e.target.value ? Number(e.target.value) : null)}>
      <option value="">by the profile</option>
      {BOLT_SIZES.map((d) => <option key={d} value={d} data-no-i18n>{boltLabel(d)}</option>)}
    </select>
  );
}

export function HallDialog() {
  useLang((s) => s.lang);
  const hallId = useHall((s) => s.id);
  const node = useSession((s) => (hallId ? s.nodes.get(hallId) : undefined));
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const project = useSession((s) => [...s.nodes.values()].find((n) => n.type === 'IfcProject'));
  const styles = (info?.catalogs['steel.hallStyles'] as unknown as Style[] | undefined) ?? [];
  const own = (project?.props.hallStyles as Style[] | undefined) ?? [];
  const catalogue = (info?.catalogs['steel.profiles'] as unknown as SectionDto[] | undefined) ?? [];
  const [libraryFor, setLibraryFor] = useState<string | null>(null);
  const profiles = catalogue.filter((p) => p.kind === 'I' || p.kind === 'RHS');
  const [tab, setTab] = useState<Tab>('frame');
  const [draft, setDraft] = useState<Draft>({ attrs: {} });
  const [preview, setPreview] = useState<HallPreview | null>(null);
  const [baysDraft, setBaysDraft] = useState<string | null>(null);
  const [bracingDraft, setBracingDraft] = useState<string | null>(null);

  // The preview follows the draft and the hall as it stands (an undo).
  useEffect(() => {
    let live = true;
    void query<HallPreview>('steel.hallPreview', { ...(hallId ? { id: hallId } : {}), ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs })
      .then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [hallId, draft, node]);
  // The hall went (an undo of its making): the configurator starts a new one.
  useEffect(() => { if (hallId && !node) useHall.setState({ id: null }); }, [hallId, node]);

  const close = () => useUi.setState({ dialog: null });
  const dirty = Boolean(draft.style) || Object.keys(draft.attrs).length > 0;
  const value = <K extends keyof HallAttrs>(k: K): HallAttrs[K] => (k in draft.attrs ? draft.attrs[k] : preview?.attrs?.[k]);
  const set = <K extends keyof HallAttrs>(k: K, v: HallAttrs[K]) => setDraft((d) => ({ ...d, attrs: { ...d.attrs, [k]: v } }));
  // A kind of connection's parameter: given, or (null) back to the profiles'.
  type NodeKind = keyof NonNullable<HallAttrs['nodeParams']>;
  const nodeParams = value('nodeParams') ?? {};
  const grades = useBoltGrades(nodeParams.eaves?.grade);
  const nodeParam = <F extends keyof NodeParamsDto>(kind: NodeKind, f: F): NodeParamsDto[F] => nodeParams[kind]?.[f];
  const setNode = (kind: NodeKind, f: keyof NodeParamsDto, v: unknown) => {
    const cur: NodeParamsDto = { ...(nodeParams[kind] ?? {}) };
    if (v == null) delete cur[f]; else (cur as Record<string, unknown>)[f] = v;
    set('nodeParams', { ...nodeParams, [kind]: cur });
  };
  const remove = async () => {
    if (!hallId || !window.confirm(t('Delete the hall with its members and connections?'))) return;
    await execute('element.delete', { ids: [hallId] });
    if (!useSession.getState().error) { useUi.setState({ selection: null, selected: [] }); close(); }
  };
  const pickStyle = (id: string) => { setDraft({ style: id, attrs: {} }); setBaysDraft(null); setBracingDraft(null); };
  const args = () => ({ ...(hallId ? { id: hallId } : { storeyId: useUi.getState().storeyId ?? undefined }), ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs });
  const apply = async () => {
    const r = (await execute(hallId ? 'steel.updateHall' : 'steel.addHall', args())) as { id?: string } | null;
    if (useSession.getState().error) return;
    if (r?.id) useHall.setState({ id: r.id });
    setDraft({ attrs: {} });
  };
  const current = draft.style ?? (dirty ? undefined : (node?.props.style as string | undefined));
  const ownStyle = own.find((s) => s.id === current);
  const saveAs = async () => {
    const name = window.prompt(t('Name of the hall style'), '');
    if (!name?.trim()) return;
    const r = (await execute('steel.saveHallStyle', { ...args(), name })) as { styleId?: string } | null;
    if (!useSession.getState().error && r?.styleId) setDraft(hallId ? { attrs: {} } : { style: r.styleId, attrs: {} });
  };
  const update = async () => {
    if (!ownStyle) return;
    await execute('steel.saveHallStyle', { ...args(), name: ownStyle.label, styleId: ownStyle.id });
    if (!useSession.getState().error && hallId) setDraft({ attrs: {} });
  };

  const mono = value('roof') === 'mono';
  // A profile of the libraries (docs/plan-profile-libraries.md), copied into the project and kept by the hall as a section of it.
  const fromLibrary = (k: string, label: string, kinds: readonly string[]) => (
    <label className="field-line roof-field"><span className="eyebrow">{label}</span>
      <ProfilePickerField testId={`hall-${k}-library`} kinds={kinds} onPick={(p, library) => {
        void (execute('profiles.use', { profile: p, library }) as Promise<{ section?: unknown } | null>).then((r) => { if (r?.section) set(k as never, r.section as never); setLibraryFor(null); });
      }} />
    </label>
  );
  const profile = (k: 'column' | 'rafter' | 'post' | 'gableRafter', label: string) => {
    const name = profileName(value(k));
    if (libraryFor === k) return fromLibrary(k, label, ['I', 'RHS']);
    return (
      <label className="field-line roof-field"><span className="eyebrow">{label}</span>
        <select value={name} data-field={`hall-${k}`} onChange={(e) => { if (e.target.value === '@library') setLibraryFor(k); else set(k, e.target.value); }}>
          {!profiles.some((p) => p.name === name) && <option value={name} data-no-i18n>{name}</option>}
          {profiles.map((p) => <option key={p.name} value={p.name} data-no-i18n>{p.name}</option>)}
          <option value="@library">From a profile library…</option>
        </select>
      </label>
    );
  };
  const len = (k: 'span' | 'eaves' | 'postSpacing' | 'purlinSpacing' | 'railSpacing' | 'purlinOverhang' | 'railBottom' | 'x' | 'y', label: string, min = 0) => (
    <label className="field-line roof-field"><span className="eyebrow">{label}</span>
      <LengthField metres={value(k) ?? null} min={min} testId={`hall-${k}`} onCommit={(v) => { if (v != null) set(k, v); }} />
    </label>
  );
  const bays = value('bays');
  const units = useStandards((st) => st.units);
  // A secondary member's profile: the catalogue's of its kind, a round bar for the bracing.
  const secondary = (k: 'purlin' | 'rail' | 'brace' | 'eavesBeamProfile', label: string) => {
    const name = secondaryName(value(k));
    const names = secondaryProfiles(k === 'eavesBeamProfile' ? 'eaves' : k, catalogue as unknown as { kind: string; name?: string }[]);
    if (libraryFor === k) return fromLibrary(k, label, k === 'brace' ? ['L', 'CHS', 'circle', 'RHS'] : ['Z', 'C', 'SIGMA', 'U', 'I', 'RHS']);
    return (
      <label className="field-line roof-field"><span className="eyebrow">{label}</span>
        <select value={name} data-field={`hall-${k}`} onChange={(e) => { if (e.target.value === '@library') setLibraryFor(k); else set(k, secondaryValue(e.target.value)); }}>
          {!names.includes(name) && <option value={name} data-no-i18n>{name}</option>}
          {names.map((n) => <option key={n} value={n} data-no-i18n>{n}</option>)}
          <option value="@library">From a profile library…</option>
        </select>
      </label>
    );
  };
  const check = (k: 'secondary' | 'roofBracing' | 'wallBracing' | 'flyBraces' | 'eavesBeam', label: string, on: boolean) => (
    <label className="field-line roof-field"><span className="eyebrow">{label}</span>
      <input type="checkbox" checked={on} data-field={`hall-${k}`} onChange={(e) => set(k, e.target.checked)} />
    </label>
  );

  const items = tab === 'gables' && preview?.gable ? preview.gable : preview?.frame ?? [];
  const styleLabel = draft.style ? styles.find((s) => s.id === draft.style)?.label ?? own.find((s) => s.id === draft.style)?.label ?? '' : '';
  const status = !dirty ? (node ? '' : t('A new hall'))
    : draft.style ? `${t(styleLabel)}${Object.keys(draft.attrs).length ? `, ${t('changed by hand')}` : ''}` : t('Changed, not applied yet');
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Portal frame hall">
      <div className="dialog wide roof-dialog" data-dialog="steel-hall" data-i18n-ctx="hall">
        <header className="dialog-head">
          <span className="panel-title">Portal frame hall {node && <span className="muted small" data-no-i18n>· {node.name}</span>}</span>
          <button type="button" className="icon" onClick={() => { if (!dirty || window.confirm(t('Close without applying the changes?'))) close(); }} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div className="type-list roof-styles">
            {node && <button type="button" className={`tree-item${!current ? ' on' : ''}`} onClick={() => { setDraft({ attrs: {} }); setBaysDraft(null); }} title="The hall as it is">As it is</button>}
            <p className="eyebrow">Project</p>
            {own.length === 0 && <p className="muted small">Configure a hall, then Save as style.</p>}
            {own.map((s) => (
              <div key={s.id} className="row roof-own">
                <button type="button" className={`tree-item grow${current === s.id ? ' on' : ''}`} onClick={() => pickStyle(s.id)}><span className="grow" data-no-i18n>{s.label}</span></button>
                <button type="button" className="icon mini" title="Rename" onClick={() => { const n = window.prompt(t('Name of the hall style'), s.label); if (n?.trim()) void execute('steel.renameHallStyle', { styleId: s.id, name: n }); }}><Pencil size={12} /></button>
                <button type="button" className="icon mini" title="Delete the style (its halls keep how they are)" onClick={() => { if (window.confirm(t('Delete this hall style? Its halls keep how they are.'))) void execute('steel.deleteHallStyle', { styleId: s.id }); }}><Trash2 size={12} /></button>
              </div>
            ))}
            <p className="eyebrow">Catalogue</p>
            {styles.map((s) => (
              <button key={s.id} type="button" className={`tree-item${current === s.id ? ' on' : ''}`} data-field={`hall-style-${s.id}`} title={s.description ? t(s.description) : undefined} onClick={() => pickStyle(s.id)}>
                <span className="grow">{s.label}</span>
              </button>
            ))}
          </div>
          <div className="roof-edit">
            <div className="tabs-inline" role="tablist">
              {TABS.map(([k, l]) => <button key={k} type="button" role="tab" data-tab={`hall-${k}`} aria-selected={tab === k} className={tab === k ? 'on' : ''} onClick={() => setTab(k)}>{l}</button>)}
            </div>
            <div className="joinery-edit">
              <div className="joinery-form">
                {tab === 'frame' && <>
                  {len('span', 'Span', 3)}
                  <label className="field-line roof-field" title="Equal bays as 5 × 6000, or each listed: 6000, 6000, 7500, in the project's unit"><span className="eyebrow">Bays</span>
                    <span className="cell-wrap">
                      <input className="cell" value={baysDraft ?? baysText(bays, units)} data-field="hall-bays" onChange={(e) => setBaysDraft(e.target.value)}
                        onBlur={() => { if (baysDraft == null) return; const b = parseBays(baysDraft, units); if (b) set('bays', b); setBaysDraft(null); }}
                        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
                      {unitLabel(units) && <span className="cell-unit">{unitLabel(units)}</span>}
                    </span>
                  </label>
                  <label className="field-line roof-field" title="The hall's length: as many bays as it has, each made equal"><span className="eyebrow">Length</span>
                    <LengthField metres={bays?.length ? bays.reduce((a, b) => a + b, 0) : null} min={1} testId="hall-length"
                      onCommit={(v) => { const b = baysForLength(bays, v); if (b) { set('bays', b); setBaysDraft(null); } }} />
                  </label>
                  {len('eaves', 'Eaves height', 1)}
                  <label className="field-line roof-field"><span className="eyebrow">Roof</span>
                    <select value={value('roof') ?? 'duo'} data-field="hall-roof" onChange={(e) => set('roof', e.target.value as HallAttrs['roof'])}>
                      <option value="duo">duo-pitch</option><option value="mono">mono-pitch</option>
                    </select>
                  </label>
                  <label className="field-line roof-field"><span className="eyebrow">Pitch</span>
                    <NumberField value={value('pitch') ?? null} suffix="°" min={0} max={45} testId="hall-pitch" onCommit={(v) => { if (v != null) set('pitch', v); }} />
                  </label>
                  {profile('column', 'Columns')}
                  {profile('rafter', 'Rafters')}
                </>}
                {tab === 'haunches' && <>
                  <HaunchFields which="eaves" h={value('eavesHaunch')} set={(h) => set('eavesHaunch', h)} />
                  {!mono && <HaunchFields which="apex" h={value('apexHaunch')} set={(h) => set('apexHaunch', h)} />}
                  <p className="muted small">Length along the rafter from the column's face (or the apex), depth below the rafter there. Each haunch may also be set on its own in a rafter's properties.</p>
                </>}
                {tab === 'gables' && <>
                  <label className="field-line roof-field"><span className="eyebrow">Gables</span>
                    <select value={value('gable') ?? 'posts'} data-field="hall-gable" onChange={(e) => set('gable', e.target.value as HallAttrs['gable'])}>
                      <option value="posts">gable posts under a simple rafter</option><option value="frame">a regular frame</option>
                    </select>
                  </label>
                  {value('gable') !== 'frame' && <>
                    {len('postSpacing', 'Post spacing', 1)}
                    {profile('post', 'Gable posts')}
                    {profile('gableRafter', 'Gable rafters')}
                  </>}
                </>}
                {tab === 'nodes' && <>
                  <label className="field-line roof-field"><span className="eyebrow">Connections</span>
                    <input type="checkbox" checked={value('nodes') !== false} data-field="hall-nodes" onChange={(e) => set('nodes', e.target.checked)} />
                  </label>
                  {value('nodes') !== false && <>
                    <label className="field-line roof-field"><span className="eyebrow">Column bases</span>
                      <select value={value('base') ?? 'pinned'} data-field="hall-base" onChange={(e) => set('base', e.target.value as HallAttrs['base'])}>
                        <option value="pinned">pinned</option><option value="fixed">fixed</option>
                      </select>
                    </label>
                    <label className="field-line roof-field"><span className="eyebrow">Anchors</span>
                      <select value={value('anchor') ?? 'straight'} data-field="hall-anchor" onChange={(e) => set('anchor', e.target.value as HallAttrs['anchor'])}>
                        <option value="straight">straight</option><option value="hooked">hooked</option><option value="plate">with an anchor plate</option>
                      </select>
                    </label>
                    <label className="field-line roof-field"><span className="eyebrow">Number of anchors</span>
                      <select value={nodeParam('base', 'anchors') ?? ''} data-field="hall-anchors" onChange={(e) => setNode('base', 'anchors', e.target.value ? Number(e.target.value) : null)}>
                        <option value="">by the profile</option>
                        {anchorCounts(value('base')).map((n) => <option key={n} value={n} data-no-i18n>{n}</option>)}
                      </select>
                    </label>
                    <label className="field-line roof-field"><span className="eyebrow">Anchor length</span>
                      <LengthField metres={nodeParam('base', 'anchorLength') ?? null} min={0.1} blank placeholder="—" testId="hall-anchor-length" onCommit={(v) => setNode('base', 'anchorLength', v)} />
                    </label>
                    <p className="eyebrow">Eaves connection</p>
                    <label className="field-line roof-field"><span className="eyebrow">Plate thickness</span>
                      <LengthField metres={nodeParam('eaves', 'plate') ?? null} min={0.004} blank placeholder="—" testId="hall-eaves-plate" onCommit={(v) => setNode('eaves', 'plate', v)} />
                    </label>
                    <label className="field-line roof-field"><span className="eyebrow">Bolt diameter</span>
                      <BoltPick value={nodeParam('eaves', 'bolt')} set={(d) => setNode('eaves', 'bolt', d)} testId="hall-eaves-bolt" />
                    </label>
                    <label className="field-line roof-field"><span className="eyebrow">Bolt grade</span>
                      <select value={nodeParam('eaves', 'grade') ?? ''} data-field="hall-eaves-grade" onChange={(e) => setNode('eaves', 'grade', e.target.value || null)}>
                        <option value="">by the profile</option>
                        {grades.map((g) => <option key={g} value={g} data-no-i18n>{g}</option>)}
                      </select>
                    </label>
                    <label className="field-line roof-field"><span className="eyebrow">Above the rafter</span>
                      <LengthField metres={nodeParam('eaves', 'extension') ?? null} min={0} blank placeholder="—" testId="hall-eaves-extension" onCommit={(v) => setNode('eaves', 'extension', v)} />
                    </label>
                    <label className="field-line roof-field"><span className="eyebrow">Column stiffeners</span>
                      <input type="checkbox" checked={nodeParam('eaves', 'stiffeners') !== false} data-field="hall-eaves-stiffeners" onChange={(e) => setNode('eaves', 'stiffeners', e.target.checked ? null : false)} />
                    </label>
                    {!mono && <>
                      <p className="eyebrow">Apex connection</p>
                      <label className="field-line roof-field"><span className="eyebrow">Plate thickness</span>
                        <LengthField metres={nodeParam('apex', 'plate') ?? null} min={0.004} blank placeholder="—" testId="hall-apex-plate" onCommit={(v) => setNode('apex', 'plate', v)} />
                      </label>
                      <label className="field-line roof-field"><span className="eyebrow">Bolt diameter</span>
                        <BoltPick value={nodeParam('apex', 'bolt')} set={(d) => setNode('apex', 'bolt', d)} testId="hall-apex-bolt" />
                      </label>
                    </>}
                    <p className="muted small">What is left empty is chosen by the profiles. Each connection may also be set on its own in its properties.</p>
                  </>}
                </>}
                {tab === 'secondary' && <>
                  {check('secondary', 'Secondary steel', value('secondary') !== false)}
                  {value('secondary') !== false && <>
                    <p className="eyebrow">Purlins</p>
                    {secondary('purlin', 'Profile')}
                    {len('purlinSpacing', 'Purlin spacing', 0.3)}
                    {len('purlinOverhang', 'Past the gables', 0)}
                    <label className="field-line roof-field"><span className="eyebrow">Lengths</span>
                      <select value={value('purlinRun') ?? 'bay'} data-field="hall-purlin-run" onChange={(e) => set('purlinRun', e.target.value as HallAttrs['purlinRun'])}>
                        <option value="bay">one a bay</option><option value="continuous">continuous</option>
                      </select>
                    </label>
                    <p className="eyebrow">Side rails</p>
                    {secondary('rail', 'Profile')}
                    {len('railSpacing', 'Side rail spacing', 0.3)}
                    {len('railBottom', 'First rail at', 0)}
                    {check('eavesBeam', 'Eaves beam', value('eavesBeam') === true)}
                    {value('eavesBeam') === true && secondary('eavesBeamProfile', 'Profile')}
                    <p className="eyebrow">Bracing</p>
                    <label className="field-line roof-field" title="The bays' numbers, from the first: 1, 6"><span className="eyebrow">Braced bays</span>
                      <input value={bracingDraft ?? (value('bracing') ?? []).join(', ')} data-field="hall-bracing" onChange={(e) => setBracingDraft(e.target.value)}
                        onBlur={() => { if (bracingDraft == null) return; const b = parseBracing(bracingDraft); if (b) set('bracing', b); setBracingDraft(null); }} />
                    </label>
                    {secondary('brace', 'Profile')}
                    {check('roofBracing', 'In the roof', value('roofBracing') !== false)}
                    {check('wallBracing', 'In the long walls', value('wallBracing') !== false)}
                    {check('flyBraces', 'Fly braces', value('flyBraces') !== false)}
                    <p className="muted small">Purlins on the rafters and side rails on the columns and gable posts, each on a cleat; X bracing on gusset plates; fly braces from every second purlin and rail to the inner flange.</p>
                  </>}
                </>}
                {tab === 'placement' && <>
                  {len('x', 'Axis A at x')}
                  {len('y', 'Axis 1 at y')}
                  <label className="field-line roof-field" title="The span's direction in plan, from x"><span className="eyebrow">Rotation</span>
                    <NumberField value={value('angle') ?? 0} suffix="°" testId="hall-angle" onCommit={(v) => { if (v != null) set('angle', v); }} />
                  </label>
                </>}
              </div>
              <div className="joinery-preview">
                {tab === 'placement' || tab === 'secondary'
                  ? <Plot items={preview?.plan ?? []} axes={preview?.axes} label="Plan" />
                  : <Plot items={tab === 'gables' && preview?.gable ? items : [...items, ...(preview?.frameNodes ?? [])]} label="Elevation" />}
                <p className="muted small">{tab === 'placement' || tab === 'secondary' ? 'The grid and the members, from above.' : tab === 'gables' && preview?.gable ? 'The gable, seen along the hall.' : 'A typical frame, seen along the hall.'}</p>
                {preview?.problem && <p className="error-text" data-field="hall-problem">{preview.problem}</p>}
                {preview?.summary && (
                  <dl className="fields roof-summary" data-field="hall-summary">
                    {summaryRows(preview.summary, lengthText).map(([k, v]) => <div key={k} className="contents"><dt>{k}</dt><dd className="num" data-no-i18n>{v}</dd></div>)}
                  </dl>
                )}
              </div>
              <div className="row joinery-actions">
                <span className="muted small grow">{status}</span>
                {node && <button type="button" className="btn" data-field="hall-delete" title="Delete the hall with its members and connections" onClick={() => void remove()}><Trash2 size={14} />Delete hall</button>}
                <button type="button" className="btn" disabled={!dirty} onClick={() => { setDraft({ attrs: {} }); setBaysDraft(null); }}><RotateCcw size={14} />Reset</button>
                {ownStyle && hallId && <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save the hall as it is configured into its style; every hall of the style follows" onClick={() => void update()}><Save size={14} />Update style</button>}
                <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save the hall as it is configured as a style of the project" onClick={() => void saveAs()}><Save size={14} />Save as style…</button>
                <button type="button" className="btn primary" data-field="hall-apply" disabled={(!dirty && Boolean(node)) || Boolean(preview?.problem)} onClick={() => void apply()}>{node ? 'Apply' : 'Create hall'}</button>
              </div>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
