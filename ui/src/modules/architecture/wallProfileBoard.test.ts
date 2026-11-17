import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { boardOf, isBoardView, takeReturn, rememberReturn } from '../../viewport/sketch/board';
import './wallProfileBoard';
import '../core/profileLibraries/profileBoard';

const node = (id: string, props: Record<string, unknown>, name = id): NodeDto => ({ id, type: 'LapinView', name, props } as unknown as NodeDto);

describe('the wall profile and section profile boards (audit S11 stage 2)', () => {
  it('claim their own views, with Draft\'s tools, constraints and a side form', () => {
    const nodes = new Map([
      ['w', node('w', { kind: 'plane', wallProfileOf: { id: 'wall1', length: 6, top: [[0, 3], [6, 3]] } })],
      ['wall1', node('wall1', {}, 'Brick wall')],
      ['p', node('p', { kind: 'plane', profileOf: { name: 'Box', material: 'aluminium', library: 'My profiles', rings: [] } })],
      ['plain', node('plain', { kind: 'plane' })],
    ]);
    const w = boardOf('w', nodes)!;
    expect(w.kind).toBe('wall-profile');
    expect(w.title).toBe('Brick wall');
    expect(w.tools).toContain('ARC');
    expect(w.panel).toBeTypeOf('function');
    const p = boardOf('p', nodes)!;
    expect(p.kind).toBe('profile');
    expect(p.title).toBe('Box');
    expect(p.panel).toBeTypeOf('function');
    expect(isBoardView('w', nodes) && isBoardView('p', nodes)).toBe(true);
    expect(boardOf('plain', nodes)).toBeNull();
  });

  it('remember the place they were opened from once, and give it back once', () => {
    rememberReturn('v', { view: 'plan' });
    rememberReturn('v', { view: '3d' });
    expect(takeReturn('v')).toEqual({ view: 'plan' });
    expect(takeReturn('v')).toBeNull();
  });
});
