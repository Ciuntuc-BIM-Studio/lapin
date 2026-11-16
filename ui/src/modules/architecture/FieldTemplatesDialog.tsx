/**
 * New field schedule from a template: one the modules ship (audit S1: the fixed schedules' tables as field schedules), a
 * .lapinschedule kept on this computer, one of the firm's folders, or a file.
 * A template names the types, storeys, materials, groups and phases of its filter, so it works in another project.
 */

import { backdropClose } from '../../shell/Dialog';
import { useEffect, useState } from 'react';
import { FileUp, HardDriveDownload, Package, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute } from '../../engine/session';
import { pickFile } from '../../engine/files';
import { useLibraryFiles, type ComputerFile } from '../../platform/libraries';
import { FirmFolderFiles } from '../core/FirmFolderFiles';
import { forgetTemplate, keptTemplates, shippedTemplates, TEMPLATE_EXT, type ShippedTemplate } from '../../shell/fieldSchedule';
import { openSchedule } from '../cost/schedules';

/** The schedule a template makes, opened; a sentence in a notice when the engine refuses. */
async function make(text: string, builtin?: string): Promise<boolean> {
  const r = await execute('architecture.importSchedule', builtin ? { builtin } : { text }) as { id?: string } | null;
  if (!r?.id) return false;
  openSchedule(r.id);
  return true;
}

export function FieldTemplatesDialog() {
  const version = useLibraryFiles((s) => s.version);
  const [kept, setKept] = useState<ComputerFile[]>([]);
  const [shipped, setShipped] = useState<ShippedTemplate[]>([]);
  useEffect(() => { void keptTemplates().then(setKept); }, [version]);
  useEffect(() => { void shippedTemplates().then(setShipped); }, []);
  const close = () => useUi.setState({ dialog: null });
  const fromFile = async () => {
    const f = await pickFile('Schedule template (.lapinschedule)', [TEMPLATE_EXT, 'json']);
    if (f && !(await make(new TextDecoder().decode(f.bytes)))) notify('That file is not a schedule template.');
  };
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="New schedule from a template" data-testid="field-templates-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">New schedule from a template</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">A template is a field schedule's setup: its filter, grouping and columns. Save one from a field schedule (Template…), then make a schedule from it in any project.</p>
          {shipped.length > 0 && <>
            <h4 className="eyebrow"><Package size={12} /> Shipped with Lapin</h4>
            <p className="muted small">The schedules the modules ship, as field schedules: make one, then change it as you like or keep it as your own template.</p>
            <ul className="plain-list" data-testid="shipped-templates">
              {shipped.map((t) => (
                <li key={t.id}><button type="button" className="link" data-shipped={t.id} onClick={() => void make('', t.id)}>{t.label}</button></li>
              ))}
            </ul>
          </>}
          <h4 className="eyebrow"><HardDriveDownload size={12} /> On this computer</h4>
          {kept.length === 0 ? <p className="muted small">No template kept yet.</p> : (
            <ul className="plain-list">
              {kept.map((f) => (
                <li key={f.id}>
                  <button type="button" className="link" data-template={f.name} data-no-i18n onClick={() => void make(f.text)}>{f.name}</button>
                  <span className="muted small" data-no-i18n>{f.at}</span><span className="grow" />
                  <button type="button" className="icon mini" title="Forget it on this computer" onClick={() => void forgetTemplate(f.name)}><Trash2 size={12} /></button>
                </li>
              ))}
            </ul>
          )}
          <div className="row"><button type="button" className="btn" data-testid="template-from-file" onClick={() => void fromFile()}><FileUp size={14} />From a file…</button></div>
          <FirmFolderFiles ext={TEMPLATE_EXT} onPick={async (f) => { if (!(await make(new TextDecoder().decode(f.bytes)))) notify('That file is not a schedule template.'); }}
            save={{ disabled: kept.length === 0, choices: kept.map((f) => f.name), get: async (name) => { const f = kept.find((x) => x.name === name); return f ? { text: f.text, name: f.name } : null; } }} />
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn" onClick={close}>Close</button></footer>
      </div>
    </div>
  );
}
