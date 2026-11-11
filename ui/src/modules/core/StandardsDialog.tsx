/**
 * Drafting standards: the project's text styles, dimension styles, axis types
 * (and the default one), tag types (a box, or a door's or window's marker
 * across its wall in plan; their leaders, decision 331), layers and layer groups; on Project, the units, line ends and the
 * section and elevation markers' sizes, each kind its own (decisions 455, 460). Sizes are paper millimetres.
 * Each change is one engine command; Standard (and layer 0) always stay.
 */

import { clearStandardsTab, standardsTab } from './standardsTab';
import { useEffect, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { PendingDialog } from './PendingDialog';
import { BuildupEditor } from './BuildupEditor';
import { LeaderEditor, type LeaderEntry } from './LeaderEditor';
import { BUILDUP_DEFAULTS, type BuildupEntry } from './buildupTag';

type Entry = Record<string, string | number | boolean | null | undefined> & { id: string; name: string };
type Book = { textStyles: Entry[]; dimStyles: Entry[]; axisStyles: Entry[]; tagTypes: Entry[]; defaultAxisStyle: string; layers: Entry[]; layerGroups: Entry[]; units?: string; lineCap?: string; sectionMarkSize?: number; sectionMarkText?: number; elevationMarkSize?: number; elevationMarkText?: number; areaRounding?: number };
type Col = { key: string; label: string; title?: string; kind: 'text' | 'mm' | 'number' | 'decimals' | 'bool' | 'color' | 'choice' | 'style' | 'group'; choices?: string[] };
type Tab = { id: 'text' | 'dim' | 'axis' | 'tag' | 'layer'; label: string; list: keyof Book; cols: Col[]; fresh: Record<string, unknown> };

const LINE_TYPES = ['continuous', 'dashed', 'dotted', 'dashdot'];
/** How a choice reads where its value alone would not say it. */
const CHOICE_LABELS: Record<string, string> = { project: 'as the project', across: 'perpendicular to the tag line', along: 'along the tag line', buildup: 'build-up',
  // An opening's height under a dimension line across it (report #92, decision 338).
  off: 'off', number: '150', h: 'h=150', hp: 'h=150, p=90', slash: '150/90' };
const TABS: Tab[] = [
  { id: 'text', label: 'Text', list: 'textStyles', fresh: { name: 'Text', height: 2.5 }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'height', label: 'Height mm', kind: 'mm' }, { key: 'font', label: 'Font', kind: 'choice', choices: ['sans', 'serif', 'mono'] },
    { key: 'bold', label: 'B', kind: 'bool' }, { key: 'italic', label: 'I', kind: 'bool' }, { key: 'color', label: 'Colour', kind: 'color' }] },
  { id: 'dim', label: 'Dimensions', list: 'dimStyles', fresh: { name: 'Dimension' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'arrow', label: 'Arrow', kind: 'choice', choices: ['tick', 'arrow', 'dot', 'none'] },
    { key: 'arrowSize', label: 'Arrow mm', kind: 'mm' }, { key: 'extGap', label: 'Gap mm', kind: 'mm' }, { key: 'extOver', label: 'Over mm', kind: 'mm' },
    { key: 'placement', label: 'Value', kind: 'choice', choices: ['above', 'centered', 'below'] }, { key: 'unit', label: 'Unit', kind: 'choice', choices: ['project', 'm', 'cm', 'mm', 'ft-in', 'in'] },
    { key: 'precision', label: 'Decimals', kind: 'decimals' },
    // A segment across a door, a window or an opening writes its height under the line, and its sill in some forms (decision 338).
    { key: 'openingHeight', label: 'Opening height', title: 'Under a dimension segment across a door, window or opening: its height (and its sill), read from the model', kind: 'choice', choices: ['off', 'number', 'h', 'hp', 'slash'] }] },
  { id: 'axis', label: 'Grid axes', list: 'axisStyles', fresh: { name: 'Axis' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'bubble', label: 'Bubble', kind: 'choice', choices: ['circle', 'square'] },
    { key: 'ends', label: 'Ends', kind: 'choice', choices: ['both', 'start', 'end', 'none'] }, { key: 'size', label: 'Size mm', kind: 'mm' },
    { key: 'lineType', label: 'Line', kind: 'choice', choices: LINE_TYPES }, { key: 'firstLabel', label: 'First', kind: 'text' }, { key: 'color', label: 'Colour', kind: 'color' }] },
  // A tag's template reads the element: {mark}, {name}, {type}, {class}, a door's or window's {width}, {height}, {sill} (in the unit given), a room's {number}, {area}, {perimeter}, {height}, {volume}, {function}; "|" starts a new line.
  { id: 'tag', label: 'Tags', list: 'tagTypes', fresh: { name: 'Tag', template: '{mark}', leaderShape: 'elbow' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'shape', label: 'Shape', kind: 'choice', choices: ['box', 'opening', 'room', 'buildup'] },
    { key: 'template', label: 'Text (outside)', kind: 'text' }, { key: 'inner', label: 'Inside', kind: 'text' },
    { key: 'unit', label: 'Unit', kind: 'choice', choices: ['cm', 'mm', 'm', 'ft-in', 'in', 'project'] }, { key: 'reach', label: 'Past faces mm', kind: 'mm' },
    // A door's or window's marker: its texts across its line (along the wall) or along it, always upright (decision 292).
    { key: 'textOrientation', label: 'Text direction', kind: 'choice', choices: ['across', 'along'] },
    { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'classes', label: 'Classes', kind: 'text' }] },
  { id: 'layer', label: 'Layers', list: 'layers', fresh: { name: 'Layer' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'color', label: 'Colour', kind: 'color' }, { key: 'lineWeight', label: 'Pen mm', kind: 'mm' },
    { key: 'lineType', label: 'Line', kind: 'choice', choices: LINE_TYPES }, { key: 'group', label: 'Group', kind: 'group' }, { key: 'visible', label: 'On', kind: 'bool' }, { key: 'locked', label: 'Lock', kind: 'bool' }] },
];

/** The markers' sizes on the Project tab (decision 460): a section's and an elevation's apart, each its symbol and its name. */
type MarkKey = 'sectionMarkSize' | 'sectionMarkText' | 'elevationMarkSize' | 'elevationMarkText';
const MARK_ROWS: { key: MarkKey; field: string; size: boolean; label: string; title: string }[] = [
  { key: 'sectionMarkSize', field: 'section-mark-size', size: true, label: 'Section marker size', title: "The size of the section markers' arrows (0.7 of it), in paper mm, in plans, sections, elevations, sheets and PDF; a section's own size in its properties goes before it" },
  { key: 'sectionMarkText', field: 'section-mark-text', size: false, label: 'Section marker text', title: "The height of the section markers' names, in paper mm; empty: the Standard text style's" },
  { key: 'elevationMarkSize', field: 'elevation-mark-size', size: true, label: 'Elevation marker size', title: "The elevation markers' circle across, in paper mm, in plans, sheets and PDF; an elevation's own size in its properties goes before it" },
  { key: 'elevationMarkText', field: 'elevation-mark-text', size: false, label: 'Elevation marker text', title: "The height of the elevation markers' names, in paper mm; empty: the Standard text style's" },
];

/** How a tag's template reads the element (report #64), one phrase for the translations. */
const TAG_HELP = 'A template reads the element: {mark}, {name}, {type}, {class}, any parameter by its name ({Fire rating}) or a standard one ({Pset_WallCommon.FireRating}); | starts a new line.';
/** A build-up tag's own rule, one phrase for the translations. */
const BUILDUP_HELP = 'A build-up tag writes the element\'s layers: its header, its line once per layer, its footer. Open it with Build-up… to set them.';

export function StandardsDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [book, setBook] = useState<Book | null>(null);
  const [tab, setTab] = useState<Tab>(() => TABS.find((t) => t.id === standardsTab()) ?? TABS[0]);
  const [group, setGroup] = useState('');
  const [project, setProject] = useState(() => standardsTab() === 'project');
  const [editing, setEditing] = useState<string | null>(null);
  useEffect(() => { clearStandardsTab(); }, []);
  useEffect(() => {
    let live = true;
    void query<Book>('standards.get').then((b) => { if (live && b) setBook(b); });
    return () => { live = false; };
  }, [nodes]);
  if (!book) return <PendingDialog title="Drafting standards" />;
  const set = (entry: Record<string, unknown>, extra: Record<string, unknown> = {}) => void execute('standards.set', { kind: tab.id, entry, ...extra });
  const fixed = (e: Entry) => (tab.id === 'layer' ? e.id === '0' : e.id === 'standard');

  const cell = (e: Entry, c: Col) => {
    const v = e[c.key];
    // A tag type made a build-up takes the build-up's defaults (its template too, unless it has one of its own) and opens its editor.
    const put = (value: unknown) => {
      if (tab.id === 'tag' && c.key === 'shape' && value === 'buildup' && e.shape !== 'buildup') {
        const { template, ...rest } = BUILDUP_DEFAULTS;
        set({ id: e.id, shape: 'buildup', ...rest, template: !e.template || e.template === '{mark}' ? template : e.template });
        setEditing(e.id);
        return;
      }
      set({ id: e.id, [c.key]: value });
    };
    switch (c.kind) {
      case 'bool': return <input type="checkbox" checked={v === true || (c.key === 'visible' && v !== false)} onChange={(ev) => put(ev.target.checked)} />;
      case 'color': return <input type="color" value={typeof v === 'string' ? v : '#18181b'} onChange={(ev) => put(ev.target.value)} />;
      case 'choice': return <select value={String(v ?? '')} onChange={(ev) => put(ev.target.value)}>{c.key === 'lineType' && tab.id === 'layer' && <option value="">continuous</option>}{c.choices!.map((x) => <option key={x} value={x}>{CHOICE_LABELS[x] ?? x}</option>)}</select>;
      case 'style': return <select value={String(v ?? 'standard')} onChange={(ev) => put(ev.target.value)}>{book.textStyles.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select>;
      case 'group': return <select value={String(v ?? '')} onChange={(ev) => put(ev.target.value || null)}><option value="">—</option>{book.layerGroups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select>;
      // Decimals: empty is the unit's usual (none in mm, one in cm, two in m, 1/16" in imperial), kept as −1.
      case 'decimals': return <input className="num" key={`${e.id}${c.key}${v}`} defaultValue={typeof v === 'number' && v >= 0 ? String(v) : ''} placeholder="auto"
        title="Empty: the unit's usual (none in mm, one in cm, two in m; 1/16&quot; in feet and inches, where 1 is 1/8&quot; and 3 is 1/32&quot;)"
        onBlur={(ev) => { const t = ev.target.value.trim(); const n = t === '' ? -1 : Math.round(Number(t.replace(',', '.'))); if (Number.isFinite(n) && n >= -1 && n !== (typeof v === 'number' ? v : -1)) put(n); }} />;
      default: {
        const numeric = c.kind === 'mm' || c.kind === 'number';
        return <input className={numeric ? 'num' : undefined} key={`${e.id}${c.key}${v}`} defaultValue={v == null ? '' : String(v)}
          onBlur={(ev) => {
            const t = ev.target.value.trim();
            if (t === String(v ?? '')) return;
            if (numeric) { const n = Number(t.replace(',', '.')); if (Number.isFinite(n) && n >= 0) put(n); }
            else if (t || c.key === 'inner' || c.key === 'classes') put(t);
          }} />;
      }
    }
  };

  // A marker size typed in the Project tab: empty clears it (the default), a number within its range sets it.
  const markOption = (key: MarkKey, text: string, was: number | undefined, min: number, max: number) => {
    const t = text.trim();
    if (t === '') { if (was != null) void execute('standards.options', { [key]: null }); return; }
    const n = Number(t.replace(',', '.'));
    if (Number.isFinite(n) && n >= min && n <= max && n !== was) void execute('standards.options', { [key]: n });
  };
  const rows = book[tab.list] as Entry[];
  const options = (
    <dl className="fields standards-options">
      <dt title="Lengths are shown and typed in this unit everywhere; the engine keeps metres">Units</dt>
      <dd><select value={String(book.units ?? 'mm')} onChange={(e) => void execute('standards.options', { units: e.target.value })}>
        {[['mm', 'millimetres'], ['cm', 'centimetres'], ['m', 'metres'], ['ft-in', 'feet and inches (12\'-3 1/2")'], ['in', 'inches']].map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select></dd>
      {/* The step the areas of spaces are shown to, half up (report #76, decision 471): stamps, legends, the room schedule, documents; the model and IFC keep the exact area. */}
      <dt title="Room areas in plans, sheets, schedules and documents, rounded half up (as the Nordic countries write them: x,0 or x,5 m²); the model, quantities and IFC keep the exact area">Area rounding</dt>
      <dd><select data-field="areaRounding" value={String(book.areaRounding ?? '')} onChange={(e) => void execute('standards.options', { areaRounding: e.target.value ? Number(e.target.value) : null })}>
        {[['', 'none (to 0.01 m²)'], ['0.1', 'to 0.1 m²'], ['0.5', 'to 0.5 m²'], ['1', 'to 1 m²']].map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select></dd>
      <dt title="How lines of true weight end and meet, on screen and on paper: round, or square caps with mitred corners">Line ends</dt>
      <dd><select value={String(book.lineCap ?? 'round')} onChange={(e) => void execute('standards.options', { lineCap: e.target.value })}>
        <option value="round">round</option><option value="square">square (mitred)</option>
      </select></dd>
      {/* The section markers' and the elevation markers' symbol size and text height, each kind its own (decisions 455, 460); a marker's own, in its properties, goes before. */}
      {MARK_ROWS.map((row) => {
        const was = book[row.key];
        const standard = Number(book.textStyles.find((t) => t.id === 'standard')?.height ?? 2.5);
        return [
          <dt key={`${row.key}t`} title={row.title}>{row.label}</dt>,
          <dd key={`${row.key}d`}><input className="num" data-field={row.field} key={`${row.key}${was ?? ''}`} defaultValue={was == null ? '' : String(was)} placeholder={row.size ? '10' : String(standard)}
            onBlur={(e) => markOption(row.key, e.target.value, was, row.size ? 1 : 0.5, row.size ? 50 : 20)} /> mm</dd>,
        ];
      })}
    </dl>
  );
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Drafting standards">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Drafting standards</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="tabs-inline">
            {TABS.map((t) => <button key={t.id} type="button" className={`tab-chip${t.id === tab.id ? ' on' : ''}`} onClick={() => { setTab(t); setProject(false); }}>{t.label}</button>)}
            <button type="button" className={`tab-chip${project ? ' on' : ''}`} onClick={() => setProject(true)}>Project</button>
          </div>
          {project ? options : tab.id === 'tag' && rows.some((r) => r.id === editing && r.shape === 'buildup')
            ? <BuildupEditor entry={rows.find((r) => r.id === editing) as unknown as BuildupEntry} onClose={() => setEditing(null)} />
            : tab.id === 'tag' && rows.some((r) => r.id === editing)
            ? <LeaderEditor entry={rows.find((r) => r.id === editing) as unknown as LeaderEntry} onClose={() => setEditing(null)} />
            : <>
          <table className="standards-table">
            <thead><tr>{tab.id === 'axis' && <th title="New axes and the BIM grids use it">Default</th>}{tab.cols.map((c) => <th key={c.key} title={c.title}>{c.label}</th>)}{tab.id === 'tag' && <th />}<th /></tr></thead>
            <tbody>
              {rows.map((e) => (
                <tr key={e.id}>
                  {tab.id === 'axis' && <td><input type="radio" name="default-axis" checked={book.defaultAxisStyle === e.id} onChange={() => set({ id: e.id }, { default: true })} /></td>}
                  {tab.cols.map((c) => <td key={c.key}>{cell(e, c)}</td>)}
                  {tab.id === 'tag' && <td>{e.shape === 'buildup'
                    ? <button type="button" className={`btn mini${editing === e.id ? ' on' : ''}`} data-field="tag-shape-buildup" title="Its header, line per layer, footer, order, leaders and width, with a preview" onClick={() => setEditing(editing === e.id ? null : e.id)}>Build-up…</button>
                    : <button type="button" className="btn mini" data-field="tag-leader" title="Its leader: straight from a box, or an elbow under the text, with a preview" onClick={() => setEditing(e.id)}>Leader…</button>}</td>}
                  <td className="row-tools">{!fixed(e) && <button type="button" className="icon mini" title="Remove" onClick={() => void execute('standards.remove', { kind: tab.id, id: e.id })}><Trash2 size={12} /></button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {tab.id === 'tag' && <p className="muted small" data-tag-help>{TAG_HELP}</p>}
          {tab.id === 'tag' && <p className="muted small">{BUILDUP_HELP}</p>}
          <div className="row">
            <button type="button" className="btn" onClick={() => set({ ...tab.fresh, name: `${tab.fresh.name} ${rows.length + 1}` })}><Plus size={14} />New</button>
            {tab.id === 'layer' && (
              <>
                <span className="grow" />
                <span className="muted small">Groups:</span>
                {book.layerGroups.map((g) => (
                  <span key={g.id} className="chip">{g.name}
                    <button type="button" className="icon mini" title="Remove the group (its layers stay)" onClick={() => void execute('standards.remove', { kind: 'layerGroup', id: g.id })}><X size={10} /></button>
                  </span>
                ))}
                <input value={group} placeholder="New group" onChange={(e) => setGroup(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter' && group.trim()) { void execute('standards.set', { kind: 'layerGroup', entry: { name: group.trim() } }); setGroup(''); } }} />
              </>
            )}
          </div>
          </>}
          {error && <p className="error-text" role="alert">{error}</p>}
        </div>
      </div>
    </div>
  );
}
