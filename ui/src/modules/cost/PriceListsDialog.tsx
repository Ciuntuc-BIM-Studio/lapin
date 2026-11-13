/**
 * Cost → Price lists… (docs/plan-4d5d.md, stage 6, step 4.1): the project's price lists; Import… reads a CSV or XLSX,
 * shows its first rows under its headings and each column's meaning, guessed from the heading (English or Romanian)
 * and changed here; the list goes into the project and into this computer's library, from which any project takes it
 * back. Lapin ships no prices: its template has a few rows of its own rough values, marked indicative.
 */

import { useEffect, useState } from 'react';
import { BookMarked, Download, FileDown, FileUp, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { pickFile, saveFile, toBase64 } from '../../engine/files';
import { readPriceList, writePriceList } from '../../platform/extensions/packs';
import { FirmFolderFiles } from '../core/FirmFolderFiles';
import { forgetFromLibrary, keepInLibrary, library, type LibraryList, type PriceRowDto } from './estimate';

/** A list of this computer's library as a .lapinprices: the file a price list extension is (docs/plan-extensions.md), marked indicative. */
async function exportList(l: LibraryList) {
  const name = l.name.replace(/[\\/:*?"<>|]/g, ' ').trim() || 'Price list';
  const saved = await saveFile(new TextEncoder().encode(writePriceList(l)), `${name}.lapinprices`, 'application/json');
  if (saved) notify('Price list exported as .lapinprices, marked indicative: the file a price list extension is.');
}

interface ListDto { id: string; name: string; currency: string; count: number; resources: number; indicative: boolean; source?: string | null }
interface ReadDto { header: number; rows: number; headings: string[]; sample: string[][]; map: Record<string, number | null>; fields: string[] }

/** What each column can mean, as the dialog names it. */
export const FIELD_LABELS: Record<string, string> = {
  code: 'Code', name: 'Description', unit: 'Unit', material: 'Material', labour: 'Labour', equipment: 'Equipment', transport: 'Transport',
  price: 'Unit price (not split)', hours: 'Man-hours per unit', trade: 'Trade', article: 'Article it belongs to (a resource)', kind: 'Kind of resource', consumption: 'Consumption per unit',
};

/** The column a heading's field is matched to, as the dialog's choice: each column picks one field; a field taken by another column is moved. */
export function setColumn(map: Record<string, number | null>, column: number, field: string | null): Record<string, number | null> {
  const next: Record<string, number | null> = {};
  for (const [f, c] of Object.entries(map)) next[f] = c === column ? null : c;
  if (field) next[field] = column;
  return next;
}

/** The field a column is matched to, if any. */
export const fieldOf = (map: Record<string, number | null>, column: number): string | null => Object.entries(map).find(([, c]) => c === column)?.[0] ?? null;

function Matching({ file, read, onDone }: { file: { name: string; bytes: Uint8Array }; read: ReadDto; onDone: () => void }) {
  const [map, setMap] = useState(read.map);
  const [header, setHeader] = useState(Math.max(0, read.header));
  const [currency, setCurrency] = useState('RON');
  const [keep, setKeep] = useState(true);
  const ok = map.code != null && ['material', 'labour', 'equipment', 'transport', 'price', 'article'].some((f) => map[f] != null);
  const go = async () => {
    const r = await execute('cost.importPrices', { data: toBase64(file.bytes), name: file.name, header, map, currency }) as { name: string; count: number; resources: number; skipped: number; notes: string[]; rows: PriceRowDto[] } | null;
    if (!r) return;
    if (keep && !keepInLibrary({ name: r.name, currency, rows: r.rows, added: new Date().toISOString().slice(0, 10) })) notify('The list is in the project; this computer\'s library is full, so it was not kept there.');
    notify(`Price list imported: ${r.count} article(s), ${r.skipped} row(s) skipped.`);
    if (r.notes.length) console.info(r.notes.join(' '));
    onDone();
  };
  return (
    <div className="price-matching" data-testid="price-matching">
      <p className="muted small">Each column's meaning is guessed from its heading. Change what is wrong: a list needs a code and a price (split into material, labour, equipment and transport, or a unit price).</p>
      <div className="price-sample-wrap">
        <table className="price-sample">
          <thead>
            <tr>{read.headings.map((h, i) => (
              <th key={i}>
                <select value={fieldOf(map, i) ?? ''} onChange={(e) => setMap(setColumn(map, i, e.target.value || null))} data-testid={`price-column-${i}`}>
                  <option value="">Not used</option>
                  {read.fields.map((f) => <option key={f} value={f}>{FIELD_LABELS[f] ?? f}</option>)}
                </select>
                <div className="muted small" data-no-i18n>{h}</div>
              </th>
            ))}</tr>
          </thead>
          <tbody>{read.sample.map((r, i) => <tr key={i}>{read.headings.map((_, j) => <td key={j} data-no-i18n>{r[j] ?? ''}</td>)}</tr>)}</tbody>
        </table>
      </div>
      <div className="row">
        <label title="The row the headings are on (the first row is 1)">Headings on row <input type="number" min={1} value={header + 1} onChange={(e) => setHeader(Math.max(0, Number(e.target.value) - 1))} style={{ width: 56 }} /></label>
        <label>Prices in <select value={currency} onChange={(e) => setCurrency(e.target.value)} data-testid="price-currency"><option value="RON">RON</option><option value="EUR">EUR</option></select></label>
        <label className="check"><input type="checkbox" checked={keep} onChange={(e) => setKeep(e.target.checked)} />Keep in this computer's library too</label>
        <span className="grow" />
        <span className="muted small" data-no-i18n>{`${read.rows}`}</span><span className="muted small">rows in the file</span>
        <button type="button" className="btn primary" disabled={!ok} onClick={() => void go()} data-testid="price-import-go">Import</button>
      </div>
    </div>
  );
}

export function PriceListsDialog() {
  const version = useSession((s) => s.version);
  const [lists, setLists] = useState<ListDto[]>([]);
  const [lib, setLib] = useState<LibraryList[]>(library());
  const [file, setFile] = useState<{ name: string; bytes: Uint8Array; read: ReadDto } | null>(null);
  useEffect(() => { void query<ListDto[]>('cost.priceLists').then((r) => setLists(r ?? [])); }, [version]);
  const close = () => useUi.setState({ dialog: null });
  const pick = async () => {
    const f = await pickFile('Price list (CSV, XLSX)', ['csv', 'xlsx', 'txt']);
    if (!f) return;
    const read = await query<ReadDto>('cost.readPrices', { data: toBase64(f.bytes) });
    if (read) setFile({ name: f.name, bytes: f.bytes, read });
  };
  const template = async () => {
    const csv = await query<string>('cost.template');
    if (!csv) return;
    const saved = await saveFile(new TextEncoder().encode('﻿' + csv), 'Lapin price list template.csv', 'text/csv');
    if (saved) notify("Template saved: Lapin's own rough values, marked indicative. Put your prices in it, then import it.");
  };
  const inProject = new Set(lists.map((l) => l.name));
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Price lists" data-testid="price-lists-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Price lists</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Prices are yours: Lapin ships no catalogue. Import your list (CSV or XLSX, from your estimating program or a supplier); it stays in the project and, if you keep it, in this computer's library for your next projects.</p>
          {file ? (
            <>
              <h4 className="eyebrow" data-no-i18n>{file.name}</h4>
              <Matching file={file} read={file.read} onDone={() => { setFile(null); setLib(library()); }} />
            </>
          ) : (
            <>
              <h4 className="eyebrow">In the project</h4>
              {lists.length === 0 ? <p className="muted">No price list yet.</p> : (
                <ul className="plain-list price-lists" data-testid="price-lists">
                  {lists.map((l) => (
                    <li key={l.id}>
                      <span data-no-i18n>{l.name}</span>
                      <span className="muted small" data-no-i18n>{`${l.count} · ${l.currency}`}</span>
                      {l.indicative && <span className="tag">indicative</span>}
                      <span className="grow" />
                      <button type="button" className="icon mini" title="Remove it from the project" onClick={() => void execute('cost.removePriceList', { id: l.id })}><Trash2 size={12} /></button>
                    </li>
                  ))}
                </ul>
              )}
              <div className="row">
                <button type="button" className="btn primary" onClick={() => void pick()} data-testid="price-import"><FileUp size={14} />Import…</button>
                <button type="button" className="btn" onClick={() => void template()} title="A CSV with the columns and a few rows of Lapin's rough values"><Download size={14} />Template</button>
                <button type="button" className="btn" onClick={() => void execute('cost.addPriceList', { template: true })} title="Lapin's rough values as a list, to try the estimate: replace them with yours" data-testid="price-indicative">Use the indicative values</button>
              </div>
              <h4 className="eyebrow"><BookMarked size={12} /> On this computer</h4>
              {lib.length === 0 ? <p className="muted small">Lists you import are kept here, for any project.</p> : (
                <ul className="plain-list price-lists">
                  {lib.map((l) => (
                    <li key={l.name}>
                      <span data-no-i18n>{l.name}</span>
                      <span className="muted small" data-no-i18n>{`${l.rows.filter((r) => !r.a).length} · ${l.currency} · ${l.added}`}</span>
                      <span className="grow" />
                      <button type="button" className="btn" disabled={inProject.has(l.name)} onClick={() => void execute('cost.addPriceList', { name: l.name, currency: l.currency, rows: l.rows, source: 'library' })}>Add to the project</button>
                      <button type="button" className="icon mini" data-action="price-export-lapinprices" title="Export .lapinprices… (the file a price list extension is; its prices marked indicative)" onClick={() => void exportList(l)}><FileDown size={12} /></button>
                      <button type="button" className="icon mini" title="Forget it on this computer" onClick={() => { forgetFromLibrary(l.name); setLib(library()); }}><Trash2 size={12} /></button>
                    </li>
                  ))}
                </ul>
              )}
              <FirmFolderFiles ext="lapinprices" onPick={(f) => { try { const l = readPriceList(f.bytes); void execute('cost.addPriceList', { name: l.name, currency: l.currency, rows: l.rows, source: 'library' }).then((r) => { if (r) notify(`Price list added to the project from the firm folder: ${l.name}.`); }); } catch (e) { notify(e instanceof Error ? e.message : String(e)); } }}
                save={{ disabled: lib.length === 0, choices: lib.map((l) => l.name), get: async (name) => { const l = lib.find((x) => x.name === name); return l ? { text: writePriceList(l), name: l.name } : null; } }} />
            </>
          )}
        </div>
        <footer className="dialog-foot">
          {file && <button type="button" className="btn" onClick={() => setFile(null)}>Back</button>}
          <span className="grow" /><button type="button" className="btn" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
