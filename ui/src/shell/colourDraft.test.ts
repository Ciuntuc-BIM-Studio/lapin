import { describe, expect, it } from 'vitest';
import { colourDraft } from './colourDraft';

describe('a colour pick', () => {
  it('sends once, however many times the picker moved', () => {
    const sent: string[] = [];
    const d = colourDraft(() => '#000000', (c) => sent.push(c));
    for (const c of ['#101010', '#202020', '#303030', '#405060']) d.move(c);
    expect(sent).toEqual([]);
    expect(d.shown('#000000')).toBe('#405060');
    d.settle();
    d.settle(); // blur after the change event
    expect(sent).toEqual(['#405060']);
    expect(d.pending()).toBe(false);
  });
  it('sends nothing when the colour came back to where it began', () => {
    const sent: string[] = [];
    const d = colourDraft(() => '#aabbcc', (c) => sent.push(c));
    d.move('#112233'); d.move('#AABBCC'); d.settle();
    expect(sent).toEqual([]);
  });
});
