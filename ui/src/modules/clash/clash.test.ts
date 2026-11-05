import { describe, expect, it } from 'vitest';
import { clashCsv, clashHtml, framing, valuesOf, type Clash, type ClashTest } from './clash';

describe('a clash framed in 3D', () => {
  it('boxes where the two meet, a metre round, at least two metres along each axis', () => {
    // A duct along y through a beam along x.
    const f = framing([2, 0.15, 2.8], { min: [0, 0, 2.6], max: [4, 0.3, 3] }, { min: [1.8, -2, 2.7], max: [2.2, 2, 2.9] });
    expect(f.box.min[0]).toBeCloseTo(0.8);
    expect(f.box.max[0]).toBeCloseTo(3.2);
    expect(f.box.max[2] - f.box.min[2]).toBeCloseTo(2.2);
    expect(f.pitch).toBeCloseTo(-0.349);
  });

  it('looks from the side of both elements, turning the least from where it looks now', () => {
    const wall = { min: [0, 0, 0] as [number, number, number], max: [10, 0.3, 3] as [number, number, number] };
    const pipe = { min: [5, -1, 1] as [number, number, number], max: [5.1, 2, 1.1] as [number, number, number] };
    // A wall along x, a pipe along y: at 45° to both.
    const f = framing([5, 0.15, 1.05], wall, pipe, 1.4);
    expect(Math.abs(Math.sin(f.azimuth) * Math.cos(f.azimuth))).toBeCloseTo(0.5);
    // A beam alone along x with a small box: square to the beam.
    const box = { min: [5, 0, 0] as [number, number, number], max: [5.3, 0.3, 0.3] as [number, number, number] };
    expect(Math.abs(Math.sin(framing([5, 0.1, 0.2], { min: [0, 0, 0], max: [10, 0.3, 0.5] }, box, 1.4).azimuth))).toBeCloseTo(1);
    expect(f.distance).toBeGreaterThanOrEqual(2.5);
  });

  it('frames the gap between two elements apart', () => {
    const f = framing([1.015, 0.5, 0.5], { min: [0, 0, 0], max: [1, 1, 1] }, { min: [1.03, 0, 0], max: [2, 1, 1] });
    expect(f.box.min[0]).toBeCloseTo(0, 6);
    expect(f.box.max[0] - f.box.min[0]).toBeCloseTo(2.03, 2);
  });
});

describe('a rule\'s values', () => {
  it('a list by commas, a number with a thousands comma kept whole', () => {
    expect(valuesOf('IfcWall, IfcSlab', 'isOneOf')).toEqual(['IfcWall', 'IfcSlab']);
    expect(valuesOf('1,200, 800', 'between')).toEqual(['1,200', '800']);
    expect(valuesOf('Wall, external', 'contains')).toEqual(['Wall, external']);
  });
});

describe('a test as CSV', () => {
  it('one row a clash, depths in mm, with the byte order mark', () => {
    const test = { id: 't', name: 'Test', kind: 'hard', setA: 'a', setB: 'b', tolerance: 0.005, enabled: true, counts: {} as ClashTest['counts'], lastRun: null } as ClashTest;
    const c: Clash = { n: 1, key: 'x|y', a: 'x', b: 'y', at: [1, 2, 3], d: 0.16, crossing: true, status: 'new', found: '2026-11-05T10:00:00Z', seen: '', aName: 'Wall, ground', bClass: 'Pipe' };
    const csv = clashCsv(test, [c]);
    expect(csv.startsWith('﻿No.,Status')).toBe(true);
    expect(csv).toContain('1,new,"Wall, ground",,,,Pipe,,160,yes,1.000,2.000,3.000');
  });
});

describe('a test as an HTML page', () => {
  it('each clash under its group with its picture, the text escaped', () => {
    const test = { id: 't', name: 'A × B', kind: 'hard', setA: 'a', setB: 'b', tolerance: 0.005, enabled: true, counts: {} as ClashTest['counts'], lastRun: null } as ClashTest;
    const base = { key: 'k', a: 'x', b: 'y', at: [1, 2, 3] as [number, number, number], d: 0.16, crossing: true, status: 'new' as const, found: '', seen: '' };
    const page = clashHtml('House', test, [
      { ...base, n: 1, key: '1', aName: 'Wall <1>', bClass: 'Pipe', group: 'Wall <1>' },
      { ...base, n: 2, key: '2', aName: 'Wall <1>', bClass: 'Duct', group: 'Wall <1>' },
      { ...base, n: 3, key: '3', aName: 'Slab', bClass: 'Pipe', group: 'Slab', notes: 'sleeve' },
    ], new Map([[1, 'data:image/png;base64,AAAA']]));
    expect(page.match(/<h2>/g)).toHaveLength(2);
    expect(page).toContain('<h2>Wall &lt;1&gt;</h2>');
    expect(page.match(/<img /g)).toHaveLength(1);
    expect(page).toContain('depth 160 mm');
    expect(page).toContain('<p>sleeve</p>');
    expect(page).toContain('3 new');
  });
});
