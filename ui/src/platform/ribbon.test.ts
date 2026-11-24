import { describe, expect, it } from 'vitest';
import { Box } from 'lucide-react';
import { mergeTabs } from './ribbon';
import type { LapinModule, ToolDef } from './module';

const tool = (id: string): ToolDef => ({ id, label: id, icon: Box });
const mod = (m: Partial<LapinModule>): LapinModule => ({ id: 'm', name: 'm', summary: '', ...m });

describe('the ribbon tabs', () => {
  it('puts a module\'s groups into another\'s tab of the same id, sorted by order', () => {
    const tabs = mergeTabs([
      mod({ tabs: [{ id: 'manage', label: 'Manage', order: 80, groups: [{ label: 'Settings', tools: [tool('materials')] }] }, { id: 'view', label: 'View', order: 90, groups: [] }] }),
      mod({ tabs: [{ id: 'manage', label: 'Manage', order: 80, groups: [{ label: 'Phases', tools: [tool('phases')] }] }, { id: 'draft', label: 'Draft', order: 10, groups: [] }] }),
    ]);
    expect(tabs.map((t) => t.id)).toEqual(['draft', 'manage', 'view']);
    expect(tabs[1].groups.map((g) => g.label)).toEqual(['Settings', 'Phases']);
  });

  it('adds the shared groups a tab includes after its own, and leaves out the ones no module offers', () => {
    const tabs = mergeTabs([
      mod({ sharedGroups: { modify: { label: 'Modify', tools: [], small: [tool('move')] } } }),
      mod({ tabs: [{ id: 'architecture', label: 'Architecture', order: 20, include: ['modify', 'missing'], groups: [{ label: 'Build', tools: [tool('wall')] }] }] }),
    ]);
    expect(tabs[0].groups.map((g) => g.label)).toEqual(['Build', 'Modify']);
    expect(tabs[0].groups[1].small?.[0].id).toBe('move');
  });

  it('makes a shared group and a tab\'s own group of the same label one, with no tool twice', () => {
    const tabs = mergeTabs([
      mod({ sharedGroups: { modify: { label: 'Modify', tools: [], small: [tool('modify.move'), tool('modify.copy'), { ...tool('modify.delete'), label: 'Delete', alsoCalled: ['Erase'] }, tool('modify.pin')] } } }),
      mod({ tabs: [{ id: 'draft', label: 'Draft', order: 10, include: ['modify'], groups: [{ label: 'Modify', tools: [{ ...tool('cad.move'), label: 'modify.move' }], small: [{ ...tool('cad.erase'), label: 'Erase' }, { ...tool('cad.copy'), label: 'modify.copy' }] }] }] }),
    ]);
    expect(tabs[0].groups.map((g) => g.label)).toEqual(['Modify']);
    expect(tabs[0].groups[0].tools.map((t) => t.id)).toEqual(['cad.move']);
    expect(tabs[0].groups[0].small?.map((t) => t.id)).toEqual(['cad.erase', 'cad.copy', 'modify.pin']);
  });

  it('leaves out a shared tool the tab has in any other group, by its label or one it is also called', () => {
    const tabs = mergeTabs([
      mod({ sharedGroups: { modify: { label: 'Modify', tools: [], small: [{ ...tool('modify.offset'), label: 'Offset element', alsoCalled: ['Offset'] }, tool('modify.pin')] } } }),
      mod({ tabs: [{ id: 'draft', label: 'Draft', order: 10, include: ['modify'], groups: [{ label: 'Edit', tools: [], small: [{ ...tool('cad.offset'), label: 'Offset' }] }] }] }),
    ]);
    expect(tabs[0].groups.map((g) => g.label)).toEqual(['Edit', 'Modify']);
    expect(tabs[0].groups[1].small?.map((t) => t.id)).toEqual(['modify.pin']);
  });

  it('shows no shared group whose tools the tab all has, and a group of its own after Modify', () => {
    const tabs = mergeTabs([
      mod({ sharedGroups: { modify: { label: 'Modify', tools: [], small: [tool('modify.pin')] }, groups: { label: 'Group', tools: [tool('modify.group')], small: [tool('modify.ungroup')] }, same: { label: 'Same', tools: [tool('x')] } } }),
      mod({ tabs: [{ id: 'a', label: 'A', order: 10, include: ['modify', 'groups'], groups: [{ label: 'Build', tools: [tool('wall')] }] }, { id: 'b', label: 'B', order: 11, include: ['same'], groups: [{ label: 'Own', tools: [{ ...tool('b.x'), label: 'x' }] }] }] }),
    ]);
    expect(tabs[0].groups.map((g) => g.label)).toEqual(['Build', 'Modify', 'Group']);
    expect(tabs[1].groups.map((g) => g.label)).toEqual(['Own']);
  });

  it('places the tabs as the product says, its own work first', () => {
    const modules = [
      mod({ tabs: [{ id: 'draft', label: 'Draft', order: 10, groups: [] }, { id: 'architecture', label: 'Architecture', order: 20, groups: [] }] }),
      mod({ tabs: [{ id: 'mep', label: 'MEP', order: 45, groups: [] }] }),
    ];
    expect(mergeTabs(modules).map((t) => t.id)).toEqual(['draft', 'architecture', 'mep']);
    expect(mergeTabs(modules, { mep: 5 }).map((t) => t.id)).toEqual(['mep', 'draft', 'architecture']);
  });
});
