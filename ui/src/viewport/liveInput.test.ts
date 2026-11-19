import { describe, expect, it } from 'vitest';
import { liveBegin, liveCommit, liveEnd, liveMeasure, livePoint, liveToggleMode, parseAngle, parseLength, useLive } from './liveInput';

describe('live input', () => {
  it('reads lengths in mm unless they say, with sums', () => {
    expect(parseLength('2400')).toBeCloseTo(2.4, 12);
    expect(parseLength('240cm')).toBeCloseTo(2.4, 12);
    expect(parseLength('2.4m')).toBeCloseTo(2.4, 12);
    expect(parseLength('3000+250')).toBeCloseTo(3.25, 12);
    expect(parseLength('3m-25cm')).toBeCloseTo(2.75, 12);
    expect(parseLength('2,5m')).toBeCloseTo(2.5, 12);
    expect(parseLength('1,5m+2,5m')).toBeCloseTo(4, 12);
    expect(parseLength('abc')).toBeNull();
    expect(parseLength('')).toBeNull();
  });

  it('reads angles in degrees', () => {
    expect(parseAngle('30')).toBe(30);
    expect(parseAngle('90°')).toBe(90);
    expect(parseAngle('45+15')).toBe(60);
    expect(parseAngle('22,5+22,5')).toBe(45);
    expect(parseAngle('x')).toBeNull();
  });

  it('takes the typed length along the cursor, the typed angle with the cursor\'s reach, or both', () => {
    const base = { x: 1, y: 1 }, cursor = { x: 4, y: 5 };              // 5 m away at 53.13°
    expect(livePoint({ base, cursor, len: '', ang: '' })).toEqual(cursor);
    const l = livePoint({ base, cursor, len: '10000', ang: '' })!;
    expect(l.x).toBeCloseTo(7, 9); expect(l.y).toBeCloseTo(9, 9);
    const a = livePoint({ base, cursor, len: '', ang: '0' })!;
    expect(a.x).toBeCloseTo(4, 9); expect(a.y).toBeCloseTo(1, 9);
    const both = livePoint({ base, cursor, len: '2000', ang: '90' })!;
    expect(both.x).toBeCloseTo(1, 9); expect(both.y).toBeCloseTo(3, 9);
    const m = liveMeasure({ base, cursor, len: '', ang: '' })!;
    expect(m.len).toBeCloseTo(5, 9);
    expect(m.ang).toBeCloseTo(53.13, 2);
  });

  it('in width/height mode, takes the typed width and height signed, an empty one following the cursor', () => {
    const base = { x: 1, y: 1 }, cursor = { x: 4, y: 5 };
    expect(livePoint({ base, cursor, len: '', ang: '', mode: 'rect' })).toEqual(cursor);
    const both = livePoint({ base, cursor, len: '2000', ang: '1500', mode: 'rect' })!;
    expect(both.x).toBeCloseTo(3, 9); expect(both.y).toBeCloseTo(2.5, 9);
    const flipped = livePoint({ base, cursor, len: '-2000', ang: '1500', mode: 'rect' })!;
    expect(flipped.x).toBeCloseTo(-1, 9); expect(flipped.y).toBeCloseTo(2.5, 9);
    const wOnly = livePoint({ base, cursor, len: '2000', ang: '', mode: 'rect' })!;
    expect(wOnly.x).toBeCloseTo(3, 9); expect(wOnly.y).toBeCloseTo(5, 9);
    // The same fields in length/angle mode mean the segment.
    const polar = livePoint({ base, cursor, len: '2000', ang: '90', mode: 'polar' })!;
    expect(polar.x).toBeCloseTo(1, 9); expect(polar.y).toBeCloseTo(3, 9);
    const m = liveMeasure({ base, cursor, len: '2000', ang: '-1500', mode: 'rect' })!;
    expect(m.dx).toBeCloseTo(2, 9); expect(m.dy).toBeCloseTo(-1.5, 9);
  });

  it('switches the mode for a rectangle tool only, clears what was typed, and remembers the mode per tool', () => {
    liveBegin('tool', { x: 0, y: 0 }, () => {});
    liveToggleMode();
    expect(useLive.getState().mode).toBe('polar');          // not a rectangle: no switch
    liveEnd('tool');
    liveBegin('tool', { x: 0, y: 0 }, () => {}, { rect: 'draft.rect' });
    expect(useLive.getState().mode).toBe('rect');           // a rectangle opens on width and height (report #173)
    useLive.setState({ len: '12' });
    liveToggleMode();
    expect(useLive.getState()).toMatchObject({ mode: 'polar', len: '', ang: '', focus: 'len' });
    liveEnd('tool');
    liveBegin('tool', { x: 0, y: 0 }, () => {}, { rect: 'draft.rect' });
    expect(useLive.getState().mode).toBe('polar');          // remembered
    liveEnd('tool');
    liveBegin('tool', { x: 0, y: 0 }, () => {}, { rect: 'other.rect' });
    expect(useLive.getState().mode).toBe('rect');           // per tool
    liveEnd('tool');
    liveBegin('tool', { x: 0, y: 0 }, () => {}, { rect: 'draft.rect' });
    liveToggleMode();
    expect(useLive.getState().mode).toBe('rect');
    liveEnd('tool');
  });

  it('Enter takes the typed width and height as the corner', () => {
    const got: { x: number; y: number }[] = [];
    liveBegin('tool', { x: 0, y: 0 }, (p) => got.push(p), { rect: 'draft.rect2' });
    expect(useLive.getState().mode).toBe('rect');
    useLive.setState({ cursor: { x: 1, y: 1 }, len: '2000', ang: '-1500' });
    expect(liveCommit()).toBe(true);
    expect(got[0].x).toBeCloseTo(2, 9); expect(got[0].y).toBeCloseTo(-1.5, 9);
    liveEnd('tool');
  });

  it('places a first point from a reference point by distance/angle or X/Y offsets (forum #126)', () => {
    const got: { x: number; y: number }[] = [];
    liveBegin('tool', { x: 10, y: 5 }, (p) => got.push(p), { rect: 'ref:architecture.wall', delta: true });
    // Distance and direction from the reference point: 3 m at 90°.
    useLive.setState({ cursor: { x: 10, y: 8 }, len: '3000', ang: '90' });
    expect(liveCommit()).toBe(true);
    expect(got[0].x).toBeCloseTo(10, 9); expect(got[0].y).toBeCloseTo(8, 9);
    // Switch with Space to X/Y offsets from the reference point: dx = 1500 mm, dy = -500 mm.
    liveToggleMode();
    expect(useLive.getState().mode).toBe('rect');
    useLive.setState({ cursor: { x: 11, y: 4 }, len: '1500', ang: '-500' });
    expect(liveCommit()).toBe(true);
    expect(got[1].x).toBeCloseTo(11.5, 9); expect(got[1].y).toBeCloseTo(4.5, 9);
    liveEnd('tool');
  });
});
