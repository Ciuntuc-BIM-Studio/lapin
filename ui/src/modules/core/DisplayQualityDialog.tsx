/**
 * View → Display quality (docs/plan-display-precision.md): how far the 3D view
 * lets a heavy part's coarser copy stray on screen. The project's, one undo
 * step; it changes what the screen draws, never the model.
 */

import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { displayQualityOf, type DisplayQuality } from '../../viewport/lod';

const CHOICES: { id: DisplayQuality; label: string; note: string }[] = [
  { id: 'fast', label: 'Fast', note: 'Far parts drawn coarser, up to 3 pixels off: for large models and slower computers.' },
  { id: 'balanced', label: 'Balanced', note: 'Far parts drawn coarser where the difference is under a pixel.' },
  { id: 'precise', label: 'Precise', note: 'Every part drawn as it is, near or far.' },
];

export function DisplayQualityDialog() {
  const quality = useSession((s) => displayQualityOf(s.nodes));
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog quality-dialog" role="dialog" aria-label="Display quality" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Display quality</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">How the 3D view draws parts of many triangles (an imported plant, furniture, pipes) when they are far away. The model, its drawings, exports and quantities keep their full precision.</p>
          {CHOICES.map((c) => (
            <label key={c.id} className="quality-choice">
              <input type="radio" name="display-quality" checked={quality === c.id} onChange={() => void execute('project.setDisplayQuality', { quality: c.id })} />
              <span><strong>{c.label}</strong><span className="muted small">{c.note}</span></span>
            </label>
          ))}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
