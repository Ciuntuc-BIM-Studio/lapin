/** The drawing's scale and how big it shows against paper; a click brings it to paper size (ZA). */

import { actualScale } from './zoom';

export function ScaleChip({ pxPerMetre, scale, onActual }: { pxPerMetre: number; scale: number; onActual: () => void }) {
  const pct = Math.round((pxPerMetre / actualScale(scale)) * 100);
  return (
    <button type="button" className="chip scale" title="The view's scale, and its size on screen against paper · click for paper size (ZA)" onClick={onActual}>
      1:{scale} · {pct} %
    </button>
  );
}
