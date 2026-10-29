import { describe, expect, it } from 'vitest';
import { translate } from './index';

describe('the Romanian interface', () => {
  it('puts known phrases into Romanian, keeping their spaces, and leaves the others', () => {
    expect(translate('Wall')).toBe('Perete');
    expect(translate(' Floor plans ')).toBe(' Planuri ');
    expect(translate('12 elements')).toBe('12 elemente');
    expect(translate('My own wall name')).toBeNull();
    expect(translate('')).toBeNull();
  });

  it('translates the undo buttons\' tooltips with what they undo and their keys', () => {
    expect(translate('Undo (Ctrl+Z)')).toBe('Anulează (Ctrl+Z)');
    expect(translate('Undo Move (Ctrl+Z)')).toBe('Anulează Mută (Ctrl+Z)');
    expect(translate('Redo My wall (Ctrl+Y, Ctrl+Shift+Z)')).toBe('Refă My wall (Ctrl+Y, Ctrl+Shift+Z)');
    expect(translate('Redo')).toBe('Refă');
  });
});
