/**
 * View → Display quality: how the 3D view renders (docs/plan-visualisation.md:
 * as a drawing, medium or realistic; this computer's choice) and how far it
 * lets a heavy part's coarser copy stray on screen (docs/plan-display-precision.md:
 * the project's, one undo step). Both change what the screen draws, never the model.
 */

import { backdropClose } from '../../shell/Dialog';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { displayQualityOf, type DisplayQuality } from '../../viewport/lod';
import { setDiagram, useDiagram } from '../../viewport/diagram';
import { setCameraLook, setRenderProfile, useCameraLook, useRenderProfile, type RenderProfile } from '../../viewport/render3d';

const RENDERING: { id: RenderProfile; label: string; note: string }[] = [
  { id: 'drawing', label: 'Drawing', note: 'The sheet’s palette and an even light: quick on any computer.' },
  { id: 'medium', label: 'Medium', note: 'The sun’s shadows, shade in corners, light from the sky.' },
  { id: 'realistic', label: 'Realistic', note: 'The sky and a ground round the model, glass the light comes through: for presenting, on a good graphics card.' },
  { id: 'diagram', label: 'Diagram', note: 'Soft volumes like a card model, with the sun’s soft shadows and thin light edges: for massing and volume studies.' },
];

const DIAGRAM_VARIANTS: { id: 'white' | 'pastel'; label: string; note: string }[] = [
  { id: 'white', label: 'White model', note: 'Every element a light white-grey.' },
  { id: 'pastel', label: 'Pastel by category', note: 'Walls, floors, roofs, structure, doors and windows, site, greenery each a muted pastel (Preferences → Display edits them).' },
];

const CHOICES: { id: DisplayQuality; label: string; note: string }[] = [
  { id: 'fast', label: 'Fast', note: 'Far parts drawn coarser, up to 3 pixels off: for large models and slower computers.' },
  { id: 'balanced', label: 'Balanced', note: 'Far parts drawn coarser where the difference is under a pixel.' },
  { id: 'precise', label: 'Precise', note: 'Every part drawn as it is, near or far.' },
];

export function DisplayQualityDialog() {
  const quality = useSession((s) => displayQualityOf(s.nodes));
  const profile = useRenderProfile((s) => s.profile);
  const look = useCameraLook();
  const variant = useDiagram((st) => st.variant);
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
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
          {profile === 'diagram' && (
            <div className="quality-sub" data-testid="diagram-variant" role="radiogroup" aria-label="Diagram colours">
              {DIAGRAM_VARIANTS.map((c) => (
                <label key={c.id} className="quality-choice">
                  <input type="radio" name="diagram-variant" checked={variant === c.id} onChange={() => setDiagram({ variant: c.id })} />
                  <span><strong>{c.label}</strong><span className="muted small">{c.note}</span></span>
                </label>
              ))}
              <p className="muted small">Only on screen: drawings, sheets and exports keep their colours. A colour set in Visibility/Graphics still wins.</p>
            </div>
          )}
          <p className="muted small">Kept on this computer. The sun is the project’s, on the day and hour of View → Sun and shadows.</p>
          {profile !== 'drawing' && (
            <dl className="fields" data-testid="camera-look">
              <dt title="Stops over the automatic exposure, which opens up after sunset so the lamps light the rooms">Exposure</dt>
              <dd className="row">
                <input type="range" min={-4} max={4} step={0.5} value={look.ev} onChange={(e) => setCameraLook({ ev: Number(e.target.value) })} style={{ flex: 1 }} aria-label="Exposure" />
                <span className="num" style={{ width: 48 }}>{`${look.ev > 0 ? '+' : ''}${look.ev} EV`}</span>
              </dd>
              <dt title="The light that comes out white: automatic is daylight by day and 4000 K at night, so warm lamps stay warm">White balance</dt>
              <dd className="row">
                <select value={look.wb === 'auto' ? 'auto' : 'fixed'} onChange={(e) => setCameraLook({ wb: e.target.value === 'auto' ? 'auto' : 5000 })} aria-label="White balance">
                  <option value="auto">Automatic</option>
                  <option value="fixed">Colour temperature</option>
                </select>
                {look.wb !== 'auto' && <>
                  <input type="range" min={2000} max={10000} step={100} value={look.wb} onChange={(e) => setCameraLook({ wb: Number(e.target.value) })} style={{ flex: 1 }} aria-label="Colour temperature" />
                  <span className="num" style={{ width: 56 }}>{`${look.wb} K`}</span>
                </>}
              </dd>
            </dl>
          )}
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
