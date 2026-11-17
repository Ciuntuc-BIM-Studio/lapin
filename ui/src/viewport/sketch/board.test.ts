import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { boardKey, boardOf, isBoardView, registerBoard, SKETCH_TOOLS, type Board } from './board';

const node = (id: string, props: Record<string, unknown>): NodeDto => ({ id, type: 'LapinView', name: id, props } as unknown as NodeDto);
const board = (viewId: string): Board => ({ kind: 'test', viewId, title: viewId, tools: SKETCH_TOOLS, constraints: true, finish: async () => undefined, cancel: async () => undefined });

describe('the shared sketch canvas', () => {
  it('finds a view\'s board through the registered resolvers, and none for plain Draft', () => {
    registerBoard((id, nodes) => (nodes.get(id)?.props.testBoard ? board(id) : null));
    const nodes = new Map([['a', node('a', { testBoard: true })], ['b', node('b', {})]]);
    expect(boardOf('a', nodes)?.kind).toBe('test');
    expect(boardOf('b', nodes)).toBeNull();
    expect(boardOf(null, nodes)).toBeNull();
    expect(isBoardView('a', nodes)).toBe(true);
    expect(isBoardView('b', nodes)).toBe(false);
  });

  it('offers the same Draft tools on every board', () => {
    expect(SKETCH_TOOLS).toEqual(['LINE', 'PLINE', 'RECTANGLE', 'CIRCLE', 'ARC', 'ELLIPSE', 'OFFSET', 'TRIM', 'EXTEND', 'FILLET', 'CHAMFER', 'ERASE']);
  });

  it('keys: Esc lets the selection go, then leaves a board in 3D keeping its work, never flat; Enter finishes in 3D only', () => {
    const leaving = { leave: () => undefined };
    expect(boardKey('Escape', { in3d: true, selected: true, board: leaving })).toBe('deselect');
    expect(boardKey('Escape', { in3d: false, selected: true, board: leaving })).toBe('deselect');
    expect(boardKey('Escape', { in3d: true, selected: false, board: leaving })).toBe('leave');
    expect(boardKey('Escape', { in3d: false, selected: false, board: leaving })).toBeNull();
    expect(boardKey('Escape', { in3d: true, selected: false, board: {} })).toBeNull();
    expect(boardKey('Enter', { in3d: true, selected: false, board: leaving })).toBe('finish');
    expect(boardKey('Enter', { in3d: false, selected: false, board: leaving })).toBeNull();
    expect(boardKey('a', { in3d: true, selected: false, board: leaving })).toBeNull();
  });
});
