import { describe, expect, it } from 'vitest';
import { formatSteps, parseSteps } from './steps';

describe('a pad footing\'s steps in one field', () => {
  it('reads L × W × H in the project\'s unit, a semicolon between steps', () => {
    expect(parseSteps('1800 × 1800 × 500; 1000 x 1000 x 400', 'mm')).toEqual([{ length: 1.8, width: 1.8, height: 0.5 }, { length: 1, width: 1, height: 0.4 }]);
    expect(parseSteps('180 × 180 × 50', 'cm')).toEqual([{ length: 1.8, width: 1.8, height: 0.5 }]);
  });
  it('a step that does not read, or of nothing, refuses the whole', () => {
    expect(parseSteps('1800 × 1800', 'mm')).toBeNull();
    expect(parseSteps('1800 × 0 × 500', 'mm')).toBeNull();
  });
  it('shows them as it reads them back', () => {
    const steps = [{ length: 1.8, width: 1.2, height: 0.5 }];
    expect(formatSteps(steps, 'mm')).toBe('1800 × 1200 × 500');
    expect(parseSteps(formatSteps(steps, 'm'), 'm')).toEqual(steps);
  });
});
