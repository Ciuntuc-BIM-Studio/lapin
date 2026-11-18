/**
 * Material standards (docs/plan-material-graphics.md, stage 3): how materials draw in cuts, plans and facades, as data the user
 * chooses. Manage → Drawing standards → Materials. The list (Lapin's built-in standards and the project's own, the project's
 * current one marked) with Apply…, Duplicate, Edit, Remove, Export…, Import… and "Save the project's graphics as a standard…";
 * the rule table editor; the apply with its preview; and the store's standards. A standard is never chosen by language or
 * country: the project says which one it uses (default Lapin).
 */

import { useCallback, useEffect, useState } from 'react';
import { Check, Copy, FileDown, FileUp, Pencil, Plus, Save, Trash2, Wand2 } from 'lucide-react';
import { Dialog } from '../../shell/Dialog';
import { confirmAction } from '../../shell/prompts';
import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../engine/files';
import { ask, run } from './LibraryChoices';
import { StandardsImportPanel, type PackRead } from './StandardsLibrary';
import { MaterialStandardEditor, newStandardDraft } from './MaterialStandardEditor';
import { MaterialStandardApply } from './MaterialStandardApply';
import { MaterialStandardStore } from './MaterialStandardStore';
import { standardMeta, type MaterialStandard, type StandardList, type StandardSummary } from './materialStandardsModel';
import { takeMaterialStandardsTab } from './materialStandardsOpen';
import type { StandardsPack } from './standardsLibraryModel';

type Tab = 'project' | 'store';
type Panel = { kind: 'edit'; id?: string; initial?: MaterialStandard } | { kind: 'apply'; id: string; name: string } | { kind: 'import'; read: PackRead } | { kind: 'save' } | null;

function SaveFromProject({ onDone }: { onDone: (id?: string) => void }) {
  const [form, setForm] = useState({ name: '', author: '', country: '', norm: '', version: '1.0.0', license: '', description: '' });
  const [withRules, setWithRules] = useState(true);
  const set = (k: keyof typeof form, v: string) => setForm((f) => ({ ...f, [k]: v }));
  const go = async () => {
    const args: Record<string, unknown> = { name: form.name.trim(), withRules };
    for (const k of ['author', 'norm', 'version', 'license', 'description'] as const) if (form[k].trim()) args[k] = form[k].trim();
    if (form.country.trim()) args.country = form.country.trim().toUpperCase().slice(0, 2);
    const r = await run<{ id: string; rules: number }>('standards.fromProject', args);
    if (r) { notify(`Saved „${form.name.trim()}” with ${r.rules} rules.`); onDone(r.id); }
  };
  return (
    <section className="standard-from-project" data-testid="standard-from-project">
      <span className="panel-title">Save the project’s graphics as a standard</span>
      <p className="muted small">The graphics set by hand on the project’s materials become a new standard, with a rule for each material.</p>
      {(['name', 'author', 'country', 'norm', 'version', 'license', 'description'] as const).map((k) => (
        <label key={k} className="field-line"><span className="small">{{ name: 'Name', author: 'Author', country: 'Country', norm: 'Norm', version: 'Version', license: 'Licence', description: 'Description' }[k]}</span>
          <input data-field={`from-project-${k}`} value={form[k]} onChange={(e) => set(k, e.target.value)} /></label>
      ))}
      <label className="field-line check"><input type="checkbox" checked={withRules} onChange={(e) => setWithRules(e.target.checked)} /><span>With a rule for each material</span></label>
      <div className="row">
        <button type="button" className="btn primary" data-action="from-project-save" disabled={!form.name.trim()} onClick={() => void go()}>Save</button>
        <button type="button" className="btn" onClick={() => onDone()}>Cancel</button>
      </div>
    </section>
  );
}

export function MaterialStandardsDialog() {
  const version = useSession((s) => s.version);
  const [tab, setTab] = useState<Tab>(() => { return takeMaterialStandardsTab() ?? 'project'; });
  const [list, setList] = useState<StandardList | null>(null);
  const [panel, setPanel] = useState<Panel>(null);
  const reload = useCallback(async () => { const r = await ask<StandardList>('standards.materialList', {}); if (r) setList(r); }, []);
  useEffect(() => { void reload(); }, [reload, version]);
  const close = () => useUi.setState({ dialog: null });

  const remove = async (s: StandardSummary) => {
    if (!confirmAction(`Remove the standard „${s.name}” from the project?`)) return;
    if (await run('standards.materialRemove', { id: s.id })) await reload();
  };
  const duplicate = async (s: StandardSummary) => {
    const r = await run<{ id: string }>('standards.materialDuplicate', { id: s.id, name: `${s.name} (copy)` });
    if (r) { await reload(); setPanel({ kind: 'edit', id: r.id }); }
  };
  const exportOne = async (s: StandardSummary) => {
    const r = await ask<{ text: string; file: string }>('standards.export', { name: s.name, materialStandards: [s.id], only: 'materialGraphics' });
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, `${s.id}.lapinstandards`), 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const importFile = async () => {
    const f = await pickFile('Drafting standards (.lapinstandards)', 'lapinstandards');
    if (!f) return;
    const text = new TextDecoder().decode(f.bytes);
    const pack = await ask<StandardsPack>('standards.readPack', { text });
    if (pack) setPanel({ kind: 'import', read: { text, pack } });
  };

  return (
    <Dialog title="Material standards" onClose={close} className="wide material-standards-dialog" dataDialog="materialStandards">
      <div className="tabs" role="tablist">
        <button type="button" role="tab" aria-selected={tab === 'project'} data-tab="project" className={`tab${tab === 'project' ? ' on' : ''}`} onClick={() => setTab('project')}>This project</button>
        <button type="button" role="tab" aria-selected={tab === 'store'} data-tab="store" className={`tab${tab === 'store' ? ' on' : ''}`} onClick={() => setTab('store')}>From the store</button>
      </div>
      {tab === 'store' && !panel && <MaterialStandardStore onUse={(s) => setPanel({ kind: 'apply', id: s.id ?? '', name: s.name })} />}
      {tab === 'store' && panel?.kind === 'apply' && <MaterialStandardApply id={panel.id} name={panel.name} onDone={() => { setPanel(null); void reload(); }} />}
      {tab === 'project' && (
        <>
          <p className="muted small">A standard tells how each material draws in cuts, plans and facades. Lapin’s own is the default; a standard is never chosen for you by your language or country.</p>
          {!panel && (
            <>
              <div className="row">
                <button type="button" className="btn compact" data-action="standard-new" onClick={() => setPanel({ kind: 'edit', initial: newStandardDraft() })}><Plus size={13} />New standard</button>
                <button type="button" className="btn compact" data-action="standard-from-project" title="The graphics set by hand on this project’s materials, as a new standard" onClick={() => setPanel({ kind: 'save' })}><Save size={13} />Save the project’s graphics as a standard…</button>
                <button type="button" className="btn compact" data-action="standard-import" onClick={() => void importFile()}><FileUp size={13} />Import…</button>
              </div>
              <ul className="standard-list" data-testid="standard-list">
                {(list?.standards ?? []).map((s) => (
                  <li key={s.id} className={`standard-row${list?.current === s.id ? ' current' : ''}`} data-standard={s.id} data-testid="standard-row">
                    <span className="catalog-text">
                      <span className="catalog-name"><span data-no-i18n>{s.name}</span>{s.builtin && <span className="badge" data-testid="standard-badge-builtin">Built in</span>}{s.indicative && <span className="badge" title="Drawn from general principles: check it against the norm">Indicative</span>}
                        {list?.current === s.id && <span className="badge on" data-testid="standard-current"><Check size={10} />In use</span>}</span>
                      <span className="muted small" data-no-i18n>{standardMeta(s)}</span>
                      {s.description && <span className="muted small" data-no-i18n>{s.description}</span>}
                    </span>
                    <span className="row">
                      <button type="button" className="btn compact primary" data-action="standard-apply-open" onClick={() => setPanel({ kind: 'apply', id: s.id, name: s.name })}><Wand2 size={12} />Apply…</button>
                      <button type="button" className="icon" title={s.builtin ? 'View' : 'Edit'} aria-label={s.builtin ? 'View' : 'Edit'} data-action="standard-edit" onClick={() => setPanel({ kind: 'edit', id: s.id })}><Pencil size={13} /></button>
                      <button type="button" className="icon" title="Duplicate" aria-label="Duplicate" data-action="standard-duplicate" onClick={() => void duplicate(s)}><Copy size={13} /></button>
                      <button type="button" className="icon" title="Export…" aria-label="Export…" data-action="standard-export" onClick={() => void exportOne(s)}><FileDown size={13} /></button>
                      {!s.builtin && <button type="button" className="icon" title="Remove" aria-label="Remove" data-action="standard-remove" onClick={() => void remove(s)}><Trash2 size={13} /></button>}
                    </span>
                  </li>
                ))}
              </ul>
            </>
          )}
          {panel?.kind === 'edit' && <MaterialStandardEditor id={panel.id} initial={panel.initial} onClose={() => { setPanel(null); void reload(); }}
            onSaved={(id) => { void reload(); setPanel({ kind: 'edit', id }); }} onDuplicate={(id) => { void reload(); setPanel({ kind: 'edit', id }); }} />}
          {panel?.kind === 'apply' && <MaterialStandardApply id={panel.id} name={panel.name} onDone={() => { setPanel(null); void reload(); }} />}
          {panel?.kind === 'import' && <StandardsImportPanel read={panel.read} onDone={() => { setPanel(null); void reload(); }} />}
          {panel?.kind === 'save' && <SaveFromProject onDone={(id) => { void reload(); setPanel(id ? { kind: 'edit', id } : null); }} />}
        </>
      )}
    </Dialog>
  );
}
