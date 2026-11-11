import { useState } from 'react';
import { ImagePlus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { prepareImage } from '../core/OwnImage';
import { LengthField } from '../../shell/cells';
import { PICTURE_HEIGHT, projectKinds } from './entourage';

const COLUMN = 110;

/** The groups a picture kind is in: a car kind's rows are spaced and turned as cars. */
const GROUPS = [{ key: 'person', label: 'People' }, { key: 'car', label: 'Cars' }, { key: 'other', label: 'Other' }] as const;

/**
 * Entourage kinds of the project's own (PR #21, decision 468): each made from a PNG or JPEG in one step (the picture
 * and its kind), its name, height and width in the project's units and its group; a kind its figures use, or its
 * picture, is not deleted (the engine says how many figures use it).
 */
export function EntourageKindsDialog() {
  const nodes = useSession((s) => s.nodes);
  const kinds = projectKinds(nodes);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  const update = (id: string, attrs: Record<string, unknown>) => void execute('element.update', { ids: [id], attrs });

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
        // As tall as a person, as wide as the picture's proportion says.
        const width = Math.min(10, Math.max(0.1, Math.round((PICTURE_HEIGHT / Math.max(0.05, aspect)) * 1000) / 1000));
        await execute('civil.addEntourageKind', {
          data, mime, name: file.name.replace(/\.[^.]+$/, ''), height: PICTURE_HEIGHT, width, category: 'person',
        });
      } catch {
        notify('The picture could not be read.');
      } finally {
        setBusy(false);
      }
    };
    input.click();
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Entourage kinds">
      <div className="dialog" data-testid="entourage-kinds">
        <header className="dialog-head">
          <span className="panel-title">Entourage kinds</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">A PNG with a see-through background stands as a cut-out; each figure of a kind takes its height and width.</p>
          {kinds.length === 0 ? <p className="muted small">No picture kinds yet.</p> : (
            <ul className="type-list">
              <li className="row muted small" style={{ alignItems: 'center', gap: 8 }}>
                <span className="grow">Name</span>
                <span style={{ width: COLUMN }}>Height</span>
                <span style={{ width: COLUMN }}>Width</span>
                <span style={{ width: COLUMN }}>Group</span>
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
                  <LengthField metres={k.height} min={0.2} max={10} style={{ width: COLUMN }} title="Height"
                    onCommit={(m) => { if (m != null && m !== k.height) update(k.id, { height: m }); }} />
                  <LengthField metres={k.width} min={0.1} max={10} style={{ width: COLUMN }} title="Width"
                    onCommit={(m) => { if (m != null && m !== k.width) update(k.id, { width: m }); }} />
                  <select value={k.category} title="Group" style={{ width: COLUMN }} onChange={(e) => update(k.id, { category: e.target.value })}>
                    {GROUPS.map((g) => <option key={g.key} value={g.key}>{g.label}</option>)}
                  </select>
                  <button type="button" className="icon" title="Delete" onClick={() => void execute('element.delete', { ids: [k.id] })}><Trash2 size={13} /></button>
                </li>
              ))}
            </ul>
          )}
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" disabled={busy} onClick={fromPicture}><ImagePlus size={14} /> Add picture…</button>
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
