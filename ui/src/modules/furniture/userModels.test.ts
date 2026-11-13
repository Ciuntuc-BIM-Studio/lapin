import { describe, expect, it } from 'vitest';
import { mergeModels, setZoneEnd, slotRows, toggleZone, userModelInfo, zonePercent, zonesArg, type UserModel } from './models';

const user: UserModel = { id: 'i', name: 'Mine', code: 'user:abc', natural: [2, 1, 0.8], slots: { Fabric: 'upholstery', Foot: 'legs', Seat: 'upholstery' }, zones: { x: [0.2, 0.8] }, types: 0, licence: 'CC0' };

describe('furniture models of the user\'s', () => {
  it('serves as a catalog model by its code, its roles those of its slots', () => {
    const m = userModelInfo(user);
    expect(m.id).toBe('user:abc');
    expect(m.roles).toEqual(['upholstery', 'legs']);
    expect(m.zones).toEqual([[0.2, 0.8], null, null]);
    expect(m.size).toEqual([2, 1, 0.8]);
    expect(m.thumbnail).toBe('');
  });
  it('lists them after Lapin\'s', () => {
    const cat = { models: [], swatches: {}, looks: [] };
    expect(mergeModels(cat, [user])!.models.map((m) => m.id)).toEqual(['user:abc']);
    expect(mergeModels(undefined, [user])).toBeUndefined();
  });
  it('keeps a zone within 0 to 1 and in order', () => {
    expect(setZoneEnd({ x: [0.3, 0.7] }, 'x', 0, 0.9).x).toEqual([0.7, 0.7]);
    expect(setZoneEnd({ x: [0.3, 0.7] }, 'x', 1, 2).x).toEqual([0.3, 1]);
    expect(toggleZone({}, 'y', true).y).toBeDefined();
    expect(toggleZone({ y: [0.1, 0.2] }, 'y', false)).toEqual({});
    expect(zonePercent([0.25, 0.755])).toEqual([25, 76]);
    expect(zonesArg({ x: [0.12345, 0.6] })).toEqual({ x: [0.123, 0.6] });
    expect(slotRows(['A', 'B'], { A: 'top' })).toEqual([['A', 'top'], ['B', 'body']]);
  });
});
