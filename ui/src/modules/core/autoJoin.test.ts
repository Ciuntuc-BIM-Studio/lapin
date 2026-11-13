import { describe, expect, it } from 'vitest';
import { autoJoinAt, withAutoJoinMenus } from './autoJoin';
import type { Grip } from '../../platform/module';

describe('an end joining automatically (decision 546)', () => {
  it('is on for a drawn element, off for an imported one, else as set', () => {
    expect(autoJoinAt({ start: [0, 0], end: [1, 0] }, 'start')).toBe(true);
    expect(autoJoinAt({ imported: { ifcClass: 'IfcWall' } }, 'end')).toBe(false);
    expect(autoJoinAt({ imported: {}, autoJoin: { start: true, end: false } }, 'start')).toBe(true);
    expect(autoJoinAt({ autoJoin: { start: true, end: false } }, 'end')).toBe(false);
  });

  it('offers each end grip the other choice, as one command', () => {
    const grips: Grip[] = [{ key: 'start', at: { x: 0, y: 0 } }, { key: 'end', at: { x: 1, y: 0 } }, { key: 'move', at: { x: 0.5, y: 0 } }];
    const out = withAutoJoinMenus('w', { autoJoin: { start: true, end: false } }, grips);
    expect(out[0].menu?.[0].label).toBe("Don't join at this end");
    expect(out[0].menu?.[0].run()).toEqual({ command: 'element.setAutoJoin', args: { ids: ['w'], start: false } });
    expect(out[1].menu?.[0].label).toBe('Join automatically at this end');
    expect(out[1].menu?.[0].run()).toEqual({ command: 'element.setAutoJoin', args: { ids: ['w'], end: true } });
    expect(out[2].menu).toBeUndefined();
  });
});
