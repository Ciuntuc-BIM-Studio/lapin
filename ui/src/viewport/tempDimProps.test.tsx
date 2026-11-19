/** Decision 738: the Temporary Dimension Properties (Revit's), kept for the user, and the angle typed to the reference while drawing. */

import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { DEFAULT_PREFS, readPrefs } from '../app/prefs';
import { TempDimSettings } from './TempDimSettings';
import { DIM_REFS, DIM_REF_LABELS, hostOf, lineDims, refOfPlace, type DimLineInfo } from './lineDims';
import { liveFields, livePoint } from './liveInput';
import { previousRef } from './angleDims';

describe('the Temporary Dimension Properties', () => {
  it('offer Revit\'s four choices for walls in its order, Lapin\'s reference line last, and two for doors and windows', () => {
    expect(DIM_REFS).toEqual(['axis', 'faces', 'coreCentre', 'core', 'reference']);
    expect(DIM_REFS.map((r) => DIM_REF_LABELS[r])).toEqual(['Centrelines', 'Faces', 'Centre of core', 'Faces of core', 'Reference line']);
    const html = renderToStaticMarkup(<TempDimSettings value={{ walls: 'core', openings: 'openings' }} onChange={() => {}} />);
    expect(html.match(/type="radio"/g)).toHaveLength(7);
    const on = (v: string) => new RegExp(`<input(?=[^>]*value="${v}")(?=[^>]*checked)[^>]*>`);
    expect(html).toMatch(on('core'));
    expect(html).toMatch(on('openings'));
    expect(html).not.toMatch(on('centre'));
    expect(html).not.toMatch(on('axis'));
  });

  it('are remembered: faces of core and openings by default, any choice read back, anything else at its default', () => {
    expect(DEFAULT_PREFS.tempDimRef).toBe('core');
    expect(DEFAULT_PREFS.tempDimOpeningRef).toBe('openings');
    expect(readPrefs({ tempDimRef: 'coreCentre', tempDimOpeningRef: 'centre' })).toMatchObject({ tempDimRef: 'coreCentre', tempDimOpeningRef: 'centre' });
    expect(readPrefs({ tempDimOpeningRef: 'middle' }).tempDimOpeningRef).toBe('openings');
  });
});

describe('the centre of core', () => {
  // A wall along +X whose core (−0.025 … 0.225) is off its axis: its middle 0.1 left of it; a neighbour 4 m to its left, its core centred.
  const host: DimLineInfo = { id: 'h', name: 'H', kind: 'wall', a: { x: 0, y: 0 }, b: { x: 6, y: 0 }, faces: [-0.045, 0.325], core: [-0.025, 0.225], coreCentre: 0.1, joins: { start: [], end: [], path: [] } };
  const other: DimLineInfo = { id: 'o', name: 'O', kind: 'wall', a: { x: 0, y: 4 }, b: { x: 6, y: 4 }, faces: [-0.145, 0.225], core: [-0.125, 0.125], coreCentre: 0, joins: { start: [], end: [], path: [] } };

  it('measures from the middle of the core to the other\'s (its axis, its core being centred)', () => {
    const [d] = lineDims(hostOf(host), [host, other], [], {}, { mode: 'coreCentre', along: false });
    expect(d.ownWhat).toBe('core centre');
    expect(d.options[d.opt].what).toBe('axis');
    expect(d.value).toBeCloseTo(3.9, 9);
    // Centrelines: axis to axis.
    expect(lineDims(hostOf(host), [host, other], [], {}, { mode: 'axis', along: false })[0].value).toBeCloseTo(4, 9);
    expect(refOfPlace('core centre')).toBe('coreCentre');
  });
});

describe('the angle typed to the previous segment while drawing', () => {
  it('sets the direction, the cursor giving the side; Tab reaches it only when there is a reference', () => {
    const ref = previousRef({ x: 5, y: 0 }, { x: 0, y: 0 });
    const s = { base: { x: 5, y: 0 }, cursor: { x: 5.2, y: 2 }, len: '3000', ang: '', rel: '90', ref, mode: 'polar' as const };
    const p = livePoint(s)!;
    expect(p.x).toBeCloseTo(5, 6);
    expect(p.y).toBeCloseTo(3, 6);
    // The cursor below: the other side.
    expect(livePoint({ ...s, cursor: { x: 5.2, y: -2 } })!.y).toBeCloseTo(-3, 6);
    // An angle from X typed wins.
    expect(livePoint({ ...s, ang: '0' })!.x).toBeCloseTo(8, 6);
    expect(liveFields({ measure: false, mode: 'polar', ref })).toEqual(['len', 'ang', 'rel']);
    expect(liveFields({ measure: false, mode: 'polar', ref: null })).toEqual(['len', 'ang']);
  });
});
