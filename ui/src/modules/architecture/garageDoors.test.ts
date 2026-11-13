import { describe, expect, it } from 'vitest';
import { garageName, garageOf, isGarage, sizeLabel, withGarageKind } from './garageDoors';

describe('garage doors in the configurator (topic #48)', () => {
  const door = { kind: 'door' as const, columns: [1, 1], rows: [1], mullions: [0], meeting: { kind: 'rebated' }, panels: [{ col: 0, row: 0, operation: 'swing-left' }, { col: 1, row: 0, operation: 'swing-right' }] };

  it('knows a garage door by its panel, only on a door', () => {
    expect(garageOf(door)).toBeNull();
    expect(garageOf({ ...door, panels: [{ col: 0, row: 0, operation: 'roller' }] })).toBe('roller');
    expect(garageOf({ kind: 'window', panels: [{ col: 0, row: 0, operation: 'roller' }] })).toBeNull();
    expect(isGarage('up-and-over')).toBe(true);
    expect(isGarage('door-sliding-left')).toBe(false);
  });

  it('turns a type into a garage door filling its opening with one panel', () => {
    const g = withGarageKind(door, 'sectional-overhead');
    expect(g.columns).toEqual([1]);
    expect(g.rows).toEqual([1]);
    expect(g.panels).toEqual([{ col: 0, row: 0, operation: 'sectional-overhead' }]);
    expect(g.mullions).toBeNull();
    expect(g.meeting).toBeNull();
    expect(garageOf(g)).toBe('sectional-overhead');
  });

  it('names the sizes and the types', () => {
    expect(sizeLabel({ region: 'AU', width: 2.4, height: 2.1 })).toBe('2400 × 2100 · AU');
    expect(sizeLabel({ region: 'EU', width: 2.5, height: 2.125 })).toBe('2500 × 2125 · EU');
    expect(garageName('roller', 4.8, 2.1)).toBe('Roller garage door 4800×2100');
  });
});
