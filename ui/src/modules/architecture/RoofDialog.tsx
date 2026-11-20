/**
 * The roof configurator (decision 231): the catalogue's styles on the left, and on the right the roof being configured —
 * its shape and edges, its covering and drainage, its timber framing, its openings — over a plan of it (the edges clicked
 * through auto, eave, gable) and a section across its ridge through the framing, both as the engine would build the draft.
 * A style picked fills every field in; any field may then be changed by hand. Apply is one engine command, one undo step.
 */

import { projectOf } from '../../shell/nodeIndex';
import { confirmAction, promptText } from '../../shell/prompts';
import { ColourField } from '../../shell/ColourField';
import { useEffect, useMemo, useState } from 'react';
import { lengthText } from '../../engine/unitsStore';
import { DescriptorField, LengthField, NumberField } from '../../shell/cells';
import { PendingDialog } from '../core/PendingDialog';
import { MaterialPicker } from '../core/MaterialPicker';
import { mergeEdge, type Edge } from './roofEdges';
import { EDGE, OUTLINE, extentsOf, type LayerExtent } from './layerExtents';
import { ArrowDown, ArrowUp, Pencil, Plus, RotateCcw, Save, Trash2, X, BookMarked, FileDown, FileUp } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../engine/files';
import type { FieldDescriptor } from '../../engine/protocol';
import { t, useLang } from '../../i18n';
import {
  applyRoofTemplateArgs, exportRoofTemplatesJson, forgetRoofTemplate, importRoofTemplates,
  libraryRoofTemplates, parseRoofTemplatesJson, renameRoofTemplate, saveRoofTemplate, subscribeRoofTemplates,
  type UserRoofTemplate,
} from './roofTemplateLibrary';

type Style = { id: string; label: string; family: string; region: string; shape: string; pitch: number; covering: string; description?: string; structure?: string | null };
type Covering = { id: string; color: string; minPitch: number };
/** A project's own style (decision 233), kept on the project. */
type OwnStyle = { id: string; label: string; shape: string; pitch: number; structure?: string };

type Item = { role: string; pts: number[]; name?: string; material?: string; layerRole?: string };
interface RoofLayer { material: string; thickness: number; role?: string; name?: string }
const ROOF_LAYER_ROLES = ['structure', 'insulation', 'sheathing', 'membrane', 'finish'] as const;
const LAYER_ROLE_FILL: Record<string, string> = {
  structure: '#c9b79c', insulation: '#e5c158', sheathing: '#b98a5a', membrane: '#4a5568', finish: '#b0563d',
};
const LAYER_PALETTE = ['#c9b79c', '#e5c158', '#b98a5a', '#4a5568', '#b0563d'];
interface Preview {
  problem?: string | null;
  attrs?: Record<string, unknown>;
  outline?: [number, number][];
  builtOutline?: [number, number][];
  eaves?: [number, number][];
  kinds?: ('eave' | 'gable')[];
  lines?: { kind: string; a: [number, number]; b: [number, number] }[];
  section?: { items: Item[]; box: [number, number, number, number]; span?: number | null; plate: number; top: number };
  /** Where the downpipes leave the gutters (decision 235), and whether they are the roof's automatic ones. */
  outlets?: [number, number][];
  downpipes?: string;
  summary?: [string, string][];
}
interface Draft { style?: string; templateId?: string; attrs: Record<string, unknown>; edges?: Edge[]; arcs?: number[] }

const TABS = [['shape', 'Shape'], ['covering', 'Covering and drainage'], ['layers', 'Layers'], ['framing', 'Framing'], ['openings', 'Openings']] as const;
type Tab = (typeof TABS)[number][0];

/** A flat roof, a vault or one without a structure: a deck under its covering. */
const unframed = (v: (k: string) => unknown) => ['flat', 'barrel'].includes(String(v('shape'))) || v('structure') === 'none';

/** Which fields each tab shows, and when (a mansard's upper pitch only on a mansard…). */
const FIELDS: Record<Tab, [string, ((v: (k: string) => unknown) => boolean)?][]> = {
  shape: [['shape'], ['pitch', (v) => v('shape') !== 'barrel'], ['overhang'], ['gableOverhang'], ['ridgeDirection', (v) => ['gable', 'shed', 'barrel'].includes(String(v('shape')))],
    ['shedFlip', (v) => v('shape') === 'shed'], ['upperPitch', (v) => v('shape') === 'mansard'], ['mansardInset', (v) => v('shape') === 'mansard'], ['rise', (v) => v('shape') === 'barrel'], ['eaveOffset']],
  covering: [['covering'], ['coveringMaterial'], ['gutterProfile'], ['gutterMaterial', (v) => v('gutterProfile') !== 'none'],
    ['gutterWidth', (v) => v('gutterProfile') !== 'none'], ['gutterOffset', (v) => v('gutterProfile') !== 'none'], ['gutterDrop', (v) => v('gutterProfile') !== 'none'],
    ['gutterFall', (v) => v('gutterProfile') !== 'none'], ['gutterEnds', (v) => v('gutterProfile') !== 'none'], ['downpipes', (v) => v('gutterProfile') !== 'none'], ['snowGuards']],
  layers: [],
  framing: [['structure', (v) => !['flat', 'barrel'].includes(String(v('shape')))],
    ['rafterSection', (v) => !['none'].includes(String(v('structure')))], ['rafterSpacing', (v) => v('structure') !== 'none'],
    ['purlins', (v) => (v('structure') ?? 'rafters') === 'rafters'],
    ['purlinSection', (v) => (v('structure') ?? 'rafters') === 'rafters' && Number(v('purlins') ?? 1) > 0],
    ['ridgeSection', (v) => (v('structure') ?? 'rafters') === 'rafters'],
    ['plateSection', (v) => v('structure') !== 'none'],
    ['webSection', (v) => v('structure') === 'trusses'], ['collarHeight', (v) => v('structure') === 'collar'],
    ['collarSection', (v) => v('structure') === 'collar'],
    ['timberGrade', (v) => v('structure') !== 'none'], ['rafterTail', (v) => v('structure') !== 'none'], ['rafterSeat', (v) => ['rafters', 'collar', undefined].includes(v('structure') as string | undefined)],
    // Without framing, the deck under the covering (decision 329).
    ['deckMaterial', unframed], ['deckThickness', unframed]],
  openings: [],
};

/** What the engine takes for a field the roof does not have yet (a roof made before the field was). */
const DEFAULTS: Record<string, unknown> = { rafterTail: 'plumb', rafterSeat: 'birdsmouth', collarHeight: 0.66, timberGrade: 'C24', webSection: '45x95 mm', purlinSection: '14x16', ridgeSection: '14x20', plateSection: '14x14', ridgeDirection: 'auto', shedFlip: false,
  gutterWidth: 0.125, gutterOffset: 0, gutterDrop: 0, gutterFall: 0, gutterEnds: 'True', downpipes: 'auto', deckMaterial: 'Reinforced concrete', deckThickness: 0.16 };

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
  return <DescriptorField f={f} value={value} onChange={onChange} />;
}

/** The plan: the wall line with its edges (thick: eave, dashed: gable; click to go auto → eave → gable), the eaves, ridges, hips and valleys. */
function PlanPlot({ p, edges, onEdge }: { p: Preview; edges: Edge[]; onEdge: (i: number) => void }) {
  const W = 360, H = 240, pad = 22;
  const all = [...(p.builtOutline ?? p.outline ?? []), ...(p.eaves ?? [])];
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
      <path d={path(p.builtOutline ?? outline)} fill="#ffffff" stroke="none" />
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
  const order = (r: string) => (r === 'wall' ? 0 : r === 'covering' || r === 'deck' || r.startsWith('layer:') ? 1 : r === 'plate' ? 2 : 3);
  const items = [...s.items].sort((a, b) => order(a.role) - order(b.role));
  const fillOf = (it: Item) => {
    if (it.role.startsWith('layer:')) {
      const idx = Number(it.role.slice(6));
      return (it.layerRole && LAYER_ROLE_FILL[it.layerRole]) || LAYER_PALETTE[idx % LAYER_PALETTE.length];
    }
    return ROLE_FILL[it.role] ?? '#c99a64';
  };
  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Section">
      {items.map((it, i) => (
        <polygon key={i} points={it.pts.reduce<string[]>((acc, v, j) => (j % 2 ? [...acc.slice(0, -1), `${acc[acc.length - 1]},${Z(v)}`] : [...acc, `${X(v)}`]), []).join(' ')}
          fill={fillOf(it)} stroke="#3a2a1a" strokeWidth={0.5} opacity={it.role === 'covering' ? 0.85 : 1}>
          <title>{it.name ? `${it.name}${it.material ? ` (${it.material})` : ''}` : it.role}</title>
        </polygon>
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
  const project = useSession((s) => projectOf(s.nodes));
  const own = (project?.props.roofStyles as OwnStyle[] | undefined) ?? [];
  const coverings = (info?.catalogs['architecture.roofCoverings'] as unknown as Covering[] | undefined) ?? [];
  const fields = useMemo(() => new Map((info?.nodeTypes.find((c) => c.type === 'IfcRoof')?.fields ?? []).map((f) => [f.key, f])), [info]);
  const initialTab = useUi((s) => s.roofDialogTab);
  const [tab, setTab] = useState<Tab>(() => initialTab ?? 'shape');
  const [draft, setDraft] = useState<Draft>({ attrs: {} });
  const [preview, setPreview] = useState<Preview | null>(null);
  const [userTemplates, setUserTemplates] = useState<UserRoofTemplate[]>(() => libraryRoofTemplates());

  useEffect(() => subscribeRoofTemplates(() => setUserTemplates(libraryRoofTemplates())), []);

  useEffect(() => {
    if (initialTab) setTab(initialTab);
  }, [initialTab]);

  // The preview follows the draft and the roof as it stands (an undo, a grip moved).
  useEffect(() => {
    if (!roofId) return;
    let live = true;
    void query<Preview>('architecture.roofPreview', { id: roofId, ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs, ...(draft.edges ? { edges: draft.edges } : {}), ...(draft.arcs ? { arcs: draft.arcs } : {}) })
      .then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [roofId, draft, node]);
  // The roof went (an undo, deleted by someone in the session): the configurator closes.
  useEffect(() => { if (!node) useUi.setState({ dialog: null }); }, [node]);

  if (!node) return <PendingDialog title="Roof" />;
  const close = () => useUi.setState({ dialog: null, roofDialogTab: null });
  const dirty = Boolean(draft.style) || Boolean(draft.templateId) || Object.keys(draft.attrs).length > 0 || draft.edges !== undefined;
  const value = (k: string): unknown => (k in draft.attrs ? draft.attrs[k] : preview?.attrs?.[k] ?? node.props[k] ?? DEFAULTS[k]);
  const set = (k: string, v: unknown) => setDraft((d) => ({ ...d, attrs: { ...d.attrs, [k]: v } }));
  const contour = (preview?.outline ?? (node.props.contour as [number, number][] | undefined) ?? []);
  const edges: Edge[] = draft.edges ?? ((preview?.attrs?.edges as Edge[] | undefined) ?? (node.props.edges as Edge[] | undefined) ?? []);
  const setEdge = (i: number, patch: Partial<NonNullable<Edge>> | null) => setDraft((d) => ({ ...d, edges: mergeEdge(edges, contour.length, i, patch) }));
  const cycleEdge = (i: number) => setEdge(i, { kind: edges[i]?.kind === undefined ? 'eave' : edges[i]?.kind === 'eave' ? 'gable' : undefined });
  const pickStyle = (id: string) => setDraft({ style: id, attrs: {} });
  const pickUserTemplate = (tpl: UserRoofTemplate) => {
    const args = applyRoofTemplateArgs(node.id, tpl, contour);
    setDraft({ templateId: tpl.id, attrs: args.attrs, edges: args.edges, ...(args.arcs ? { arcs: args.arcs } : {}) });
  };
  const apply = async () => {
    await execute('architecture.updateRoof', draftArgs());
    if (!useSession.getState().error) setDraft({ attrs: {} });
  };
  const current = draft.style ?? draft.templateId ?? (dirty ? undefined : (node.props.style as string | undefined));
  const draftArgs = () => ({ id: node.id, ...(draft.style ? { style: draft.style } : {}), attrs: draft.attrs, ...(draft.edges ? { edges: draft.edges } : {}), ...(draft.arcs ? { arcs: draft.arcs } : {}) });
  // Saved as the project's own style: a new one under a name, or the one it has saved again (its roofs follow).
  const saveAs = async () => {
    const name = promptText(t('Name of the roof style'), node.props.style && own.some((s) => s.id === node.props.style) ? '' : `${node.name}`);
    if (!name?.trim()) return;
    await execute('architecture.saveRoofStyle', { ...draftArgs(), name });
    if (!useSession.getState().error) setDraft({ attrs: {} });
  };
  const ownStyle = own.find((s) => s.id === (draft.style ?? node.props.style));
  const activeTemplate = draft.templateId ? userTemplates.find((u) => u.id === draft.templateId) : undefined;
  const update = async () => {
    if (!ownStyle) return;
    await execute('architecture.saveRoofStyle', { ...draftArgs(), name: ownStyle.label, styleId: ownStyle.id });
    if (!useSession.getState().error) setDraft({ attrs: {} });
  };
  const saveAsTemplate = () => {
    const defaultName = ownStyle?.label ?? activeTemplate?.label ?? node.name;
    const name = promptText(t('Name of the roof template'), defaultName);
    if (!name?.trim()) return;
    const source: Record<string, unknown> = { ...node.props, ...(preview?.attrs ?? {}), ...draft.attrs };
    let res = saveRoofTemplate(name, source, { contour });
    if (res.status === 'exists') {
      if (!confirmAction(t('A user template with this name already exists. Replace it?'))) return;
      res = saveRoofTemplate(name, source, { overwrite: true, contour });
    }
    if (res.status === 'full') {
      notify("This computer's library is full: the roof template was not saved.");
      return;
    }
    if (res.status === 'kept') notify(`Saved user template "${res.template.label}".`);
  };
  const exportUserTemplates = async (list: UserRoofTemplate[], baseName: string) => {
    const text = exportRoofTemplatesJson(list);
    const saved = await saveFile(new TextEncoder().encode(text), `${safeFileName(baseName, 'roof-templates')}.json`, 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const importUserTemplates = async () => {
    const f = await pickFile('Roof templates (JSON)', 'json');
    if (!f) return;
    let parsed: unknown;
    try { parsed = JSON.parse(new TextDecoder().decode(f.bytes)); } catch { notify('That file is not a roof template.'); return; }
    const incoming = parseRoofTemplatesJson(parsed);
    if (incoming.length === 0) { notify('That file contains no valid roof templates.'); return; }
    const firstPass = importRoofTemplates(incoming, false);
    let totalAdded = firstPass.added;
    let totalUpdated = firstPass.updated;
    if (firstPass.skipped > 0 && confirmAction(t(`${firstPass.skipped} template(s) already exist. Replace them?`))) {
      const secondPass = importRoofTemplates(incoming, true);
      totalAdded += secondPass.added;
      totalUpdated += secondPass.updated;
      if (secondPass.full) { notify("This computer's library is full."); return; }
    } else if (firstPass.full) {
      notify("This computer's library is full.");
      return;
    }
    notify(`Imported ${totalAdded} new and ${totalUpdated} updated template(s).`);
  };
  const families = [...new Set(styles.map((s) => s.family))];
  const covering = coverings.find((c) => c.id === value('covering'));
  const pitch = Number(value('pitch'));
  const openings = (node.props.openings as { id: string; name: string; kind: string; width?: number; length?: number }[] | undefined) ?? [];
  const rawLayers = value('layers');
  const roofLayers: RoofLayer[] = Array.isArray(rawLayers) ? rawLayers : [];
  const layerExtents = extentsOf({ layerExtents: value('layerExtents') });
  const setLayersAndExtents = (nextLayers: RoofLayer[], nextExtents: LayerExtent[]) =>
    setDraft((d) => ({ ...d, attrs: { ...d.attrs, layers: nextLayers, layerExtents: nextExtents } }));
  const updateLayer = (i: number, patch: Partial<RoofLayer>) =>
    set('layers', roofLayers.map((l, k) => (k === i ? { ...l, ...patch } : l)));
  const removeLayer = (i: number) => {
    const nextL = roofLayers.filter((_, k) => k !== i);
    const nextE = layerExtents.filter((x) => x.layer !== i).map((x) => (x.layer > i ? { ...x, layer: x.layer - 1 } : x));
    setLayersAndExtents(nextL, nextE);
  };
  const moveLayer = (i: number, dir: -1 | 1) => {
    const j = i + dir;
    if (j < 0 || j >= roofLayers.length) return;
    const nextL = [...roofLayers];
    [nextL[i], nextL[j]] = [nextL[j], nextL[i]];
    const nextE = layerExtents.map((x) => (x.layer === i ? { ...x, layer: j } : x.layer === j ? { ...x, layer: i } : x));
    setLayersAndExtents(nextL, nextE);
  };
  const addLayer = () =>
    set('layers', [...roofLayers, { material: String(value('coveringMaterial') || value('covering') || 'Clay tile'), thickness: 0.04, role: roofLayers.length === 0 ? 'sheathing' : 'finish' }]);
  const clearLayers = () => setLayersAndExtents([], []);
  const setExtents = (next: LayerExtent[]) => set('layerExtents', next);
  const edgeValue = (x: LayerExtent) => (x.edge === EDGE ? `${EDGE}:${x.index ?? 0}` : x.edge);
  const fromEdge = (x: LayerExtent, v: string): LayerExtent => {
    if (v.startsWith(`${EDGE}:`)) return { ...x, edge: EDGE, index: Number(v.slice(EDGE.length + 1)) };
    const { index: _drop, ...rest } = x;
    return { ...rest, edge: v };
  };

  const rows = FIELDS[tab].filter(([k, when]) => fields.has(k) && (!when || when(value))).map(([k]) => {
    const f = fields.get(k)!;
    const fieldValue = k === 'collarSection' ? (value(k) ?? value('rafterSection')) : value(k);
    return (
      <label key={k} className="field-line roof-field"><span className="eyebrow">{f.label}</span>
        <FieldInput f={f} value={fieldValue} onChange={(v) => set(k, v)} />
      </label>
    );
  });

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Roof configurator">
      <div className="dialog wide roof-dialog">
        <header className="dialog-head">
          <span className="panel-title">Roof configurator <span className="muted small">· {node.name}</span></span>
          <button type="button" className="icon" onClick={() => { if (!dirty || confirmAction('Close without applying the changes?')) close(); }} title="Close"><X size={15} /></button>
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
                <button type="button" className="icon mini" title="Rename" onClick={() => { const n = promptText(t('Name of the roof style'), s.label); if (n?.trim()) void execute('architecture.renameRoofStyle', { styleId: s.id, name: n }); }}><Pencil size={12} /></button>
                <button type="button" className="icon mini" title="Delete the style (its roofs keep how they look)" onClick={() => { if (confirmAction(t('Delete this roof style? Its roofs keep how they look.'))) void execute('architecture.deleteRoofStyle', { styleId: s.id }); }}><Trash2 size={12} /></button>
              </div>
            ))}
            <div className="row roof-own">
              <p className="eyebrow grow">User templates</p>
              <button type="button" className="icon mini" title="Import user templates from JSON…" onClick={() => void importUserTemplates()}><FileUp size={12} /></button>
              {userTemplates.length > 0 && <button type="button" className="icon mini" title="Export all user templates to JSON…" onClick={() => void exportUserTemplates(userTemplates, 'roof-templates')}><FileDown size={12} /></button>}
            </div>
            {userTemplates.length === 0 && <p className="muted small">Save a roof as a template to reuse it in future projects.</p>}
            {userTemplates.map((tpl) => (
              <div key={tpl.id} className="row roof-own">
                <button type="button" className={`tree-item grow${current === tpl.id ? ' on' : ''}`} onClick={() => pickUserTemplate(tpl)}>
                  <span className="grow">{tpl.label}</span>
                  <span className="muted small">{t(tpl.shape)}{tpl.shape === 'barrel' ? '' : ` ${tpl.pitch}°`}{tpl.structure && tpl.structure !== 'rafters' ? ` · ${tpl.structure}` : ''}</span>
                </button>
                <button type="button" className="icon mini" title="Rename" onClick={() => { const n = promptText(t('Name of the roof template'), tpl.label); if (n?.trim()) renameRoofTemplate(tpl.id, n); }}><Pencil size={12} /></button>
                <button type="button" className="icon mini" title="Export template to JSON…" onClick={() => void exportUserTemplates([tpl], tpl.label)}><FileDown size={12} /></button>
                <button type="button" className="icon mini" title="Delete user template" onClick={() => { if (confirmAction(t('Delete this user roof template?'))) { forgetRoofTemplate(tpl.id); if (draft.templateId === tpl.id) setDraft({ attrs: {} }); } }}><Trash2 size={12} /></button>
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
                        <ColourField value={typeof value('gutterColor') === 'string' && value('gutterColor') ? String(value('gutterColor')) : '#8a9096'} onChange={(v) => set('gutterColor', v)} />
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
                {tab === 'layers' && (
                  <div className="roof-layers" data-testid="roof-layers-editor">
                    {roofLayers.length === 0 ? (
                      <p className="muted small">Using default covering and structure. Add layers to define a custom multi-layer buildup.</p>
                    ) : (
                      <>
                        <p className="muted small">Bottom (inside) to top (outside) above the rafters or base plane.</p>
                        <table className="roof-edges">
                          <thead><tr><th>#</th><th>Role</th><th>Material</th><th>Thickness</th><th /></tr></thead>
                          <tbody>
                            {roofLayers.map((l, i) => (
                              <tr key={i}>
                                <td className="num">{i + 1}</td>
                                <td>
                                  <select value={l.role ?? ''} onChange={(e) => updateLayer(i, { role: e.target.value || undefined })}>
                                    <option value="">—</option>
                                    {ROOF_LAYER_ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
                                  </select>
                                </td>
                                <td><MaterialPicker value={l.material} placeholder="none" allowEmpty onChange={(m) => updateLayer(i, { material: m })} /></td>
                                <td><LengthField metres={l.thickness} min={0.001} max={2} onCommit={(m) => { if (m != null) updateLayer(i, { thickness: m }); }} /></td>
                                <td className="row" style={{ gap: 2 }}>
                                  <button type="button" className="icon mini" title="Move down" disabled={i === 0} onClick={() => moveLayer(i, -1)}><ArrowUp size={12} /></button>
                                  <button type="button" className="icon mini" title="Move up" disabled={i === roofLayers.length - 1} onClick={() => moveLayer(i, 1)}><ArrowDown size={12} /></button>
                                  <button type="button" className="icon mini" title="Remove" onClick={() => removeLayer(i)}><X size={12} /></button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </>
                    )}
                    <div className="row" style={{ gap: 6, marginTop: 6 }}>
                      <button type="button" className="small" data-testid="roof-layer-add" onClick={addLayer}><Plus size={12} /> Add layer</button>
                      {roofLayers.length > 0 && (
                        <button type="button" className="small" onClick={clearLayers}>Clear custom layers</button>
                      )}
                    </div>
                    {roofLayers.length > 0 && (
                      <div className="layer-extents" style={{ marginTop: 12 }}>
                        <p className="eyebrow">Unlocked layer extensions</p>
                        {layerExtents.length === 0 ? (
                          <p className="muted small">Every layer ends at the roof's overhang.</p>
                        ) : (
                          layerExtents.map((x, ei) => (
                            <div key={ei} className="layer-extent" style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(0, 1fr) auto auto', gap: 4, alignItems: 'center', marginTop: 6 }}>
                              <select style={{ minWidth: 0 }} value={x.layer} onChange={(e) => setExtents(layerExtents.map((y, k) => (k === ei ? { ...y, layer: Number(e.target.value) } : y)))}>
                                {roofLayers.map((l, li) => <option key={li} value={li}>{li + 1} · {l.name || l.material || l.role || 'Layer'}</option>)}
                              </select>
                              <select style={{ minWidth: 0 }} value={edgeValue(x)} onChange={(e) => setExtents(layerExtents.map((y, k) => (k === ei ? fromEdge(y, e.target.value) : y)))}>
                                <option value={OUTLINE}>All round</option>
                                {contour.map((_, idx) => <option key={idx} value={`${EDGE}:${idx}`}>Edge {idx + 1}</option>)}
                              </select>
                              <LengthField metres={x.offset} min={-1} max={1} onCommit={(m) => { if (m != null) setExtents(layerExtents.map((y, k) => (k === ei ? { ...y, offset: m } : y))); }} />
                              <button type="button" className="icon" title="Remove" onClick={() => setExtents(layerExtents.filter((_, k) => k !== ei))}><X size={12} /></button>
                            </div>
                          ))
                        )}
                        <div className="row" style={{ marginTop: 6 }}>
                          <button type="button" className="small" data-testid="roof-layer-extent-add" onClick={() => setExtents([...layerExtents, { layer: 0, edge: OUTLINE, offset: 0.05 }])}>
                            <Plus size={12} /> Add layer extension
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
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
                {preview && ((tab === 'framing' || tab === 'layers') && preview.section ? <SectionPlot s={preview.section} /> : <PlanPlot p={preview} edges={edges} onEdge={cycleEdge} />)}
                {tab !== 'framing' && tab !== 'layers' && <p className="muted small">Click an edge: auto, eave, gable.</p>}
                {(tab === 'framing' || tab === 'layers') && preview?.section && <p className="muted small">Section across the ridge, at the middle.</p>}
                {preview?.problem && <p className="error-text">{preview.problem}</p>}
                {preview?.summary && (
                  <dl className="fields roof-summary">
                    {preview.summary.map(([k, v]) => <div key={k} className="contents"><dt>{k}</dt><dd className="num">{v}</dd></div>)}
                  </dl>
                )}
              </div>
              <div className="row joinery-actions">
                <span className="muted small grow">{dirty ? (draft.style ? `${styles.find((s) => s.id === draft.style)?.label ?? own.find((s) => s.id === draft.style)?.label ?? draft.style}${Object.keys(draft.attrs).length || draft.edges ? ', changed by hand' : ''}` : activeTemplate ? activeTemplate.label : 'Changed, not applied yet') : ''}</span>
                <button type="button" className="btn" disabled={!dirty} onClick={() => setDraft({ attrs: {} })}><RotateCcw size={14} />Reset</button>
                {ownStyle && <button type="button" className="btn" disabled={Boolean(preview?.problem)} title={`Save the roof as it is configured into ${ownStyle.label}; every roof of the style follows`} onClick={() => void update()}><Save size={14} />Update style</button>}
                <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save the roof as it is configured as a style of the project" onClick={() => void saveAs()}><Save size={14} />Save as style…</button>
                <button type="button" className="btn" disabled={Boolean(preview?.problem)} title="Save the roof as it is configured as a reusable template for future projects" onClick={saveAsTemplate}><BookMarked size={14} />Save as template…</button>
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
