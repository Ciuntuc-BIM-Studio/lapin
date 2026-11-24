/** Join walls (JW, forum topic 249): a wall clicked, then the one to join it to; two selected are joined at once; both carried on to where they meet. */
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

const execute = vi.hoisted(() => vi.fn(async () => ({ kind: 'L' })));
vi.mock('../../engine/session', async (orig) => ({ ...(await orig<typeof import('../../engine/session')>()), execute }));

import { useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { cancel, feedPoint, submit, useCad } from '../../viewport/cad/session';
import type { NodeDto } from '../../engine/protocol';

const node = (id: string, type: string): NodeDto => ({ id, type, name: id, parentId: 's', px: 0, py: 0, props: { start: [0, 0], end: [1, 0] }, editable: true, module: null, storeyId: 's' });
let under: string | null = null;

beforeEach(() => {
  execute.mockClear();
  cancel(true);
  useSession.setState({ nodes: new Map([['w1', node('w1', 'IfcWall')], ['w2', node('w2', 'IfcWall')], ['c1', node('c1', 'IfcColumn')]]) } as never);
  useUi.setState({ tool: null, selected: [], selection: null });
  useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => under, pickModel: () => under, shapeOf: () => null }, history: [] });
});

let modules: typeof import('../../platform/modules');
beforeAll(async () => { modules = await import('../../platform/modules'); }, 60_000);
const tool = async () => modules.findTool('architecture.join')!;
const click = async (id: string | null) => { under = id; await feedPoint({ x: 0, y: 0 }); };

describe('Join walls', () => {
  it('takes a wall clicked, then the wall to join it to, and joins them carried on to where they meet; again until Enter', async () => {
    (await tool()).run!();
    await vi.waitFor(() => expect(useCad.getState().command).toBe('JOINWALLS'));
    expect(useCad.getState().step?.hint).toMatch(/first wall/);
    await click('w1');
    expect(useCad.getState().step?.hint).toMatch(/carried on/);
    await click('c1'); // not a wall: still waiting for the second
    await click('w1'); // the same wall: still waiting
    expect(execute).not.toHaveBeenCalled();
    await click('w2');
    expect(execute).toHaveBeenCalledWith('architecture.joinWalls', expect.objectContaining({ a: 'w1', b: 'w2', extend: true }));
    expect(useCad.getState().command).toBe('JOINWALLS');
    await submit('');
    expect(useCad.getState().command).toBeNull();
  });

  it('joins two selected walls at once, and starts from one selected', async () => {
    useUi.setState({ selected: ['w1', 'w2'] });
    (await tool()).run!();
    expect(execute).toHaveBeenCalledWith('architecture.joinWalls', expect.objectContaining({ a: 'w1', b: 'w2', extend: true }));
    execute.mockClear();
    useUi.setState({ selected: ['w2'] });
    (await tool()).run!();
    await vi.waitFor(() => expect(useCad.getState().command).toBe('JOINWALLS'));
    await click('w1');
    expect(execute).toHaveBeenCalledWith('architecture.joinWalls', expect.objectContaining({ a: 'w2', b: 'w1' }));
  });
});
