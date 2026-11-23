import { describe, expect, it } from 'vitest';
import { fillOpening, openingFormat } from './openingFormat';

describe('opening heights under a dimension line (forum topic 229)', () => {
  it('writes each preset as it wrote before, a window and a door (no sill)', () => {
    const cases: [string, string[], string[]][] = [
      ['number', ['150'], ['210']],
      ['h', ['h=150'], ['h=210']],
      ['hp', ['h=150', 'p=90'], ['h=210']],
      ['slash', ['150/90'], ['210']],
      ['line', ['h=150, p=90'], ['h=210']],
    ];
    for (const [choice, window, door] of cases) {
      expect(fillOpening(openingFormat(choice)!, '150', '90')).toEqual(window);
      expect(fillOpening(openingFormat(choice)!, '210', null)).toEqual(door);
    }
    expect(openingFormat('off')).toBeNull();
  });

  it('takes the style\'s own format, its prefixes and rows', () => {
    expect(fillOpening(openingFormat('custom', 'H {h}[; P {p}]')!, '150', '90')).toEqual(['H 150; P 90']);
    expect(fillOpening(openingFormat('custom', 'H {h}[; P {p}]')!, '210', null)).toEqual(['H 210']);
    expect(fillOpening('hgt {h}|sill {p}', '210', null)).toEqual(['hgt 210']);
    expect(openingFormat('custom', '  ')).toBe('h={h}|p={p}');
  });
});
