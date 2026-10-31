import { describe, expect, it } from 'vitest';
import { initialSettings, PRESETS, presetOf, withPart, withPreset, withTab } from './interface';

describe('the interface modes', () => {
  it('starts comfortable on the desktop, compact in the web build on a short window, until the user chooses', () => {
    expect(initialSettings(null, { web: false, height: 600 })).toMatchObject({ preset: 'comfortable', chosen: false });
    expect(initialSettings(null, { web: true, height: 1000 })).toMatchObject({ preset: 'comfortable', chosen: false });
    expect(initialSettings(null, { web: true, height: 700 })).toMatchObject({ preset: 'compact', parts: PRESETS.compact, chosen: false });
    // Chosen once: kept, whatever the window.
    expect(initialSettings(withPreset('focus'), { web: true, height: 700 })).toMatchObject({ preset: 'focus', chosen: true });
    expect(initialSettings(withPreset('comfortable'), { web: true, height: 700 })).toMatchObject({ preset: 'comfortable' });
  });

  it('sets every part from a preset; a part changed on its own makes it custom, and back', () => {
    const compact = withPreset('compact');
    expect(compact.parts.ribbon).toBe('icons');
    expect(compact.parts.commandLine).toBe('status');
    const custom = withPart(compact, 'ribbon', 'large');
    expect(custom.preset).toBe('custom');
    expect(withPart(custom, 'ribbon', 'icons').preset).toBe('compact');
    expect(presetOf(PRESETS.focus)).toBe('focus');
  });

  it('reads what was kept, leaving out what it does not know', () => {
    const s = initialSettings({ chosen: true, parts: { ...PRESETS.compact, ribbon: 'huge', quickTools: 'off' } }, { web: false, height: 900 });
    expect(s.parts.ribbon).toBe('large');
    expect(s.parts.quickTools).toBe('off');
    expect(s.preset).toBe('custom');
    expect(initialSettings('garbage', { web: false, height: 900 }).preset).toBe('comfortable');
  });

  it('hides tabs for this user, apart from the presets; View always stays; read back, unknown entries left out', () => {
    const s = withTab(withTab(withPreset('compact'), 'structure', false), 'mep', false);
    expect(s.hiddenTabs).toEqual(['structure', 'mep']);
    expect(s.preset).toBe('compact');
    expect(withTab(s, 'structure', true).hiddenTabs).toEqual(['mep']);
    expect(withTab(s, 'view', false).hiddenTabs).toEqual(['structure', 'mep']);
    expect(withPart(s, 'ribbon', 'large').hiddenTabs).toEqual(['structure', 'mep']);
    expect(withPreset('focus', s.hiddenTabs).hiddenTabs).toEqual(['structure', 'mep']);
    expect(initialSettings({ chosen: true, parts: PRESETS.compact, hiddenTabs: ['site', 3, 'view'] }, { web: false, height: 900 }).hiddenTabs).toEqual(['site']);
    expect(initialSettings(null, { web: false, height: 900 }).hiddenTabs).toEqual([]);
  });
});
