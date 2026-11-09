import { useState } from 'react';
import { ImagePlus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { prepareImage } from '../core/OwnImage';
import { LengthField } from '../../shell/cells';
import { projectKinds } from './entourage';

const COLUMN = 110;

export function EntourageKindsDialog() {
  const nodes = useSession((s) => s.nodes);
  const kinds = projectKinds(nodes);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });

  const fromPicture = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/png,image/jpeg';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      setBusy(true);
      try {
        const { mime, data, aspect } = await prepareImage(file);
        const image = await execute('image.add', { name: file.name, mime, data }) as { id?: string } | null;
        if (!image?.id) return;
        const height = 1.75;
        const width = Math.min(10, Math.max(0.1, Math.round((height / Math.max(0.05, aspect)) * 1000) / 1000));
        await execute('civil.addEntourageKind', {
          imageId: image.id, name: file.name.replace(/\.[^.]+$/, ''), height, width, category: 'person',
        });
      } catch (e) {
        notify(`The picture could not be read: ${e instanceof Error ? e.message : String(e)}`);
      } finally {
        setBusy(false);
      }
    };
    input.click();
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Entourage kinds">
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">Entourage kinds</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <ul className="type-list">
            <li className="row muted small" style={{ alignItems: 'center', gap: 8 }}>
              <span className="grow">Name</span>
              <span style={{ width: COLUMN }}>Height</span>
              <span style={{ width: COLUMN }}>Width</span>
              <span style={{ width: 20 }} />
            </li>
            {kinds.map((k) => (
              <li key={k.id} className="row" style={{ alignItems: 'center', gap: 8 }}>
                <input
                  className="grow"
                  aria-label="Name"
                  defaultValue={k.name}
                  onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== k.name) void execute('element.rename', { id: k.id, name: v }); }}
                />
                <LengthField
                  metres={k.height}
                  min={0.2}
                  max={10}
                  style={{ width: COLUMN }}
                  title="Height"
                  onCommit={(m) => { if (m != null && m !== k.height) void execute('element.update', { ids: [k.id], attrs: { height: m } }); }}
                />
                <LengthField
                  metres={k.width}
                  min={0.1}
                  max={10}
                  style={{ width: COLUMN }}
                  title="Width"
                  onCommit={(m) => { if (m != null && m !== k.width) void execute('element.update', { ids: [k.id], attrs: { width: m } }); }}
                />
                <button type="button" className="icon" title="Delete" onClick={() => void execute('element.delete', { ids: [k.id] })}><Trash2 size={13} /></button>
              </li>
            ))}
          </ul>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" disabled={busy} onClick={fromPicture}><ImagePlus size={14} /> Add Picture</button>
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
