import { describe, expect, it } from 'vitest';
import { materialSetChoice, materialSetPatch } from './viewStyles';
import { currentOf, shortVersion } from './materialStandardsModel';

describe('a view\'s material graphics choice', () => {
  it('sends none or a set id, never an empty text', () => {
    expect(materialSetPatch('none')).toEqual({ materialGraphicsSet: 'none' });
    expect(materialSetPatch('abc')).toEqual({ materialGraphicsSet: 'abc' });
  });
  it('shows none when the view draws by the materials alone', () => {
    expect(materialSetChoice(null)).toBe('none');
    expect(materialSetChoice(undefined)).toBe('none');
    expect(materialSetChoice('abc')).toBe('abc');
  });
});

describe('the standard the project draws by', () => {
  const standards = [{ id: 'lapin', name: 'Lapin', version: '1.1.0' }, { id: 'din', name: 'DIN' }] as never;
  it('is named with its major and minor version', () => {
    expect(shortVersion('1.0.0')).toBe('1.0');
    expect(currentOf({ current: 'lapin', standards })).toBe('Lapin 1.1');
    expect(currentOf({ current: 'din', standards })).toBe('DIN');
    expect(currentOf({ current: 'gone', standards })).toBeNull();
  });
});
