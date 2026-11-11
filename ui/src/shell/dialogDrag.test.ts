import { describe, expect, it } from 'vitest';
import { clampOffset } from './dialogDrag';

const win = { w: 1000, h: 800 };
const box = { left: 300, top: 200, right: 700, bottom: 600 };

describe('dialog dragging', () => {
  it('moves freely inside the window', () => {
    expect(clampOffset(box, win, { x: 100, y: -50 })).toEqual({ x: 100, y: -50 });
  });
  it('keeps the dialog wholly inside the window', () => {
    expect(clampOffset(box, win, { x: -900, y: 900 })).toEqual({ x: -300, y: 200 });
    expect(clampOffset(box, win, { x: 900, y: -900 })).toEqual({ x: 300, y: -200 });
  });
  it('centres on an axis where the dialog is larger than the window', () => {
    expect(clampOffset({ left: -50, top: 0, right: 1050, bottom: 400 }, win, { x: 80, y: 0 }).x).toBe(0);
  });
});
