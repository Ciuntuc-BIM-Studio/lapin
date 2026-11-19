import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { boardOf, isBoardView } from '../../viewport/sketch/board';
import { ROLES } from '../../viewport/cad/session';
import { STAIR_SKETCH_ROLES } from './stairSketchBoard';

const node = (id: string, props: Record<string, unknown>, name = id): NodeDto => ({ id, type: 'LapinView', name, props } as unknown as NodeDto);

describe('the stair by sketch board (Revit\'s stair by sketch, 19.11.2026)', () => {
  it('claims a stair\'s sketch view and a new stair\'s, with Draft\'s lines and arcs, constraints and a side form', () => {
    const nodes = new Map([
      ['s', node('s', { kind: 'plan', stairSketchOf: 'stair1' })],
      ['stair1', node('stair1', {}, 'Stair 1')],
      ['n', node('n', { kind: 'plan', stairSketchOf: { storeyId: 'st1' } })],
      ['plain', node('plain', { kind: 'plan' })],
    ]);
    const s = boardOf('s', nodes)!;
    expect(s.kind).toBe('stair-sketch');
    expect(s.title).toBe('Stair 1');
    expect(s.tools).toContain('ARC');
    expect(s.tools).not.toContain('CIRCLE');
    expect(s.panel).toBeTypeOf('function');
    expect(boardOf('n', nodes)!.title).toBe('New stair');
    expect(isBoardView('s', nodes)).toBe(true);
    expect(boardOf('plain', nodes)).toBeNull();
  });

  it('draws with roles the drafting session knows: boundary, riser and path', () => {
    for (const r of STAIR_SKETCH_ROLES) expect(ROLES as readonly string[]).toContain(r.role);
  });
});
