import { describe, expect, it } from 'vitest';
import { addPenStep, graphicsOf, hasOwnPens, layerGraphicsJson, penOf, penStepProblem, removePenStep, setPen, setPenStepRange } from './layerPens';

const SCALES = [1, 5, 10, 20, 50, 100, 200, 500];

describe('a layer\'s own pens by scale', () => {
  it('sets the outline and the lines between layers for all scales and per range, and keeps the rest of the graphics', () => {
    let g = setPen({ hatch: 'SOLID' }, null, 'separatorLine', 'weight', 0.7);
    expect(g).toEqual({ hatch: 'SOLID', separatorLine: { weight: 0.7 } });
    g = addPenStep(g, SCALES);
    expect(g.byScale).toEqual([{ from: 100 }]);
    g = setPen(g, 0, 'separatorLine', 'weight', 0.35);
    g = setPen(g, 0, 'separatorLine', 'color', '#ff0000');
    expect(penOf(g, 0, 'separatorLine', 'weight')).toBe(0.35);
    expect(penOf(g, null, 'separatorLine', 'weight')).toBe(0.7);
    expect(layerGraphicsJson(g)).toEqual({ hatch: 'SOLID', separatorLine: { weight: 0.7 }, byScale: [{ from: 100, separatorLine: { weight: 0.35, color: '#ff0000' } }] });
  });

  it('a new range starts after the last one, at the next standard scale', () => {
    let g = addPenStep(null, SCALES);
    g = setPenStepRange(g, 0, 'to', 199)!;
    g = addPenStep(g, SCALES);
    expect(g.byScale![1]).toEqual({ from: 200 });
  });

  it('taking every field back leaves no graphics, and a range that sets nothing is not saved', () => {
    let g = setPen(null, null, 'cutLine', 'weight', 0.5);
    expect(hasOwnPens(g)).toBe(true);
    g = setPen(g, null, 'cutLine', 'weight', undefined);
    expect(g).toBeNull();
    g = addPenStep(null, SCALES);
    expect(hasOwnPens(g)).toBe(false);
    expect(layerGraphicsJson(g)).toBeUndefined();
    expect(removePenStep(g, 0)).toBeNull();
  });

  it('says what a range gives wrong', () => {
    let g = addPenStep(null, SCALES);
    g = setPenStepRange(g, 0, 'to', 50)!;
    expect(penStepProblem(g)?.text).toBe('"From" must not be larger than "to"');
    g = setPenStepRange(g, 0, 'from', undefined)!;
    g = setPenStepRange(g, 0, 'to', undefined)!;
    expect(penStepProblem(g)?.text).toBe('A step needs a scale: from, to, or both');
    expect(penStepProblem(setPenStepRange(g, 0, 'from', 100))).toBeNull();
  });

  it('reads a stored layer\'s graphics', () => {
    expect(graphicsOf(undefined)).toBeNull();
    expect(graphicsOf({})).toBeNull();
    expect(graphicsOf('x')).toBeNull();
    expect(graphicsOf({ cutLine: { weight: 0.13 } })).toEqual({ cutLine: { weight: 0.13 } });
  });
});

describe('the layer pens\' strings', async () => {
  const { LANGUAGES, loadAllLanguages, translate } = await import('../../i18n');
  await loadAllLanguages();
  const { LAYER_PENS_TIP } = await import('./LayerPensEditor');
  const strings = ['Scales', 'All scales', 'Pens by scale', 'Remove the range', 'Add a range of scales', LAYER_PENS_TIP,
    'The first range that holds the view\'s scale wins over “All scales”; empty: the material\'s lines (Manage → Materials).', 'Cut outline', 'Separator lines', 'Weight, mm', 'material\'s'];
  for (const lang of LANGUAGES.map((l) => l.id).filter((l) => l !== 'en')) {
    it(`are translated in ${lang}`, () => {
      for (const s of strings) expect(translate(s, lang), s).not.toBeNull();
    });
  }
});
