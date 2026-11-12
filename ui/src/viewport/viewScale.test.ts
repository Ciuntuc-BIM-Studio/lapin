/** A view's scale changed from the view (forum #30, decision 492). */

import { describe, expect, it } from 'vitest';
import { parseScale, scaleCommand, scaleOptions } from './viewScale';

describe('the scale picker', () => {
  it('offers the project\'s scales, the view\'s own among them where it is not one', () => {
    expect(scaleOptions([20, 50, 100], 100)).toEqual([20, 50, 100]);
    expect(scaleOptions([20, 50, 100], 75)).toEqual([20, 50, 75, 100]);
    expect(scaleOptions([], 100)).toEqual([100]);
  });

  it('reads a scale typed as 1:75, 75 or 1/75, within 1:1 to 1:10000', () => {
    expect(parseScale('1:75')).toBe(75);
    expect(parseScale(' 75 ')).toBe(75);
    expect(parseScale('1/250')).toBe(250);
    expect(parseScale('1:12,5')).toBe(12.5);
    expect(parseScale('0')).toBeNull();
    expect(parseScale('1:20000')).toBeNull();
    expect(parseScale('big')).toBeNull();
    expect(parseScale('')).toBeNull();
  });

  it('changes a view as its settings do, and makes a level\'s default plan its plan view', () => {
    expect(scaleCommand('v1', 50)).toEqual({ command: 'element.update', args: { ids: ['v1'], attrs: { scale: 50 } } });
    expect(scaleCommand('plan:s1', 50)).toEqual({ command: 'view.create', args: { kind: 'plan', storeyId: 's1', scale: 50 } });
  });
});
