import { describe, expect, it } from 'vitest';
import { glyphBounds, glyphSvg } from './glyph';

describe('a role\'s symbol drawn alone', () => {
  it('takes the box of its lines and texts, and draws them y up as an SVG', () => {
    const ring = { pts: [2, 0, 0, 2, -2, 0, 0, -2], closed: true };
    const bar = { pts: [-2, 0, 2, 0], closed: false, heavy: true };
    expect(glyphBounds([ring, bar])).toEqual([-2, -2, 2, 2]);
    const svg = glyphSvg([ring, bar, { pts: [3, 0], closed: false, text: 'V', h: 2 }]);
    expect(svg).toContain('<polygon');
    expect(svg).toContain('stroke-width="0.5"');
    expect(svg).toContain('>V</text>');
    expect(glyphSvg([])).toBe('');
  });
});
