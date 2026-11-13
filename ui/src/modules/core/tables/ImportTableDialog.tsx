/**
 * Import table… (docs/plan-user-extensibility.md §7.1): a CSV or XLSX file, or cells pasted from a spreadsheet, read into one of the
 * tables Lapin takes (loads, load cases, property values…). The first rows are shown with the heading row selectable, each column
 * given its meaning (guessed from its heading, or the mapping kept on this computer for the same headings), the values the file lacks
 * (a storey), and what the import would do row by row before it does it, in one step to undo. A mapping goes to a .lapinmapping
 * file and comes back from one.
 */

import { useEffect, useMemo, useState } from 'react';
import { Download, FileDown, FileUp, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { pickFile, saveFile, toBase64 } from '../../../engine/files';
import { storeys } from '../../../engine/model';
import { activeStorey, useSession } from '../../../engine/session';
import {
  applyEntries, missingRequired, parseMappingFile, recallMapping, rememberMapping, setColumnKey, setColumnUnit, tableMappingText,
  type MapItem, type TableTarget,
} from './tableModel';
import { importTable, previewTable, readTable, tableDialog, tableTargets, tableTemplate, type TablePreview, type TableRead, type TableSource } from './tables';

const LEAVE = '';
const OPEN = 'open';

export function ImportTableDialog() {
  const preset = tableDialog.getState();
  const state = useSession((s) => s);
  const [targets, setTargets] = useState<TableTarget[]>([]);
  const [targetId, setTargetId] = useState<string | null>(preset.target);
  const [source, setSource] = useState<{ name: string; src: TableSource } | null>(null);
  const [pasted, setPasted] = useState('');
  const [table, setTable] = useState<TableRead | null>(null);
  const [headerRow, setHeaderRow] = useState(0);
  const [mapping, setMapping] = useState<MapItem[]>([]);
  const [defaults, setDefaults] = useState<Record<string, string>>(preset.defaults ?? {});
  const [skip, setSkip] = useState(false);
  const [preview, setPreview] = useState<TablePreview | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const target = targets.find((t) => t.id === targetId) ?? null;
  const close = () => useUi.setState({ dialog: null });
  const storeyList = useMemo(() => storeys(state), [state]);

  useEffect(() => { void tableTargets().then(setTargets); }, []);
  // A single target in this edition, or the one the caller named: nothing to choose.
  useEffect(() => { if (!targetId && targets.length === 1) setTargetId(targets[0].id); }, [targets, targetId]);

  const headers = table ? (table.rows[headerRow] ?? []) : [];

  /** The mapping for a table: the one kept for its headings, else the engine's guess. */
  const startMapping = (t: TableRead, guess: MapItem[] | undefined, tid: string, hr: number): MapItem[] =>
    recallMapping(tid, t.rows[hr] ?? []) ?? guess ?? [];

  const load = async (src: TableSource, name: string, tid: string | null, hr?: number) => {
    setProblem(null); setPreview(null);
    const r = await readTable(src, tid, hr);
    if (!r.value) { setProblem(r.error ?? 'The table could not be read.'); setTable(null); return; }
    setSource({ name, src });
    setTable(r.value);
    const row = hr ?? r.value.headerRow;
    setHeaderRow(row);
    if (tid) setMapping(startMapping(r.value, r.value.mapping, tid, row));
  };

  const chooseFile = async () => {
    const f = await pickFile('Spreadsheet or CSV', ['csv', 'tsv', 'txt', 'xlsx']);
    if (!f) return;
    const xlsx = /\.xlsx$/i.test(f.name);
    await load(xlsx ? { data: toBase64(f.bytes) } : { text: new TextDecoder().decode(f.bytes) }, f.name, targetId);
  };
  const usePasted = () => { if (pasted.trim()) void load({ text: pasted }, 'pasted cells', targetId); };

  // A target chosen after the table: its columns guessed again.
  useEffect(() => {
    if (!source || !targetId) return;
    let live = true;
    void readTable(source.src, targetId, headerRow).then((r) => { if (live && r.value) setMapping(startMapping(r.value, r.value.mapping, targetId, headerRow)); });
    return () => { live = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [targetId]);

  const changeHeader = (row: number) => { if (source) void load(source.src, source.name, targetId, row); };

  // The defaults the target may need: a storey when the file has no storey column, and any required column the table lacks.
  const hasKey = (k: string) => mapping.some((m) => m.key === k);
  const defaultKeys = target ? target.columns.filter((c) => !hasKey(c.key) && (c.key === 'storey' || c.required || c.key in (preset.defaults ?? {})) && (c.key === 'storey' || c.kind === 'choice' || c.kind === 'text')).map((c) => c.key) : [];
  const effDefaults = useMemo(() => {
    const d: Record<string, string> = {};
    for (const k of defaultKeys) {
      const v = defaults[k] ?? (k === 'storey' ? (activeStorey(state)?.id ?? storeyList[0]?.id) : undefined);
      if (v) d[k] = v;
    }
    return d;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [defaultKeys.join(','), defaults, storeyList]);

  const request = useMemo(() => (source && target && table
    ? { ...source.src, target: target.id, headerRow, mapping: mapping.flatMap((m) => (m.key ? [{ column: m.column, key: m.key, unit: m.unit ?? null }] : [])), defaults: effDefaults }
    : null), [source, target, table, headerRow, mapping, effDefaults]);

  // The live preview.
  const lacking = target ? missingRequired(target, mapping, (k) => k in effDefaults) : [];
  useEffect(() => {
    if (!request || lacking.length) { setPreview(null); return; }
    let live = true;
    const t = setTimeout(() => {
      void previewTable(request).then((r) => { if (!live) return; setPreview(r.value); setProblem(r.error); });
    }, 150);
    return () => { live = false; clearTimeout(t); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [request, lacking.length]);

  const run = async () => {
    if (!request || !target || busy) return;
    setBusy(true);
    const r = await importTable({ ...request, skipErrors: skip });
    setBusy(false);
    if ('error' in r) { setProblem(r.error); notify(r.error); return; }
    rememberMapping(target.id, headers, mapping);
    notify(r.skipped ? `${r.rows} rows imported, ${r.skipped} with errors left out.` : `${r.rows} rows imported.`);
    close();
  };

  const template = async () => {
    if (!target) return;
    const t = await tableTemplate(target.id);
    if (t && await saveFile(new TextEncoder().encode(t.text), t.file, 'text/csv')) notify('The template was saved: fill it in and import it.');
  };
  const exportMapping = async () => {
    if (!target) return;
    const name = `${target.id.replace('.', '-')}.lapinmapping`;
    if (await saveFile(new TextEncoder().encode(tableMappingText(target.id, headers, mapping)), name, 'application/json')) notify('The mapping was saved as a .lapinmapping file.');
  };
  const importMapping = async () => {
    const f = await pickFile('Mapping (.lapinmapping)', ['lapinmapping', 'json']);
    if (!f) return;
    try {
      const m = parseMappingFile(new TextDecoder().decode(f.bytes));
      if (m.kind !== 'table') throw new Error('This mapping is for drafting layers, not for a table.');
      if (target && m.target !== target.id) throw new Error(`This mapping is for ${m.target}, not for ${target.id}.`);
      const applied = applyEntries(headers, m.mapping);
      if (!applied) { notify("None of the mapping's headings is in this table."); return; }
      setMapping(applied);
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };

  const unitsOf = (key: string | null) => target?.columns.find((c) => c.key === key)?.units ?? [];
  const mapAt = (i: number) => mapping.find((m) => m.column === i);
  const nCols = table ? Math.max(0, ...table.rows.slice(0, 200).map((r) => r.length)) : 0;
  const shown = table ? table.rows.slice(0, headerRow + 9) : [];
  const title = tableDialog.getState().title ?? 'Import table';

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Import table">
      <div className="dialog wide import-table" data-dialog="import-table">
        <header className="dialog-head"><span className="panel-title">{title}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <dl className="fields">
            <dt>Table</dt>
            <dd>
              <select value={targetId ?? ''} data-field="table-target" disabled={targets.length === 0} onChange={(e) => setTargetId(e.target.value || null)}>
                {!targetId && <option value="">Choose what the table is…</option>}
                {targets.map((t) => <option key={t.id} value={t.id}>{t.label}</option>)}
              </select>
              {target?.description && <span className="muted small"> {target.description}</span>}
            </dd>
            <dt>File</dt>
            <dd className="pair">
              <button type="button" className="btn" data-field="table-file" onClick={() => void chooseFile()}><FileUp size={14} />Choose a CSV or Excel file…</button>
              {target && <button type="button" className="btn" data-field="table-template" onClick={() => void template()} title="A CSV with this table's columns and a few example rows"><Download size={14} />Download template</button>}
              {source && <span className="muted small" data-no-i18n>{source.name}</span>}
            </dd>
            <dt>Or paste</dt>
            <dd>
              <textarea className="paste" data-field="table-paste" value={pasted} placeholder="Cells copied from a spreadsheet, headings first" onChange={(e) => setPasted(e.target.value)} />
              <button type="button" className="btn mini" data-field="table-use-pasted" disabled={!pasted.trim()} onClick={usePasted}>Use the pasted cells</button>
            </dd>
          </dl>
          {problem && !table && <p className="muted" data-testid="table-problem">{problem}</p>}
          {table && (
            <>
              <p className="muted small"><span data-no-i18n>{table.count}</span> <span>rows</span> · <span>Heading row:</span> <select value={headerRow} data-field="table-header-row" onChange={(e) => changeHeader(Number(e.target.value))}>
                {table.rows.slice(0, 20).map((_, i) => <option key={i} value={i}>{i + 1}</option>)}
              </select></p>
              <p className="muted small">Each column's meaning is guessed from its heading; change what is wrong.</p>
              <div className="scroll-x">
                <table className="layer-table map-table" data-testid="table-columns">
                  <thead>
                    <tr>{Array.from({ length: nCols }, (_, i) => <th key={i}>{headers[i] || `(${i + 1})`}</th>)}</tr>
                    <tr>{Array.from({ length: nCols }, (_, i) => {
                      const m = mapAt(i);
                      const units = unitsOf(m?.key ?? null);
                      return (
                        <th key={i}>
                          <select value={m?.key ?? LEAVE} data-field={`table-column-${i}`} disabled={!target} onChange={(e) => setMapping(setColumnKey(mapping, i, e.target.value || null))}>
                            <option value={LEAVE}>(leave out)</option>
                            {target?.columns.map((c) => <option key={c.key} value={c.key}>{c.label}{c.required ? ' *' : ''}</option>)}
                            {target?.open && <option value={OPEN}>As a property</option>}
                          </select>
                          {units.length > 0 && (
                            <select value={m?.unit ?? ''} data-field={`table-unit-${i}`} onChange={(e) => setMapping(setColumnUnit(mapping, i, e.target.value || null))}>
                              <option value="">(unit from the heading or the row)</option>
                              {units.map((u) => <option key={u} value={u}>{u}</option>)}
                            </select>
                          )}
                        </th>
                      );
                    })}</tr>
                  </thead>
                  <tbody>
                    {shown.map((r, ri) => (
                      <tr key={ri} className={ri === headerRow ? 'row-head' : ri < headerRow ? 'muted' : ''}>
                        {Array.from({ length: nCols }, (_, i) => <td key={i} data-no-i18n title={r[i]}>{r[i] ?? ''}</td>)}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {target && (
                <dl className="fields">
                  {defaultKeys.map((k) => {
                    const col = target.columns.find((c) => c.key === k)!;
                    return [
                      <dt key={`t${k}`} title="For the rows that do not say it">{k === 'storey' ? 'Storey for every row' : col.label}</dt>,
                      <dd key={`d${k}`}>
                        {k === 'storey' ? (
                          <select value={defaults[k] ?? activeStorey(state)?.id ?? storeyList[0]?.id ?? ''} data-field="table-default-storey" onChange={(e) => setDefaults({ ...defaults, [k]: e.target.value })}>
                            {storeyList.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
                          </select>
                        ) : col.choices ? (
                          <select value={defaults[k] ?? ''} data-field={`table-default-${k}`} onChange={(e) => setDefaults({ ...defaults, [k]: e.target.value })}>
                            <option value="">(none)</option>{col.choices.map((c) => <option key={c} value={c}>{c}</option>)}
                          </select>
                        ) : <input value={defaults[k] ?? ''} data-field={`table-default-${k}`} onChange={(e) => setDefaults({ ...defaults, [k]: e.target.value })} />}
                      </dd>,
                    ];
                  })}
                  <dt>Mapping</dt>
                  <dd className="pair">
                    <button type="button" className="btn mini" data-field="table-export-mapping" onClick={() => void exportMapping()}><FileDown size={12} />Export mapping…</button>
                    <button type="button" className="btn mini" data-field="table-import-mapping" onClick={() => void importMapping()}><FileUp size={12} />Import mapping…</button>
                    <span className="muted small">Kept on this computer for tables with these headings.</span>
                  </dd>
                </dl>
              )}
              {lacking.length > 0 && <p className="muted" data-testid="table-lacking">Still to map: <span data-no-i18n>{lacking.map((c) => c.label).join(', ')}</span></p>}
              {preview && (
                <div data-testid="table-preview">
                  <p><strong data-field="table-ok">{preview.ok}</strong> <span>rows ready</span>{preview.errors ? <>, <strong className="row-bad" data-field="table-errors">{preview.errors}</strong> <span>with errors</span></> : ''} · <span data-no-i18n>{preview.steps}</span> <span>steps</span></p>
                  <div className="scroll-x">
                    <table className="layer-table">
                      <tbody>
                        {preview.rows.map((r) => (
                          <tr key={r.row} className={r.error ? 'row-bad' : ''}>
                            <td className="num">{r.row}</td>
                            <td data-no-i18n>{r.error ? r.error : r.steps.join(' · ')}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
              {problem && <p className="row-bad" data-testid="table-problem">{problem}</p>}
            </>
          )}
        </div>
        <footer className="dialog-foot">
          <label title="The rows that cannot be read are left out; the others are imported"><input type="checkbox" data-field="table-skip" checked={skip} onChange={(e) => setSkip(e.target.checked)} /> Skip rows with errors</label>
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-field="table-import" disabled={!request || lacking.length > 0 || busy || !preview || (preview.errors > 0 && !skip) || preview.ok === 0} onClick={() => void run()}>{busy ? 'Importing…' : 'Import'}</button>
        </footer>
      </div>
    </div>
  );
}
