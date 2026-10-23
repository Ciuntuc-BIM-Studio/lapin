import { describe, expect, it } from 'vitest';
import { decodeFrame, encodeFrame } from './frame';

describe('engine frames', () => {
  it('reads the header and turns buffer references into typed arrays', () => {
    const positions = new Float32Array([0, 1, 2, 3.5]);
    const indices = new Int32Array([0, 1, 2]);
    const frame = encodeFrame({ id: 4, result: { name: 'wall', positions: { $buf: 0, n: 4, t: 'f32' }, indices: { $buf: 16, n: 3, t: 'i32' }, list: [1, 2] } }, [positions, indices]);
    const msg = decodeFrame<{ id: number; result: { name: string; positions: Float32Array; indices: Int32Array; list: number[] } }>(frame);
    expect(msg.id).toBe(4);
    expect(msg.result.name).toBe('wall');
    expect(msg.result.positions).toBeInstanceOf(Float32Array);
    expect([...msg.result.positions]).toEqual([0, 1, 2, 3.5]);
    expect([...msg.result.indices]).toEqual([0, 1, 2]);
    expect(msg.result.list).toEqual([1, 2]);
  });

  it('refuses what is not a frame', () => {
    expect(() => decodeFrame(new TextEncoder().encode('{"id":1}').buffer)).toThrow(/Not a Lapin frame/);
  });
});
