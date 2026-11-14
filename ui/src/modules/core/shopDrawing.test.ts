import { describe, expect, it } from 'vitest';
import { axisRange, clampStation, defaultChoices, drawableElement, drawingArgs, mm, newSection, nothingChosen, type RulesDto } from './shopDrawing';

const rules: RulesDto = {
  rule: 'steel-member', projection: 'iso-e', drawing: null,
  rules: [
    { id: 'steel-member', name: 'Steel member', views: ['front', 'top'], iso: true, dims: 'overall', sections: [{ role: 'section:x', station: 9 }] },
    { id: 'plain', name: 'Plain', views: ['left'], iso: false, dims: 'none', sections: [] },
  ],
  frame: { length: 3, width: 0.2, height: 0.2, min: [0, -0.1, -0.1], max: [3, 0.1, 0.1] },
};

describe('the shop drawing dialog', () => {
  it('takes its defaults from the rule and the project projection, so Create can be pressed at once', () => {
    const c = defaultChoices(rules);
    expect(c).toMatchObject({ rule: 'steel-member', views: ['front', 'top'], iso: true, scale: null, paper: null, projection: 'iso-e', dims: 'overall', prefer: 'paper' });
    expect(c.sections).toEqual([{ role: 'section:x', station: 3 }]);
  });

  it('changing the rule takes its views and keeps the page choices', () => {
    const c = defaultChoices(rules, 'plain', { scale: 10, paper: 'A3', projection: 'third-angle', dims: 'overall', prefer: 'scale' });
    expect(c).toMatchObject({ rule: 'plain', views: ['left'], iso: false, scale: 10, paper: 'A3', projection: 'third-angle', dims: 'overall', prefer: 'scale' });
  });

  it('keeps a section\'s station inside the frame along its axis', () => {
    expect(axisRange(rules.frame, 'section:y')).toEqual([-0.1, 0.1]);
    expect(clampStation(rules.frame, 'section:x', -2)).toBe(0);
    expect(clampStation(rules.frame, 'section:z', 5)).toBe(0.1);
    expect(clampStation(rules.frame, 'section:x', Number.NaN)).toBe(1.5);
    expect(newSection(rules.frame, 'section:x')).toEqual({ role: 'section:x', station: 1.5 });
  });

  it('builds the arguments: ticked faces in order, nulls for auto, the drawing\'s id when it exists', () => {
    const c = { ...defaultChoices(rules), views: ['top', 'front'] as ('top' | 'front')[] };
    expect(drawingArgs('e1', c)).toEqual({
      element: 'e1', rule: 'steel-member', views: ['front', 'top'], sections: [{ role: 'section:x', station: 3 }],
      iso: true, scale: null, paper: null, projection: 'iso-e', dims: 'overall', prefer: 'paper',
    });
    expect(drawingArgs('e1', c, 'd1').id).toBe('d1');
  });

  it('writes lengths in millimetres and notices an empty choice', () => {
    expect(mm(1.2345)).toBe('1235 mm');
    expect(nothingChosen({ ...defaultChoices(rules), views: [], iso: false, sections: [] })).toBe(true);
    expect(nothingChosen(defaultChoices(rules))).toBe(false);
  });

  it('draws one element of the model, never a sheet, a view or the structure', () => {
    const nodes = new Map([['m', { type: 'IfcMember' }], ['s', { type: 'LapinSheet' }], ['st', { type: 'IfcBuildingStorey' }]]);
    expect(drawableElement(['m'], nodes)).toBe('m');
    expect(drawableElement(['s'], nodes)).toBeNull();
    expect(drawableElement(['st'], nodes)).toBeNull();
    expect(drawableElement(['m', 'm'], nodes)).toBeNull();
    expect(drawableElement([], nodes)).toBeNull();
  });
});
