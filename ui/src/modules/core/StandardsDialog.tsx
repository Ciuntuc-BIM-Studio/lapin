/**
 * Drafting standards: the project's text styles, dimension styles, axis types
 * (and the default one), tag types (a box, or a door's or window's marker
 * across its wall in plan), layers and layer groups. Sizes are paper millimetres.
 * Each change is one engine command; Standard (and layer 0) always stay.
 */

import { useEffect, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';

type Entry = Record<string, string | number | boolean | null | undefined> & { id: string; name: string };
type Book = { textStyles: Entry[]; dimStyles: Entry[]; axisStyles: Entry[]; tagTypes: Entry[]; defaultAxisStyle: string; layers: Entry[]; layerGroups: Entry[]; units?: string; lineCap?: string };
type Col = { key: string; label: string; kind: 'text' | 'mm' | 'number' | 'bool' | 'color' | 'choice' | 'style' | 'group'; choices?: string[] };
type Tab = { id: 'text' | 'dim' | 'axis' | 'tag' | 'layer'; label: string; list: keyof Book; cols: Col[]; fresh: Record<string, unknown> };

const LINE_TYPES = ['continuous', 'dashed', 'dotted', 'dashdot'];
const TABS: Tab[] = [
  { id: 'text', label: 'Text', list: 'textStyles', fresh: { name: 'Text', height: 2.5 }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'height', label: 'Height mm', kind: 'mm' }, { key: 'font', label: 'Font', kind: 'choice', choices: ['sans', 'serif', 'mono'] },
    { key: 'bold', label: 'B', kind: 'bool' }, { key: 'italic', label: 'I', kind: 'bool' }, { key: 'color', label: 'Colour', kind: 'color' }] },
  { id: 'dim', label: 'Dimensions', list: 'dimStyles', fresh: { name: 'Dimension' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'arrow', label: 'Arrow', kind: 'choice', choices: ['tick', 'arrow', 'dot', 'none'] },
    { key: 'arrowSize', label: 'Arrow mm', kind: 'mm' }, { key: 'extGap', label: 'Gap mm', kind: 'mm' }, { key: 'extOver', label: 'Over mm', kind: 'mm' },
    { key: 'placement', label: 'Value', kind: 'choice', choices: ['above', 'centered', 'below'] }, { key: 'unit', label: 'Unit', kind: 'choice', choices: ['m', 'cm', 'mm', 'ft-in', 'in'] },
    { key: 'precision', label: 'Decimals', kind: 'number' }] },
  { id: 'axis', label: 'Grid axes', list: 'axisStyles', fresh: { name: 'Axis' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'bubble', label: 'Bubble', kind: 'choice', choices: ['circle', 'square'] },
    { key: 'ends', label: 'Ends', kind: 'choice', choices: ['both', 'start', 'end', 'none'] }, { key: 'size', label: 'Size mm', kind: 'mm' },
    { key: 'lineType', label: 'Line', kind: 'choice', choices: LINE_TYPES }, { key: 'firstLabel', label: 'First', kind: 'text' }, { key: 'color', label: 'Colour', kind: 'color' }] },
  // A tag's template reads the element: {mark}, {name}, {type}, {class}, a door's or window's {width}, {height}, {sill} (in the unit given); "|" starts a new line.
  { id: 'tag', label: 'Tags', list: 'tagTypes', fresh: { name: 'Tag', template: '{mark}' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'shape', label: 'Shape', kind: 'choice', choices: ['box', 'opening'] },
    { key: 'template', label: 'Text (outside)', kind: 'text' }, { key: 'inner', label: 'Inside', kind: 'text' },
    { key: 'unit', label: 'Unit', kind: 'choice', choices: ['cm', 'mm', 'm'] }, { key: 'reach', label: 'Past faces mm', kind: 'mm' },
    { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'classes', label: 'Classes', kind: 'text' }] },
  { id: 'layer', label: 'Layers', list: 'layers', fresh: { name: 'Layer' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'color', label: 'Colour', kind: 'color' }, { key: 'lineWeight', label: 'Pen mm', kind: 'mm' },
    { key: 'lineType', label: 'Line', kind: 'choice', choices: LINE_TYPES }, { key: 'group', label: 'Group', kind: 'group' }, { key: 'visible', label: 'On', kind: 'bool' }, { key: 'locked', label: 'Lock', kind: 'bool' }] },
];

export function StandardsDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [book, setBook] = useState<Book | null>(null);
  const [tab, setTab] = useState<Tab>(TABS[0]);
  const [group, setGroup] = useState('');
  const [project, setProject] = useState(false);
  useEffect(() => { void query<Book>('standards.get').then((b) => b && setBook(b)); }, [nodes]);
  if (!book) return null;
  const set = (entry: Record<string, unknown>, extra: Record<string, unknown> = {}) => void execute('standards.set', { kind: tab.id, entry, ...extra });
  const fixed = (e: Entry) => (tab.id === 'layer' ? e.id === '0' : e.id === 'standard');

  const cell = (e: Entry, c: Col) => {
    const v = e[c.key];
    const put = (value: unknown) => set({ id: e.id, [c.key]: value });
    switch (c.kind) {
      case 'bool': return <input type="checkbox" checked={v === true || (c.key === 'visible' && v !== false)} onChange={(ev) => put(ev.target.checked)} />;
      case 'color': return <input type="color" value={typeof v === 'string' ? v : '#18181b'} onChange={(ev) => put(ev.target.value)} />;
      case 'choice': return <select value={String(v ?? '')} onChange={(ev) => put(ev.target.value)}>{c.key === 'lineType' && tab.id === 'layer' && <option value="">continuous</option>}{c.choices!.map((x) => <option key={x}>{x}</option>)}</select>;
      case 'style': return <select value={String(v ?? 'standard')} onChange={(ev) => put(ev.target.value)}>{book.textStyles.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select>;
      case 'group': return <select value={String(v ?? '')} onChange={(ev) => put(ev.target.value || null)}><option value="">—</option>{book.layerGroups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select>;
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

  const rows = book[tab.list] as Entry[];
  const options = (
    <dl className="fields standards-options">
      <dt title="Lengths are shown and typed in this unit everywhere; the engine keeps metres">Units</dt>
      <dd><select value={String(book.units ?? 'mm')} onChange={(e) => void execute('standards.options', { units: e.target.value })}>
        {[['mm', 'millimetres'], ['cm', 'centimetres'], ['m', 'metres'], ['ft-in', 'feet and inches (12\'-3 1/2")'], ['in', 'inches']].map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select></dd>
      <dt title="How lines of true weight end and meet, on screen and on paper: round, or square caps with mitred corners">Line ends</dt>
      <dd><select value={String(book.lineCap ?? 'round')} onChange={(e) => void execute('standards.options', { lineCap: e.target.value })}>
        <option value="round">round</option><option value="square">square (mitred)</option>
      </select></dd>
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
          {project ? options : <>
          <table className="standards-table">
            <thead><tr>{tab.id === 'axis' && <th title="New axes and the BIM grids use it">Default</th>}{tab.cols.map((c) => <th key={c.key}>{c.label}</th>)}<th /></tr></thead>
            <tbody>
              {rows.map((e) => (
                <tr key={e.id}>
                  {tab.id === 'axis' && <td><input type="radio" name="default-axis" checked={book.defaultAxisStyle === e.id} onChange={() => set({ id: e.id }, { default: true })} /></td>}
                  {tab.cols.map((c) => <td key={c.key}>{cell(e, c)}</td>)}
                  <td className="row-tools">{!fixed(e) && <button type="button" className="icon mini" title="Remove" onClick={() => void execute('standards.remove', { kind: tab.id, id: e.id })}><Trash2 size={12} /></button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
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
