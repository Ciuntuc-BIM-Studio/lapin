/**
 * View → Display quality: how the 3D view renders (docs/plan-visualisation.md:
 * as a drawing, medium or realistic; this computer's choice) and how far it
 * lets a heavy part's coarser copy stray on screen (docs/plan-display-precision.md:
 * the project's, one undo step). Both change what the screen draws, never the model.
 */

import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { displayQualityOf, type DisplayQuality } from '../../viewport/lod';
import { setRenderProfile, useRenderProfile, type RenderProfile } from '../../viewport/render3d';

const RENDERING: { id: RenderProfile; label: string; note: string }[] = [
  { id: 'drawing', label: 'Drawing', note: 'The sheet’s palette and an even light: quick on any computer.' },
  { id: 'medium', label: 'Medium', note: 'The sun’s shadows, shade in corners, light from the sky.' },
  { id: 'realistic', label: 'Realistic', note: 'The sky and a ground round the model, glass the light comes through: for presenting, on a good graphics card.' },
];

const CHOICES: { id: DisplayQuality; label: string; note: string }[] = [
  { id: 'fast', label: 'Fast', note: 'Far parts drawn coarser, up to 3 pixels off: for large models and slower computers.' },
  { id: 'balanced', label: 'Balanced', note: 'Far parts drawn coarser where the difference is under a pixel.' },
  { id: 'precise', label: 'Precise', note: 'Every part drawn as it is, near or far.' },
];

export function DisplayQualityDialog() {
  const quality = useSession((s) => displayQualityOf(s.nodes));
  const profile = useRenderProfile((s) => s.profile);
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog quality-dialog" role="dialog" aria-label="Display quality" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Display quality</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <h4 className="eyebrow">Rendering</h4>
          {RENDERING.map((c) => (
            <label key={c.id} className="quality-choice">
              <input type="radio" name="render-profile" checked={profile === c.id} onChange={() => setRenderProfile(c.id)} />
              <span><strong>{c.label}</strong><span className="muted small">{c.note}</span></span>
            </label>
          ))}
          <p className="muted small">Kept on this computer. The sun is the project’s, on the day and hour of View → Sun and shadows.</p>
          <h4 className="eyebrow" style={{ marginTop: 12 }}>Precision</h4>
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
