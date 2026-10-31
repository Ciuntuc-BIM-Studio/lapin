import { describe, expect, it } from 'vitest';
import { groupByLayer, hiddenInText } from './sectionMarkers';

describe('View → Sections…', () => {
  it('groups the sections under their layers, keeping an empty layer and making one for a layer only a section names', () => {
    const groups = groupByLayer({
      layers: [{ name: 'Sections', shown: true }, { name: 'Details', shown: false }],
      sections: [
        { id: 'b', name: 'B-B', layer: 'Sections', shown: true, hiddenIn: 0 },
        { id: 'a', name: 'A-A', layer: 'Sections', shown: false, hiddenIn: 2 },
        { id: 'c', name: 'C-C', layer: 'Rebar', shown: true, hiddenIn: 0 },
      ],
    });
    expect(groups.map((g) => g.layer.name)).toEqual(['Sections', 'Details', 'Rebar']);
    expect(groups[0].sections.map((s) => s.name)).toEqual(['A-A', 'B-B']);
    expect(groups[1].sections).toEqual([]);
    expect(groups[2].layer).toEqual({ name: 'Rebar', shown: true });
  });

  it('says in how many views a section is hidden', () => {
    expect(hiddenInText(0)).toBe('');
    expect(hiddenInText(1)).toBe('hidden in 1 view');
    expect(hiddenInText(3)).toBe('hidden in 3 views');
  });
});
