/**
 * Studio → Parts… (audit S6): the parts of this project — each a parametric object of the user's own (what used to be a family) —
 * with how many objects use each. New, Import…, Export… (.lapinpart), Keep on this computer, the firm's folders, Rename, the IFC
 * class, Delete (refused while used), Place on a plan, and Edit part: the part's own edit mode (sketches, steps, parameters,
 * elements) on a placed object, where its content is edited.
 */

import { confirmAction } from '../../../shell/prompts';
import { backdropClose } from '../../../shell/Dialog';
import { useEffect, useMemo, useState } from 'react';
import { Component, FileDown, FileUp, HardDriveDownload, MapPin, PencilRuler, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { t as tr } from '../../../i18n';
import { keepComputerFile } from '../../../platform/libraries';
import { FirmFolderFiles } from '../../core/FirmFolderFiles';
import { importLibraryText } from '../../../platform/ImportPreview';
import { freeName } from '../../core/titleBlocks/titleBlockModel';
import { createPart, deletePart, exportPart, partsDialog, updatePart, useFamilies, usePlaceFamily } from '../families/familyApi';
import { IFC_CLASSES, blankFamily, className } from '../families/familyModel';
import { editPartOfType } from './partEditing';

const close = () => useUi.setState({ dialog: null });
const problem = () => { const e = useSession.getState().error; if (e) notify(e); return !!e; };

export function PartsDialog() {
  const { families: parts, loaded } = useFamilies();
  const [selected, setSelected] = useState<string | null>(() => partsDialog.open);
  useEffect(() => { partsDialog.open = null; }, []);
  const current = parts.find((f) => f.id === selected) ?? null;
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => { setName(current?.name ?? ''); }, [current?.id, current?.name]);
  useEffect(() => { if (loaded && selected && !parts.some((f) => f.id === selected)) setSelected(null); }, [loaded, parts, selected]);
  const taken = useMemo(() => parts.map((f) => f.name), [parts]);

  const startNew = async () => {
    setBusy(true);
    try {
      const r = await createPart(freeName(tr('Part'), taken), blankFamily());
      if (problem() || !r) return;
      setSelected(r.id);
      notify('Part added to the project. Edit part opens its sketches and parameters.');
    } finally { setBusy(false); }
  };

  const rename = async () => {
    if (!current) return;
    const n = name.trim();
    if (!n) { setName(current.name); return; }
    if (n === current.name) return;
    await updatePart(current.id, { name: n });
    problem();
  };

  const setClass = async (ifcClass: string) => {
    if (!current?.raw) return;
    await updatePart(current.id, { def: { ...current.raw, ifcClass } as never });
    problem();
  };

  const remove = async () => {
    if (!current) return;
    if (!confirmAction(tr('Delete this part from the project? Undo brings it back.'))) return;
    await deletePart(current.id);
    if (!problem()) setSelected(null);
  };

  const exportOne = async () => {
    if (!current) return;
    const r = await exportPart(current.id);
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), `${safeFileName(current.name, 'part')}.lapinpart`, 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };

  const keepHere = async () => {
    if (!current) return;
    const r = await exportPart(current.id);
    if (r) { await keepComputerFile('lapinpart', current.name, r.text); notify(`Kept on this computer: ${current.name}.`); }
  };

  // Through the one import preview (audit D13): a part of its name there kept both, replaced or skipped.
  const bring = async (text: string, file = 'part.lapinpart') => {
    const r = await importLibraryText(text, file) as { id?: string } | null;
    if (problem() || !r?.id) return;
    setSelected(r.id);
  };
  const importFile = async () => {
    const f = await pickFile('Part (.lapinpart, .lapinfamily)', ['lapinpart', 'lapinfamily']);
    if (f) await bring(new TextDecoder().decode(f.bytes), f.name);
  };

  const place = () => {
    if (!current) return;
    usePlaceFamily.setState({ familyId: current.id, values: {}, rotation: 0, mirror: false });
    useUi.setState({ dialog: null, tool: 'studio.part' });
  };

  const edit = async () => { if (current) { setBusy(true); try { await editPartOfType(current.id); } finally { setBusy(false); } } };

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide templates parts-dialog" role="dialog" aria-label="Parts" data-dialog="parts" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Parts</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row">
              <button type="button" className="btn" disabled={busy} onClick={() => void startNew()} data-field="part-new" title="A new part, a box to start from"><Plus size={13} />New</button>
              <button type="button" className="btn" onClick={() => void importFile()} data-field="part-import" title="A part's .lapinpart file (an older .lapinfamily too), exported from Lapin on this or another computer"><FileUp size={13} />Import…</button>
            </div>
            <section>
              <h4 className="eyebrow"><Component size={12} /> This project</h4>
              {parts.length === 0 ? <p className="muted small">None yet: start a new one, or make one from a sketch (Draft → right-click).</p> : (
                <ul className="doc-templates sy-list">
                  {parts.map((f) => (
                    <li key={f.id} className={f.id === selected ? 'active' : undefined}>
                      <button type="button" data-field={`part-item-${f.id}`} onClick={() => setSelected(f.id)}>
                        <span className="sy-item-text"><strong data-no-i18n>{f.name}</strong>{f.uses > 0 && <span className="muted small">{`${f.uses} ${tr(f.uses === 1 ? 'object' : 'objects')}`}</span>}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </section>
            <FirmFolderFiles ext="lapinpart" onPick={(f) => bring(new TextDecoder().decode(f.bytes), f.name)}
              save={{ disabled: !current, get: async () => { if (!current) { notify('Choose a part to save.'); return null; } const r = await exportPart(current.id); return r ? { text: r.text, name: current.name } : null; } }} />
          </aside>
          <main className="tpl-detail" key={current?.id ?? ''}>
            {!current ? <p className="muted">Choose a part, or start a new one. A part is edited in its own edit mode (Edit part): its sketches, steps, parameters and the elements it holds. Objects of a part are placed from Studio → Part, and take their own values.</p> : (
              <>
                <div className="row wrap">
                  <input className="tpl-title-input" value={name} aria-label="Name" data-field="part-name" data-no-i18n onChange={(e) => setName(e.target.value)} onBlur={() => void rename()}
                    onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
                  <span className="grow" />
                  <button type="button" className="btn primary" disabled={busy} onClick={() => void edit()} data-field="part-edit" title="Opens the part's edit mode on a placed object (one is placed when there is none)"><PencilRuler size={13} />Edit part</button>
                </div>
                <div className="row wrap">
                  <label className="row"><span className="muted small">IFC class</span>
                    <select value={current.def.ifcClass} disabled={!current.raw} data-field="part-class" onChange={(e) => void setClass(e.target.value)}>
                      {[...new Set([current.def.ifcClass, ...IFC_CLASSES])].map((c) => <option key={c} value={c} data-no-i18n>{className(c)} ({c})</option>)}
                    </select></label>
                </div>
                <div className="row wrap">
                  <button type="button" className="btn" onClick={place} data-field="part-place" title="Place objects of this part on a plan"><MapPin size={13} />Place on a plan</button>
                  <button type="button" className="btn" onClick={() => void exportOne()} data-field="part-export" title="The part as a .lapinpart file, to import on another computer"><FileDown size={13} />Export…</button>
                  <button type="button" className="btn" onClick={() => void keepHere()} data-field="part-keep" title="Keep a copy on this computer (the Catalog offers it in every project)"><HardDriveDownload size={13} />Keep on this computer</button>
                  <button type="button" className="btn" onClick={() => void remove()} data-field="part-delete" title="Refused while objects of it stand in the project"><Trash2 size={13} />Delete</button>
                  <span className="muted small">{current.uses > 0 ? `${current.uses} ${tr(current.uses === 1 ? 'object stands in the project' : 'objects stand in the project')}` : 'No object of it yet.'}</span>
                </div>
                <p className="muted small">{`${current.def.params.length} ${tr(current.def.params.length === 1 ? 'parameter' : 'parameters')}`}</p>
              </>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}
