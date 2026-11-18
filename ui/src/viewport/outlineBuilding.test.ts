import { describe, expect, it, vi } from 'vitest';

const call = vi.fn();
vi.mock('../engine/session', () => ({
  engine: { call: (...a: unknown[]) => call(...a) },
  query: vi.fn(),
  useSession: { getState: () => ({ nodes: new Map(), render: new Map() }) },
}));

const { pickOutline, PICKED } = await import('./outlineShape');

describe('the Floor tool\'s Trace building (forum topic 158)', () => {
  it('takes the engine\'s traced outline, its arcs included, for the storey and the point clicked', async () => {
    call.mockResolvedValueOnce({ points: [[-0.1, -0.1], [10.1, -0.1], [10.1, 6.04], [-0.1, 6.04]], arcs: [0, 0, -2.06, 0] });
    const o = await pickOutline('building', { x: 5, y: 3 }, 's1');
    expect(call).toHaveBeenCalledWith('doc.query', { name: 'architecture.buildingOutline', args: { storeyId: 's1', at: [5, 3] } });
    expect(typeof o).not.toBe('string');
    if (typeof o === 'string') return;
    expect(o.pts).toHaveLength(4);
    expect(o.arcs).toEqual([0, 0, -2.06, 0]);
    expect(PICKED).toContain('building');
  });

  it('says why when the walls leave a gap, as the engine names it', async () => {
    call.mockRejectedValueOnce(new Error('The walls do not close round the building: there is a gap of 1 m between (5.5, 8) and (4.5, 8). Close it, or draw the floor\'s outline.'));
    expect(await pickOutline('building', { x: 5, y: 3 }, 's1')).toMatch(/gap of 1 m between \(5\.5, 8\) and \(4\.5, 8\)/);
  });
});
