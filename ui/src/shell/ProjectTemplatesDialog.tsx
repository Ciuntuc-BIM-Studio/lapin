/**
 * Project templates (issues #9 and #20, decision 470). File → Save as template… keeps the open project's set-up on this
 * computer under a name; File → New from template… lists the templates (Lapin's default first) to start a project from, to
 * make one the default File → New uses, to rename, export, delete or import. Templates are .lapin files: one exported goes
 * to another computer or an office as it is, and any project's file can be imported (its model is left out).
 */

import { useEffect, useState } from 'react';
import { Download, FilePlus2, FileUp, Pencil, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../app/store';
import { setPrefs, usePrefs } from '../app/prefs';
import { engine, engineInBrowser, useSession } from '../engine/session';
import { enter, newProject, pickFile, pickPath, projectTitle, saveFile, toBase64 } from '../engine/files';
import { deleteTemplate, importTemplate, templateAdditions, refreshTemplates, renameTemplate, saveAsTemplate, startFromTemplate, startFromTemplateFile, templateData, useTemplates, type TemplateInfo } from '../engine/templates';

const FILTERS = [{ name: 'Lapin project or template', extensions: ['lapin'] }];
const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
const close = () => useUi.setState({ dialog: null });
const when = (iso: string) => new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
const size = (bytes: number) => (bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`);

/** What a template keeps, said once for both dialogs. */
const KEEPS = 'A template keeps the project’s set-up: its types, materials, units and drafting standards, storeys, views, sheets and title blocks. It keeps no element: no wall, no drawing, no issue.';

/** A new project, the open one's unsaved changes asked about first. */
async function startWith(run: () => Promise<void>): Promise<void> {
  const doc = useSession.getState().doc;
  if (doc?.dirty && !window.confirm('The project has unsaved changes. Start a new one anyway?')) return;
  try {
    await run();
    close();
    enter();
  } catch (e) { notify(message(e)); }
}

/** File → Save as template…: the open project's set-up under a name. */
export function SaveTemplateDialog() {
  const doc = useSession((s) => s.doc);
  const [name, setName] = useState(() => projectTitle(doc?.path));
  const [asDefault, setAsDefault] = useState(false);
  const [busy, setBusy] = useState(false);
  // What modules offer to put in too (my furniture library, decision 476), each its own check box, off until ticked.
  const additions = templateAdditions.filter((a) => a.shown());
  const [added, setAdded] = useState<Record<string, boolean>>({});
  const save = async () => {
    setBusy(true);
    try {
      const extra: Record<string, unknown> = {};
      for (const a of additions) if (added[a.id]) Object.assign(extra, await a.params());
      let id = await saveAsTemplate(name, false, extra);
      if (id === 'exists') {
        if (!window.confirm('A template has this name on this computer. Replace it?')) { setBusy(false); return; }
        id = await saveAsTemplate(name, true, extra);
      }
      if (asDefault && id !== 'exists') setPrefs({ defaultTemplate: id });
      notify(asDefault ? 'Template saved: new projects start from it.' : 'Template saved: File → New from template… starts a project from it.');
      close();
    } catch (e) { notify(message(e)); setBusy(false); }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Save as template" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="dialog" data-testid="save-template" style={{ width: 460 }}>
        <header className="dialog-head">
          <span className="panel-title">Save as template</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="small">{KEEPS}</p>
          <dl className="fields">
            <dt>Name</dt>
            <dd><input type="text" value={name} autoFocus data-field="template-name" data-no-i18n onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter' && name.trim()) void save(); }} /></dd>
          </dl>
          <label className="row small"><input type="checkbox" data-field="template-default" checked={asDefault} onChange={(e) => setAsDefault(e.target.checked)} /> Start every new project from it (File → New project)</label>
          {additions.map((a) => (
            <label key={a.id} className="row small"><input type="checkbox" data-field={`template-add-${a.id}`} checked={!!added[a.id]} onChange={(e) => setAdded({ ...added, [a.id]: e.target.checked })} /> {a.label}</label>
          ))}
          <p className="muted small">{engineInBrowser ? 'Kept in this browser. Export it from File → New from template… to keep a file.' : 'Kept on this computer, in your Lapin folder. Export it to share it with an office.'}</p>
        </div>
        <footer className="dialog-foot"><span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-field="template-save" disabled={!name.trim() || busy} onClick={() => void save()}>Save</button>
        </footer>
      </div>
    </div>
  );
}

function Row({ t, isDefault }: { t: TemplateInfo; isDefault: boolean }) {
  const exportIt = async () => {
    try {
      if (engineInBrowser) {
        await saveFile(Uint8Array.from(atob(await templateData(t.id)), (c) => c.charCodeAt(0)), `${t.id}.lapin`, 'application/zip');
        return;
      }
      const path = await pickPath('save', `${t.id}.lapin`, FILTERS);
      if (!path) return;
      await engine.call('templates.export', { id: t.id, path: path.endsWith('.lapin') ? path : `${path}.lapin` });
      notify(`Exported ${path.split(/[\\/]/).pop()}.`);
    } catch (e) { notify(message(e)); }
  };
  const rename = async () => {
    const name = window.prompt('The template’s new name:', t.name)?.trim();
    if (!name || name === t.name) return;
    try {
      const id = await renameTemplate(t.id, name);
      if (isDefault) setPrefs({ defaultTemplate: id });
    } catch (e) { notify(message(e)); }
  };
  const remove = async () => {
    if (!window.confirm(`Delete the template „${t.name}” from this computer? Projects made from it keep everything.`)) return;
    try {
      await deleteTemplate(t.id);
      if (isDefault) setPrefs({ defaultTemplate: null });
    } catch (e) { notify(message(e)); }
  };
  return (
    <div className="recent" data-template-row={t.id}>
      <input type="radio" name="default-template" title="Start new projects from it" checked={isDefault} onChange={() => setPrefs({ defaultTemplate: t.id })} />
      <span className="recent-body">
        <span className="recent-name" data-no-i18n>{t.name}</span>
        <span className="muted small" data-no-i18n>{when(t.at)} · {size(t.size)}</span>
      </span>
      <button type="button" className="btn primary" onClick={() => void startWith(() => startFromTemplate(t.id))}>New project</button>
      <button type="button" className="icon" title="Rename" onClick={() => void rename()}><Pencil size={14} /></button>
      <button type="button" className="icon" title="Export as a .lapin file" onClick={() => void exportIt()}><Download size={14} /></button>
      <button type="button" className="icon" title="Delete" onClick={() => void remove()}><Trash2 size={14} /></button>
    </div>
  );
}

/** File → New from template…: the templates on this computer, Lapin's default first. */
export function ProjectTemplatesDialog() {
  const { list, read } = useTemplates();
  const chosen = usePrefs((p) => p.defaultTemplate);
  const connected = useSession((s) => s.connected);
  useEffect(() => { if (connected) void refreshTemplates(); }, [connected]);
  const isDefault = (id: string | null) => (id === null ? !chosen || !list.some((t) => t.id === chosen) : chosen === id);
  const importIt = async () => {
    try {
      let r: string | 'exists';
      let file: { path?: string; data?: string };
      let name: string;
      if (engineInBrowser) {
        const picked = await pickFile('Lapin project or template', 'lapin');
        if (!picked) return;
        file = { data: toBase64(picked.bytes) };
        name = picked.name.replace(/\.lapin$/i, '');
      } else {
        const path = await pickPath('open', undefined, FILTERS);
        if (!path) return;
        file = { path };
        name = projectTitle(path);
      }
      r = await importTemplate(file, name);
      if (r === 'exists') {
        if (!window.confirm('A template has this name on this computer. Replace it?')) return;
        r = await importTemplate(file, name, true);
      }
      notify('Template imported.');
    } catch (e) { notify(message(e)); }
  };
  const fromFile = async () => {
    if (engineInBrowser) {
      const picked = await pickFile('Lapin project or template', 'lapin');
      if (picked) await startWith(() => startFromTemplateFile({ data: toBase64(picked.bytes) }));
      return;
    }
    const path = await pickPath('open', undefined, FILTERS);
    if (path) await startWith(() => startFromTemplateFile({ path }));
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Project templates" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="dialog" data-testid="project-templates" style={{ width: 700 }}>
        <header className="dialog-head">
          <span className="panel-title">Project templates</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="small">{KEEPS}</p>
          <p className="small">The one chosen on the left is what File → New project starts from.</p>
          <div className="recent" data-template-row="lapin-default">
            <input type="radio" name="default-template" title="Start new projects from it" checked={isDefault(null)} onChange={() => setPrefs({ defaultTemplate: null })} />
            <span className="recent-body">
              <span className="recent-name">Lapin default</span>
              <span className="muted small">Lapin’s own start: its storeys, plans and styles</span>
            </span>
            <button type="button" className="btn primary" onClick={() => void newProject(undefined, true).then((made) => { if (made) close(); })}>New project</button>
          </div>
          {list.map((t) => <Row key={t.id} t={t} isDefault={isDefault(t.id)} />)}
          {read && list.length === 0 && <p className="muted small">None yet: set up a project, then File → Save as template…</p>}
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" onClick={() => void importIt()}><FileUp size={14} />Import…</button>
          <button type="button" className="btn" title="A new project from a template or project file, without keeping it here" onClick={() => void fromFile()}><FilePlus2 size={14} />New from a file…</button>
          <span className="grow" />
          <button type="button" className="btn" disabled={!useSession.getState().doc} onClick={() => useUi.setState({ dialog: 'saveTemplate' })}>Save as template…</button>
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
