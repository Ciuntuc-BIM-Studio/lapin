import { describe, expect, it } from 'vitest';
import { nextVisualStyle } from './visualStyle';

describe('visual style', () => {
  it('cycles shaded, edges, X-ray, wireframe and round', () => {
    expect(nextVisualStyle('shaded')).toBe('edges');
    expect(nextVisualStyle('edges')).toBe('xray');
    expect(nextVisualStyle('xray')).toBe('wireframe');
    expect(nextVisualStyle('wireframe')).toBe('shaded');
  });
});
