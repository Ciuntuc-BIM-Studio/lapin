/**
 * A layered type dialog's files (report #52): its kind's types taken out as CSV (a spreadsheet's,
 * one row a layer) or JSON, and types brought in from either; a type of the same name is changed,
 * the others made, in one step to undo (architecture.exportTypes, architecture.importTypes).
 */

import { FileDown, FileUp } from 'lucide-react';
import { notify } from '../../app/store';
import { download, pickFile } from '../../engine/files';
import { execute, query } from '../../engine/session';

export function TypeFiles({ kind }: { kind: 'wall' | 'floor' }) {
  const out = async (format: 'csv' | 'json') => {
    const r = await query<{ text: string }>('architecture.exportTypes', { kinds: [kind], format });
    if (!r) return;
    // A byte order mark first, so a spreadsheet reads the CSV as UTF-8 (ă, ș, ț, ø).
    const bytes = new TextEncoder().encode(format === 'csv' ? `﻿${r.text}` : r.text);
    download(bytes, `${kind === 'wall' ? 'Wall' : 'Floor'} types.${format}`, format === 'csv' ? 'text/csv' : 'application/json');
    notify('The types were exported: one row a layer in the CSV, thicknesses in millimetres.');
  };
  const bring = async () => {
    const file = await pickFile('Types (CSV or JSON)', ['csv', 'json', 'txt']);
    if (!file) return;
    const r = await execute('architecture.importTypes', { text: new TextDecoder().decode(file.bytes) });
    if (r) notify('The types were imported: those of the same name changed, the others made, in one step to undo.');
  };
  return (
    <div className="small" data-field="type-files" style={{ display: 'flex', flexDirection: 'column', alignItems: 'stretch', gap: 4, marginTop: 8 }}>
      <button type="button" className="btn small" title="Wall and floor types from a CSV (Kind, Type, Mark, Layer, Material, Thickness (mm), Core) or a JSON file" onClick={() => void bring()}><FileUp size={13} /> Import…</button>
      <button type="button" className="btn small" onClick={() => void out('csv')}><FileDown size={13} /> Export CSV</button>
      <button type="button" className="btn small" onClick={() => void out('json')}><FileDown size={13} /> Export JSON</button>
    </div>
  );
}
