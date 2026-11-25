import { describe, expect, it, vi } from 'vitest';
import { scrubValue } from './cells';
import { pieSectors } from './PieMenu';
import { allStepsFor, countNodes, evaluateMissions, noteMissionEvent, openOnboarding, resetOnboarding, useOnboarding } from '../app/onboarding';
import { gotoCameraBookmark, saveCameraBookmark, useCameraBookmarks } from '../app/cameraBookmarks';
import { recordToolClick, resetMicroTips } from '../app/microTips';
import { useCamera } from '../viewport/camera';
import { useUi } from '../app/store';

describe('Phase 2 game-like UX', () => {
  it('computes clean horizontal drag-to-scrub values with fine/coarse modifiers and clamping', () => {
    const cfg = { step: 0.01, fine: 0.001, coarse: 0.1, min: 0, max: 10 };
    expect(scrubValue(2.5, 40, cfg, { shift: false, ctrl: false })).toBe(2.6);
    expect(scrubValue(2.5, 40, cfg, { shift: true, ctrl: false })).toBe(2.51);
    expect(scrubValue(2.5, 40, cfg, { shift: false, ctrl: true })).toBe(3.5);
    expect(scrubValue(0.05, -100, cfg, { shift: false, ctrl: false })).toBe(0);
    expect(scrubValue(9.9, 100, cfg, { shift: false, ctrl: false })).toBe(10);
  });

  it('builds 8 context-aware pie menu sectors for selection vs empty canvas', () => {
    const sel = pieSectors(true);
    const empty = pieSectors(false);
    expect(sel).toHaveLength(8);
    expect(empty).toHaveLength(8);
    expect(sel.map((s) => s.id)).toContain('move');
    expect(sel.map((s) => s.id)).toContain('similar');
    expect(empty.map((s) => s.id)).toContain('wall');
    expect(empty.map((s) => s.id)).toContain('walk');
  });

  it('evaluates role-differentiated onboarding steps against live state', () => {
    resetOnboarding();
    openOnboarding('role');
    expect(allStepsFor('architecture').length).toBe(7);
    expect(allStepsFor('structure').some((s) => s.id === 'str.column')).toBe(true);
    expect(allStepsFor('mep').some((s) => s.id === 'mep.xray')).toBe(true);
    expect(allStepsFor('site').some((s) => s.id === 'site.terrain')).toBe(true);
    expect(allStepsFor('drafting').some((s) => s.id === 'cad.dim')).toBe(true);

    noteMissionEvent('xrayPeek');
    useUi.setState({ view: '3d', dialog: 'palette' });
    const newly = evaluateMissions('mep');
    expect(newly).toContain('mep.xray');
    expect(newly).toContain('mep.3d');
    expect(newly).toContain('basics.palette');
    expect(countNodes([]).total).toBe(0);
    useOnboarding.setState({ open: false });
  });

  it('saves and jumps to 3D camera bookmarks smoothly', () => {
    useCameraBookmarks.setState({ slots: {} });
    const orbitTo = vi.fn();
    useCamera.setState({ azimuth: 1.2, pitch: -0.4, distance: 15, target: { x: 3, y: 4, z: 0 }, api: { orbitTo } as never });
    expect(gotoCameraBookmark(1)).toBe(false);
    expect(saveCameraBookmark(1)).toBe(true);
    expect(gotoCameraBookmark(1)).toBe(true);
    expect(orbitTo).toHaveBeenCalledWith({ azimuth: 1.2, pitch: -0.4, distance: 15, target: { x: 3, y: 4, z: 0 } }, true);
  });

  it('fires a one-time Just-in-Time micro-tip on the 3rd manual ribbon click', () => {
    resetMicroTips();
    expect(recordToolClick('architecture.wall', 'Wall', 'WA')).toBeNull();
    expect(recordToolClick('architecture.wall', 'Wall', 'WA')).toBeNull();
    expect(recordToolClick('architecture.wall', 'Wall', 'WA')).toContain('WA');
    // Never repeats once shown.
    expect(recordToolClick('architecture.wall', 'Wall', 'WA')).toBeNull();
  });
});
