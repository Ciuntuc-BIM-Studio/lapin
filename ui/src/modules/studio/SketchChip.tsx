/**
 * The chip over a constrained sketch's view: how many degrees of freedom it has left, or that it is fully constrained, with
 * the button that shows or hides its constraints (remembered per view). Shown while the view has constraints or a constraint
 * tool is on.
 */

import { Eye, EyeOff } from 'lucide-react';
import { useUi } from '../../app/store';
import { chipOf } from '../../viewport/cad/constraints';
import { useCad } from '../../viewport/cad/session';
import { setConstraintsHidden, useSketch } from '../../viewport/cad/sketchStore';
import { constraintCommandOn } from './constraintTools';

export function SketchChip() {
  const view = useUi((s) => s.view);
  const shown = useSketch((s) => s.shown);
  const sketch = useSketch((s) => (s.shown ? s.views[s.shown] : undefined));
  const hidden = useSketch((s) => (s.shown ? !!s.hidden[s.shown] : false));
  const toolOn = useCad((s) => !!s.command && constraintCommandOn());
  if ((view !== 'plan' && view !== 'drawing') || !shown) return null;
  if (!sketch?.constraints.length && !toolOn) return null;
  const chip = chipOf(sketch?.status ?? null);
  return (
    <div className={`sketch-chip ${chip.tone}`} role="status" data-testid="sketch-chip" data-tone={chip.tone}>
      <span className="sketch-chip-dot" />
      <span data-testid="sketch-chip-text">{chip.text}</span>
      <button type="button" className="sketch-chip-eye" data-testid="sketch-constraints-toggle" aria-pressed={!hidden} title={hidden ? 'Show the constraints' : 'Hide the constraints'}
        onClick={() => setConstraintsHidden(shown, !hidden)}>{hidden ? <EyeOff size={13} /> : <Eye size={13} />}</button>
    </div>
  );
}
