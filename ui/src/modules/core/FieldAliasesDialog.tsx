/**
 * Manage → Field names…: the names your office gives to fields (docs/plan-user-extensibility.md §11.4). A name reads the
 * first of its sources the element has, so a schedule, a tag or a formula asks for `Phase` and gets the right property
 * whichever file the element comes from. Apply replaces the table in one step; the table travels as a .lapinaliases file.
 */

import { promptText } from '../../shell/prompts';
import { backdropClose } from '../../shell/Dialog';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowUp, FileDown, FileUp, HardDriveDownload, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query } from '../../engine/session';
import { pickFile, saveFile } from '../../engine/files';
import { useLibraryFiles, type ComputerFile } from '../../platform/libraries';
import { FieldPicker } from '../../shell/FieldPicker';
import { aliases, forgetAliasesFile, keepAliasesFile, keptAliasesFiles, moved, ALIASES_EXT, type FieldAlias } from '../../shell/fieldSchedule';
import { FirmFolderFiles } from './FirmFolderFiles';
import { importLibraryText } from '../../platform/ImportPreview';

/** A name the engine accepts: no dot, brace, bar or equals sign. */
export const validAliasName = (n: string): boolean => n.trim() !== '' && !/[.{}|=]/.test(n);

export function FieldAliasesDialog() {
  const [rows, setRows] = useState<FieldAlias[]>([]);
  const [kept, setKept] = useState<ComputerFile[]>([]);
  const version = useLibraryFiles((s) => s.version);
  // Only the newest read lands (an import's read must not be overwritten by the opening one coming late).
  const ticket = useRef(0);
  const load = useCallback(() => { const mine = ++ticket.current; void aliases().then((a) => { if (mine === ticket.current) setRows(a.map((x) => ({ ...x, sources: [...x.sources] }))); }); }, []);
  useEffect(load, [load]);
  useEffect(() => { let live = true; void keptAliasesFiles().then((k) => { if (live) setKept(k); }); return () => { live = false; }; }, [version]);
  const close = () => useUi.setState({ dialog: null });
  const set = (i: number, patch: Partial<FieldAlias>) => setRows(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const names = rows.map((r) => r.name.trim().toLowerCase());
  const bad = (r: FieldAlias, i: number) => !validAliasName(r.name) || names.indexOf(r.name.trim().toLowerCase()) !== i || r.sources.filter((s) => s.trim()).length === 0;
  const ok = rows.every((r, i) => !bad(r, i));
  const apply = async () => {
    const list = rows.map((r) => ({ name: r.name.trim(), sources: r.sources.filter((s) => s.trim()), ...(r.description?.trim() ? { description: r.description.trim() } : {}) }));
    if (await execute('fields.setAliases', { aliases: list }) !== null) close();
  };
  // Through the one import preview (audit D13): an alias of its name there replaced or skipped.
  const bring = async (text: string) => {
    const r = await importLibraryText(text, 'aliases.lapinaliases');
    if (r) load();
  };
  const exported = async () => (await query<{ text: string; file: string }>('fields.exportAliases'));
  const toFile = async () => {
    const r = await exported();
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), r.file, 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const keep = async () => {
    const r = await exported();
    if (!r) return;
    const name = promptText('Name of this set of field names', 'Field names');
    if (name?.trim()) { await keepAliasesFile(name.trim(), r.text); notify('Kept in this computer\'s library.'); }
  };
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide field-aliases-dialog" role="dialog" aria-label="Field names" data-testid="field-aliases-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Field names</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Your office's names for fields. A name reads the first source an element has: Phase ← Pset_Firma.Etapa, then ArchiCADProperties.Faza. Schedules, tags, formulas and filters then ask for the name, whichever file the element comes from.</p>
          <div className="fs-rows" data-testid="alias-table">
            {rows.map((r, i) => (
              <div className={`alias-row${bad(r, i) ? ' bad' : ''}`} key={i} data-alias={r.name}>
                <input className="alias-name" value={r.name} placeholder="Name" aria-label="Name" data-testid={`alias-name-${i}`} onChange={(e) => set(i, { name: e.target.value })} />
                <div className="alias-sources">
                  {r.sources.map((s, k) => (
                    <div className="fs-row" key={k}>
                      <FieldPicker value={s} onPick={(t) => set(i, { sources: r.sources.map((x, m) => (m === k ? t : x)) })} testId={`alias-source-${i}-${k}`} />
                      <button type="button" className="icon mini" disabled={k === 0} title="Move up" onClick={() => set(i, { sources: moved(r.sources, k, -1) })}><ArrowUp size={11} /></button>
                      <button type="button" className="icon mini" disabled={k === r.sources.length - 1} title="Move down" onClick={() => set(i, { sources: moved(r.sources, k, 1) })}><ArrowDown size={11} /></button>
                      <button type="button" className="icon mini" title="Remove" onClick={() => set(i, { sources: r.sources.filter((_, m) => m !== k) })}><Trash2 size={11} /></button>
                    </div>
                  ))}
                  <button type="button" className="btn small" onClick={() => set(i, { sources: [...r.sources, ''] })} data-testid={`alias-add-source-${i}`}><Plus size={12} />Source</button>
                </div>
                <input className="alias-description" value={r.description ?? ''} placeholder="Description" aria-label="Description" onChange={(e) => set(i, { description: e.target.value })} />
                <button type="button" className="icon mini" title="Remove the name" onClick={() => setRows(rows.filter((_, j) => j !== i))}><Trash2 size={12} /></button>
              </div>
            ))}
            {rows.length === 0 && <p className="muted small">No field names yet.</p>}
            <button type="button" className="btn small" data-testid="alias-add" onClick={() => setRows([...rows, { name: '', sources: [''] }])}><Plus size={12} />Name</button>
          </div>
          {!ok && <p className="muted small">Each name needs a source, is not used twice, and has no dot, brace, bar or equals sign.</p>}

          <h4 className="eyebrow"><HardDriveDownload size={12} /> On this computer</h4>
          {kept.length === 0 ? <p className="muted small">Sets of field names you keep here are there for any project.</p> : (
            <ul className="plain-list">
              {kept.map((f) => (
                <li key={f.id}>
                  <span data-no-i18n>{f.name}</span><span className="muted small" data-no-i18n>{f.at}</span><span className="grow" />
                  <button type="button" className="btn" onClick={() => void bring(f.text)}>Add to the project</button>
                  <button type="button" className="icon mini" title="Forget it on this computer" onClick={() => void forgetAliasesFile(f.name)}><Trash2 size={12} /></button>
                </li>
              ))}
            </ul>
          )}
          <div className="row">
            <button type="button" className="btn" data-testid="alias-import" onClick={() => void pickFile('Field names (.lapinaliases)', [ALIASES_EXT, 'json']).then((f) => f && bring(new TextDecoder().decode(f.bytes)))}><FileUp size={14} />Import…</button>
            <button type="button" className="btn" data-testid="alias-export" onClick={() => void toFile()}><FileDown size={14} />Export…</button>
            <button type="button" className="btn" onClick={() => void keep()}>Keep in this computer's library</button>
          </div>
          <FirmFolderFiles ext={ALIASES_EXT} onPick={(f) => bring(new TextDecoder().decode(f.bytes))}
            save={{ get: async () => { const r = await exported(); return r ? { text: r.text, name: 'Field names' } : null; } }} />
        </div>
        <footer className="dialog-foot">
          <span className="grow" /><button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!ok} data-testid="alias-apply" onClick={() => void apply()}>Apply</button>
        </footer>
      </div>
    </div>
  );
}
