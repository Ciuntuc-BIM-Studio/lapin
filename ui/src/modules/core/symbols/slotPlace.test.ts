import { describe, expect, it } from 'vitest';
import { listPlace, slotsFor } from './SlotPicker';
import { liveHintSeen, liveToggleMode, resetLiveHint, useLive } from '../../../viewport/liveInput';

describe('listPlace (reports #168, #169)', () => {
  it('opens below with room, as tall as the room allows', () => {
    const p = listPlace({ left: 100, top: 100, bottom: 126 }, 1200, 900);
    expect(p).toMatchObject({ position: 'fixed', left: 100, top: 126, maxHeight: 420 });
  });
  it('opens above near the bottom of the window', () => {
    const p = listPlace({ left: 100, top: 600, bottom: 626 }, 1200, 700);
    expect(p.bottom).toBe(100);
    expect(p.top).toBe('auto');
  });
  it('stays inside the window on the right', () => {
    expect(listPlace({ left: 1190, top: 10, bottom: 30 }, 1200, 900).left).toBe(936);
  });
});

describe('slotsFor: the north arrow slot (report #168)', () => {
  const view = (props: Record<string, unknown>) => ({ type: 'LapinView', props } as unknown as Parameters<typeof slotsFor>[0]);
  it('only a site plan has it', () => {
    expect(slotsFor(view({ kind: 'plan' }))).toEqual(['viewTitle']);
    expect(slotsFor(view({ kind: 'plan', site: 'site' }))).toEqual(['northArrow', 'viewTitle']);
  });
});

describe('the rectangle hint (report #173)', () => {
  it('shows until the box has been switched once', () => {
    resetLiveHint();
    try { localStorage.removeItem('lapin.liveHintSeen'); } catch { /* none */ }
    expect(liveHintSeen()).toBe(false);
    useLive.setState({ rect: 'test-rect', mode: 'rect' });
    liveToggleMode();
    expect(liveHintSeen()).toBe(true);
    resetLiveHint();
    try { localStorage.removeItem('lapin.liveHintSeen'); } catch { /* none */ }
  });
});
