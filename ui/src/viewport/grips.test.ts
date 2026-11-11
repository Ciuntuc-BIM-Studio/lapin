import { describe, expect, it } from 'vitest';
import { groupGrips, movers, snapGrip } from './grips';
import type { Grip } from '../platform/module';

const g = (owner: string, key: string, x: number, y: number, kind: Grip['kind'] = 'end'): Grip => ({ owner, key, at: { x, y }, kind });

describe('grips of the selection', () => {
  it('gathers the ends of different elements at one point, keeps one element\'s own grips apart and kinds apart', () => {
    const groups = groupGrips([g('a', 'end', 4, 0), g('b', 'start', 4.001, 0), g('a', 'start', 0, 0), g('c', 'mid', 4, 0, 'edgeMid'), g('a', 'x', 4, 0.002)], 0.01);
    const shared = groups.find((x) => x.members.length === 2)!;
    expect(shared.members.map((m) => m.owner)).toEqual(['a', 'b']);
    expect(groups.find((x) => x.members[0].kind === 'edgeMid')!.members).toHaveLength(1);
    // Element a's second grip near the same point is its own, not merged with its first.
    expect(groups.filter((x) => x.members.some((m) => m.owner === 'a'))).toHaveLength(3);
  });

  it('moves everyone, or one with Alt, or the one Tab picked (then all again)', () => {
    const [group] = groupGrips([g('a', 'end', 4, 0), g('b', 'start', 4, 0)], 0.01);
    const owners = (p: number, alone = false) => movers(group, p, alone).map((m) => m.owner);
    expect(owners(2)).toEqual(['a', 'b']);
    expect(owners(2, true)).toEqual(['a']);
    expect(owners(0)).toEqual(['a']);
    expect(owners(1)).toEqual(['b']);
    expect(owners(3)).toEqual(['a']);
    expect(owners(-1)).toEqual(['a', 'b']);
  });
});

describe('a module\'s grip snap', () => {
  const node = { id: 'a' } as never;
  const grip = g('a', 'end', 0, 0);
  const tenMm = (_n: unknown, _g: unknown, to: { x: number; y: number }) => ({ x: Math.round(to.x * 100) / 100, y: Math.round(to.y * 100) / 100 });
  it('takes the first answer, and leaves the point when none answers or the grip is free', () => {
    const to = { x: 1.2349, y: 2 };
    expect(snapGrip([undefined, () => undefined, tenMm, () => ({ x: 9, y: 9 })], node, grip, to, false)).toEqual({ x: 1.23, y: 2 });
    expect(snapGrip([() => undefined], node, grip, to, false)).toBe(to);
    expect(snapGrip([tenMm], node, grip, to, true)).toBe(to);
  });
});
