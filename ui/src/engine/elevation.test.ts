import { describe, expect, it } from 'vitest';
import type { NodeDto } from './protocol';
import { elevationOf, offsetFor, raisedBy } from './elevation';

const node = (type: string, props: Record<string, unknown> = {}): NodeDto => ({ id: type, type, name: type, props, editable: true } as unknown as NodeDto);
const foundations = node('IfcBuildingStorey', { elevation: -1.5, height: 1.5 });
const first = node('IfcBuildingStorey', { elevation: 3, height: 3 });

describe('an element\'s elevation (report #117)', () => {
  it('a floor on the foundations level has its top at the level plus its offset', () => {
    const e = elevationOf(node('IfcSlab', { offset: 0.1 }), foundations, null, ['offset'])!;
    expect(e).toMatchObject({ key: 'offset', of: 'top', absolute: -1.4, offset: 0.1 });
    expect(offsetFor(e, -1.5)).toBe(0);
  });
  it('a column and a wall are at their base, a beam at its top over the level\'s top', () => {
    expect(elevationOf(node('IfcColumn', { baseOffset: -0.2 }), first, null, ['baseOffset'])).toMatchObject({ of: 'base', absolute: 2.8 });
    expect(elevationOf(node('IfcWall'), first, null, ['baseOffset'])).toMatchObject({ key: 'baseOffset', absolute: 3 });
    const beam = elevationOf(node('IfcBeam', { topOffset: -0.3 }), first, null, ['topOffset'])!;
    expect(beam).toMatchObject({ of: 'top', absolute: 5.7 });
    expect(offsetFor(beam, 6)).toBe(0);
  });
  it('a footing is at its type\'s bottom under its level, plus its offset', () => {
    const type = node('IfcFootingType', { bottom: -1.8 });
    const e = elevationOf(node('IfcFooting', { offset: 0.3 }), foundations, type, ['offset'])!;
    expect(e).toMatchObject({ of: 'bottom', origin: -3.3 });
    expect(e.absolute).toBeCloseTo(-3.0, 9);
    expect(offsetFor(e, -3.3)).toBe(0);
  });
  it('has none off a storey or on a class without an offset', () => {
    expect(elevationOf(node('IfcSlab'), null, null, ['offset'])).toBeNull();
    expect(elevationOf(node('IfcDoor'), first, null, [])).toBeNull();
  });
});

describe('an element dragged up in a section (decision 454)', () => {
  it('shows its offset and elevation moved by the drag so far, unchanged with none', () => {
    const beam = elevationOf(node('IfcBeam', { topOffset: -0.3 }), first, null, ['topOffset'])!;
    expect(raisedBy(beam, 0.5)).toMatchObject({ key: 'topOffset', offset: 0.2, absolute: 6.2 });
    expect(raisedBy(beam, 0)).toBe(beam);
  });
});
