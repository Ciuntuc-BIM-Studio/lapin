/** Decision 739: an angle held on two elements locked and unlocked by its padlock and its right-click menu. */

import { describe, expect, it, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import type { NodeDto } from '../../engine/protocol';

const sent: unknown[][] = [];
vi.mock('../../engine/session', async (orig) => ({
  ...(await orig<typeof import('../../engine/session')>()),
  execute: (command: string, args: unknown) => { sent.push([command, args]); return Promise.resolve(null); },
}));

const { Padlock } = await import('./AngleLock');
const { angleLockItems, lockCommand } = await import('./dimensionMenu');

const angle = (id: string, extra: Record<string, unknown> = {}): NodeDto => ({
  id, type: 'IfcAnnotation', name: 'Angle', parentId: null, px: 0, py: 0, editable: true, module: null, storeyId: null,
  props: { predefinedType: 'DIMENSION', dimKind: 'angular', holds: [{ element: 'a', way: 1 }, { element: 'b', way: 1 }], ...extra },
});

describe('the angle lock', () => {
  it('draws the padlock shut when locked and open when not', () => {
    const shut = renderToStaticMarkup(<svg><Padlock x={0} y={0} locked /></svg>);
    const open = renderToStaticMarkup(<svg><Padlock x={0} y={0} locked={false} /></svg>);
    expect(shut).toContain('data-angle-lock="locked"');
    expect(open).toContain('data-angle-lock="open"');
    expect(shut).not.toEqual(open.replace('open', 'locked'));
  });

  it('toggles by element.update\'s locked, from the right-click menu too', () => {
    expect(lockCommand('d', true)).toEqual(['element.update', { id: 'd', attrs: { locked: true } }]);
    const nodes = new Map([['d', angle('d', { locked: true })], ['e', angle('e')], ['f', angle('f', { holds: undefined })]]);
    const [unlock] = angleLockItems(['d'], nodes);
    expect(unlock.label).toBe('Unlock the angle');
    expect(unlock.checked).toBe(true);
    unlock.onSelect();
    const [lock] = angleLockItems(['e'], nodes);
    expect(lock.label).toBe('Lock the angle');
    lock.onSelect();
    expect(sent).toEqual([['element.update', { id: 'd', attrs: { locked: false } }], ['element.update', { id: 'e', attrs: { locked: true } }]]);
    // An angle on no elements, or two selected, has nothing to lock.
    expect(angleLockItems(['f'], nodes)).toEqual([]);
    expect(angleLockItems(['d', 'e'], nodes)).toEqual([]);
  });
});
