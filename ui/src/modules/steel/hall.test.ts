import { describe, expect, it } from 'vitest';
import type { ModelState } from '../../engine/model';
import type { NodeDto, RelationDto } from '../../engine/protocol';
import { baysText, fit, hallOf, parseBays, parseBracing, summaryRows } from './hall';

const node = (id: string, type: string): NodeDto => ({ id, type, name: id, props: {}, editable: true } as unknown as NodeDto);

describe('the hall configurator', () => {
  it('reads bays typed equal or listed, and writes them back the same way', () => {
    expect(parseBays('5 × 6')).toEqual([6, 6, 6, 6, 6]);
    expect(parseBays('3x7.5')).toEqual([7.5, 7.5, 7.5]);
    expect(parseBays('6, 6, 7.5')).toEqual([6, 6, 7.5]);
    expect(parseBays('6 6;7.5')).toEqual([6, 6, 7.5]);
    expect(parseBays('6, -1')).toBeNull();
    expect(parseBays('')).toBeNull();
    expect(baysText([6, 6, 6, 6, 6])).toBe('5 × 6');
    expect(baysText([6, 7.5, 6])).toBe('6, 7.5, 6');
    expect(baysText([6])).toBe('6');
  });

  it('reads the braced bays as whole numbers, in order, once each', () => {
    expect(parseBracing('6, 1, 1')).toEqual([1, 6]);
    expect(parseBracing('')).toEqual([]);
    expect(parseBracing('1.5')).toBeNull();
    expect(parseBracing('0')).toBeNull();
  });

  it('finds the hall a member belongs to, by the hall aggregating it', () => {
    const nodes = new Map([['h', node('h', 'LapinSteelHall')], ['m', node('m', 'LapinSteelMember')], ['x', node('x', 'IfcColumn')]]);
    const relations = new Map<string, RelationDto>([['r', { id: 'r', class: 'IfcRelAggregates', relating: 'h', related: ['m'] } as RelationDto]]);
    const model: ModelState = { nodes, relations };
    expect(hallOf(model, 'm')?.id).toBe('h');
    expect(hallOf(model, 'h')?.id).toBe('h');
    expect(hallOf(model, 'x')).toBeNull();
    expect(hallOf(model, null)).toBeNull();
  });

  it('fits the preview into its box, y up, centred', () => {
    const f = fit([{ role: 'column', pts: [0, 0, 10, 0, 10, 5, 0, 5] }], [], 120, 80, 10)!;
    expect(f.k).toBe(10);
    expect(f.X(0)).toBe(10);
    expect(f.X(10)).toBe(110);
    expect(f.Y(0)).toBe(65);
    expect(f.Y(5)).toBe(15);
    expect(fit([], [], 10, 10, 1)).toBeNull();
  });

  it('says the summary in rows, kilograms or tonnes, the gable posts only when there are some', () => {
    const s = { frames: 6, span: 20, length: 30, area: 600, eaves: 6, apex: 7.8, columns: 8, rafters: 12, posts: 0, weights: { column: 2950, rafter: 4620.4, haunch: 312.6, post: 0, total: 7883 }, perArea: 13.14 };
    const rows = summaryRows(s, (m) => `${m} m`);
    expect(rows.map((r) => r[0])).toEqual(['Frames', 'Span', 'Length', 'Eaves height', 'Apex height', 'Columns', 'Rafters', 'Haunches', 'Total steel', 'Per m² of floor']);
    expect(rows.find((r) => r[0] === 'Haunches')?.[1]).toBe('313 kg');
    expect(rows.find((r) => r[0] === 'Total steel')?.[1]).toBe('7.88 t');
    expect(rows.find((r) => r[0] === 'Per m² of floor')?.[1]).toBe('13.1 kg/m²');
    expect(summaryRows({ ...s, posts: 10 }, String).map((r) => r[0])).toContain('Gable posts');
  });
});
