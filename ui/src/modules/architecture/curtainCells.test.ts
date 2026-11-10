import { describe, expect, it } from 'vitest';
import { clearKeys, panelJson } from './curtainCells';

describe('curtain panel types (report #133)', () => {
  it('send their kind\'s own fields only', () => {
    expect(panelJson({ kind: 'empty', thickness: 0.03, offset: 0.1 })).toEqual({ kind: 'empty' });
    expect(panelJson({ kind: 'glass', thickness: 0.024, offset: 0, frameWidth: 0.05, material: '' })).toEqual({ kind: 'glass', thickness: 0.024, offset: 0, material: null });
    expect(panelJson({ kind: 'framed', thickness: 0.03, offset: 0.02, infill: 'solid', material: 'Aluminium' })).toEqual({
      kind: 'framed', thickness: 0.03, offset: 0.02, frameWidth: 0.05, frameDepth: 0.06, inset: 0.01, infill: 'solid', frameMaterial: null, material: 'Aluminium',
    });
  });

  it('reset every cell\'s setting with the cells chosen', () => {
    expect(clearKeys(['panel'], ['panel:0:0', 'panel:1:0'])).toEqual(['panel', 'panel:0:0', 'panel:1:0']);
    expect(clearKeys(['panel:1:0'], ['panel:1:0'])).toEqual(['panel:1:0']);
  });
});
