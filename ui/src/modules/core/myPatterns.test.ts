import { describe, expect, it } from 'vitest';
import { modelPatternNames } from './myPatterns';

describe('my patterns', () => {
  it('finds the real-size patterns by their %TYPE comment', () => {
    const text = ';%TYPE=MODEL\n*BRICK, brick\n0, 0,0, 0,75\n;%TYPE=PAPER\n*ANSI31, lines\n45, 0,0, 0,3\n';
    expect(modelPatternNames(text)).toEqual(['BRICK']);
    expect(modelPatternNames('*A, a\n45,0,0,0,3')).toEqual([]);
  });
});
