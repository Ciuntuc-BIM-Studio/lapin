/** A pen's #rrggbbaa is drawn as its colour at its opacity (material graphics, decision 670). */

import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { DrawingLayer } from './DrawingLayer';

describe('DrawingLayer', () => {
  it('draws an 8-digit stroke and fill as colour plus opacity, not as a broken colour', () => {
    const html = renderToStaticMarkup(
      <svg><DrawingLayer pxPerMetre={100} toScreen={(p) => p}
        drawing={{ scale: 50, pens: [{ layer: 'Hatch', stroke: '#ff000080', width: 0.25, fill: '#00ff0040', loops: [[0, 0, 1, 0, 1, 1]], segments: [0, 0, 1, 1] }] }} /></svg>,
    );
    expect(html).toContain('stroke="#ff0000"');
    expect(html).toMatch(/stroke-opacity="0\.50/);
    expect(html).toContain('fill="#00ff00"');
    expect(html).toMatch(/fill-opacity="0\.25/);
    expect(html).not.toContain('#ff000080');
  });
});
