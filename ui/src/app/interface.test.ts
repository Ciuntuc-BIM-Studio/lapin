import { describe, expect, it } from 'vitest';
import { initialSettings, PRESETS, presetOf, visibleTabIds, withPart, withPreset, withTab, withTabMoved, withWorkspace } from './interface';

describe('the interface modes', () => {
  it('starts in Flow, on the desktop and the web, whatever the window, until the user chooses (decision 881)', () => {
    expect(initialSettings(null, { web: false, height: 600 })).toMatchObject({ preset: 'flow', parts: PRESETS.flow, chosen: false });
    expect(initialSettings(null, { web: true, height: 1000 })).toMatchObject({ preset: 'flow', chosen: false });
    expect(initialSettings(null, { web: true, height: 700 })).toMatchObject({ preset: 'flow', chosen: false });
    expect(initialSettings({ chosen: false, workspace: 'mep' }, { web: false, height: 900 })).toMatchObject({ preset: 'flow', workspace: 'mep' });
    // Chosen once: kept, whatever the window.
    expect(initialSettings(withPreset('focus'), { web: true, height: 700 })).toMatchObject({ preset: 'focus', chosen: true });
    expect(initialSettings(withPreset('comfortable'), { web: true, height: 700 })).toMatchObject({ preset: 'comfortable' });
  });

  it('docks the quick tools by the browser; a floating bar kept from before is docked once, then kept if chosen again', () => {
    expect(PRESETS.comfortable.quickTools).toBe('docked');
    const old = withPart(withPreset('comfortable'), 'quickTools', 'floating');
    expect(initialSettings(old, { web: false, height: 900 }).parts.quickTools).toBe('docked');
    expect(initialSettings({ ...old, rev: 2 }, { web: false, height: 900 }).parts.quickTools).toBe('floating');
  });

  it('shows the tools\' keys while Alt is held by default; Always is a part of its own, and a setting kept before it still reads', () => {
    for (const p of Object.values(PRESETS)) expect(p.keyTips).toBe('alt');
    const always = withPart(withPreset('comfortable'), 'keyTips', 'always');
    expect(always).toMatchObject({ preset: 'custom', parts: { keyTips: 'always' } });
    expect(initialSettings(JSON.parse(JSON.stringify(always)), { web: false, height: 900 }).parts.keyTips).toBe('always');
    expect(withPart(always, 'keyTips', 'alt').preset).toBe('comfortable');
    const { keyTips: _gone, ...before } = PRESETS.comfortable;
    expect(initialSettings({ chosen: true, parts: before }, { web: false, height: 900 })).toMatchObject({ preset: 'comfortable', parts: { keyTips: 'alt' } });
    expect(initialSettings({ chosen: true, parts: { ...before, keyTips: 'sometimes' } }, { web: false, height: 900 }).parts.keyTips).toBe('alt');
  });

  it('sets every part from a preset; a part changed on its own makes it custom, and back', () => {
    const compact = withPreset('compact');
    expect(compact.parts.ribbon).toBe('icons');
    expect(compact.parts.commandLine).toBe('status');
    const custom = withPart(compact, 'ribbon', 'large');
    expect(custom.preset).toBe('custom');
    expect(withPart(custom, 'ribbon', 'icons').preset).toBe('compact');
    expect(presetOf(PRESETS.focus)).toBe('focus');
    expect(presetOf(PRESETS.flow)).toBe('flow');
    expect(PRESETS.flow.quickTools).toBe('floating');
    expect(PRESETS.flow.panels).toBe('bothRail');
  });

  it('reads what was kept, leaving out what it does not know', () => {
    const s = initialSettings({ chosen: true, parts: { ...PRESETS.compact, ribbon: 'huge', quickTools: 'off' } }, { web: false, height: 900 });
    expect(s.parts.ribbon).toBe('large');
    expect(s.parts.quickTools).toBe('off');
    expect(s.preset).toBe('custom');
    expect(initialSettings('garbage', { web: false, height: 900 }).preset).toBe('flow');
  });

  // The tabs of the full edition, as the modules give them.
  const TABS = [['draft', 10], ['architecture', 20], ['interior', 25], ['structure', 30], ['wood', 33], ['steel', 34], ['rebar', 35], ['site', 40], ['mep', 45], ['studio', 50], ['annotate', 60], ['manage', 80], ['construction', 84], ['view', 90]].map(([id, order]) => ({ id: id as string, order: order as number }));
  const shown = (s: Parameters<typeof visibleTabIds>[0], tabs = TABS) => visibleTabIds(s, tabs);

  it('shows a workspace\'s tabs in its order, every tab in All, View always, a tab the edition lacks left out', () => {
    const s = withPreset('compact');
    expect(s.workspace).toBe('all');
    expect(shown(s)).toEqual(TABS.map((t) => t.id));
    expect(shown(withWorkspace(s, 'structure'))).toEqual(['draft', 'structure', 'rebar', 'wood', 'steel', 'annotate', 'manage', 'construction', 'view']);
    expect(shown(withWorkspace(s, 'mep'))).toEqual(['draft', 'mep', 'annotate', 'manage', 'construction', 'view']);
    // An edition without Wood and Rebar (Architecture + Structure is lighter still): only what it has.
    expect(shown(withWorkspace(s, 'structure'), TABS.filter((t) => !['wood', 'steel', 'rebar'].includes(t.id)))).toEqual(['draft', 'structure', 'annotate', 'manage', 'construction', 'view']);
    // A workspace and the interface's mode are apart.
    expect(withPart(withWorkspace(s, 'site'), 'ribbon', 'large').workspace).toBe('site');
    expect(withPreset('focus', withWorkspace(s, 'site')).workspace).toBe('site');
  });

  it('makes a workspace the user\'s own when a tab is shown, hidden or moved, and keeps it', () => {
    const arch = withWorkspace(withPreset('comfortable'), 'architecture');
    // Interior is not this office's: hidden, then Structure brought in, in its usual place.
    let mine = withTab(arch, 'interior', false, TABS);
    expect(mine.workspace).toBe('mine');
    expect(shown(mine)).toEqual(['draft', 'architecture', 'studio', 'annotate', 'manage', 'construction', 'view']);
    mine = withTab(mine, 'structure', true, TABS);
    expect(shown(mine)).toEqual(['draft', 'architecture', 'structure', 'studio', 'annotate', 'manage', 'construction', 'view']);
    // Moved: Structure before Architecture; at an end it stays.
    mine = withTabMoved(mine, 'structure', -1, TABS);
    expect(shown(mine).slice(0, 3)).toEqual(['draft', 'structure', 'architecture']);
    expect(withTabMoved(mine, 'draft', -1, TABS)).toBe(mine);
    // View cannot be hidden; another workspace chosen and back, the user's own is still there.
    expect(withTab(mine, 'view', false, TABS)).toBe(mine);
    expect(shown(withWorkspace(withWorkspace(mine, 'all'), 'mine'))).toEqual(shown(mine));
    // Kept and read back; unknown entries left out.
    const back = initialSettings(JSON.parse(JSON.stringify(mine)), { web: false, height: 900 });
    expect(back.workspace).toBe('mine');
    expect(shown(back)).toEqual(shown(mine));
    expect(initialSettings({ chosen: true, parts: PRESETS.compact, workspace: 'kitchen' }, { web: false, height: 900 }).workspace).toBe('all');
  });

  it('reads a workspace kept before tabs were merged with their new ids (decision 367)', () => {
    const s = initialSettings({ chosen: true, parts: PRESETS.compact, workspace: 'mine', mine: ['draft', 'finishes', 'furniture', 'sitework', 'site', 'planning', 'cost', 'view'] }, { web: false, height: 900 });
    expect(s.mine).toEqual(['draft', 'interior', 'site', 'construction', 'view']);
  });

  it('reads the tabs an older Lapin kept hidden as the user\'s own workspace', () => {
    const s = initialSettings({ chosen: true, parts: PRESETS.compact, hiddenTabs: ['site', 3, 'view', 'mep'] }, { web: false, height: 900 });
    expect(s.workspace).toBe('mine');
    expect(shown(s)).toEqual(TABS.map((t) => t.id).filter((id) => !['site', 'mep'].includes(id)));
    expect(initialSettings(null, { web: false, height: 900 }).workspace).toBe('all');
  });
});
