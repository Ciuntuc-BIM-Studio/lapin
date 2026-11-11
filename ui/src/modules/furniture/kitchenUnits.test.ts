import { describe, expect, it } from 'vitest';
import { addUnit, valueOf, fieldsFor, findType, fixedSizes, moveUnit, removeUnit, setField, unitsTotal, widthOptions, type KitchenUnit, type UnitsCatalog } from './kitchenUnits';

// The engine's catalog, cut down to what these steps use.
const cat: UnitsCatalog = {
  steps: [0.15, 0.2, 0.3, 0.4, 0.45, 0.5, 0.6, 0.8],
  typical: [],
  types: [
    {
      key: 'base', label: 'Base unit', line: 'base', min: 0.1, max: 0.8, tall: false, defaults: { width: 0.6, front: 'drawerDoor' },
      fields: [
        { key: 'front', label: 'Front', kind: 'choice', choices: ['drawerDoor', 'doors', 'drawers'] },
        { key: 'doors', label: 'Doors', kind: 'number', min: 1, max: 2, when: { key: 'front', values: ['doors'] } },
        { key: 'drawers', label: 'Drawers', kind: 'number', min: 1, max: 4, when: { key: 'front', values: ['drawers'] } },
      ],
    },
    { key: 'dishwasher', label: 'Dishwasher', line: 'base', min: 0.45, max: 0.6, tall: false, sizes: [0.45, 0.6], defaults: { width: 0.6 }, fields: [] },
    { key: 'hob', label: 'Hob over an oven', line: 'base', min: 0.6, max: 0.9, tall: false, sizes: [0.6, 0.8, 0.9], defaults: { width: 0.6, fuel: 'induction' }, fields: [{ key: 'fuel', label: 'Hob', kind: 'choice', choices: ['induction', 'gas'] }] },
    { key: 'wall', label: 'Wall unit', line: 'upper', min: 0.1, max: 0.8, tall: false, defaults: { width: 0.6, front: 'doors' }, fields: [{ key: 'height', label: 'Height', kind: 'length', min: 0.2, max: 1.4 }] },
  ],
};

describe('a kitchen’s units (report #76)', () => {
  const run: KitchenUnit[] = [{ type: 'base', width: 0.4 }, { type: 'dishwasher', width: 0.45 }, { type: 'hob', width: 0.6 }];

  it('adds their widths', () => {
    expect(unitsTotal(run)).toBe(1.45);
    expect(unitsTotal([])).toBe(0);
  });

  it('offers a cabinet the standard widths and an appliance its sizes', () => {
    expect(widthOptions(findType(cat, 'base', false), cat.steps)).toEqual([0.15, 0.2, 0.3, 0.4, 0.45, 0.5, 0.6, 0.8]);
    expect(widthOptions(findType(cat, 'dishwasher', false), cat.steps)).toEqual([0.45, 0.6]);
    expect(fixedSizes(findType(cat, 'dishwasher', false))).toBe(true);
    expect(fixedSizes(findType(cat, 'base', false))).toBe(false);
    // The lines keep their own types.
    expect(findType(cat, 'wall', false)).toBeUndefined();
    expect(findType(cat, 'wall', true)?.label).toBe('Wall unit');
  });

  it('shows the fields its front uses', () => {
    const t = findType(cat, 'base', false);
    expect(fieldsFor({ type: 'base', width: 0.6 }, t).map((f) => f.key)).toEqual(['front']);
    expect(fieldsFor({ type: 'base', width: 0.6, front: 'drawers' }, t).map((f) => f.key)).toEqual(['front', 'drawers']);
  });

  it('shows what a field is when the unit does not set it, as the engine builds it', () => {
    const t = findType(cat, 'base', false);
    const doors = t!.fields.find((f) => f.key === 'doors')!;
    expect(valueOf({ type: 'base', width: 0.8, front: 'doors' }, doors, t)).toBe(2);
    expect(valueOf({ type: 'base', width: 0.6, front: 'doors' }, doors, t)).toBe(1);
    expect(valueOf({ type: 'base', width: 0.8, front: 'doors', doors: 1 }, doors, t)).toBe(1);
    expect(valueOf({ type: 'base', width: 0.6 }, t!.fields[0], t)).toBe('drawerDoor');
  });

  it('adds, moves and takes out units', () => {
    const added = addUnit(run, findType(cat, 'base', false)!, 0);
    expect(added.map((u) => u.type)).toEqual(['base', 'base', 'dishwasher', 'hob']);
    expect(added[1]).toEqual({ type: 'base', width: 0.6, front: 'drawerDoor' });
    expect(moveUnit(run, 1, 1).map((u) => u.type)).toEqual(['base', 'hob', 'dishwasher']);
    expect(moveUnit(run, 0, -1)).toBe(run);
    expect(removeUnit(run, 1).map((u) => u.type)).toEqual(['base', 'hob']);
  });

  it('changes a unit’s type from the new type’s defaults, keeping its width when the new type takes it', () => {
    const asBase = setField(run, 2, 'type', 'base', cat, false);
    expect(asBase[2]).toEqual({ type: 'base', width: 0.6, front: 'drawerDoor' });
    // A 400 base unit made a dishwasher: 400 is not a dishwasher's size, so it takes its default 600.
    expect(setField(run, 0, 'type', 'dishwasher', cat, false)[0]).toEqual({ type: 'dishwasher', width: 0.6 });
    // A front changed: the fields it no longer uses go.
    const drawers = setField([{ type: 'base', width: 0.6, front: 'doors', doors: 2 }], 0, 'front', 'drawers', cat, false);
    expect(drawers[0]).toEqual({ type: 'base', width: 0.6, front: 'drawers' });
  });
});
