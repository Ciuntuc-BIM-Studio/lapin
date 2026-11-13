/**
 * A library dialog's „Firm folders” block (docs/plan-user-extensibility.md §4.2): the files of this library's kind in the
 * firm's folders (File → Preferences… → Libraries), each brought in through the dialog's own import (its choices and all),
 * and „Save to firm folder…” writing the library's own export into a writable folder. Desktop only: the browser edition
 * shows nothing.
 */

import { useState } from 'react';
import { FileUp, FolderOpen, HardDriveDownload, Save } from 'lucide-react';
import { keepComputerFile } from '../../platform/libraries';
import { notify, useUi } from '../../app/store';
import { firmFoldersAvailable, prefsStart, useFirmFiles, writableFolders, writeFirmFile } from './firmFolders';

export interface FirmSave {
  /** What the library exports (its own export), with the name the file is given; null when there is nothing to save (the library says why). */
  get: (choice?: string) => Promise<{ text: string; name: string } | null>;
  disabled?: boolean;
  /** Several things to save (the lists of this computer): a select of them comes before the button, and `get` is given the one chosen. */
  choices?: string[];
}

/** The kinds whose „this computer” is a plain file store (the others have libraries of their own). */
const KEPT_HERE = new Set(['lin', 'lapinstandards', 'lapinrules', 'lapinbarshapes', 'lapinparams', 'lapinfamily', 'lapinpart']);

const base = (p: string) => p.split(/[\\/]/).filter(Boolean).pop() ?? p;

export function FirmFolderFiles({ ext, onPick, save }: { ext: string; onPick: (f: { name: string; bytes: Uint8Array }) => void | Promise<void>; save?: FirmSave }) {
  const { files, problems, folders, loading } = useFirmFiles([ext]);
  const writable = writableFolders(folders);
  const [target, setTarget] = useState<string>('');
  const [choice, setChoice] = useState<string>('');
  if (!firmFoldersAvailable()) return null;
  const to = writable.find((f) => f.path === target) ?? writable[0];
  const saveHere = async () => {
    if (!save || !to) return;
    const r = await save.get(save.choices ? (save.choices.includes(choice) ? choice : save.choices[0]) : undefined);
    if (!r) return;
    try { notify(`Saved to ${await writeFirmFile(to.path, r.name, ext, r.text)}.`); } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const openPrefs = () => { prefsStart.page = 'libraries'; useUi.setState({ dialog: 'preferences' }); };
  return (
    <section className="firm-files" data-field="firm-files" data-kind={ext}>
      <h4 className="eyebrow"><FolderOpen size={12} /> Firm folders</h4>
      {folders.length === 0 ? (
        <p className="muted small">None yet. <button type="button" className="link" onClick={openPrefs} data-field="firm-add-folder">Add a firm folder…</button></p>
      ) : loading ? <p className="muted small">Reading the firm folders…</p> : files.length === 0 ? (
        <p className="muted small">No file of this kind in the firm folders.</p>
      ) : (
        <ul className="doc-templates firm-list">
          {files.map((f) => (
            <li key={f.file}>
              <button type="button" data-firm-file={f.name} title={f.file} onClick={() => void onPick({ name: `${f.name}.${f.kind}`, bytes: new TextEncoder().encode(f.text) })}>
                <FileUp size={13} />
                <span className="sy-item-text"><strong data-no-i18n>{f.name}</strong><span className="muted small" data-no-i18n>{`${base(f.folder)} · ${f.modified}`}</span></span>
              </button>
              {KEPT_HERE.has(ext) && <button type="button" className="icon mini" data-action="firm-keep" title="Keep a copy on this computer (the Catalog offers it in every project)"
                onClick={() => void keepComputerFile(ext, f.name, f.text).then(() => notify(`Kept on this computer: ${f.name}.`))}><HardDriveDownload size={12} /></button>}
            </li>
          ))}
        </ul>
      )}
      {problems.length > 0 && <p className="muted small" data-no-i18n>{problems[0]}</p>}
      {save && folders.length > 0 && (writable.length === 0 ? (
        <p className="muted small">Make a folder writable in Preferences → Libraries to save here.</p>
      ) : (
        <div className="row">
          {save.choices && save.choices.length > 0 && <select value={save.choices.includes(choice) ? choice : save.choices[0]} onChange={(e) => setChoice(e.target.value)} data-field="firm-choice" aria-label="What to save">{save.choices.map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}</select>}
          {writable.length > 1 && <select value={to?.path ?? ''} onChange={(e) => setTarget(e.target.value)} data-field="firm-target" aria-label="Firm folder">{writable.map((f) => <option key={f.path} value={f.path} data-no-i18n>{base(f.path)}</option>)}</select>}
          <button type="button" className="btn small" disabled={save.disabled} onClick={() => void saveHere()} data-field="firm-save" title="Writes this library's file into the firm folder, replacing one of the same name"><Save size={13} />Save to firm folder…</button>
        </div>
      ))}
    </section>
  );
}
