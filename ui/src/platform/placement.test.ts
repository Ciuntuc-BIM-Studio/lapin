import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { askPlacement, placedAt, placementKey, requestFor, resetPlacement, usePlacement, verdictFor } from './placement';

const wall = { id: 'w', type: 'IfcWall', name: 'W', props: { start: [0, 0], end: [6, 0] } } as unknown as NodeDto;
const arc = { a: { x: 0, y: 0 }, b: { x: 6, y: 0 }, rise: 0 };
const what = { kind: 'door' as const, width: 0.9, typeId: 't' };

describe('placement checks', () => {
  beforeEach(() => { vi.useFakeTimers(); resetPlacement(); });

  it('keeps the offered place only when the cursor is within a metre of it, on the cursor\'s side', () => {
    const v = { ok: false, message: 'no', snapAlong: 3 };
    expect(placedAt(arc, { x: 2.5, y: 0.2 }, v)).toEqual({ point: { x: 3, y: 0.2 }, snapped: true });
    expect(placedAt(arc, { x: 1.5, y: 0.2 }, v).snapped).toBe(false);
    expect(placedAt(arc, { x: 2.5, y: 0.2 }, { ok: true, snapAlong: 3 }).snapped).toBe(false);
    expect(placedAt(arc, { x: 2.5, y: 0.2 }, null).snapped).toBe(false);
  });

  it('asks the first check that answers, keeps the answer with its request, and asks once per place', async () => {
    const r = requestFor(wall, { x: 2, y: 0.1 }, what);
    if (!r) throw new Error('no axis');
    const none = vi.fn(() => null);
    const refuse = vi.fn(async () => ({ ok: false, message: 'too close' }));
    askPlacement(r.req, [none, refuse]);
    await vi.runAllTimersAsync();
    expect(verdictFor(r.req)).toEqual({ ok: false, message: 'too close' });
    askPlacement(r.req, [none, refuse]);
    await vi.runAllTimersAsync();
    expect(refuse).toHaveBeenCalledTimes(1);
    const moved = requestFor(wall, { x: 3, y: 0.1 }, what)!;
    expect(placementKey(moved.req)).not.toBe(placementKey(r.req));
    expect(verdictFor(moved.req)).toBeNull();
  });

  it('answers only the latest question when the cursor moves on', async () => {
    const slow = (ms: number) => async () => { await new Promise((res) => setTimeout(res, ms)); return { ok: true }; };
    const a = requestFor(wall, { x: 1, y: 0 }, what)!, b = requestFor(wall, { x: 4, y: 0 }, what)!;
    askPlacement(a.req, [slow(50)]);
    askPlacement(b.req, [slow(10)]);
    await vi.runAllTimersAsync();
    expect(usePlacement.getState().key).toBe(placementKey(b.req));
  });
});

describe('wrapText', () => {
  it('breaks a sentence at spaces into lines of at most the width', async () => {
    const { wrapText } = await import('../viewport/wrapText');
    expect(wrapText('The door cannot go there: it is longer than the wall.', 20)).toEqual(['The door cannot go', 'there: it is longer', 'than the wall.']);
    expect(wrapText('', 20)).toEqual([]);
  });
});
