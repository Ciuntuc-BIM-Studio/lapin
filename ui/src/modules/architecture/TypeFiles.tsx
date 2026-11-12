/**
 * A layered type dialog's files (report #52): its kind's types taken out as CSV (a spreadsheet's,
 * one row a layer) or JSON, and types brought in from either; a type of the same name is changed,
 * the others made, in one step to undo (architecture.exportTypes, architecture.importTypes). The JSON also
 * carries the materials' definitions (hatches, fills, pens, colour) and the types' parameters; before an import
 * the engine says what differs from the project's of the same name (architecture.readTypes) and the user
 * chooses (LibraryChoices). A whole library of any kind of type is a type pack: the button opens Type packs.
 */

import { useState } from 'react';
import { FileDown, FileUp, PackageOpen } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { pickFile, saveFile } from '../../engine/files';
import { query } from '../../engine/session';
import { ask, defaultChoices, ImportSummary, LibraryChoices, run, type ImportSummaryData, type LibraryChoiceState, type MaterialConflict, type TypeConflict } from '../core/LibraryChoices';

interface Preview { types: { kind: string; name: string; exists: boolean; differs?: string[] }[]; materials: MaterialConflict[] }
interface Result { created: number; updated: number; skipped: number; renamed: string[]; materials: { made: number; updated: number; kept: number; renamed: string[] } }

/** Under the buttons, shortly; the whole of it in their tooltips. */
const SHORT = 'CSV and JSON: layers for spreadsheets (JSON also materials and parameters). A type library carries any type with everything it is made of.';
const NOTICE = 'CSV and JSON carry layers for spreadsheets and other projects (JSON also the materials\' graphics and the types\' parameters); a type library (.lapintypes) carries any kind of type with everything it is made of: materials with their graphics, profiles and IFC properties.';

export function TypeFiles({ kind, onImported }: { kind: 'wall' | 'floor'; onImported?: (names: string[]) => void }) {
  const [pending, setPending] = useState<{ text: string; preview: Preview; types: TypeConflict[]; choices: LibraryChoiceState } | null>(null);
  const [summary, setSummary] = useState<ImportSummaryData | null>(null);
  const out = async (format: 'csv' | 'json') => {
    const r = await query<{ text: string }>('architecture.exportTypes', { kinds: [kind], format });
    if (!r) { notify('The types could not be exported.'); return; }
    // A byte order mark first, so a spreadsheet reads the CSV as UTF-8 (ă, ș, ț, ø).
    const bytes = new TextEncoder().encode(format === 'csv' ? `﻿${r.text}` : r.text);
    const saved = await saveFile(bytes, `${kind === 'wall' ? 'Wall' : 'Floor'} types.${format}`, format === 'csv' ? 'text/csv' : 'application/json');
    if (saved) notify(format === 'csv' ? 'The types were exported: one row a layer in the CSV, thicknesses in millimetres.' : 'The types were exported as JSON, with their layers, parameters and the materials they use.');
  };
  const finish = async (text: string, preview: Preview, choices: LibraryChoiceState) => {
    setPending(null);
    const r = await run<Result>('architecture.importTypes', { text, materials: choices.materials, types: choices.types });
    if (!r) return;
    setSummary({ typesMade: r.created, typesChanged: r.updated, typesSkipped: r.skipped, materialsMade: r.materials.made, materialsUpdated: r.materials.updated, materialsKept: r.materials.kept, renamed: [...r.renamed, ...r.materials.renamed] });
    if (r.created + r.updated === 0) { notify("No type was imported: the file's types were all skipped."); return; }
    notify('The types were imported: those of the same name changed, the others made, in one step to undo.');
    const skipped = new Set(Object.entries(choices.types).filter(([, c]) => c === 'skip').map(([n]) => n));
    onImported?.([...r.renamed, ...preview.types.filter((t) => t.kind === kind && !skipped.has(t.name) && choices.types[t.name] !== 'both').map((t) => t.name)]);
  };
  const bring = async () => {
    setSummary(null);
    const file = await pickFile('Types (CSV or JSON)', ['csv', 'json', 'txt']);
    if (!file) return;
    const text = new TextDecoder().decode(file.bytes);
    const preview = await ask<Preview>('architecture.readTypes', { text });
    if (!preview) return;
    if (preview.types.length === 0) { notify('The file has no types.'); return; }
    const types: TypeConflict[] = preview.types.filter((t) => t.exists && (t.differs?.length ?? 0) > 0).map((t) => ({ key: t.name, name: t.name, label: t.kind === 'floor' ? 'Floor' : 'Wall', differs: t.differs! }));
    const choices = defaultChoices(preview.materials, types, 'update');
    if (preview.materials.length === 0 && types.length === 0) { await finish(text, preview, choices); return; }
    setPending({ text, preview, types, choices });
  };
  return (
    <div className="small" data-field="type-files" style={{ display: 'flex', flexDirection: 'column', alignItems: 'stretch', gap: 4, marginTop: 8 }}>
      <button type="button" className="btn small" title="Wall and floor types from a CSV (Kind, Type, Mark, Layer, Material, Thickness (mm), Core) or a JSON file" onClick={() => void bring()} data-action="types-import"><FileUp size={13} /> Import…</button>
      <button type="button" className="btn small" title={NOTICE} onClick={() => void out('csv')}><FileDown size={13} /> Export CSV</button>
      <button type="button" className="btn small" title={NOTICE} onClick={() => void out('json')}><FileDown size={13} /> Export JSON</button>
      <button type="button" className="btn small" title="Manage → Type packs…: any kind of type with its materials, graphics, profiles and IFC properties, as a .lapintypes" onClick={() => useUi.setState({ dialog: 'typePacks' })} data-action="type-library"><PackageOpen size={13} /> Type library (.lapintypes)…</button>
      <p className="muted small" title={NOTICE} data-testid="type-files-notice">{SHORT}</p>
      {summary && <ImportSummary r={summary} />}
      {pending && (
        <div className="dialog-backdrop" onClick={() => setPending(null)}>
          <div className="dialog wide" role="dialog" aria-label="Import types" data-dialog="type-conflicts" onClick={(e) => e.stopPropagation()}>
            <header className="dialog-head"><span className="panel-title">Import types</span></header>
            <div className="dialog-body">
              <p className="muted small">The file has types or materials the project already has under the same name, made otherwise. Choose for each what the import does.</p>
              <LibraryChoices materials={pending.preview.materials} types={pending.types} value={pending.choices} onChange={(choices) => setPending({ ...pending, choices })} />
            </div>
            <footer className="dialog-foot">
              <button type="button" className="btn" onClick={() => setPending(null)}>Cancel</button>
              <span className="grow" />
              <button type="button" className="btn primary" onClick={() => void finish(pending.text, pending.preview, pending.choices)} data-action="types-import-go">Import</button>
            </footer>
          </div>
        </div>
      )}
    </div>
  );
}
