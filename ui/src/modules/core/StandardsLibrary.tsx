/**
 * Drafting standards as a library (docs/plan-user-extensibility.md §3.7): Export standards… writes the project's styles, tag types,
 * layers, line types, hatches and symbols as a .lapinstandards file; Import standards… reads one and asks, entry by entry, what to do
 * with what the project already has made otherwise (update, keep both, skip); the project's options (units, scales, marker sizes,
 * object styles, pen table, marker symbols) come only when asked. One step to undo.
 */

import { useState } from 'react';
import { FileDown, FileUp } from 'lucide-react';
import { notify } from '../../app/store';
import { pickFile, safeFileName, saveFile } from '../../engine/files';
import { ask, runOk as run, TYPE_CHOICES } from './LibraryChoices';
import { FirmFolderFiles } from './FirmFolderFiles';
import {
  allChoices, conflictsOf, defaultStandardsChoices, entryKey, kindLabel, summarise, type StandardsPack,
} from './standardsLibraryModel';

export interface PackRead { text: string; pack: StandardsPack }

/** The standards in the firm's folders, read like an imported file; Save to firm folder… writes the project's own export. */
export function StandardsFirmFolders({ onRead }: { onRead: (r: PackRead) => void }) {
  const read = async (f: { bytes: Uint8Array }) => {
    const text = new TextDecoder().decode(f.bytes);
    const pack = await ask<StandardsPack>('standards.readPack', { text });
    if (pack) onRead({ text, pack });
  };
  const get = async () => {
    const r = await ask<{ text: string; file: string }>('standards.export', {});
    return r ? { text: r.text, name: r.file.replace(/\.[^.]+$/, '') } : null;
  };
  return <FirmFolderFiles ext="lapinstandards" onPick={read} save={{ get }} />;
}

export function StandardsLibraryButtons({ onRead }: { onRead: (r: PackRead) => void }) {
  const exportAll = async () => {
    const r = await ask<{ text: string; file: string }>('standards.export', {});
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, 'standards.lapinstandards'), 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const importFile = async () => {
    const f = await pickFile('Drafting standards (.lapinstandards)', 'lapinstandards');
    if (!f) return;
    const text = new TextDecoder().decode(f.bytes);
    const pack = await ask<StandardsPack>('standards.readPack', { text });
    if (pack) onRead({ text, pack });
  };
  return (
    <>
      <button type="button" className="btn small" data-field="standards-export" title="The styles, tag types, layers, line types, hatches and symbols as a .lapinstandards file, for another project or computer" onClick={() => void exportAll()}><FileDown size={13} />Export standards…</button>
      <button type="button" className="btn small" data-field="standards-import" title="A .lapinstandards file: what the project lacks is added; what it has made otherwise asks" onClick={() => void importFile()}><FileUp size={13} />Import standards…</button>
    </>
  );
}

export function StandardsImportPanel({ read, onDone }: { read: PackRead; onDone: () => void }) {
  const { pack } = read;
  const conflicts = conflictsOf(pack);
  const [choices, setChoices] = useState(() => defaultStandardsChoices(pack));
  const [options, setOptions] = useState(false);
  const sum = summarise(pack);
  const apply = async () => {
    const r = await run<{ added?: number; updated?: number; kept?: number; skipped?: number }>('standards.importFrom', { text: read.text, choices, ...(pack.options ? { options } : {}) });
    if (r) notify(`${r.added ?? 0} added, ${r.updated ?? 0} updated, ${r.kept ?? 0} the same, ${r.skipped ?? 0} skipped.`);
    onDone();
  };
  return (
    <div className="standards-import" data-testid="standards-import">
      <h4 className="eyebrow">Standards in the file{pack.name ? ' · ' : ''}<span data-no-i18n>{pack.name}</span></h4>
      <p className="small muted">{`${sum.added} new, ${sum.same} the same as the project's, ${sum.differ} made otherwise.`}</p>
      {conflicts.length > 0 && (
        <table className="layer-table">
          <thead><tr><th>Kind</th><th>Name</th><th>
            <select value="" title="The same choice for every entry" data-choice-all="standards" onChange={(e) => { if (e.target.value) setChoices(allChoices(pack, e.target.value)); }}>
              <option value="">Apply to all…</option>{TYPE_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
          </th></tr></thead>
          <tbody>
            {conflicts.map((e) => (
              <tr key={entryKey(e)}>
                <td className="muted small">{kindLabel(e.kind)}</td>
                <td data-no-i18n>{e.name}</td>
                <td><select value={choices[entryKey(e)] ?? 'update'} data-choice-standard={entryKey(e)} onChange={(ev) => setChoices({ ...choices, [entryKey(e)]: ev.target.value })}>
                  {TYPE_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </select></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {pack.options && (
        <label className="field-line check"><input type="checkbox" checked={options} data-field="standards-import-options" onChange={(e) => setOptions(e.target.checked)} />
          <span>Also the project's options (units, scales, marker sizes, object styles, pen table, marker symbols)</span></label>
      )}
      <p className="muted small">Keep both makes the file's beside the project's, with a number after its name. Updating changes the project's in place. One step to undo.</p>
      <div className="row">
        <button type="button" className="btn primary" data-field="standards-import-apply" onClick={() => void apply()}>Import</button>
        <button type="button" className="btn" onClick={onDone}>Cancel</button>
      </div>
    </div>
  );
}
