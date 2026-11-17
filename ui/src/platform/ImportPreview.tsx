/**
 * The one import preview of every library file (audit D13): what the file holds, item by item, and what the import will do
 * with each — new, the same, the same name or the same item changed — with the same choices everywhere (keep both, „Name (2)”;
 * replace in place; skip) and one choice for all. Used inline by the libraries' own dialogs (Symbols, Drafting standards,
 * Line types, Type packs, the wall and floor types) and as the import dialog the firm folders, the Catalog and the other
 * libraries open (`LibraryImportHost`, `importLibraryText`).
 */

import { useState } from 'react';
import { create } from 'zustand';
import { notify } from '../app/store';
import { engine, execute, useSession } from '../engine/session';
import { phrase } from '../shell/prompts';
import { Dialog } from '../shell/Dialog';
import {
  CHOICE_LABELS, STATUS_LABELS, chooseAll, defaultChoices, isConflict, previewCounts, rowsByName,
  type Choice, type LibraryHeader, type LibraryPreview, type LibraryRow, type LibraryTally,
} from './libraryFile';

/** An item's kind as the preview names it (a type's kind comes named by the engine). */
const KINDS: Record<string, string> = {
  symbol: 'Symbol', material: 'Material', rulePack: 'Rule pack', titleBlock: 'Title block', part: 'Part', parameter: 'Parameter',
  alias: 'Alias', barShape: 'Bar shape', lineType: 'Line type', pattern: 'Hatch pattern', schedule: 'Schedule', furniture: 'Furniture',
  profiles: 'Profile library', priceList: 'Price list', text: 'Text style', dim: 'Dimension style', axis: 'Axis style', tag: 'Tag type',
  layer: 'Layer', layerGroup: 'Layer group',
};
export const kindLabel = (kind: string): string => KINDS[kind] ?? kind;

/** Who wrote the file, when and from what; a file of a build before the envelope says so. */
export function HeaderLine({ header }: { header: LibraryHeader | null | undefined }) {
  if (!header) return null;
  if (header.envelope === 0) return <p className="muted small" data-testid="library-header">Written by an earlier Lapin.</p>;
  return (
    <p className="muted small" data-testid="library-header">
      {phrase('Written by Lapin {app} on {date}, from {source}.', { app: header.app ?? '?', date: (header.created ?? '').slice(0, 10), source: header.source ?? 'Lapin' })}
    </p>
  );
}

/** One item's choice, in the words every import uses. */
export function ChoiceSelect({ choices, value, onChange, labels, ...data }: {
  choices: Choice[]; value: Choice; onChange: (c: Choice) => void; labels?: Partial<Record<Choice, string>>;
} & Record<`data-${string}`, string>) {
  return (
    <select value={value} onChange={(e) => onChange(e.target.value as Choice)} {...data}>
      {choices.map((c) => <option key={c} value={c}>{labels?.[c] ?? CHOICE_LABELS[c]}</option>)}
    </select>
  );
}

/** The same choice for every item at once. */
export function ChoiceAll({ choices, onChoose, labels, title = 'The same choice for every item that differs', ...data }: {
  choices: Choice[]; onChoose: (c: Choice) => void; labels?: Partial<Record<Choice, string>>; title?: string;
} & Record<`data-${string}`, string>) {
  return (
    <select value="" title={title} onChange={(e) => { if (e.target.value) onChoose(e.target.value as Choice); }} {...data}>
      <option value="">Apply to all…</option>
      {choices.map((c) => <option key={c} value={c}>{labels?.[c] ?? CHOICE_LABELS[c]}</option>)}
    </select>
  );
}

/** What the three choices do, said the same under every import. */
export const ChoicesNote = () => (
  <p className="muted small">Keep both brings the file's in beside the one there, with (2) after its name. Replace changes the one there in place: what uses it follows. Skip leaves it as it is. One step to undo.</p>
);

export function ImportPreview({ rows, value, onChange, labels, header, showAll = true }: {
  rows: LibraryRow[]; value: Record<string, Choice>; onChange: (v: Record<string, Choice>) => void;
  /** The words of the choices where a library says them otherwise (a material's „keep”). */
  labels?: Partial<Record<Choice, string>>;
  header?: LibraryHeader | null;
  /** Every row (new and the same too), else only those to choose for. */
  showAll?: boolean;
}) {
  const shown = showAll ? rows : rows.filter(isConflict);
  const offered = [...new Set(rows.filter(isConflict).flatMap((r) => r.choices))];
  const counts = previewCounts({ rows });
  return (
    <div className="library-choices import-preview" data-testid="import-preview">
      <HeaderLine header={header} />
      <p className="small" data-testid="import-counts">{phrase('{added} new, {same} the same, {choose} to choose for.', counts)}</p>
      {shown.length > 0 && (
        <table className="layer-table">
          <thead><tr><th>Name</th><th>Status</th><th>What differs</th><th>
            {offered.length > 0 && <ChoiceAll choices={offered} labels={labels} onChoose={(c) => onChange(chooseAll(rows, value, c))} data-choice-all="library" />}
          </th></tr></thead>
          <tbody>
            {shown.map((r) => (
              <tr key={r.key} data-status={r.status}>
                <td><span className="muted small"><span>{kindLabel(r.kind)}</span> · </span><span data-no-i18n>{r.name}</span></td>
                <td>{STATUS_LABELS[r.status]}</td>
                <td><span className="muted small">{r.differs.map((d, i) => <span key={d}>{i > 0 && ', '}<span>{d}</span></span>)}</span></td>
                <td>{isConflict(r)
                  ? <ChoiceSelect choices={r.choices} labels={labels} value={value[r.key] ?? r.choice ?? 'both'} onChange={(c) => onChange({ ...value, [r.key]: c })} data-choice={r.key} />
                  : <span className="muted">–</span>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <ChoicesNote />
    </div>
  );
}

/** What an import did, as one sentence. */
export const tallySentence = (r: LibraryTally): string => {
  const said = phrase('{added} added, {updated} updated, {kept} kept, {skipped} skipped.', { added: r.added ?? 0, updated: r.updated ?? 0, kept: r.kept ?? 0, skipped: r.skipped ?? 0 });
  return r.renamed?.length ? `${said} ${phrase('Kept beside: {names}.', { names: r.renamed.join(', ') })}` : said;
};

// ------------------------------------------------------------ the import dialog

interface Pending { title: string; preview: LibraryPreview; run: (choices: Record<string, Choice>) => Promise<LibraryTally | null>; done: (r: LibraryTally | null) => void }

const useImport = create<{ pending: Pending | null }>(() => ({ pending: null }));

/** The import dialog, over any other (mounted once, by core's background). */
export function LibraryImportHost() {
  const pending = useImport((s) => s.pending);
  return pending ? <ImportDialog key={pending.preview.format + pending.title} pending={pending} /> : null;
}

function ImportDialog({ pending }: { pending: Pending }) {
  const [choices, setChoices] = useState(() => defaultChoices(pending.preview.rows));
  const [busy, setBusy] = useState(false);
  const close = (r: LibraryTally | null) => { useImport.setState({ pending: null }); pending.done(r); };
  const go = async () => {
    setBusy(true);
    const r = await pending.run(choices).catch((e: unknown) => { notify(e instanceof Error ? e.message : String(e)); return null; });
    setBusy(false);
    if (r) close(r);
  };
  return (
    <Dialog title={<><span>Import preview</span> · <span data-no-i18n>{pending.title}</span></>} onClose={() => close(null)} className="wide" dataDialog="libraryImport" testId="library-import"
      footer={<>
        <button type="button" className="btn" onClick={() => close(null)}>Cancel</button>
        <button type="button" className="btn primary" disabled={busy} onClick={() => void go()} data-testid="library-import-go">Import</button>
      </>}>
      <ImportPreview rows={pending.preview.rows} value={choices} onChange={setChoices} header={pending.preview.header} />
    </Dialog>
  );
}

/**
 * A preview shown when the file has something to choose for (straight on when all of it is new or the same), then the import
 * as chosen; what it did said, and given back (null: cancelled or refused).
 */
export function askImport(title: string, preview: LibraryPreview, run: (choices: Record<string, Choice>) => Promise<LibraryTally | null>): Promise<LibraryTally | null> {
  const said = (r: LibraryTally | null) => { if (r) notify(tallySentence(r)); return r; };
  if (preview.conflicts === 0) return run(defaultChoices(preview.rows)).then(said);
  return new Promise((resolve) => useImport.setState({ pending: { title, preview, run, done: (r) => resolve(said(r)) } }));
}

/**
 * One file kept in this computer's libraries (a price list, a profile library, a .pat in My patterns: not the project's, so
 * no engine preview), through the same preview and choices (audit D13): straight on when nothing there has its name, or what
 * has it is the same; else the import preview asks to keep both („Name (2)”), replace or skip. The choice — `new`, `same`,
 * `both`, `update`, `skip` — or null when cancelled.
 */
export function chooseOnComputer<T>(title: string, kind: string, name: string, item: T, have: (name: string) => T | undefined, same: (a: T, b: T) => boolean): Promise<'new' | 'same' | Choice | null> {
  const [row] = rowsByName([{ name, item }], have, same, kind);
  if (row.status === 'new' || row.status === 'same') return Promise.resolve(row.status);
  const preview: LibraryPreview = { format: kind, extension: '', what: kindLabel(kind), header: null, rows: [row], new: 0, same: 0, conflicts: 1 };
  return new Promise((resolve) => useImport.setState({
    pending: { title, preview, run: async (choices) => { resolve(choices[row.key] ?? 'both'); return {}; }, done: (r) => { if (!r) resolve(null); } },
  }));
}

/** A question to the engine whose refusal is said. */
async function ask<T>(name: string, args: Record<string, unknown>): Promise<T | null> {
  try { return await engine.call<T>('doc.query', { name, args }); } catch (e) { notify(e instanceof Error ? e.message : String(e)); return null; }
}

/**
 * A library file of any kind brought into the open project through the one preview and import (`library.preview`,
 * `library.import`): the file's name (its extension tells .lin and .pat apart) and text. Null when there is no project,
 * the file is refused or the user cancels.
 */
export async function importLibraryText(text: string, file: string): Promise<LibraryTally | null> {
  if (!useSession.getState().connected) { notify('Open a project first.'); return null; }
  const ext = file.includes('.') ? file.slice(file.lastIndexOf('.')) : undefined;
  const preview = await ask<LibraryPreview>('library.preview', { text, ext });
  if (!preview) return null;
  return askImport(file, preview, async (choices) => {
    const r = await execute('library.import', { text, ext, choices }) as LibraryTally | null;
    if (!r) notify(useSession.getState().error ?? 'The import failed.');
    return r;
  });
}
