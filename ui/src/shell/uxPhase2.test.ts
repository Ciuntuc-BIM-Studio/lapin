import { describe, expect, it, vi } from 'vitest';
import { scrubValue } from './cells';
import { pieSectors } from './PieMenu';
import { chordOf } from '../app/shortcuts';
import { commands } from '../app/commands';
import { allStepsFor, countNodes, sandboxSteps, evaluateMissions, noteMissionEvent, openOnboarding, resetOnboarding, useOnboarding } from '../app/onboarding';
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
    // An integer field (a count) never gets a fraction, Shift's fine step included.
    const count = { step: 1, fine: 0.1, coarse: 10, min: 1, integer: true };
    expect(scrubValue(3, 40, count, { shift: true, ctrl: false })).toBe(13);
    expect(scrubValue(3, 12, count, { shift: false, ctrl: false })).toBe(6);
    expect(scrubValue(2.6, 0, count, { shift: false, ctrl: false })).toBe(3);
    expect(scrubValue(3, -400, count, { shift: false, ctrl: true })).toBe(1);
  });

  it('builds a role sandbox from the engine\'s real commands, as one batch', () => {
    const all = () => true;
    const arch = sandboxSteps('architecture', 's1', all);
    expect(arch.map((s) => s.command)).toEqual(['architecture.addWall', 'architecture.addWall', 'architecture.addWall', 'architecture.addSlab']);
    expect(arch[0].args).toEqual({ storeyId: 's1', x1: 0, y1: 0, x2: 6, y2: 0 });
    expect(arch[3].args.points).toEqual([[0, 0], [6, 0], [6, 5], [0, 5]]);
    expect(sandboxSteps('structure', 's1', all).map((s) => s.command)).toEqual(['structure.addColumn', 'structure.addColumn', 'structure.addColumn']);
    expect(sandboxSteps('structure', 's1', (m) => m !== 'structure')).toEqual([]);
    expect(sandboxSteps('drafting', 's1', all)).toEqual([]);
  });

  it('binds camera bookmarks to Alt+digit by the key\'s place, free of the system\'s screenshot keys', () => {
    const mac = { key: '¡', code: 'Digit1', ctrlKey: false, metaKey: false, altKey: true, shiftKey: false };
    expect(chordOf(mac)).toBe('Alt+1');
    expect(chordOf({ ...mac, key: '⁄', shiftKey: true })).toBe('Alt+Shift+1');
    expect(chordOf({ ...mac, key: '#', code: 'Digit3', shiftKey: true })).toBe('Alt+Shift+3');
    const defs = commands();
    expect(defs.find((c) => c.id === 'app.bookmarkGoto.3')?.defaults).toEqual(['Alt+3']);
    expect(defs.find((c) => c.id === 'app.bookmarkSave.4')?.defaults).toEqual(['Alt+Shift+4']);
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
