import { describe, expect, it, vi } from 'vitest';
import { DEFAULT_PREFS, readPrefs } from '../app/prefs';
import { BUILTIN_MATERIAL_STANDARDS, applyNewProjectStandard, newProjectStandardId } from './newProjectStandard';

describe('the material standard of a new project', () => {
  it('is Lapin’s unless the user chose another: nothing is applied, and the language or country never decides', async () => {
    expect(DEFAULT_PREFS.newProjectMaterialStandard).toBeNull();
    expect(newProjectStandardId(null)).toBeNull();
    expect(newProjectStandardId('lapin')).toBeNull();
    const apply = vi.fn(async () => ({}));
    expect(await applyNewProjectStandard(null, { apply, say: vi.fn() })).toBe(false);
    expect(apply).not.toHaveBeenCalled();
  });

  it('applies the chosen standard after the project is made', async () => {
    const apply = vi.fn(async () => ({ materials: 3 }));
    expect(await applyNewProjectStandard('din-1356', { apply, say: vi.fn() })).toBe(true);
    expect(apply).toHaveBeenCalledWith('din-1356');
  });

  it('says so, and keeps Lapin’s, when it cannot', async () => {
    const say = vi.fn();
    expect(await applyNewProjectStandard('gone', { apply: async () => null, say })).toBe(false);
    expect(await applyNewProjectStandard('gone', { apply: async () => { throw new Error('x'); }, say })).toBe(false);
    expect(say).toHaveBeenCalledTimes(2);
  });

  it('is kept in the preferences, unreadable or Lapin’s own as none', () => {
    expect(readPrefs({ newProjectMaterialStandard: 'bs-8888' }).newProjectMaterialStandard).toBe('bs-8888');
    expect(readPrefs({ newProjectMaterialStandard: 'lapin' }).newProjectMaterialStandard).toBeNull();
    expect(readPrefs({ newProjectMaterialStandard: 7 }).newProjectMaterialStandard).toBeNull();
    expect(readPrefs({ newProjectMaterialStandard: '../x' }).newProjectMaterialStandard).toBeNull();
  });

  it('offers Lapin first', () => { expect(BUILTIN_MATERIAL_STANDARDS[0].id).toBe('lapin'); });
});
