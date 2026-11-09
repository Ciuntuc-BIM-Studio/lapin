import { useState } from 'react';
import { ImagePlus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { prepareImage } from '../core/OwnImage';
import { projectKinds } from './entourage';

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
    <div className="dialog-backdrop" role="dialog" aria-label="Entourage Kinds">
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">Entourage Kinds</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <ul className="type-list">
            {kinds.map((k) => (
              <li key={k.id} className="row" style={{ alignItems: 'center', gap: 8 }}>
                <input
                  className="grow"
                  aria-label="Name"
                  defaultValue={k.name}
                  onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== k.name) void execute('element.rename', { id: k.id, name: v }); }}
                />
                <input
                  type="number"
                  step={10}
                  style={{ width: 84 }}
                  aria-label="Height"
                  defaultValue={Math.round(k.height * 1000)}
                  onBlur={(e) => {
                    const v = Number(e.target.value);
                    if (Number.isFinite(v) && v >= 200 && v <= 10000 && v !== Math.round(k.height * 1000)) void execute('element.update', { ids: [k.id], attrs: { height: v / 1000 } });
                  }}
                />
                <input
                  type="number"
                  step={10}
                  style={{ width: 84 }}
                  aria-label="Width"
                  defaultValue={Math.round(k.width * 1000)}
                  onBlur={(e) => {
                    const v = Number(e.target.value);
                    if (Number.isFinite(v) && v >= 100 && v <= 10000 && v !== Math.round(k.width * 1000)) void execute('element.update', { ids: [k.id], attrs: { width: v / 1000 } });
                  }}
                />
                <button type="button" className="icon" title="Delete" onClick={() => void execute('element.delete', { ids: [k.id] })}><Trash2 size={13} /></button>
              </li>
            ))}
            <li>
              <button type="button" className="tree-item" disabled={busy} onClick={fromPicture}>
                <ImagePlus size={12} /> From Picture
              </button>
            </li>
          </ul>
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
