/** Two panes each lending the command line a host: the active pane's picks (decision 655). */

import { beforeEach, describe, expect, it, vi } from 'vitest';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), useSession: { getState: vi.fn(() => ({ nodes: new Map(), error: null })), setState: vi.fn(), subscribe: vi.fn() } }));
vi.mock('../../engine/session', () => engine);

import { useCad, type CadHost } from './session';
import { registerCadHost } from './hosts';
import { closePane, focusPane, splitPane, useUi } from '../../app/store';

const hostOf = (viewId: string, found: string | null): CadHost => ({ viewId, pickDraft: () => null, pickAny: () => found, shapeOf: () => null });

beforeEach(() => {
  closePane(1);
  useUi.setState({ view: 'plan' });
  useCad.setState({ host: null });
});

describe('the command line host with two panes', () => {
  it('a plan beside a 3D view whose work plane registers when a command starts: a click in the plan picks in the plan', () => {
    const offPlan = registerCadHost(hostOf('plan', 'wall'), 0);
    splitPane('row'); // the 3D pane comes in front
    expect(useUi.getState().pane).toBe(1);
    expect(useCad.getState().host?.pickAny({ x: 0, y: 0 })).toBe('wall');
    // A command starts: the 3D view's work plane lends its host (it picks drafted lines only).
    const off3d = registerCadHost(hostOf('plan', null), 1);
    expect(useCad.getState().host?.pickAny({ x: 0, y: 0 })).toBeNull();
    // The click lands in the plan's pane, which focuses it first.
    focusPane(0);
    expect(useCad.getState().host?.pickAny({ x: 0, y: 0 })).toBe('wall');
    focusPane(1);
    expect(useCad.getState().host?.pickAny({ x: 0, y: 0 })).toBeNull();
    // The command ends: the 3D host goes, the plan's stays.
    off3d();
    expect(useCad.getState().host?.pickAny({ x: 0, y: 0 })).toBe('wall');
    offPlan();
    expect(useCad.getState().host).toBeNull();
  });

  it('a view in the inactive pane registering again (its drawing reloaded) does not take the commands over', () => {
    splitPane('row');
    const off3d = registerCadHost(hostOf('plane', null), 1);
    const offPlan = registerCadHost(hostOf('plan', 'wall'), 0);
    expect(useCad.getState().host?.viewId).toBe('plane');
    off3d();
    expect(useCad.getState().host?.viewId).toBe('plan');
    offPlan();
  });
});
