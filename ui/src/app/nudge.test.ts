import { describe, expect, it } from 'vitest';
import { addPending, cameraDirection, niceStep, nudgeDistance, nudgeReady, planDirection, viewDirection, NUDGES, type NudgeState } from './nudge';
import { keyIntent, type KeyPress, type KeySnapshot } from './keys';
import { resolveKeymap, type CommandDef } from './shortcuts';
import { readPrefs } from './prefs';

const close = (a: { x: number; y: number }, x: number, y: number) => { expect(a.x).toBeCloseTo(x, 9); expect(a.y).toBeCloseTo(y, 9); };

describe('the nudge step', () => {
  it('rounds to 1, 2 or 5 x 10^n and stays between 1 mm and 1 m', () => {
    expect(niceStep(0.0004)).toBe(0.001);
    expect(niceStep(0.0031)).toBe(0.002);
    expect(niceStep(0.0045)).toBe(0.005);
    expect(niceStep(0.012)).toBe(0.01);
    expect(niceStep(0.2)).toBe(0.2);
    expect(niceStep(7)).toBe(1);
    expect(niceStep(Number.NaN)).toBe(0.001);
  });

  it('follows the zoom: about four pixels, ten times that with Shift', () => {
    // 1000 px per metre: 4 px is 4 mm, rounded to 5 mm.
    expect(nudgeDistance(1000, false, 0)).toBe(0.005);
    expect(nudgeDistance(1000, true, 0)).toBe(0.05);
    // 100 px per metre: 40 mm, rounded to 50.
    expect(nudgeDistance(100, false, null)).toBe(0.05);
    // Zoomed far out it is a metre at most, zoomed far in a millimetre at least.
    expect(nudgeDistance(0.01, false, 0)).toBe(1);
    expect(nudgeDistance(1e6, false, 0)).toBe(0.001);
  });

  it('takes the preference when one is set, whatever the zoom', () => {
    expect(nudgeDistance(1000, false, 0.01)).toBe(0.01);
    expect(nudgeDistance(0.5, false, 0.01)).toBe(0.01);
    expect(nudgeDistance(0.5, true, 0.01)).toBe(0.1);
  });

  it('keeps only a usable step in the preferences', () => {
    expect(readPrefs({ nudgeStep: 0.01 }).nudgeStep).toBe(0.01);
    expect(readPrefs({ nudgeStep: -1 }).nudgeStep).toBe(0);
    expect(readPrefs({ nudgeStep: 'x' }).nudgeStep).toBe(0);
    expect(readPrefs({ nudgeStep: 50 }).nudgeStep).toBe(1);
    expect(readPrefs(null).nudgeStep).toBe(0);
  });
});

describe('the nudge direction', () => {
  it('goes along the screen in a plan that is not turned', () => {
    close(planDirection('right', 0, null), 1, 0);
    close(planDirection('left', 0, null), -1, 0);
    close(planDirection('up', 0, null), 0, 1);
    close(planDirection('down', 0, null), 0, -1);
  });

  it('goes along the screen in a turned plan: the screen is what the user sees', () => {
    // PlanView's rot is -orientation: a plan shown turned by 30 degrees has its screen right at 30 degrees in the plan.
    const rot = (-30 * Math.PI) / 180;
    close(planDirection('right', rot, null), Math.cos(Math.PI / 6), Math.sin(Math.PI / 6));
    close(planDirection('up', rot, null), -Math.sin(Math.PI / 6), Math.cos(Math.PI / 6));
  });

  it('goes along the UCS axis nearest to the arrow when a UCS is set', () => {
    // A UCS turned by 30 degrees, the plan not turned: right goes along its X axis, up along its Y.
    close(planDirection('right', 0, 30), Math.cos(Math.PI / 6), Math.sin(Math.PI / 6));
    close(planDirection('up', 0, 30), -Math.sin(Math.PI / 6), Math.cos(Math.PI / 6));
    close(planDirection('left', 0, 30), -Math.cos(Math.PI / 6), -Math.sin(Math.PI / 6));
    // A UCS turned 80 degrees: its X axis is nearer the screen's up, and the screen's right is nearer its -Y axis.
    close(planDirection('up', 0, 80), Math.cos((80 * Math.PI) / 180), Math.sin((80 * Math.PI) / 180));
    close(planDirection('right', 0, 80), Math.sin((80 * Math.PI) / 180), -Math.cos((80 * Math.PI) / 180));
  });

  it('is the view\'s own in a section, an elevation or a drawing', () => {
    expect(viewDirection('right')).toEqual({ x: 1, y: 0 });
    expect(viewDirection('up')).toEqual({ x: 0, y: 1 });
    expect(viewDirection('down')).toEqual({ x: 0, y: -1 });
  });

  it('is the nearest axis to the camera\'s in 3D: world axes with no UCS, the UCS\'s with one', () => {
    // A camera looking along +X (azimuth 0): right is -Y, up (away) is +X.
    close(cameraDirection('up', 0, null), 1, 0);
    close(cameraDirection('right', 0, null), 0, -1);
    // Looking at 45 degrees and a bit more: the nearest axes.
    close(cameraDirection('up', (50 * Math.PI) / 180, null), 0, 1);
    close(cameraDirection('right', (50 * Math.PI) / 180, null), 1, 0);
    // With a UCS turned 90 degrees, looking along +X: right is world -Y, which is the UCS's -X axis.
    close(cameraDirection('right', 0, 90), 0, -1);
    close(cameraDirection('up', 0, 90), 1, 0);
  });

  it('has eight commands, each an arrow, the big ones with Shift', () => {
    expect(NUDGES).toHaveLength(8);
    expect(NUDGES.filter((n) => n.big).map((n) => n.dir).sort()).toEqual(['down', 'left', 'right', 'up']);
  });
});

describe('when the arrows are the nudge\'s', () => {
  const ok: NudgeState = { screen: 'workspace', dialog: null, view: 'plan', selected: 1, tool: null, typing: false, walking: false, gesture: false, host: true };
  it('needs a selection in a view that moves it', () => {
    expect(nudgeReady(ok)).toBe(true);
    expect(nudgeReady({ ...ok, selected: 0 })).toBe(false);
    expect(nudgeReady({ ...ok, view: 'sheet' })).toBe(false);
    expect(nudgeReady({ ...ok, view: 'schedule' })).toBe(false);
    expect(nudgeReady({ ...ok, view: '3d' })).toBe(true);
    expect(nudgeReady({ ...ok, host: false })).toBe(false);
  });

  it('leaves the arrows to what asks for them', () => {
    expect(nudgeReady({ ...ok, tool: 'wall' })).toBe(false);
    expect(nudgeReady({ ...ok, typing: true })).toBe(false);
    expect(nudgeReady({ ...ok, walking: true })).toBe(false);
    expect(nudgeReady({ ...ok, gesture: true })).toBe(false);
    expect(nudgeReady({ ...ok, dialog: 'materials' })).toBe(false);
    expect(nudgeReady({ ...ok, screen: 'start' })).toBe(false);
  });
});

describe('the keys', () => {
  const cmd = (id: string, defaults: string[]): CommandDef => ({ id, label: id, category: 'test', defaults, run: () => undefined });
  const COMMANDS = [
    cmd('app.delete', ['Delete']),
    ...NUDGES.map((n) => cmd(n.id, [n.big ? `Shift+${n.key}` : n.key, `${n.big ? 'Ctrl+Shift+' : 'Ctrl+'}${n.key}`])),
  ];
  const KEYMAP = resolveKeymap(COMMANDS, Object.fromEntries(COMMANDS.map((c) => [c.id, c.defaults ?? []])), []);
  const press = (key: string, more: Partial<KeyPress> = {}): KeyPress => ({ key, ctrlKey: false, metaKey: false, shiftKey: false, inField: false, inDialog: false, ...more });
  const state = (more: Partial<KeySnapshot> = {}): KeySnapshot => ({ screen: 'workspace', dialog: null, tool: null, selected: ['w1'], buffer: '', step: null, hasLast: false, hotbar: 5, keymap: KEYMAP, nudge: true, ...more });

  it('runs the nudge for an arrow, Shift+arrow and Ctrl or Cmd with them', () => {
    expect(keyIntent(press('ArrowLeft'), state())).toEqual({ kind: 'command', id: 'modify.nudgeLeft' });
    expect(keyIntent(press('ArrowUp', { shiftKey: true }), state())).toEqual({ kind: 'command', id: 'modify.nudgeUpFar' });
    expect(keyIntent(press('ArrowRight', { ctrlKey: true }), state())).toEqual({ kind: 'command', id: 'modify.nudgeRight' });
    expect(keyIntent(press('ArrowDown', { metaKey: true, shiftKey: true }), state())).toEqual({ kind: 'command', id: 'modify.nudgeDownFar' });
  });

  it('leaves the arrows alone when the nudge is not ready, in a field, a tree or a dialog, or while an input method composes', () => {
    expect(keyIntent(press('ArrowLeft'), state({ nudge: false }))).toBeNull();
    expect(keyIntent(press('ArrowLeft', { ctrlKey: true }), state({ nudge: false }))).toBeNull();
    expect(keyIntent(press('ArrowLeft', { inField: true }), state())).toBeNull();
    expect(keyIntent(press('ArrowLeft', { inWidget: true }), state())).toBeNull();
    expect(keyIntent(press('ArrowLeft', { inDialog: true }), state())).toBeNull();
    expect(keyIntent(press('ArrowLeft'), state({ dialog: 'materials' }))).toBeNull();
    expect(keyIntent(press('ArrowLeft', { composing: true }), state())).toBeNull();
    expect(keyIntent(press('ArrowLeft', { altKey: true }), state())).toBeNull();
  });
});

describe('a key held down', () => {
  it('adds the nudges of the same elements into one move, and keeps others apart', () => {
    let q = addPending([], { ids: ['a'], by: [0.005, 0], frame: undefined });
    q = addPending(q, { ids: ['a'], by: [0.005, 0], frame: undefined });
    q = addPending(q, { ids: ['a'], by: [0, 0.005], frame: undefined });
    expect(q).toEqual([{ ids: ['a'], by: [0.01, 0.005], frame: undefined }]);
    q = addPending(q, { ids: ['b'], by: [0.005, 0], frame: undefined });
    q = addPending(q, { ids: ['b'], by: [0.005, 0], frame: { origin: [0, 0, 0] } });
    expect(q).toHaveLength(3);
  });
});
