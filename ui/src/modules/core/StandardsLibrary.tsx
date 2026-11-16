/**
 * Drafting standards as a library (docs/plan-user-extensibility.md §3.7): Export standards… writes the project's styles, tag types,
 * layers, line types, hatches and symbols as a .lapinstandards file; Import standards… reads one and asks, entry by entry, what to do
 * with what the project already has made otherwise (update, keep both, skip); the project's options (units, scales, marker sizes,
 * object styles, pen table, marker symbols) come only when asked. One step to undo.
 */

import { useEffect, useState } from 'react';
import { FileDown, FileUp } from 'lucide-react';
import { notify } from '../../app/store';
import { pickFile, safeFileName, saveFile } from '../../engine/files';
import { ask, runOk as run } from './LibraryChoices';
import { ImportPreview, tallySentence } from '../../platform/ImportPreview';
import { defaultChoices, type Choice, type LibraryPreview, type LibraryTally } from '../../platform/libraryFile';
import { FirmFolderFiles } from './FirmFolderFiles';
import type { StandardsPack } from './standardsLibraryModel';

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
  // The one import preview (audit D13): each entry of the file new, the same, or the same entry made otherwise (replaced by default).
  const [preview, setPreview] = useState<LibraryPreview | null>(null);
  const [choices, setChoices] = useState<Record<string, Choice>>({});
  const [options, setOptions] = useState(false);
  useEffect(() => {
    let live = true;
    void ask<LibraryPreview>('library.preview', { text: read.text }).then((p) => { if (live && p) { setPreview(p); setChoices(defaultChoices(p.rows)); } });
    return () => { live = false; };
  }, [read.text]);
  const apply = async () => {
    const r = await run<LibraryTally>('standards.importFrom', { text: read.text, choices, ...(pack.options ? { options } : {}) });
    if (r) notify(tallySentence(r));
    onDone();
  };
  return (
    <div className="standards-import" data-testid="standards-import">
      <h4 className="eyebrow">Standards in the file{pack.name ? ' · ' : ''}<span data-no-i18n>{pack.name}</span></h4>
      {preview && <ImportPreview rows={preview.rows} value={choices} onChange={setChoices} header={preview.header} showAll={false} />}
      {pack.options && (
        <label className="field-line check"><input type="checkbox" checked={options} data-field="standards-import-options" onChange={(e) => setOptions(e.target.checked)} />
          <span>Also the project's options (units, scales, marker sizes, object styles, pen table, marker symbols)</span></label>
      )}
      <div className="row">
        <button type="button" className="btn primary" data-field="standards-import-apply" onClick={() => void apply()}>Import</button>
        <button type="button" className="btn" onClick={onDone}>Cancel</button>
      </div>
    </div>
  );
}
