import { describe, expect, it } from 'vitest';
import type { DimLineInfo } from '../../viewport/lineDims';
import { beamDims, columnDims, columnLines, columnMove } from './memberDims';

const L = (id: string, a: [number, number], b: [number, number], faces: [number, number], core: [number, number], kind = 'wall'): DimLineInfo =>
  ({ id, name: id, kind, a: { x: a[0], y: a[1] }, b: { x: b[0], y: b[1] }, faces, core, joins: { start: [], end: [], path: [] } });
const r6 = (v: number) => Math.round(v * 1e6) / 1e6;

// The walls of DimLinesTests: plaster 20, brick 250 (core), insulation 100; the south one's core face at y = 0.125, its finish at 0.225.
const south = L('S', [0, 0], [6, 0], [-0.145, 0.225], [-0.125, 0.125]);
const west = L('W', [0, 0], [0, 5], [-0.2, 0.2], [-0.125, 0.125]);
const plaster = { section: { kind: 'rectangle', width: 0.3, depth: 0.3 }, finishLayers: [{ thickness: 0.015 }] };

describe('columns\' and beams\' temporary dimensions (decision 733)', () => {
  it('a column is two lines through its point, its core the section, its faces the finish layers (as the engine gives them)', () => {
    const [x, y] = columnLines('C', 'C', { x: 2, y: 2 }, { section: { width: 0.4, depth: 0.3 }, finishLayers: [{ thickness: 0.015 }, { thickness: 0.025, faces: ['+x'] }] });
    expect([r6(x.a.x), r6(x.b.x)]).toEqual([1.785, 2.24]);
    expect(x.faces.map(r6)).toEqual([-0.165, 0.165]);
    expect(x.core).toEqual([-0.15, 0.15]);
    expect(y.faces.map(r6)).toEqual([-0.24, 0.215]);
    expect(y.core).toEqual([-0.2, 0.2]);
    // Turned 90°: its x runs up the plan.
    const [tx] = columnLines('C', 'C', { x: 6, y: 2 }, { section: { width: 0.4, depth: 0.3 } }, 90);
    expect([r6(tx.a.x), r6(tx.a.y), r6(tx.b.y)]).toEqual([6, 1.8, 2.2]);
  });

  it('core face to core face from a plastered column to a layered wall leaves the plaster out of both; faces take it in', () => {
    const own = columnLines('C', 'C', { x: 3, y: 2 }, plaster);
    const core = columnDims(own, [south, west], [], {}, 'core');
    const right = core[0].dims.find((d) => d.key === 'x:x:right')!;
    expect(right.target.id).toBe('S');
    expect(r6(right.value)).toBe(1.725);
    // Along its y: the west wall to its left (−x), core face 0.125 to the column's at 2.85.
    expect(r6(core[1].dims.find((d) => d.key === 'y:x:left')!.value)).toBe(2.725);
    const faces = columnDims(own, [south, west], [], {}, 'faces');
    expect(r6(faces[0].dims.find((d) => d.key === 'x:x:right')!.value)).toBe(1.61);
    expect(r6(columnDims(own, [south, west], [], {}, 'axis')[0].dims.find((d) => d.key === 'x:x:right')!.value)).toBe(2);
    // Columns have no free ends to measure along.
    expect(core.flatMap((c) => c.dims).every((d) => d.kind === 'across')).toBe(true);
  });

  it('a value typed moves the column square to the line measured: 1500 to the wall\'s core moves it 225 toward it', () => {
    const own = columnLines('C', 'C', { x: 3, y: 2 }, plaster);
    const [xs] = columnDims(own, [south], [], {}, 'core');
    const d = xs.dims.find((x) => x.key === 'x:x:right')!;
    const by = columnMove(xs.host, d, 1.5)!;
    expect([r6(by.x), r6(by.y)]).toEqual([0, -0.225]);
    expect(columnMove(xs.host, d, 1.725)).toBeNull();
  });

  it('measures to other columns, and a column\'s own other line is never a neighbour', () => {
    const a = columnLines('A', 'A', { x: 0, y: 0 }, plaster);
    const b = columnLines('B', 'B', { x: 0, y: 6 }, plaster);
    const [, ay] = columnDims(a, [...a, ...b], [], {}, 'core');
    expect(ay.dims).toHaveLength(0); // nothing beside its y line
    const [ax] = columnDims(a, [...a, ...b], [], {}, 'core');
    const up = ax.dims.find((d) => d.key === 'x:x:left')!;
    expect(up.target.id).toBe('B');
    expect(r6(up.value)).toBe(5.7);
  });

  it('a beam measures to beams and walls, not to columns, across and from its free ends', () => {
    const beam = L('B', [1, 2], [4, 2], [-0.125, 0.125], [-0.125, 0.125], 'beam');
    const other = L('O', [0, 4], [6, 4], [-0.125, 0.125], [-0.125, 0.125], 'beam');
    const col = columnLines('C', 'C', { x: 2.5, y: 3 }, plaster);
    const dims = beamDims({ id: 'B', a: beam.a, b: beam.b, ref: 0, faces: beam.faces, core: beam.core, joins: beam.joins }, [south, west, beam, other, ...col], [], {}, 'faces');
    const by = Object.fromEntries(dims.map((d) => [d.key, d]));
    expect(by['x:left'].target.id).toBe('O');
    expect(r6(by['x:left'].value)).toBe(1.75);
    expect(r6(by['x:right'].value)).toBe(1.65);   // 1.875 − 0.225
    expect(r6(by['e:start'].value)).toBe(0.8);    // its end at x = 1 to the west wall's face at 0.2
  });
});
