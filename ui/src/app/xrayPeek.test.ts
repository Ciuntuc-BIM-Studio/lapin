import { beforeEach, describe, expect, it } from 'vitest';
import { beginXrayPeek, canStartXrayPeek, endXrayPeek, useXrayPeek } from './xrayPeek';
import { useUi } from './store';
import { useCad } from '../viewport/cad/session';

describe('Hold-to-Peek X-Ray', () => {
  beforeEach(() => {
    useXrayPeek.setState({ active: false, prev: null });
    useUi.setState({ screen: 'workspace', view: '3d', dialog: null, visualStyle: 'shaded' });
    useCad.setState({ buffer: '', step: null });
  });

  it('allows peeking only in 3D workspace when idle and not already in xray', () => {
    const base = { screen: 'workspace', view: '3d', dialog: null, visualStyle: 'shaded' as const, buffer: '', hasStep: false, liveActive: false, hasValueOwner: false, walking: false };
    expect(canStartXrayPeek(base)).toBe(true);
    expect(canStartXrayPeek({ ...base, buffer: 'x' })).toBe(true);
    expect(canStartXrayPeek({ ...base, buffer: 'wa' })).toBe(false);
    expect(canStartXrayPeek({ ...base, view: 'plan' })).toBe(false);
    expect(canStartXrayPeek({ ...base, dialog: 'palette' })).toBe(false);
    expect(canStartXrayPeek({ ...base, visualStyle: 'xray' })).toBe(false);
    expect(canStartXrayPeek({ ...base, walking: true })).toBe(false);
  });

  it('switches to xray, clears a lone x from command buffer, and restores the prior style on release', () => {
    useUi.setState({ visualStyle: 'edges' });
    useCad.setState({ buffer: 'x' });
    expect(beginXrayPeek()).toBe(true);
    expect(useUi.getState().visualStyle).toBe('xray');
    expect(useCad.getState().buffer).toBe('');
    expect(useXrayPeek.getState()).toEqual({ active: true, prev: 'edges' });

    endXrayPeek();
    expect(useUi.getState().visualStyle).toBe('edges');
    expect(useXrayPeek.getState()).toEqual({ active: false, prev: null });
  });
});
