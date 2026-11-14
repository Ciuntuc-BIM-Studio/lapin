import { describe, expect, it } from 'vitest';
import { highlightVars, preselectionColour, readHighlight, selectionColour } from './highlight';

describe('the preselection and selection colours', () => {
  it('are today\'s look until the user says otherwise; anything unreadable is left at it', () => {
    expect(readHighlight(null)).toEqual({ preselect: true, preselectColour: null, selectColour: null });
    expect(readHighlight('garbage')).toEqual({ preselect: true, preselectColour: null, selectColour: null });
    expect(readHighlight({ preselect: false, preselectColour: '#00FF88', selectColour: 'red' })).toEqual({ preselect: false, preselectColour: '#00ff88', selectColour: null });
  });

  it('sets a page variable only for a colour chosen', () => {
    expect(highlightVars(readHighlight(null))).toEqual({});
    const v = highlightVars(readHighlight({ selectColour: '#112233', preselectColour: '#445566' }));
    expect(v['--hl-select']).toBe('#112233');
    expect(v['--hl-pre']).toBe('#445566');
    expect(v['--hl-select-wash']).toContain('#112233');
  });

  it('gives 3D the accent it always had, the preselection following the selection unless it has its own', () => {
    const d = readHighlight(null);
    expect(selectionColour(d)).toBe('#b5532d');
    expect(preselectionColour(d)).toBe('#b5532d');
    expect(preselectionColour(readHighlight({ selectColour: '#112233' }))).toBe('#112233');
    expect(preselectionColour(readHighlight({ selectColour: '#112233', preselectColour: '#445566' }))).toBe('#445566');
  });
});
