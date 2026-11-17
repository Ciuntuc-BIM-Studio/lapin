import { describe, expect, it } from 'vitest';
import { EXT_SUPPRESS, EXT_SUPPRESS_LABELS, extSuppressAttr } from './dimensionLines';

describe("a dimension's extension lines left out (issue #66)", () => {
  it('stores one of the choices, and empty or anything else as its style', () => {
    expect(EXT_SUPPRESS.map(extSuppressAttr)).toEqual(['none', 'first', 'second', 'both']);
    expect(extSuppressAttr('')).toBeNull();
    expect(extSuppressAttr('sometimes')).toBeNull();
  });

  it('labels every choice', () => {
    expect(EXT_SUPPRESS.every((c) => EXT_SUPPRESS_LABELS[c])).toBe(true);
  });
});
