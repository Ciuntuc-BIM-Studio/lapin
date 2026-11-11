import { describe, expect, it } from 'vitest';
// @ts-expect-error a plain script, shared with scripts/check-web.mjs
import { unanswered, uiCalls } from '../../../scripts/check-commands.mjs';

describe('commands and queries the interface calls', () => {
  it('all exist in the engine, as the kind they are called as', () => {
    expect(uiCalls().length).toBeGreaterThan(100);
    expect(unanswered().map((b: { file: string; mod: string; name: string; why: string }) => `${b.file} ${b.mod}.${b.name}: ${b.why}`)).toEqual([]);
  });
});
