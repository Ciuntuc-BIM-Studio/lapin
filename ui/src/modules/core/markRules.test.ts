import { describe, expect, it } from 'vitest';
import { DEFAULT_MARK_PATTERN, isDefaultRule, patternOk, sameRule } from './MarkRulesDialog';

// Decision 737: the rules dialog sends only what changed, a rule back to the built-in one as null, and no pattern the engine refuses.
describe('mark rules', () => {
  const builtIn = { prefix: '', suffix: '', pattern: DEFAULT_MARK_PATTERN, scope: 'project' as const };

  it('knows the built-in rule, an empty pattern being it', () => {
    expect(isDefaultRule(builtIn)).toBe(true);
    expect(isDefaultRule({ ...builtIn, pattern: '  ' })).toBe(true);
    expect(isDefaultRule({ ...builtIn, prefix: 'P-' })).toBe(false);
    expect(isDefaultRule({ ...builtIn, scope: 'storey' })).toBe(false);
    expect(isDefaultRule({ ...builtIn, pattern: '{storey.code}-W{= n | 00}' })).toBe(false);
  });

  it('compares rules as the engine keeps them', () => {
    expect(sameRule(builtIn, { ...builtIn, pattern: '' })).toBe(true);
    expect(sameRule(builtIn, { ...builtIn, suffix: '/A' })).toBe(false);
  });

  it('accepts a pattern whose fields are each closed', () => {
    expect(patternOk('{storey.code}-W{= n | 00}')).toBe(true);
    expect(patternOk('{prefix}{n')).toBe(false);
    expect(patternOk('n}')).toBe(false);
    expect(patternOk('{{n}}')).toBe(false);
  });
});
