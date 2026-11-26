import { describe, expect, it } from 'vitest';
import { NOTES_LINES, shortNotes } from './updates';

describe('the update window\'s notes', () => {
  it('shows nothing when there are none', () => {
    expect(shortNotes(undefined)).toBe('');
    expect(shortNotes('  \n ')).toBe('');
  });

  it('shows short notes whole', () => {
    expect(shortNotes('- one\r\n- two')).toBe('\n\n- one\n- two');
  });

  it('cuts a long list, so the window fits a small screen', () => {
    const body = Array.from({ length: 60 }, (_, i) => `- change ${i}`).join('\n');
    const lines = shortNotes(body).trim().split('\n');
    expect(lines).toHaveLength(NOTES_LINES + 1);
    expect(lines.at(-1)).toBe('…');
  });

  it('cuts by length too, and one very long line', () => {
    expect(shortNotes('x'.repeat(5000)).length).toBeLessThan(200);
    const many = Array.from({ length: 9 }, () => 'y'.repeat(150)).join('\n');
    expect(shortNotes(many).length).toBeLessThan(800);
  });
});
