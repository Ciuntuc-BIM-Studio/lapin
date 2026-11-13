/**
 * Drafting standards → Line types (docs/plan-user-extensibility.md §3.6): Lapin's line types and the project's own, each drawn;
 * a project's type edited as dashes and gaps with a live preview, renamed (every layer, axis style and object style that names it
 * follows), removed (what named it draws continuous); Import .lin… (AutoCAD's file: in millimetres or inches, the types shown
 * and chosen, a name the project has with other dashes asking update / keep both / skip) and Export .lin….
 */

import { useEffect, useState } from 'react';
import { Copy, FileDown, FileUp, Plus, Trash2, X } from 'lucide-react';
import { notify } from '../../../app/store';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { t as tr } from '../../../i18n';
import { TYPE_CHOICES } from '../LibraryChoices';
import { LineSample } from './LineSample';
import { FirmFolderFiles } from '../FirmFolderFiles';
import { exportLin, importLin, readLin, removeLineType, setLineType, useLineTypes } from './lineTypes';
import {
  addPair, dashesText, defaultLineChoices, fromPairs, lineConflicts, parseDashes, removePair, setPairValue, toPairs, validDashes,
  type OwnLineType, type ReadLineType,
} from './lineTypeModel';

interface Reading { text: string; units: 'mm' | 'in'; types: ReadLineType[]; skipped: string[]; chosen: Record<string, boolean>; choices: Record<string, string> }

/** The name a new type starts with: free among every name in use. */
function freeName(base: string, taken: string[]): string {
  const used = new Set(taken.map((n) => n.toLowerCase()));
  if (!used.has(base.toLowerCase())) return base;
  for (let n = 2; ; n++) if (!used.has(`${base} ${n}`.toLowerCase())) return `${base} ${n}`;
}

export function LineTypesTab() {
  const lt = useLineTypes();
  const [selected, setSelected] = useState<string | null>(null);
  const [reading, setReading] = useState<Reading | null>(null);
  const own = lt?.own ?? [];
  const builtin = lt?.builtin ?? [];
  const taken = [...builtin.map((b) => b.name), 'continuous', 'solid', ...own.map((o) => o.name)];
  const current: { name: string; dashes: number[]; own: OwnLineType | null } | null = (() => {
    const o = own.find((x) => x.name === selected);
    if (o) return { name: o.name, dashes: o.dashes, own: o };
    const b = builtin.find((x) => x.name === selected);
    return b ? { name: b.name, dashes: b.dashes, own: null } : null;
  })();

  const make = async (name: string, dashes: number[], description?: string) => {
    const r = await setLineType({ name, dashes, ...(description ? { description } : {}) });
    if (r) setSelected(r.name);
  };

  const importFile = async (units: 'mm' | 'in', again?: { text: string }, given?: { bytes: Uint8Array }) => {
    let text = again?.text;
    if (text == null) {
      const f = given ?? await pickFile('AutoCAD line types (.lin)', 'lin');
      if (!f) return;
      text = new TextDecoder().decode(f.bytes);
    }
    const r = await readLin(text, units);
    if (!r) return;
    if (r.lineTypes.length === 0) { notify('The file has no line types Lapin can read (a .lin file: *NAME, then A, dash, -gap, …).'); return; }
    setReading({ text, units, types: r.lineTypes, skipped: r.skipped, chosen: Object.fromEntries(r.lineTypes.map((l) => [l.name, true])), choices: defaultLineChoices(r.lineTypes) });
  };

  const applyImport = async () => {
    if (!reading) return;
    const names = reading.types.filter((l) => reading.chosen[l.name]).map((l) => l.name);
    if (names.length === 0) { setReading(null); return; }
    const r = await importLin({ text: reading.text, units: reading.units, names, choices: reading.choices });
    setReading(null);
    if (r) notify(`${r.added} line types added, ${r.updated} updated, ${r.skipped} skipped.`);
  };

  const exportAll = async () => {
    const r = await exportLin();
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, 'lapin.lin'), 'text/plain');
    if (saved) notify(`Exported ${saved}.`);
  };

  if (!lt) return <p className="muted">Reading…</p>;

  if (reading) {
    const conflicts = lineConflicts(reading.types);
    return (
      <div className="line-types-import" data-testid="lin-import">
        <h4 className="eyebrow">Line types in the file</h4>
        <div className="row">
          <label className="small muted">Dashes in the file are in
            <select value={reading.units} data-field="lin-units" onChange={(e) => void importFile(e.target.value as 'mm' | 'in', reading)}>
              <option value="mm">millimetres</option><option value="in">inches</option>
            </select>
          </label>
        </div>
        <table className="layer-table">
          <thead><tr><th /><th>Name</th><th>Pattern</th><th>In the project</th><th>
            {conflicts.length > 0 && (
              <select value="" title="The same choice for every line type that differs" data-choice-all="lineTypes"
                onChange={(e) => { const v = e.target.value; if (v) setReading({ ...reading, choices: Object.fromEntries(conflicts.map((c) => [c.name, v])) }); }}>
                <option value="">Apply to all…</option>{TYPE_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            )}
          </th></tr></thead>
          <tbody>
            {reading.types.map((l) => (
              <tr key={l.name}>
                <td><input type="checkbox" checked={reading.chosen[l.name] ?? false} data-lin-choose={l.name} onChange={(e) => setReading({ ...reading, chosen: { ...reading.chosen, [l.name]: e.target.checked } })} /></td>
                <td><span data-no-i18n>{l.name}</span>{l.description && <span className="muted small" data-no-i18n> · {l.description}</span>}
                  {l.complex && <span className="muted small" title="The text or shapes in the line are left out; its dashes are kept"> · {tr('text or shapes left out')}</span>}</td>
                <td><LineSample dashes={l.dashes} /></td>
                <td className="small">
                  {l.builtin ? <span className="muted">comes in beside Lapin's, named (file)</span> : !l.exists ? <span className="muted">new</span> : l.same ? <span className="muted">the same</span>
                    : <select value={reading.choices[l.name] ?? 'update'} data-choice-lintype={l.name} onChange={(e) => setReading({ ...reading, choices: { ...reading.choices, [l.name]: e.target.value } })}>
                      {TYPE_CHOICES.map(([v, label]) => <option key={v} value={v}>{label}</option>)}
                    </select>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {reading.skipped.length > 0 && <p className="muted small" data-no-i18n>{reading.skipped.join(' · ')}</p>}
        <div className="row">
          <button type="button" className="btn primary" data-field="lin-import-apply" onClick={() => void applyImport()}>Import</button>
          <button type="button" className="btn" onClick={() => setReading(null)}>Cancel</button>
        </div>
      </div>
    );
  }

  return (
    <div className="line-types" data-testid="line-types">
      <div className="row">
        <button type="button" className="btn" data-field="lt-new" onClick={() => void make(freeName(tr('Line type'), taken), [6, 2, 1, 2])}><Plus size={13} />New</button>
        <button type="button" className="btn" data-field="lt-import" title="An AutoCAD line type file: its dashes and gaps are kept, text and shapes in a line are left out" onClick={() => void importFile('mm')}><FileUp size={13} />Import .lin…</button>
        <button type="button" className="btn" data-field="lt-export" disabled={own.length === 0} onClick={() => void exportAll()}><FileDown size={13} />Export .lin…</button>
      </div>
      <div className="line-types-body" style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
        <div style={{ minWidth: 260 }}>
          <h4 className="eyebrow">This project's</h4>
          {own.length === 0 && <p className="muted small">None of your own yet: make one, or import a .lin file.</p>}
          <ul className="doc-templates lt-list">
            {own.map((o) => (
              <li key={o.id} className={o.name === selected ? 'active' : undefined}>
                <button type="button" data-lt={o.name} onClick={() => setSelected(o.name)}><strong data-no-i18n>{o.name}</strong><LineSample dashes={o.dashes} width={120} /></button>
              </li>
            ))}
          </ul>
          <h4 className="eyebrow">Lapin's</h4>
          <ul className="doc-templates lt-list">
            <li><span className="small"><span>continuous</span></span></li>
            {builtin.map((b) => (
              <li key={b.name} className={b.name === selected ? 'active' : undefined}>
                <button type="button" data-lt={b.name} onClick={() => setSelected(b.name)}><strong data-no-i18n>{b.name}</strong><LineSample dashes={b.dashes} width={120} /></button>
              </li>
            ))}
          </ul>
          <FirmFolderFiles ext="lin" onPick={(f) => importFile('mm', undefined, f)}
            save={{ disabled: own.length === 0, get: async () => { const r = await exportLin(); return r ? { text: r.text, name: r.file.replace(/\.[^.]+$/, '') } : null; } }} />
        </div>
        <div className="grow">
          {!current ? <p className="muted">Choose a line type, or make one. Layers, grid axes, object styles and linework take it by its name.</p>
            : <LineTypeEditor key={current.name} cur={current} taken={taken} onSelect={setSelected} onCopy={(name, dashes) => void make(name, dashes)} />}
        </div>
      </div>
    </div>
  );
}

function LineTypeEditor({ cur, taken, onSelect, onCopy }: {
  cur: { name: string; dashes: number[]; own: OwnLineType | null }; taken: string[]; onSelect: (name: string | null) => void; onCopy: (name: string, dashes: number[]) => void;
}) {
  const o = cur.own;
  const [pairs, setPairs] = useState(() => toPairs(cur.dashes));
  const [text, setText] = useState('');
  useEffect(() => { setPairs(toPairs(cur.dashes)); }, [cur.dashes.join(',')]);
  const dashes = fromPairs(pairs);
  const valid = validDashes(dashes);
  const commit = (next: [number, number][]) => { setPairs(next); if (o && validDashes(fromPairs(next))) void setLineType({ name: o.name, dashes: fromPairs(next) }); };
  return (
    <div data-testid="line-type-editor">
      {o ? (
        <label className="field-line"><span className="eyebrow">Name</span>
          <input data-field="lt-name" defaultValue={o.name} key={o.name}
            onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== o.name) void setLineType({ name: o.name, newName: v }).then((r) => { if (r) onSelect(r.name); else e.target.value = o.name; }); }} />
        </label>
      ) : <h3 className="tpl-title" data-no-i18n>{cur.name}</h3>}
      {o && (
        <label className="field-line"><span className="eyebrow">Description</span>
          <input data-field="lt-description" defaultValue={o.description ?? ''} key={`${o.name}d${o.description ?? ''}`}
            onBlur={(e) => { const v = e.target.value.trim(); if (v !== (o.description ?? '')) void setLineType({ name: o.name, description: v }); }} />
        </label>
      )}
      <div className="lt-preview"><LineSample dashes={valid ? dashes : []} width={300} scale={6} stroke={2} /></div>
      <table className="layer-table lt-pairs">
        <thead><tr><th>Dash mm</th><th>Gap mm</th><th /></tr></thead>
        <tbody>
          {pairs.map(([d, g], i) => (
            <tr key={i}>
              {([d, g] as number[]).map((v, part) => (
                <td key={part}>
                  <input className="num" disabled={!o} data-lt-value={`${i}-${part}`} key={`${i}${part}${v}`} defaultValue={String(v)}
                    onBlur={(e) => { const n = Number(e.target.value.replace(',', '.')); if (Number.isFinite(n) && n !== v) commit(setPairValue(pairs, i, part as 0 | 1, n)); else e.target.value = String(v); }} />
                </td>
              ))}
              <td>{o && pairs.length > 1 && <button type="button" className="icon mini" title="Remove this dash and gap" onClick={() => commit(removePair(pairs, i))}><X size={11} /></button>}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {!valid && <p className="error-text small">Each dash and gap is above 0 and under 100 mm.</p>}
      {o && (
        <div className="row">
          <button type="button" className="btn small" data-field="lt-add-pair" onClick={() => commit(addPair(pairs))}><Plus size={12} />Dash and gap</button>
          <input placeholder="Or type them: 6 2 1 2" value={text} aria-label="Dashes and gaps" data-field="lt-dashes-text" onChange={(e) => setText(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { const d = parseDashes(text); if (d) { commit(toPairs(d)); setText(''); } else notify('Dashes and gaps are numbers in mm, above 0 and under 100.'); } }} />
        </div>
      )}
      <p className="muted small">Paper millimetres, a dash then its gap, repeated along the line: <span data-no-i18n>{dashesText(dashes)}</span></p>
      <div className="row">
        <button type="button" className="btn" data-field="lt-duplicate" title="A copy of this type in the project, to change" onClick={() => onCopy(freeName(`${cur.name} copy`, taken), cur.dashes)}><Copy size={13} />Duplicate</button>
        {o && <button type="button" className="btn danger" data-field="lt-remove" title="What names it draws continuous" onClick={() => void removeLineType(o.name).then(() => onSelect(null))}><Trash2 size={13} />Remove</button>}
      </div>
    </div>
  );
}
