/**
 * Structure types from a CSV or XLSX (report #135), and back: one row a beam, column or precast lintel type, its columns
 * guessed by the engine (English, Romanian, German, French, Czech; a producer's „height.value” and „height.unit” too) and
 * shown to be corrected; the profile found in the project, the profile libraries and Lapin's catalogue, else a rectangle
 * of h × b. Rows of one section whose names differ by their length can be one type with its stock lengths. A preview of
 * what is made, what the project has already and what differs, with the choice for each (LibraryChoices, decision 506);
 * one step to undo (structure.previewTypes, structure.importTypes, structure.typesCsv).
 */

import { useState } from 'react';
import { FileDown, FileUp } from 'lucide-react';
import { notify } from '../../app/store';
import { pickFile, saveFile, toBase64 } from '../../engine/files';
import { query } from '../../engine/session';
import { mm } from '../../engine/model';
import { allLibraries } from '../core/profileLibraries/store';
import { ask, defaultChoices, ImportSummary, LibraryChoices, run, type ImportSummaryData, type LibraryChoiceState } from '../core/LibraryChoices';
import { counts, FIELD_LABELS, librariesArg, setColumn, type ColumnMap, type Preview } from './typeTables';

type Kind = 'beam' | 'column' | 'lintel';
interface Pending { data: string; libraries: unknown[]; kind: Kind; merge: boolean; map: ColumnMap | null; preview: Preview; choices: LibraryChoiceState }

const STATUS: Record<string, string> = { new: 'New', same: 'Already in the project', differs: 'Differs', error: 'Not read' };

export function TypeTableFiles({ kind }: { kind: 'column' | 'beam' | 'strip' | 'pad' }) {
  const [pending, setPending] = useState<Pending | null>(null);
  const [summary, setSummary] = useState<ImportSummaryData | null>(null);
  const [busy, setBusy] = useState(false);

  const out = async () => {
    const text = await query<string>('structure.typesCsv', { kind: kind === 'column' ? 'column' : kind === 'beam' ? 'beam' : 'all' });
    if (text == null) { notify('The types could not be exported.'); return; }
    // A byte order mark first, so a spreadsheet reads the CSV as UTF-8.
    const saved = await saveFile(new TextEncoder().encode(`﻿${text}`), kind === 'column' ? 'Column types.csv' : 'Beam types.csv', 'text/csv');
    if (saved) notify('The types were exported: one row a type, sizes in millimetres.');
  };

  const preview = async (p: Omit<Pending, 'preview' | 'choices'>, keepChoices?: LibraryChoiceState) => {
    setBusy(true);
    const r = await ask<Preview>('structure.previewTypes', { data: p.data, kind: p.kind, merge: p.merge, libraries: p.libraries, ...(p.map ? { map: p.map } : {}) });
    setBusy(false);
    if (!r) return;
    const choices = keepChoices ?? defaultChoices([], r.conflicts, 'update');
    setPending({ ...p, map: p.map ?? r.map, preview: r, choices });
  };

  const bring = async () => {
    setSummary(null);
    const file = await pickFile('Structure types (CSV or XLSX)', ['csv', 'txt', 'xlsx']);
    if (!file) return;
    const libraries = librariesArg((await allLibraries()).entries);
    const base = { data: toBase64(file.bytes), libraries, kind: (kind === 'column' ? 'column' : 'beam') as Kind, merge: false, map: null };
    const first = await ask<Preview>('structure.previewTypes', { data: base.data, kind: base.kind, merge: false, libraries });
    if (!first) return;
    // Rows that differ only by their length: one type of all of them, offered first.
    await preview({ ...base, merge: first.mergeable });
  };

  const finish = async () => {
    if (!pending) return;
    const r = await run<ImportSummaryData & { renamed: string[] }>('structure.importTypes',
      { data: pending.data, kind: pending.kind, merge: pending.merge, libraries: pending.libraries, map: pending.map, choices: pending.choices.types });
    if (!r) return;
    setPending(null);
    setSummary(r);
    notify('The types were imported, in one step to undo.');
  };

  const n = pending ? counts(pending.preview.rows) : null;
  return (
    <div className="small" data-field="structure-type-files" style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 8 }}>
      <button type="button" className="btn small" data-action="structure-types-import" title="Beam, column and lintel types from a CSV or XLSX: one row a type (name, kind, profile or h and b, material, mark, bearing, manufacturer, code, lengths)" onClick={() => void bring()}><FileUp size={13} /> Import…</button>
      <button type="button" className="btn small" data-action="structure-types-export" title="The column and beam types as a CSV the import reads back" onClick={() => void out()}><FileDown size={13} /> Export CSV</button>
      {summary && <ImportSummary r={summary} />}
      {pending && (
        <div className="dialog-backdrop" onClick={() => setPending(null)}>
          <div className="dialog wide" role="dialog" aria-label="Import structure types" data-dialog="structure-types-import" onClick={(e) => e.stopPropagation()}>
            <header className="dialog-head"><span className="panel-title">Import structure types</span></header>
            <div className="dialog-body">
              <div className="row" style={{ gap: 12, flexWrap: 'wrap' }}>
                <label className="row"><span className="muted small">Rows that say no kind are</span>
                  <select value={pending.kind} data-field="types-kind" onChange={(e) => void preview({ ...pending, kind: e.target.value as Kind }, pending.choices)}>
                    <option value="beam">Beams</option><option value="column">Columns</option><option value="lintel">Lintels (precast)</option>
                  </select></label>
                <label className="row" title="Rows of one section, material and maker whose names differ by their length become one type with all their lengths (KP 7 - 100, KP 7 - 125 → KP 7)">
                  <input type="checkbox" checked={pending.merge} data-field="types-merge" onChange={(e) => void preview({ ...pending, merge: e.target.checked }, pending.choices)} />
                  <span>One type for the lengths of one section</span></label>
              </div>
              <p className="eyebrow">Columns</p>
              <div className="fields" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: 4 }}>
                {pending.preview.fields.map((f) => {
                  const c = pending.map?.[f.key] ?? null;
                  return (
                    <label key={f.key} className="row" data-column-field={f.key}>
                      <span className="muted small grow">{FIELD_LABELS[f.key] ?? f.key}</span>
                      <select value={c?.index ?? -1} onChange={(e) => void preview({ ...pending, map: setColumn(pending.map ?? {}, f.key, Number(e.target.value)) }, pending.choices)}>
                        <option value={-1}>none</option>
                        {pending.preview.headings.map((h, i) => <option key={i} value={i} data-no-i18n>{h || `#${i + 1}`}</option>)}
                      </select>
                      {f.dimension === 'length' && c && <select value={c.unit ?? 'mm'} onChange={(e) => void preview({ ...pending, map: setColumn(pending.map ?? {}, f.key, c.index, e.target.value) }, pending.choices)}>
                        {pending.preview.units.map((u) => <option key={u} value={u} data-no-i18n>{u}</option>)}
                      </select>}
                    </label>
                  );
                })}
              </div>
              <p className="eyebrow">Types</p>
              <table className="layer-table" data-testid="structure-types-preview">
                <thead><tr><th>Name</th><th>Kind</th><th>Section</th><th>Material</th><th>Mark</th><th>Lengths</th><th>Import</th></tr></thead>
                <tbody>
                  {pending.preview.rows.map((r) => (
                    <tr key={`${r.line}:${r.name}`} data-row-status={r.status}>
                      <td data-no-i18n>{r.name}</td>
                      <td>{r.kind === 'lintel' ? 'Lintel' : r.kind === 'column' ? 'Column' : 'Beam'}</td>
                      <td data-no-i18n>{r.section ?? ''}</td>
                      <td data-no-i18n>{r.material ?? ''}</td>
                      <td data-no-i18n>{r.mark ?? ''}</td>
                      <td data-no-i18n>{r.lengths.map((l) => mm(l)).join(', ')}</td>
                      <td>{r.status === 'error' ? <span className="error-text" title={r.problem}>{STATUS.error}</span> : <span>{STATUS[r.status]}</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {n && <p className="muted small" data-no-i18n>{`${n.new} + ${n.same} + ${n.differs}${n.error ? ` (−${n.error})` : ''}`}</p>}
              <LibraryChoices materials={[]} types={pending.preview.conflicts} value={pending.choices} onChange={(choices) => setPending({ ...pending, choices })} />
            </div>
            <footer className="dialog-foot">
              <button type="button" className="btn" onClick={() => setPending(null)}>Cancel</button>
              <span className="grow" />
              <button type="button" className="btn primary" disabled={busy || (n?.new ?? 0) + (n?.differs ?? 0) === 0} onClick={() => void finish()} data-action="structure-types-import-go">Import</button>
            </footer>
          </div>
        </div>
      )}
    </div>
  );
}
