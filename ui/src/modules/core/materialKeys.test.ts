import { describe, expect, it } from 'vitest';
import { isKeyFormat, keyFormatArg, writeKey } from './materialKeys';

describe('material key formats', () => {
  it('write the number as the engine does', () => {
    expect(writeKey('{n}', 3)).toBe('3');
    expect(writeKey('F{n}', 3)).toBe('F3');
    expect(writeKey('M-{n:00}', 3)).toBe('M-03');
    expect(writeKey('M-{n:00}', 123)).toBe('M-123');
  });

  it('hold their number once, short', () => {
    expect(isKeyFormat('F{n}')).toBe(true);
    expect(isKeyFormat('{n:000}')).toBe(true);
    expect(isKeyFormat('F')).toBe(false);
    expect(isKeyFormat('{n}{n}')).toBe(false);
    expect(isKeyFormat('A very long prefix of a key {n}')).toBe(false);
  });

  it('give no value of their own for the default', () => {
    expect(keyFormatArg(' {n} ')).toBeNull();
    expect(keyFormatArg('')).toBeNull();
    expect(keyFormatArg(' F{n} ')).toBe('F{n}');
  });
});
