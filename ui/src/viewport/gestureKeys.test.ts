/** One rule for Enter and Esc in every view (audit 2026-11-15): Enter finishes, Esc cancels the step in progress. */
import { describe, expect, it } from 'vitest';
import { gestureKey } from './gestureKeys';
import { enterStep, type PointGesture } from './gesture3d';
import type { Gesture } from '../platform/module';

const chain: Gesture = { kind: 'twoPoints', repeat: true };
const polygon: Gesture = { kind: 'polygon' };
const polyline: Gesture = { kind: 'polyline' };
const pickThen: Gesture = { kind: 'pickThenPoint', types: ['LapinRebar'] };
const point: Gesture = { kind: 'point' };

describe('Enter finishes', () => {
  it('closes an outline of 3 points or more, ends a polyline of 2 or more', () => {
    expect(gestureKey('Enter', polygon, 3, false)).toBe('complete');
    expect(gestureKey('Enter', polygon, 2, false)).toBeNull();
    expect(gestureKey('Enter', polyline, 2, false)).toBe('complete');
    expect(gestureKey('Enter', polyline, 1, false)).toBeNull();
  });
  it('ends a chain (what it made stays: only the point in progress goes)', () => {
    expect(gestureKey('Enter', chain, 1, false)).toBe('clear');
    expect(gestureKey('Enter', chain, 0, false)).toBeNull();
    expect(gestureKey('Enter', { kind: 'twoPoints' }, 1, false)).toBeNull();
  });
  it('ends a pick-then-point gesture at its pick', () => {
    expect(gestureKey('Enter', pickThen, 1, true)).toBe('completePick');
    expect(gestureKey('Enter', pickThen, 1, false)).toBeNull();
  });
  it('does nothing with no tool, or with nothing drawn', () => {
    expect(gestureKey('Enter', undefined, 0, false)).toBeNull();
    expect(gestureKey('Enter', point, 0, false)).toBeNull();
  });
  it('in 3D too: a chain ends on Enter, an outline closes', () => {
    expect(enterStep(chain as PointGesture, [{ x: 0, y: 0 }])).toEqual({ pts: [], done: null });
    expect(enterStep(polygon as PointGesture, [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }])?.done?.length).toBe(3);
  });
});

describe('Esc cancels the step in progress', () => {
  it('drops the points of what is not made yet, whatever the gesture', () => {
    for (const g of [chain, polygon, polyline, point]) expect(gestureKey('Escape', g, 2, false)).toBe('clear');
    // A pick-then-point gesture's pick is the step in progress: Esc no longer ends it at the pick (that is Enter's).
    expect(gestureKey('Escape', pickThen, 1, true)).toBe('clear');
  });
  it('with nothing in progress, leaves the tool', () => {
    for (const g of [chain, polygon, polyline, point, pickThen]) expect(gestureKey('Escape', g, 0, false)).toBe('exit');
  });
});
