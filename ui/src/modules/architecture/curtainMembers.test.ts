import { describe, expect, it } from 'vitest';
import { keysFor } from './CurtainMembersDialog';

const pieces = [
  { key: 'mullion:0', role: 'mullion' as const }, { key: 'mullion:1', role: 'mullion' as const }, { key: 'mullion:2', role: 'mullion' as const },
  { key: 'transom:0:1', role: 'transom' as const }, { key: 'transom:1:1', role: 'transom' as const },
];

describe('the members a curtain wall styles (report #128)', () => {
  it('sends a role when every member of it is chosen, so it keeps to what the grid becomes', () => {
    expect(keysFor(new Set(['mullion:0', 'mullion:1', 'mullion:2']), pieces)).toEqual(['mullion']);
    expect(keysFor(new Set(pieces.map((p) => p.key)), pieces)).toEqual(['mullion', 'transom']);
  });
  it('sends the pieces when only some are chosen', () => {
    expect(keysFor(new Set(['mullion:1', 'transom:0:1']), pieces)).toEqual(['mullion:1', 'transom:0:1']);
    expect(keysFor(new Set(), pieces)).toEqual([]);
  });
});
