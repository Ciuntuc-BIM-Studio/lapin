/** The terrain brush's settings: the radius of a stroke, how much it raises or lowers, the level it flattens to (blank: the ground where you click), how hard it smooths. */

import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { setBrush, useBrush } from './prefs';
import { LengthField } from '../../shell/cells';

export function BrushDialog() {
  const b = useBrush();
  const close = () => useUi.setState({ dialog: null });
  const num = (t: string) => Number(t.replace(',', '.'));
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Terrain brush" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Terrain brush</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <dl className="fields">
            <dt>Radius</dt><dd><LengthField metres={b.radius} min={0.25} onCommit={(m) => { if (m != null) setBrush({ radius: m }); }} /></dd>
            <dt>Raise / lower by</dt><dd><LengthField metres={b.amount} min={0.01} onCommit={(m) => { if (m != null) setBrush({ amount: m }); }} /></dd>
            <dt>Flatten to</dt><dd><LengthField metres={b.level} blank placeholder="the ground at the click" onCommit={(m) => setBrush({ level: m })} /></dd>
            <dt>Smoothing</dt><dd><input type="range" min={0.1} max={1} step={0.1} value={b.strength} onChange={(e) => setBrush({ strength: num(e.target.value) })} /> <span className="num">{Math.round(b.strength * 100)}%</span></dd>
          </dl>
          <p className="muted small">Each click of a brush tool is one stroke, undone with Ctrl Z; a terrain's strokes can be cleared in the inspector.</p>
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
