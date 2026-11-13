/**
 * Manage → Classifications… (docs/plan-user-extensibility.md §7.3): the classification systems the project holds — Uniclass,
 * OmniClass, CCI, the country's or the office's, brought in from a table (Lapin ships none: their licences are the user's) —, a tree
 * and a search of each one's items, the items imported from a table or exported as CSV, a system deleted with its codes; and the
 * project's shared parameters (name, kind, property set, choices), which a parameter in Properties is made from.
 */

import { useEffect, useState } from 'react';
import { FileDown, Plus, Table2, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { safeFileName, saveFile } from '../../../engine/files';
import { t as tr } from '../../../i18n';
import { openImportTable } from '../tables/tables';
import { ItemTree } from './ItemTree';
import { SharedParams } from './SharedParams';
import { freeSystemName, type ClassSystem } from './classModel';
import { createSystem, deleteSystem, exportSystem, updateSystem, useSystems } from './classifications';

export function ClassificationsDialog() {
  const systems = useSystems();
  const [tab, setTab] = useState<'systems' | 'params'>('systems');
  const [selected, setSelected] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { if (!systems.some((s) => s.id === selected)) setSelected(systems[0]?.id ?? null); }, [systems.map((s) => s.id).join(',')]);
  const current: ClassSystem | undefined = systems.find((s) => s.id === selected);

  const make = async () => {
    const r = await createSystem({ name: freeSystemName(tr('Classification'), systems) });
    if (r) setSelected(r.id);
  };
  const importItems = () => {
    if (!current) return;
    useUi.setState({ dialog: null });
    openImportTable('core.classification', 'Import classification items', { system: current.name });
  };
  const importCodes = () => {
    if (!current) return;
    useUi.setState({ dialog: null });
    openImportTable('core.classify', 'Import classification codes', { system: current.name });
  };
  const exportCsv = async () => {
    if (!current) return;
    const r = await exportSystem(current.id);
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, 'classification.csv'), 'text/csv');
    if (saved) notify(`Exported ${saved}.`);
  };
  const remove = async () => {
    if (!current) return;
    const uses = current.uses > 0 ? ` Its codes are taken off ${current.uses} elements and types.` : '';
    if (!window.confirm(`${tr('Delete this classification?')}${uses}`)) return;
    await deleteSystem(current.id);
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide templates classifications-dialog" role="dialog" aria-label="Classifications" data-dialog="classifications" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Classifications</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <div className="tabs-inline">
            <button type="button" className={`tab-chip${tab === 'systems' ? ' on' : ''}`} data-field="class-tab-systems" onClick={() => setTab('systems')}>Systems</button>
            <button type="button" className={`tab-chip${tab === 'params' ? ' on' : ''}`} data-field="class-tab-params" onClick={() => setTab('params')}>Shared parameters</button>
          </div>
          {tab === 'params' ? <SharedParams /> : (
            <div className="tpl-body">
              <aside className="tpl-list">
                <div className="row"><button type="button" className="btn" data-field="class-new" onClick={() => void make()}><Plus size={13} />New</button></div>
                {systems.length === 0 && <p className="muted small">None yet. Make one, then Import items from a table (a CSV or Excel file of the system you hold the licence of); or keep the office's own keynotes.</p>}
                <ul className="doc-templates">
                  {systems.map((s) => (
                    <li key={s.id} className={s.id === selected ? 'active' : undefined}>
                      <button type="button" data-class-system={s.name} onClick={() => setSelected(s.id)}>
                        <span className="sy-item-text"><strong data-no-i18n>{s.name}</strong><span className="muted small">{`${s.items} ${tr('items')} · ${s.uses} ${tr('used')}`}</span></span>
                      </button>
                    </li>
                  ))}
                </ul>
              </aside>
              <main className="tpl-detail">
                {!current ? <p className="muted">Choose a classification, or make one.</p> : (
                  <>
                    <label className="field-line"><span className="eyebrow">Name</span>
                      <input key={current.id + current.name} data-field="class-name" defaultValue={current.name} onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== current.name) void updateSystem({ id: current.id, name: v }); }} /></label>
                    <label className="field-line"><span className="eyebrow">Source</span>
                      <input key={current.id + 'src' + (current.source ?? '')} data-field="class-source" placeholder="Whose it is: the publisher, the office" defaultValue={current.source ?? ''} onBlur={(e) => { const v = e.target.value.trim(); if (v !== (current.source ?? '')) void updateSystem({ id: current.id, source: v }); }} /></label>
                    <label className="field-line"><span className="eyebrow">Edition</span>
                      <input key={current.id + 'ed' + (current.edition ?? '')} data-field="class-edition" defaultValue={current.edition ?? ''} onBlur={(e) => { const v = e.target.value.trim(); if (v !== (current.edition ?? '')) void updateSystem({ id: current.id, edition: v }); }} /></label>
                    <div className="row wrap">
                      <button type="button" className="btn" data-field="class-import" title="A CSV or Excel file or pasted cells: code, title, parent code" onClick={importItems}><Table2 size={13} />Import items from table…</button>
                      <button type="button" className="btn" data-field="class-import-codes" title="A table of elements (by GlobalId, mark or name) and their codes in this system" onClick={importCodes}><Table2 size={13} />Classify elements from table…</button>
                      <button type="button" className="btn" data-field="class-export" disabled={current.items === 0} onClick={() => void exportCsv()}><FileDown size={13} />Export CSV…</button>
                      <span className="grow" />
                      <button type="button" className="btn danger" data-field="class-delete" onClick={() => void remove()}><Trash2 size={13} />Delete</button>
                    </div>
                    <p className="muted small">Lapin ships no classification: the system's licence is yours. Codes are put on elements and types in their properties, or from a table (Import table… → Classification codes on elements).</p>
                    <input type="search" placeholder={tr('Find a code or a title')} value={search} data-field="class-search" onChange={(e) => setSearch(e.target.value)} />
                    <div className="class-tree-box"><ItemTree system={current.id} search={search} /></div>
                  </>
                )}
              </main>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
