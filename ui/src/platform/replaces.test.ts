/**
 * `replaces` (docs/overrides.md): a module replaces a tool, a dialog, a view or a File action by id, each replacement
 * given the one before it; nothing changes without one; an unknown tool or File action is refused.
 */

import { describe, expect, it } from 'vitest';
import { Square } from 'lucide-react';
import type { ComponentType } from 'react';
import type { FileAction, LapinModule, ModuleViewDef, ToolDef } from './module';
import { replacementsOf, resolveFileActions, through, withReplacedTools } from './replaces';

const tool = (id: string, label = id): ToolDef => ({ id, label, icon: Square });
const wall = tool('architecture.wall', 'Wall');
const Dlg: ComponentType = () => null;

const architecture: LapinModule = {
  id: 'architecture', name: 'Architecture', summary: '',
  tabs: [{ id: 'architecture', label: 'Architecture', order: 1, groups: [{ label: 'Build', tools: [wall, tool('architecture.door')], small: [tool('architecture.slab')] }] }],
  sharedGroups: { walls: { label: 'Walls', tools: [wall] } },
  hotbar: [wall],
  dialogs: { wallTypes: Dlg },
  views: { gantt: { label: 'Gantt', component: Dlg } },
};

const replacer = (id: string, label: string, log?: string[]): LapinModule => ({
  id, name: id, summary: '',
  replaces: {
    tools: { 'architecture.wall': (o) => { log?.push(`${id} got ${o.label}`); return { ...o, id: 'renamed', label }; } },
  },
});

describe('replaces', () => {
  it('changes nothing without replacements', () => {
    const modules = [architecture];
    expect(withReplacedTools(modules)).toBe(modules);
    expect(replacementsOf(modules)).toEqual([]);
    expect(through(modules, 'dialogs', 'wallTypes', Dlg)).toBe(Dlg);
    const base: FileAction[] = [{ id: 'file.save', label: 'Save', run: () => 1 }];
    expect(resolveFileActions(base, modules).get('file.save')).toBe(base[0]);
  });

  it('puts a replaced tool everywhere it was, one object, keeping its id, each replacement given the one before', () => {
    const log: string[] = [];
    const modules = withReplacedTools([architecture, replacer('a', 'Wall A', log), replacer('b', 'Wall B', log)]);
    expect(log).toEqual(['a got Wall', 'b got Wall A']);
    const [arch] = modules;
    const inTab = arch.tabs![0].groups[0].tools[0];
    expect(inTab).toMatchObject({ id: 'architecture.wall', label: 'Wall B' });
    expect(arch.sharedGroups!.walls.tools[0]).toBe(inTab);
    expect(arch.hotbar![0]).toBe(inTab);
    // The others as they were.
    expect(arch.tabs![0].groups[0].tools[1]).toBe(architecture.tabs![0].groups[0].tools[1]);
    expect(arch.tabs![0].groups[0].small![0]).toBe(architecture.tabs![0].groups[0].small![0]);
    expect(architecture.hotbar![0]).toBe(wall);
  });

  it('lets a replacement call the original', async () => {
    const calls: string[] = [];
    const orig: ToolDef = { ...wall, run: () => calls.push('original') };
    const base: LapinModule = { ...architecture, hotbar: [orig], tabs: [], sharedGroups: {} };
    const [m] = withReplacedTools([base, { id: 'x', name: 'x', summary: '', replaces: { tools: { 'architecture.wall': (o) => ({ ...o, run: () => { calls.push('mine'); o.run?.(); } }) } } }]);
    m.hotbar![0].run!();
    expect(calls).toEqual(['mine', 'original']);
  });

  it('refuses a tool or a File action nobody has', () => {
    expect(() => withReplacedTools([architecture, { id: 'x', name: 'x', summary: '', replaces: { tools: { 'nope.tool': (o) => o } } }])).toThrow(/„x” replaces the tool „nope.tool”/);
    expect(() => resolveFileActions([], [{ id: 'y', name: 'y', summary: '', replaces: { fileActions: { 'file.nope': (o) => o } } }])).toThrow(/„y” replaces the File action „file.nope”/);
  });

  it('replaces dialogs, views and File actions through each module in turn, and lists what is replaced', () => {
    const Mine: ComponentType = () => null;
    const seen: (ComponentType | null)[] = [];
    const view: ModuleViewDef = { label: 'Mine', component: Mine };
    const m: LapinModule = {
      id: 'litestep', name: 'Lite-STEP', summary: '',
      replaces: {
        dialogs: { wallTypes: (o) => { seen.push(o); return Mine; }, fresh: (o) => { seen.push(o); return Mine; } },
        views: { gantt: (o) => ({ ...view, label: `${o?.label} (mine)` }) },
        fileActions: { 'file.save': (o) => ({ ...o, label: 'Save mine', run: () => `mine+${o.run()}` }) },
      },
    };
    const modules = [architecture, m];
    expect(through(modules, 'dialogs', 'wallTypes', architecture.dialogs!.wallTypes)).toBe(Mine);
    // A dialog may stand where none was: its original is null.
    expect(through<ComponentType | null>(modules, 'dialogs', 'fresh', null)).toBe(Mine);
    expect(seen).toEqual([Dlg, null]);
    expect(through<ModuleViewDef | null>(modules, 'views', 'gantt', architecture.views!.gantt)?.label).toBe('Gantt (mine)');
    const save = resolveFileActions([{ id: 'file.save', label: 'Save', run: () => 'saved' }, { id: 'file.open', label: 'Open', run: () => 0 }], modules);
    expect(save.get('file.save')).toMatchObject({ id: 'file.save', label: 'Save mine' });
    expect(save.get('file.save')!.run()).toBe('mine+saved');
    expect(save.get('file.open')!.label).toBe('Open');
    expect(replacementsOf(modules)).toEqual([
      { kind: 'dialog', id: 'wallTypes', module: 'litestep' }, { kind: 'dialog', id: 'fresh', module: 'litestep' },
      { kind: 'view', id: 'gantt', module: 'litestep' }, { kind: 'fileAction', id: 'file.save', module: 'litestep' },
    ]);
  });
});
