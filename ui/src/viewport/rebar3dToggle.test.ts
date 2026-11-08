import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { NodeDto } from '../engine/protocol';

const engine = vi.hoisted(() => ({ execute: vi.fn(), query: vi.fn(), useSession: vi.fn() }));
vi.mock('../engine/session', () => engine);

import { rebarKey, toggleRebar3d } from './rebar3d';

const node = (id: string, type: string, props: Record<string, unknown>): NodeDto => ({ id, type, name: id, props } as unknown as NodeDto);

describe('the bars in 3D under the Rebar category (decision 402: "architects don\'t want to see rebars")', () => {
  beforeEach(() => { engine.execute.mockReset(); engine.query.mockReset(); });

  it('are asked again when the 3D view\'s graphics change, not when another project setting does', () => {
    const nodes = new Map<string, NodeDto>([['p', node('p', 'IfcProject', { view3d: {}, units: 'm' })]]);
    const key = rebarKey(nodes);
    nodes.set('p', node('p', 'IfcProject', { view3d: {}, units: 'mm' }));
    expect(rebarKey(nodes)).toBe(key);
    nodes.set('p', node('p', 'IfcProject', { view3d: { graphics: { categories: { rebar: { hidden: false } } } }, units: 'mm' }));
    expect(rebarKey(nodes)).not.toBe(key);
    // A saved 3D view's own graphics.
    nodes.set('v', node('v', 'LapinView', { kind: '3d' }));
    const saved = rebarKey(nodes, 'v');
    nodes.set('v', node('v', 'LapinView', { kind: '3d', graphics: { categories: { rebar: { hidden: true } } } }));
    expect(rebarKey(nodes, 'v')).not.toBe(saved);
  });

  it('Bars in 3D shows them in an architectural 3D (hidden by default) and hides them again without leaving a row', async () => {
    engine.query.mockResolvedValueOnce({ graphics: {}, hiddenByDefault: ['spaces', 'rebar'] });
    expect(await toggleRebar3d('3d')).toBe(true);
    expect(engine.execute).toHaveBeenLastCalledWith('view.graphics', { id: '3d', category: 'rebar', value: { hidden: false } });
    engine.query.mockResolvedValueOnce({ graphics: { categories: { rebar: { hidden: false, halftone: true } } }, hiddenByDefault: ['rebar'] });
    expect(await toggleRebar3d('3d')).toBe(false);
    expect(engine.execute).toHaveBeenLastCalledWith('view.graphics', { id: '3d', category: 'rebar', value: { halftone: true } });
  });

  it('hides them in a structural 3D, where they show by default', async () => {
    engine.query.mockResolvedValueOnce({ graphics: null, hiddenByDefault: ['spaces'] });
    expect(await toggleRebar3d('v1')).toBe(false);
    expect(engine.execute).toHaveBeenLastCalledWith('view.graphics', { id: 'v1', category: 'rebar', value: { hidden: true } });
  });
});
