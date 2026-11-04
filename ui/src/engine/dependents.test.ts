import { describe, expect, it } from 'vitest';
import { dependentsNotice } from './dependents';
import { translate } from '../i18n';

describe('the notice after a deletion', () => {
  it('names what was rebuilt, five at most, and says nothing when nothing was', () => {
    expect(dependentsNotice([])).toBeNull();
    expect(dependentsNotice(['W-02'])).toBe('Deleted. Rebuilt because they depended on it: W-02.');
    expect(dependentsNotice(['a', 'b', 'c', 'd', 'e', 'f', 'g'])).toBe('Deleted. Rebuilt because they depended on it: a, b, c, d, e +2.');
  });

  it('is said in the interface’s language, the names as they are', () => {
    expect(translate('Deleted. Rebuilt because they depended on it: W-02, D1.', 'ro')).toBe('Șters. Reconstruite pentru că depindeau de el: W-02, D1.');
  });
});
