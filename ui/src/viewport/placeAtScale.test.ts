import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { otherScales, partOf, placeAgainLabel, sheetOfView } from './placeAtScale';
import { loadAllLanguages, translate } from '../i18n';

await loadAllLanguages();

describe('a view placed again at another scale (decision 547)', () => {
  it('offers the project\'s scales but its own', () => {
    expect(otherScales([20, 50, 100], 100)).toEqual([20, 50]);
    expect(otherScales([20, 50, 100], 75)).toEqual([20, 50, 100]);
    expect(placeAgainLabel(25)).toBe('Place again at 1:25…');
    expect(translate(placeAgainLabel(25), 'ro')).toBe('Plasează din nou la 1:25…');
  });

  it('takes a drag as the part, a click as the view as cropped', () => {
    expect(partOf({ x: 50, y: 40 }, { x: 20, y: 60 })).toEqual({ x0: 20, y0: 40, x1: 50, y1: 60 });
    expect(partOf({ x: 50, y: 40 }, { x: 50.4, y: 40.2 })).toBeNull();
  });

  it('finds the sheet a view is on', () => {
    const sheet = { id: 's', type: 'LapinSheet', name: 'A-101', props: { viewports: [{ id: 'v1', viewId: 'plan1' }] } } as unknown as NodeDto;
    const nodes = new Map<string, NodeDto>([['s', sheet]]);
    expect(sheetOfView(nodes, 'plan1')?.id).toBe('s');
    expect(sheetOfView(nodes, 'plan2')).toBeNull();
  });
});
