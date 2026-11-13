/** A line type drawn: its dashes (paper mm) across a short line at a scale of pixels to a millimetre. */

import { dashSegments } from './lineTypeModel';

export function LineSample({ dashes, width = 150, scale = 4, stroke = 1.6 }: { dashes: number[]; width?: number; scale?: number; stroke?: number }) {
  const segs = dashes.length >= 2 ? dashSegments(dashes, width / scale) : [[0, width / scale] as [number, number]];
  const d = segs.map(([a, b]) => `M${(a * scale).toFixed(2)} 6H${(b * scale).toFixed(2)}`).join('');
  return (
    <svg className="line-sample" width={width} height={12} viewBox={`0 0 ${width} 12`} aria-hidden="true" data-no-i18n>
      <path d={d} stroke="currentColor" strokeWidth={stroke} fill="none" />
    </svg>
  );
}
