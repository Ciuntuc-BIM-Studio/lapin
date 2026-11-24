import { describe, expect, it } from 'vitest';
import { deliveredAs, fileCount, newSetName, publishableViews, sameAs, sheetsOf, type PublishSet } from './publish';
import type { NodeDto } from '../../../engine/protocol';

const node = (id: string, type: string, name: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type, name, props } as unknown as NodeDto);
const set = (over: Partial<PublishSet> = {}): PublishSet => ({ id: 's', name: 'Permit', sheets: ['a', 'b'], views: ['v'], missing: 0, formats: { pdf: true, combined: false, dxf: false }, ...over });

describe('publish sets', () => {
  it('count a PDF and a DXF for each sheet and view as asked, and one for all', () => {
    const c = { sheets: ['a', 'b'], views: ['v'], formats: { pdf: true, combined: true, dxf: true } };
    expect(fileCount(c)).toBe(7);
    expect(fileCount({ ...c, formats: { pdf: false, combined: true, dxf: false } })).toBe(1);
    expect(fileCount({ sheets: [], views: [], formats: c.formats })).toBe(0);
  });

  it('save one file as itself in a browser, more as a zip', () => {
    expect(deliveredAs({ sheets: ['a'], views: [], formats: { pdf: true, combined: false, dxf: false } })).toEqual({ zip: false, ext: 'pdf' });
    expect(deliveredAs({ sheets: ['a'], views: [], formats: { pdf: false, combined: false, dxf: true } })).toEqual({ zip: false, ext: 'dxf' });
    expect(deliveredAs({ sheets: ['a', 'b'], views: [], formats: { pdf: false, combined: true, dxf: false } })).toEqual({ zip: false, ext: 'pdf' });
    expect(deliveredAs({ sheets: ['a'], views: [], formats: { pdf: true, combined: false, dxf: true } })).toEqual({ zip: true, ext: 'zip' });
  });

  it('tell a choice changed from its set by its order and formats too', () => {
    expect(sameAs(set(), { sheets: ['a', 'b'], views: ['v'], formats: { pdf: true, combined: false, dxf: false } })).toBe(true);
    expect(sameAs(set(), { sheets: ['b', 'a'], views: ['v'], formats: { pdf: true, combined: false, dxf: false } })).toBe(false);
    expect(sameAs(set(), { sheets: ['a', 'b'], views: ['v'], formats: { pdf: true, combined: false, dxf: true } })).toBe(false);
    expect(sameAs(null, { sheets: [], views: [], formats: { pdf: true, combined: false, dxf: false } })).toBe(false);
  });

  it('name a new set with a number no set has', () => {
    expect(newSetName([])).toBe('Set 1');
    expect(newSetName([set({ name: 'Set 2' })])).toBe('Set 3');
    expect(newSetName([set({ name: 'set 2' }), set({ name: 'Set 3' })])).toBe('Set 4');
    expect(newSetName([set({ name: 'Permit' })], 'Permit')).toBe('Permit 2');
  });

  it('list the sheets by number and the views that print, a level without a plan of its own by its default plan', () => {
    const nodes = new Map<string, NodeDto>([
      ['st', node('st', 'IfcBuildingStorey', 'Ground floor', { elevation: 0 })],
      ['s2', node('s2', 'LapinSheet', 'Sections', { number: 'A-10' })],
      ['s1', node('s1', 'LapinSheet', 'Plans', { number: 'A-2' })],
      ['sec', node('sec', 'LapinView', 'A-A', { kind: 'section' })],
      ['sch', node('sch', 'LapinSchedule', 'Doors')],
    ]);
    expect(sheetsOf(nodes).map((n) => n.id)).toEqual(['s1', 's2']);
    expect(publishableViews(nodes, new Map()).map((v) => v.id)).toEqual(['plan:st', 'sec', '3d']);
  });
});
