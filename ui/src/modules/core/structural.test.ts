import { describe, expect, it } from 'vitest';
import { byRule, isStructuralClass, setArgs, sourceText, typeTick, typeTickNext } from './structural';
import { withStructural } from '../architecture/structuralTools';
import { fieldLabel } from '../../shell/conditionGroup';

describe('the Structural tick (docs/plan-structural-usage.md)', () => {
  it('applies to the classes that may bear load and their types, not to doors or coverings', () => {
    for (const c of ['IfcWall', 'IfcSlab', 'IfcRoof', 'IfcColumn', 'IfcBeam', 'IfcFooting', 'IfcMember', 'IfcWallType', 'IfcBeamType']) expect(isStructuralClass(c), c).toBe(true);
    for (const c of ['IfcDoor', 'IfcWindow', 'IfcCovering', 'IfcStair', 'LapinRoomFinish']) expect(isStructuralClass(c), c).toBe(false);
  });

  it('says where its value came from, greyed when the rule decides', () => {
    expect(sourceText({ id: 'w', applies: true, said: true, effective: true, source: 'own' })).toBe('set on this element');
    expect(sourceText({ id: 'w', applies: true, said: false, effective: false, source: 'typePset' })).toBe("from its type's LoadBearing property");
    expect(sourceText({ id: 'w', applies: true, said: null, effective: false, source: 'rule:partition' })).toBe('by rule: a partition, not structural');
    expect(sourceText({ id: 's', applies: true, said: null, effective: true, source: 'rule:class' })).toBe('by rule: its kind is structural');
    expect(byRule({ id: 's', applies: true, said: null, effective: true, source: 'rule:class' })).toBe(true);
    expect(byRule({ id: 's', applies: true, said: false, effective: false, source: 'own' })).toBe(false);
  });

  it('sets, clears or goes back to the rule, on the elements or their type', () => {
    expect(setArgs(['a', 'b'], true)).toEqual({ ids: ['a', 'b'], value: true });
    expect(setArgs(['a'], null)).toEqual({ ids: ['a'], value: null });
    expect(setArgs(['a'], false, true)).toEqual({ ids: ['a'], value: false, onType: true });
  });

  it('a type dialog\'s tick: by rule until said, then structural or not; a click goes yes, then no', () => {
    expect(typeTick(undefined)).toBe('rule');
    expect(typeTick('yes')).toBe('rule');
    expect(typeTick(true)).toBe('yes');
    expect(typeTick(false)).toBe('no');
    expect(typeTickNext('rule')).toBe(true);
    expect(typeTickNext('no')).toBe(true);
    expect(typeTickNext('yes')).toBe(false);
    // Sent on the type itself.
    expect(setArgs(['wt'], typeTickNext('rule'), true)).toEqual({ ids: ['wt'], value: true, onType: true });
  });

  it('the Structure tab draws walls and floors ticked, nothing else, and not when unticked', () => {
    expect(withStructural('architecture.addWall', { x1: 0 }, true)).toEqual({ x1: 0, loadBearing: true });
    expect(withStructural('architecture.addSlab', { points: [] }, true)).toEqual({ points: [], loadBearing: true });
    expect(withStructural('architecture.addWall', { x1: 0 }, false)).toEqual({ x1: 0 });
    expect(withStructural('architecture.addDoor', { x: 1 }, true)).toEqual({ x: 1 });
  });

  it('filters name the facet Structural', () => {
    expect(fieldLabel('@structural')).toBe('Structural');
  });
});
