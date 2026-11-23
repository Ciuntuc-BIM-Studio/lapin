/**
 * MEP symbols… (docs/plan-mep.md): the set the project's plans draw their MEP symbols with — Lapin's ISO 4067 / IEC 60617
 * set, or one of the project's — and, role by role, the symbol each draws: Lapin's, or one the user drew (Drafting → Save as
 * symbol…: its base point the insertion point, its angle along the node). A set of the project's falls back on its base for
 * the roles it does not draw; the Romanian STAS set waits for its symbols and starts one to fill. Sets go out and come in as
 * .lapinsymbols files (the symbols library format).
 */

import { backdropClose } from '../../shell/Dialog';
import { useEffect, useState } from 'react';
import { FileDown, FileUp, Plus, Trash2, Pencil, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { pickFile, saveFile } from '../../engine/files';
import { confirmAction, promptText } from '../../shell/prompts';
import { defaultChoices, type LibraryPreview } from '../../platform/libraryFile';
import { listSymbols } from '../core/symbols/symbols';
import { glyphSvg, type Glyph } from './glyph';

export interface SymbolSetInfo { id: string; name: string; standard?: string | null; base?: string | null; builtin: boolean; pending: boolean; description?: string | null; needs?: string[] | null; own: number }
export interface SymbolKeyInfo { key: string; label: string; from: string; paper: boolean; symbolId?: string | null; symbolName?: string | null; own: boolean; services: string[]; glyph?: Glyph[] }
export interface SymbolSetsInfo { set: string; shown: string; sets: SymbolSetInfo[]; keys: SymbolKeyInfo[] }

export const symbolSetsQuery = (setId?: string) => query<SymbolSetsInfo>('mep.symbolSets', setId ? { setId } : {});

/** A role's symbol drawn small, from the engine's picture of it (paper mm). */
export const GlyphThumb = ({ glyph }: { glyph?: Glyph[] }) => (
  <span className="sy-thumb" data-no-i18n aria-hidden dangerouslySetInnerHTML={{ __html: glyph && glyph.length ? glyphSvg(glyph) : '' }} />
);

export function SymbolSetsDialog() {
  const version = useSession((s) => s.version);
  const [info, setInfo] = useState<SymbolSetsInfo | null>(null);
  const [symbols, setSymbols] = useState<{ id: string; name: string; builtin: boolean; scale: string }[]>([]);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    let live = true;
    void symbolSetsQuery().then((r) => { if (live) setInfo(r); });
    void listSymbols(false).then((l) => { if (live) setSymbols(l.items.filter((s) => !s.builtin).map((s) => ({ id: s.id, name: s.name, builtin: s.builtin, scale: s.scale }))); });
    return () => { live = false; };
  }, [version]);
  if (!info) return null;
  const chosen = info.sets.find((s) => s.id === info.set);
  const nameOf = (id: string) => info.sets.find((s) => s.id === id)?.name ?? id;
  const stas = info.sets.find((s) => s.pending);

  const newSet = async (from?: string) => {
    const name = from ? undefined : promptText('Name of the new symbol set', 'Office symbols');
    if (!from && !name?.trim()) return;
    await execute('mep.createSymbolSet', from ? { from } : { name: name!.trim(), base: chosen?.id ?? 'iso' });
  };
  const rename = async () => {
    if (!chosen || chosen.builtin) return;
    const name = promptText('Name of the symbol set', chosen.name);
    if (name?.trim()) await execute('mep.updateSymbolSet', { id: chosen.id, name: name.trim() });
  };
  const remove = async () => {
    if (!chosen || chosen.builtin || !confirmAction('Delete the symbol set „{name}”? Its symbols stay in the project.', { name: chosen.name })) return;
    await execute('mep.deleteSymbolSet', { id: chosen.id });
  };
  const exportSet = async () => {
    if (!chosen || chosen.builtin) return;
    const r = await query<{ text: string; file: string }>('mep.exportSymbolSet', { id: chosen.id });
    if (!r) { notify(useSession.getState().error ?? 'The set could not be exported.'); return; }
    const saved = await saveFile(new TextEncoder().encode(r.text), r.file, 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const importSet = async () => {
    const f = await pickFile('MEP symbol set (.lapinsymbols)', 'lapinsymbols');
    if (!f) return;
    const text = new TextDecoder().decode(f.bytes);
    const read = await query<{ name: string; roles: unknown[]; preview: LibraryPreview }>('mep.readSymbolSet', { text });
    if (!read) { notify(useSession.getState().error ?? 'That file holds no MEP symbol set.'); return; }
    // A symbol of a name the project has: kept beside it, as Manage → Symbols does by default.
    const r = await execute('mep.importSymbolSet', { text, choices: defaultChoices(read.preview.rows) }) as { name?: string } | null;
    if (r?.name) notify('Symbol set imported: the plans draw with it.');
  };
  const setRole = (key: string, symbolId: string | null) => void execute('mep.setRoleSymbol', { role: key, symbolId });

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" role="dialog" aria-label="MEP symbols" data-dialog="mep-symbols" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">MEP symbols</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <dl className="fields">
            <dt title="The set every plan of the project draws its MEP symbols with">Plans draw with</dt>
            <dd>
              <select data-field="mep-symbol-set" value={info.set} onChange={(e) => void execute('mep.setSymbolSet', { id: e.target.value })}>
                <optgroup label="Lapin">{info.sets.filter((s) => s.builtin && !s.pending).map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}</optgroup>
                {info.sets.some((s) => !s.builtin) && <optgroup label="This project">{info.sets.filter((s) => !s.builtin).map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}</optgroup>}
              </select>
            </dd>
            {chosen && !chosen.builtin && (
              <>
                <dt>Falls back on</dt>
                <dd>
                  <select value={chosen.base ?? 'iso'} onChange={(e) => void execute('mep.updateSymbolSet', { id: chosen.id, base: e.target.value })}>
                    {info.sets.filter((s) => s.id !== chosen.id && !s.pending).map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
                  </select>
                </dd>
              </>
            )}
          </dl>
          <div className="row" style={{ gap: 6, flexWrap: 'wrap' }}>
            <button type="button" className="btn" data-field="mep-symbols-new" onClick={() => void newSet()}><Plus size={13} />New set</button>
            {stas && <button type="button" className="btn" data-field="mep-symbols-stas" title="No STAS symbol is drawn yet: a set of your own, falling back on ISO, to give each role your drawn symbol" onClick={() => void newSet(stas.id)}><Plus size={13} />Start a Romanian (STAS) set</button>}
            <button type="button" className="btn" disabled={!chosen || chosen.builtin} onClick={() => void rename()}><Pencil size={13} />Rename</button>
            <button type="button" className="btn" disabled={!chosen || chosen.builtin} onClick={() => void remove()}><Trash2 size={13} />Delete</button>
            <span className="grow" />
            <button type="button" className="btn" disabled={!chosen || chosen.builtin || chosen.own === 0} title="The set and its symbols as a .lapinsymbols file, for another project or office" onClick={() => void exportSet()}><FileDown size={13} />Export…</button>
            <button type="button" className="btn" data-field="mep-symbols-import" onClick={() => void importSet()}><FileUp size={13} />Import…</button>
          </div>
          <p className="muted small" style={{ marginTop: 8 }}>
            {chosen?.builtin
              ? 'Lapin\'s ISO 4067 / IEC 60617 symbols. Give a role a symbol of your own and a set of the project\'s is made from them.'
              : 'Draw a symbol with Drafting, select it and Save as symbol… (its base point is where the node stands, its angle the node\'s direction); then choose it here for a role. Roles left as they are draw the set it falls back on.'}
          </p>
          <table className="grid" data-testid="mep-symbol-roles">
            <thead><tr><th>Role</th><th>Symbol</th><th>Drawn with</th><th title="On paper: the same printed size at any scale; on the model: its real size">Size</th></tr></thead>
            <tbody>
              {info.keys.map((k) => (
                <tr key={k.key} data-role={k.key}>
                  <td>{k.label}</td>
                  <td><GlyphThumb glyph={k.glyph} /></td>
                  <td>
                    <select value={k.own && k.symbolId ? k.symbolId : ''} data-field={`mep-symbol-${k.key}`} onChange={(e) => setRole(k.key, e.target.value || null)}>
                      <option value="">{k.own ? 'as the set it falls back on' : nameOf(k.from)}</option>
                      {symbols.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
                    </select>
                  </td>
                  <td className="muted">{k.paper ? 'on paper' : 'on the model'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
