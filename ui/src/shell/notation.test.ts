import { describe, expect, it } from 'vitest';
import { groupKeys, notationPatch, tokenHint, type NotationKey } from './notation';

const key = (k: string, tokens: string[] = []): NotationKey => ({ key: k, label: k, kind: 'template', default: '', value: '', own: false, tokens, example: '' });

describe('the project notation', () => {
  it('sends a preset, one text, an emptied text as null, or a reset', () => {
    expect(notationPatch('rebar', { preset: 'uk' })).toEqual({ domain: 'rebar', preset: 'uk' });
    expect(notationPatch('rebar', { preset: null })).toEqual({ domain: 'rebar', preset: null });
    expect(notationPatch('rebar', { key: 'symbol', text: 'φ' })).toEqual({ domain: 'rebar', values: { symbol: 'φ' } });
    expect(notationPatch('rebar', { key: 'symbol', text: '' })).toEqual({ domain: 'rebar', values: { symbol: null } });
    expect(notationPatch('rebar', { reset: true })).toEqual({ domain: 'rebar', reset: true });
  });

  it('writes a key\'s tokens and puts the schedule\'s texts apart', () => {
    expect(tokenHint(key('diameter', ['symbol', 'd']))).toBe('{symbol} {d}');
    const g = groupKeys([key('symbol'), key('diameter'), key('heading.mark'), key('schedule.total')]);
    expect(g.drawing.map((k) => k.key)).toEqual(['symbol', 'diameter']);
    expect(g.schedule.map((k) => k.key)).toEqual(['heading.mark', 'schedule.total']);
  });
});
