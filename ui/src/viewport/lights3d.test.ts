import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import { CAPS, chooseLights, kelvinRgb, LightRig, panelNits, planckXy, pointCandela, poolOf, profileCandela, spotCandela, whiteBalanceGains, type LightProfileDto, type LightSourceDto } from './lights3d';
import { exposureGain, NIGHT_GAIN, nightOf, whiteBalanceKelvin } from './render3d';

const src = (id: string, x: number, lumens = 1000, extra: Partial<LightSourceDto> = {}): LightSourceDto => ({
  id, kind: 'point', lumens, kelvin: 3000, beam: 60, field: 90, width: 0.6, length: 0.6, profile: null, on: true,
  position: [x, 0, 2.5], direction: [0, 0, -1], across: [1, 0, 0], ...extra,
});

describe('colour temperature (docs/plan-render.md, S2)', () => {
  it('lies on Planck’s locus: illuminant A at 2856 K, the locus at 6500 K', () => {
    const [xa, ya] = planckXy(2856);
    expect(xa).toBeCloseTo(0.4476, 2);
    expect(Math.abs(ya - 0.4074)).toBeLessThan(0.001);
    const [x65, y65] = planckXy(6500);
    expect(Math.abs(x65 - 0.3135)).toBeLessThan(0.001);
    expect(Math.abs(y65 - 0.3236)).toBeLessThan(0.001);
  });

  it('is warm at 2700 K, cold at 10 000 K, held to 1000–12 000 K', () => {
    const warm = kelvinRgb(2700);
    expect(warm[0]).toBe(1);
    expect(warm[2]).toBeLessThan(0.5);
    const cold = kelvinRgb(10000);
    expect(cold[2]).toBe(1);
    expect(cold[0]).toBeLessThan(0.85);
    expect(kelvinRgb(30000)).toEqual(kelvinRgb(12000));
    expect(kelvinRgb(100)).toEqual(kelvinRgb(1000));
  });

  it('a white balance makes its own temperature white and keeps the brightness', () => {
    const g = whiteBalanceGains(3000);
    const [r, gg, b] = kelvinRgb(3000);
    const out = [r * g[0], gg * g[1], b * g[2]];
    expect(out[0] / out[2]).toBeCloseTo(kelvinRgb(6504)[0] / kelvinRgb(6504)[2], 4);
    expect(0.2126 * g[0] + 0.7152 * g[1] + 0.0722 * g[2]).toBeCloseTo(1, 6);
    expect(whiteBalanceGains(6504).every((x) => Math.abs(x - 1) < 1e-6)).toBe(true);
  });
});

describe('lumens into intensity', () => {
  it('an even light is Φ / 4π candela', () => expect(pointCandela(1000)).toBeCloseTo(79.577, 3));
  it('a spot spreads its lumens in its field: Φ / (2π (1 − cos θ/2))', () => {
    expect(spotCandela(1000, 60)).toBeCloseTo(1000 / (2 * Math.PI * (1 - Math.cos(Math.PI / 6))), 6);
    // A half sphere (180°): Φ / 2π.
    expect(spotCandela(1000, 179)).toBeGreaterThan(1000 / (2 * Math.PI) * 0.99);
    expect(spotCandela(1000, 30)).toBeGreaterThan(spotCandela(1000, 60));
  });
  it('a panel is a Lambertian emitter: Φ / (π w l) nits', () => expect(panelNits(3600, 0.6, 0.6)).toBeCloseTo(3600 / (Math.PI * 0.36), 6));
  it('a profile’s peak is the even light’s times its scale', () => expect(profileCandela(1000, 12)).toBeCloseTo((1000 / (4 * Math.PI)) * 12, 6));
});

describe('the budget', () => {
  it('draws the nearest and brightest first, the first ones with shadows, none past the radius or off', () => {
    const list = [src('far', 50), src('near', 1), src('mid', 5), src('bright', 8, 20000), src('off', 0.5, 1000, { on: false })];
    const c = chooseLights(list, [0, 0, 1.6], { spotShadow: 0, pointShadow: 1, spot: 0, point: 2, rect: 0, radius: 30 });
    expect(c.total).toBe(4);
    expect(c.chosen.map((x) => [x.source.id, x.pool])).toEqual([['near', 'pointShadow'], ['bright', 'point'], ['mid', 'point']]);
    expect(c.drawn).toBe(3);
    expect(c.shadowed).toBe(1);
  });

  it('a spot or a profile with a cone goes to the spots, a panel to the panels (then as a point), a pendant’s profile to the points', () => {
    const cone: LightProfileDto = { key: 'builtin:downlight-medium', name: '', scale: 12, beam: 40, field: 120, vertical: 32, horizontal: 16, table: [] };
    const round: LightProfileDto = { ...cone, key: 'builtin:pendant', field: 360, beam: 0 };
    const profiles = new Map([[cone.key, cone], [round.key, round]]);
    expect(poolOf(src('s', 0, 1, { kind: 'spot' }), undefined, true)).toBe('spotShadow');
    expect(poolOf(src('p', 0, 1, { profile: cone.key }), cone, false)).toBe('spot');
    expect(poolOf(src('q', 0, 1, { profile: round.key, kind: 'spot' }), round, false)).toBe('spot');
    expect(poolOf(src('r', 0, 1, { profile: round.key }), round, false)).toBe('point');
    expect(poolOf(src('a', 0, 1, { kind: 'area' }), undefined, true)).toBe('rect');
    const panels = [src('a1', 1, 1000, { kind: 'area' }), src('a2', 2, 1000, { kind: 'area' })];
    const c = chooseLights(panels, [0, 0, 1.6], { spotShadow: 0, pointShadow: 0, spot: 0, point: 1, rect: 1, radius: 30 }, profiles);
    expect(c.chosen.map((x) => x.pool)).toEqual(['rect', 'point']);
  });

  it('the medium view draws fewer than the realistic one, the drawing none', () => {
    const many = Array.from({ length: 60 }, (_, i) => src(`l${i}`, i % 10, 1000, { kind: i % 2 ? 'spot' : 'point', position: [i % 10, Math.floor(i / 10), 2.5] }));
    const medium = chooseLights(many, [5, 3, 1.6], CAPS.medium);
    const realistic = chooseLights(many, [5, 3, 1.6], CAPS.realistic);
    expect(medium.drawn).toBe(CAPS.medium.spotShadow + CAPS.medium.spot + CAPS.medium.point);
    expect(realistic.drawn).toBeGreaterThan(medium.drawn);
    expect(chooseLights(many, [5, 3, 1.6], CAPS.drawing).drawn).toBe(0);
  });
});

describe('the rig', () => {
  it('makes as many lights as the model needs in steps, fills them from the choice and darkens the rest', () => {
    const rig = new LightRig();
    const list = [src('a', 1), src('b', 2, 650, { kind: 'spot', beam: 36, field: 50, kelvin: 2700 }), src('c', 3)];
    rig.setProfile('medium', list);
    const lights = () => rig.group.children.filter((o): o is THREE.Light => (o as THREE.Light).isLight === true);
    // Spots: one with shadows (a pool of 2); points: two (no point shadows in medium), a pool of 2.
    expect(lights().filter((l) => l instanceof THREE.SpotLight && l.castShadow).length).toBe(2);
    expect(lights().filter((l) => l instanceof THREE.PointLight).length).toBe(2);
    const made = lights().length;
    rig.update(list, new Map(), [0, 0, 1.6]);
    const lit = lights().filter((l) => l.intensity > 0);
    expect(lit.length).toBe(3);
    const spot = lit.find((l): l is THREE.SpotLight => l instanceof THREE.SpotLight)!;
    expect(spot.castShadow).toBe(true);
    expect(spot.angle).toBeCloseTo((50 * Math.PI) / 360, 6);
    expect(spot.color.r).toBeGreaterThan(spot.color.b);
    // Its shadow drawn once, then kept while nothing changes; the model changed: drawn again.
    expect(spot.shadow.needsUpdate).toBe(true);
    spot.shadow.needsUpdate = false;
    rig.update(list, new Map(), [1, 1, 1.6]);
    expect(spot.shadow.needsUpdate).toBe(false);
    rig.markDirty();
    rig.update(list, new Map(), [1, 1, 1.6]);
    expect(spot.shadow.needsUpdate).toBe(true);
    // The same needs: the same lights (no new shaders); the drawing: none.
    rig.setProfile('medium', list);
    expect(lights().length).toBe(made);
    rig.setProfile('drawing', list);
    expect(lights().length).toBe(0);
    rig.dispose();
  });
});

describe('the night', () => {
  it('comes from 2° over the horizon to nautical dusk, the exposure opening with it', () => {
    expect(nightOf(null)).toBe(0);
    expect(nightOf(30)).toBe(0);
    expect(nightOf(-4)).toBeCloseTo(0.5, 6);
    expect(nightOf(-20)).toBe(1);
    expect(exposureGain(0)).toBe(1);
    expect(exposureGain(1)).toBe(NIGHT_GAIN);
    expect(exposureGain(0.5)).toBeCloseTo(Math.sqrt(NIGHT_GAIN), 6);
    expect(whiteBalanceKelvin('auto', 0)).toBe(6504);
    expect(whiteBalanceKelvin('auto', 1)).toBe(4000);
    expect(whiteBalanceKelvin(3200, 0)).toBe(3200);
  });
});
