import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { filterPalette, ZONE, type PaletteEntry } from './finishPainter';
import { paintedZonesOf } from './PaintedFinishesSection';

const entry = (over: Partial<PaletteEntry>): PaletteEntry => ({
  key: over.name ?? 'k',
  name: 'Finish',
  kind: 'surface',
  use: 'walls',
  layers: [{ material: 'Plaster', thickness: 0.015 }],
  ...over,
});

describe('Finish Painter improvements (forum #124)', () => {
  it('filters palette entries by use and search terms across name and layer materials', () => {
    const list = [
      entry({ name: 'ETICS EPS 100', use: 'walls', layers: [{ material: 'EPS', thickness: 0.1 }, { material: 'Silicate render', thickness: 0.01 }] }),
      entry({ name: 'Floor tiles', use: 'floors', layers: [{ material: 'Ceramic tiles', thickness: 0.015 }] }),
      entry({ name: 'Gypsum board ceiling', use: 'ceilings', layers: [{ material: 'Gypsum board', thickness: 0.0125 }] }),
      entry({ name: 'Lime plaster', use: 'any', layers: [{ material: 'Lime', thickness: 0.02 }] }),
    ];
    expect(filterPalette(list, '', 'all')).toHaveLength(4);
    expect(filterPalette(list, '', 'floors').map((e) => e.name)).toEqual(['Floor tiles', 'Lime plaster']);
    expect(filterPalette(list, 'silicate', 'walls').map((e) => e.name)).toEqual(['ETICS EPS 100']);
    expect(filterPalette(list, 'eps walls', 'all').map((e) => e.name)).toEqual(['ETICS EPS 100']);
    expect(filterPalette(list, 'tiles', 'walls')).toEqual([]);
  });

  it('collects painted finish zones belonging to a host element', () => {
    const nodes = new Map<string, NodeDto>([
      ['w1', { id: 'w1', type: 'IfcWall', name: 'Wall 1', props: {} } as unknown as NodeDto],
      ['z1', { id: 'z1', type: ZONE, name: 'ETICS EPS 100', props: { hostId: 'w1', face: 'Face.Right' } } as unknown as NodeDto],
      ['z2', { id: 'z2', type: ZONE, name: 'Tiles', props: { hostId: 'w2', face: 'Face.Left' } } as unknown as NodeDto],
    ]);
    expect(paintedZonesOf(nodes, 'w1').map((z) => z.id)).toEqual(['z1']);
    expect(paintedZonesOf(nodes, 'w3')).toEqual([]);
  });
});
