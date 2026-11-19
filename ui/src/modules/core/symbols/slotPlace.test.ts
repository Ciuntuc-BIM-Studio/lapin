import { describe, expect, it } from 'vitest';
import { listPlace } from './SlotPicker';

describe('listPlace (reports #168, #169)', () => {
  it('opens below with room, as tall as the room allows', () => {
    const p = listPlace({ left: 100, top: 100, bottom: 126 }, 1200, 900);
    expect(p).toMatchObject({ position: 'fixed', left: 100, top: 126, maxHeight: 420 });
  });
  it('opens above near the bottom of the window', () => {
    const p = listPlace({ left: 100, top: 600, bottom: 626 }, 1200, 700);
    expect(p.bottom).toBe(100);
    expect(p.top).toBeUndefined();
  });
  it('stays inside the window on the right', () => {
    expect(listPlace({ left: 1190, top: 10, bottom: 30 }, 1200, 900).left).toBe(936);
  });
});
