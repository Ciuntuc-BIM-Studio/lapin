/**
 * Studio → Families… (docs/plan-user-extensibility.md §5.6): the families of this project — each a light parametric object of the
 * user's own — with how many objects use each, the editor (parameters, bodies, formulas, conditions, a live preview), Place,
 * export and import of .lapinfamily files, keeping one on this computer, and the firm's folders. A family cannot be deleted while
 * objects of it stand in the project.
 */

import { useEffect, useMemo, useState } from 'react';
import { Component, FileDown, FileUp, HardDriveDownload, MapPin, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { t as tr } from '../../../i18n';
import { keepComputerFile } from '../../../platform/libraries';
import { FirmFolderFiles } from '../../core/FirmFolderFiles';
import { FamilyEditor } from './FamilyEditor';
import { editPartSketchIn } from '../PartSketchEdit';
import { createFamily, deleteFamily, exportFamily, familiesDialog, importFamily, updateFamily, useFamilies, usePlaceFamily } from './familyApi';
import { blankFamily, normaliseFamily, quickProblem, sameDef, type FamilyDef } from './familyModel';
import { freeName } from '../../core/titleBlocks/titleBlockModel';

const close = () => useUi.setState({ dialog: null });
const problem = () => { const e = useSession.getState().error; if (e) notify(e); return !!e; };

export function FamiliesDialog() {
  const { families, loaded } = useFamilies();
  const [selected, setSelected] = useState<string | null>(() => familiesDialog.open);
  useEffect(() => { familiesDialog.open = null; }, []);
  // A new family (not yet in the project) or the one chosen, edited as a draft until it is saved.
  const [creating, setCreating] = useState(false);
  const current = creating ? null : families.find((f) => f.id === selected) ?? null;
  const [name, setName] = useState('');
  const [def, setDef] = useState<FamilyDef>(blankFamily());
  const [busy, setBusy] = useState(false);
  const key = creating ? 'new' : current?.id ?? '';
  // The draft follows the family chosen (and a change from elsewhere: undo).
  const sourceJson = current ? JSON.stringify([current.name, current.def]) : '';
  useEffect(() => {
    if (creating) return;
    if (current) { setName(current.name); setDef(normaliseFamily(current.def)); }
  }, [selected, sourceJson, creating]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { if (loaded && selected && !creating && !families.some((f) => f.id === selected)) setSelected(null); }, [loaded, families, selected, creating]);

  const dirty = creating ? true : !!current && (name.trim() !== current.name || !sameDef(def, normaliseFamily(current.def)));
  const taken = useMemo(() => families.map((f) => f.name), [families]);

  const startNew = () => { setCreating(true); setSelected(null); setName(freeName(tr('Family'), taken)); setDef(blankFamily()); };
  const choose = (id: string) => { setCreating(false); setSelected(id); };

  const save = async () => {
    const quick = quickProblem(def);
    if (quick) { notify(tr(quick)); return; }
    setBusy(true);
    try {
      if (creating) {
        const r = await createFamily(name.trim() || tr('Family'), def);
        if (problem() || !r) return;
        setCreating(false); setSelected(r.id);
        notify('Family added to the project.');
      } else if (current) {
        await updateFamily(current.id, { name: name.trim() || current.name, def });
        if (problem()) return;
        notify('Family saved: its objects follow.');
      }
    } finally { setBusy(false); }
  };

  const remove = async () => {
    if (!current) return;
    if (!window.confirm(tr('Delete this family from the project? Undo brings it back.'))) return;
    await deleteFamily(current.id);
    if (!problem()) setSelected(null);
  };

  const exportOne = async () => {
    if (!current) return;
    const r = await exportFamily(current.id);
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), `${safeFileName(current.name, 'family')}.lapinfamily`, 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };

  const keepHere = async () => {
    if (!current) return;
    const r = await exportFamily(current.id);
    if (r) { await keepComputerFile('lapinfamily', current.name, r.text); notify(`Kept on this computer: ${current.name}.`); }
  };

  const bring = async (text: string) => {
    const r = await importFamily(text);
    if (problem() || !r) return;
    setCreating(false); setSelected(r.id);
    notify('Family added to the project.');
  };
  const importFile = async () => {
    const f = await pickFile('Family (.lapinfamily)', 'lapinfamily');
    if (f) await bring(new TextDecoder().decode(f.bytes));
  };

  const place = () => {
    if (!current) return;
    usePlaceFamily.setState({ familyId: current.id, values: {}, rotation: 0, mirror: false });
    useUi.setState({ dialog: null, tool: 'studio.family' });
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide templates families-dialog" role="dialog" aria-label="Families" data-dialog="families" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Families</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row">
              <button type="button" className="btn" onClick={startNew} data-field="fam-new" title="A new family, a box to start from"><Plus size={13} />New</button>
              <button type="button" className="btn" onClick={() => void importFile()} data-field="fam-import" title="A family's .lapinfamily file, exported from Lapin on this or another computer"><FileUp size={13} />Import…</button>
            </div>
            <section>
              <h4 className="eyebrow"><Component size={12} /> This project</h4>
              {families.length === 0 && !creating ? <p className="muted small">None yet: start a new one, or make one from a sketch (Draft → right-click).</p> : (
                <ul className="doc-templates sy-list">
                  {families.map((f) => (
                    <li key={f.id} className={!creating && f.id === selected ? 'active' : undefined}>
                      <button type="button" data-field={`fam-item-${f.id}`} onClick={() => choose(f.id)}>
                        <span className="sy-item-text"><strong data-no-i18n>{f.name}</strong>{f.uses > 0 && <span className="muted small">{`${f.uses} ${tr(f.uses === 1 ? 'object' : 'objects')}`}</span>}</span>
                      </button>
                    </li>
                  ))}
                  {creating && <li className="active"><button type="button"><span className="sy-item-text"><strong data-no-i18n>{name}</strong><span className="muted small">not saved yet</span></span></button></li>}
                </ul>
              )}
            </section>
            <FirmFolderFiles ext="lapinfamily" onPick={(f) => bring(new TextDecoder().decode(f.bytes))}
              save={{ disabled: !current, get: async () => { if (!current) { notify('Choose a family to save.'); return null; } const r = await exportFamily(current.id); return r ? { text: r.text, name: current.name } : null; } }} />
          </aside>
          <main className="tpl-detail" key={key}>
            {!creating && !current ? <p className="muted">Choose a family, or start a new one: its parameters, its bodies and the formulas between them. Objects of a family are placed from Studio → Family, and take their own values.</p> : (
              <>
                <div className="row wrap">
                  <input className="tpl-title-input" value={name} aria-label="Name" data-field="fam-name" data-no-i18n onChange={(e) => setName(e.target.value)} />
                  <span className="grow" />
                  <button type="button" className="btn primary" disabled={busy || !dirty} onClick={() => void save()} data-field="fam-save">{creating ? 'Add to the project' : 'Save'}</button>
                  {!creating && dirty && <button type="button" className="btn" onClick={() => current && (setName(current.name), setDef(normaliseFamily(current.def)))} data-field="fam-revert">Revert</button>}
                </div>
                {current && (
                  <div className="row wrap">
                    <button type="button" className="btn" disabled={dirty} onClick={place} data-field="fam-place" title="Place objects of this family on a plan"><MapPin size={13} />Place on a plan</button>
                    <button type="button" className="btn" onClick={() => void exportOne()} data-field="fam-export" title="The family as a .lapinfamily file, to import on another computer"><FileDown size={13} />Export…</button>
                    <button type="button" className="btn" onClick={() => void keepHere()} data-field="fam-keep" title="Keep a copy on this computer (the Catalog offers it in every project)"><HardDriveDownload size={13} />Keep on this computer</button>
                    <button type="button" className="btn" onClick={() => void remove()} data-field="fam-delete" title="Refused while objects of it stand in the project"><Trash2 size={13} />Delete</button>
                    <span className="muted small">{current.uses > 0 ? `${current.uses} ${tr(current.uses === 1 ? 'object stands in the project' : 'objects stand in the project')}` : 'No object of it yet.'}</span>
                  </div>
                )}
                <FamilyEditor def={def} onChange={setDef} onEditSketch={current ? (sk) => void editPartSketchIn(current.id, sk) : undefined} />
              </>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}
