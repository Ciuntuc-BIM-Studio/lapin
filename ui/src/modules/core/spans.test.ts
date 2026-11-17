import { afterEach, describe, expect, it } from 'vitest';
import { useStandards } from '../../engine/unitsStore';
import { parseSpans, spansError, spanTokens } from './spans';
import { levelPlan, namesApart } from './newLevels';

const round = (v: number[]) => v.map((x) => Math.round(x * 1e6) / 1e6);
const spans = (text: string) => { const r = parseSpans(text); return r.ok ? round(r.spans) : r; };

describe('spans typed in one field (issue #83, forum 137)', () => {
  afterEach(() => useStandards.setState({ units: 'mm' }));

  it('mixes single spans and N*span in one field', () => {
    useStandards.setState({ units: 'mm' });
    expect(spans('100 2*300 200')).toEqual([0.1, 0.3, 0.3, 0.2]);
    expect(spans('3*600')).toEqual([0.6, 0.6, 0.6]);
    expect(spans('2 * 300 x')).toEqual({ ok: false, error: 'Not a length:', token: 'x' });
    expect(spans('2 * 300; 4x100')).toEqual([0.3, 0.3, 0.1, 0.1, 0.1, 0.1]);
    expect(spans('')).toEqual([]);
  });

  it('reads the project\'s units, a comma or a dot for the decimals, and a unit of the span\'s own', () => {
    useStandards.setState({ units: 'm' });
    expect(spans('5.4 3*3.0')).toEqual([5.4, 3, 3, 3]);
    expect(spans('5,4 3*3,0')).toEqual([5.4, 3, 3, 3]);
    useStandards.setState({ units: 'cm' });
    expect(spans('3*600')).toEqual([6, 6, 6]);
    expect(spans('250 2*6m')).toEqual([2.5, 6, 6]);
    useStandards.setState({ units: 'ft-in' });
    expect(spanTokens('20\' 6" 2*18\'')).toEqual(['20\' 6"', '2*18\'']);
    expect(spans('20\' 6" 2*18\'')).toEqual([6.2484, 5.4864, 5.4864]);
  });

  it('names the piece that does not read', () => {
    useStandards.setState({ units: 'mm' });
    expect(spans('6000 abc 4500')).toEqual({ ok: false, error: 'Not a length:', token: 'abc' });
    expect(spansError(parseSpans('100 2*abc'))).toBe('Not a length: 2*abc');
    expect(spansError(parseSpans('100 0 200'))).toBe('A span is a length above zero: 0');
    expect(spansError(parseSpans('0*300'))).toBe('The count before * is a whole number from 1 to 200: 0*300');
    expect(spansError(parseSpans('500*300'))).toBe('The count before * is a whole number from 1 to 200: 500*300');
    expect(spansError(parseSpans('3*'))).toBe('Not a length: 3*');
    expect(spansError(parseSpans('100 200'))).toBeNull();
  });
});

describe('New levels (issue #83)', () => {
  it('stacks the storeys from the base level, named by the pattern', () => {
    expect(levelPlan(6, [3, 3, 2.8], 'Level {n}', 2)).toEqual([
      { name: 'Level 2', elevation: 6, height: 3 },
      { name: 'Level 3', elevation: 9, height: 3 },
      { name: 'Level 4', elevation: 12, height: 2.8 },
    ]);
    expect(levelPlan(0, [3], '', 1)[0].name).toBe('Level 1');
    expect(levelPlan(0, [3, 3], 'E{n} – office', 0).map((l) => l.name)).toEqual(['E0 – office', 'E1 – office']);
    expect(namesApart('Attic', 1)).toBe(true);
    expect(namesApart('Attic', 2)).toBe(false);
  });
});
