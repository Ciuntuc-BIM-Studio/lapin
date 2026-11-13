import { describe, expect, it } from 'vitest';
import { alignAngle, edgeAngle, normalizeDeg, orientationOf, planToScreen, readable, screenAxes, screenCorners, screenToPlan, turnOf, uprightText } from './orientation';
import { resolveAim } from './cad/aim';

const close = (a: number, b: number, eps = 1e-9) => expect(Math.abs(a - b)).toBeLessThan(eps);

describe('a plan view’s orientation (decision 526)', () => {
  it('reads its angle, follows the UCS, and is none otherwise', () => {
    expect(orientationOf(30, null)).toBe(30);
    expect(orientationOf(390, null)).toBe(30);
    expect(orientationOf('ucs', { origin: { x: 5, y: 1 }, angle: 12 })).toBe(12);
    expect(orientationOf('ucs', null)).toBe(0);
    expect(orientationOf(undefined, null)).toBe(0);
    expect(normalizeDeg(-180)).toBe(180);
    expect(turnOf(0)).toBe(0);
    close(turnOf(30), -Math.PI / 6);
  });

  it('shows a wall along 30° horizontal on screen when the view is turned to 30°', () => {
    const rot = turnOf(30), view = { cx: 0, cy: 0, scale: 100 }, size = { w: 800, h: 600 };
    const a = planToScreen({ x: 0, y: 0 }, view, size, rot);
    const b = planToScreen({ x: Math.cos(Math.PI / 6) * 4, y: Math.sin(Math.PI / 6) * 4 }, view, size, rot);
    close(a.y, b.y, 1e-6);
    expect(b.x - a.x).toBeCloseTo(400, 6);
  });

  it('picks back the plan point under the cursor: the screen map and its inverse agree at any turn', () => {
    const view = { cx: 3.2, cy: -1.5, scale: 57 }, size = { w: 913, h: 517 };
    for (const deg of [0, 17, 90, -135, 180]) {
      const rot = turnOf(deg);
      const p = { x: 7.25, y: 2.5 };
      const s = planToScreen(p, view, size, rot);
      const q = screenToPlan(s.x, s.y, view, size, rot);
      close(q.x, p.x, 1e-9);
      close(q.y, p.y, 1e-9);
    }
  });

  it('gives the key plan the screen’s turned box, corner by corner', () => {
    const view = { cx: 0, cy: 0, scale: 10 }, size = { w: 200, h: 100 }, rot = turnOf(90);
    const c = screenCorners((x, y) => screenToPlan(x, y, view, size, rot), size.w, size.h);
    expect(c).toHaveLength(4);
    // Turned a quarter: the screen's 20 m width lies along the plan's y.
    close(Math.abs(c[1].y - c[0].y), 20, 1e-9);
    close(Math.abs(c[1].x - c[0].x), 0, 1e-9);
  });

  it('aligns to an edge the nearer way round, whichever end is picked first', () => {
    expect(edgeAngle({ x: 0, y: 0 }, { x: 0, y: 1 })).toBe(90);
    close(alignAngle({ x: 0, y: 0 }, { x: Math.cos(Math.PI / 6), y: Math.sin(Math.PI / 6) }), 30);
    close(alignAngle({ x: Math.cos(Math.PI / 6), y: Math.sin(Math.PI / 6) }, { x: 0, y: 0 }), 30);
    close(alignAngle({ x: 1, y: 0 }, { x: 0, y: 0 }), 0);
  });

  it('keeps texts readable: upright within ±90°, turned over on their footprint', () => {
    expect(readable(0)).toBe(true);
    expect(readable(90)).toBe(true);
    expect(readable(-90)).toBe(false);
    expect(readable(135)).toBe(false);
    // Turned by 30°: a level text reads at 30°.
    expect(uprightText({ x: 1, y: 2, height: 2.5 }, 30, 0.1)).toEqual({ x: 1, y: 2, angle: 30, anchor: 'middle' });
    // A text along 60° in a plan turned by 120° would read at 180°: turned over to 0°, its start now its end, moved across by its height.
    const t = uprightText({ x: 0, y: 0, angle: 60, anchor: 'start', height: 2, baseline: 'bottom' }, 120, 0.1);
    close(t.angle, 0);
    expect(t.anchor).toBe('end');
    close(t.x, -Math.sin(Math.PI / 3) * 0.2);
    close(t.y, Math.cos(Math.PI / 3) * 0.2);
    // Centred on its line (baseline middle): turned over where it is.
    const m = uprightText({ x: 4, y: 5, angle: 0, anchor: 'middle', height: 2, baseline: 'middle' }, 180, 0.1);
    expect(m).toEqual({ x: 4, y: 5, angle: 0, anchor: 'middle' });
    // Not turned: as the engine drew it.
    expect(uprightText({ x: 0, y: 0, angle: 200, height: 1 }, 0, 0.1).angle).toBe(200);
  });

  it('squares ortho to the screen in a turned plan with no UCS', () => {
    const axes = screenAxes(30)!;
    const base = { x: 0, y: 0 };
    // A cursor a little off the 30° line: ortho holds it on that line (horizontal on screen).
    const raw = { x: Math.cos(Math.PI / 6) * 5 - 0.1, y: Math.sin(Math.PI / 6) * 5 + 0.1 };
    const a = resolveAim(raw, { base, ortho: true, polar: false, snap: null, acquired: [], tol: 0.01, frame: axes });
    close(Math.atan2(a.point.y, a.point.x), Math.PI / 6, 1e-9);
    expect(screenAxes(0)).toBeNull();
  });
});
