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

  it('places the tabs as the product says, its own work first', () => {
    const modules = [
      mod({ tabs: [{ id: 'draft', label: 'Draft', order: 10, groups: [] }, { id: 'architecture', label: 'Architecture', order: 20, groups: [] }] }),
      mod({ tabs: [{ id: 'mep', label: 'MEP', order: 45, groups: [] }] }),
    ];
    expect(mergeTabs(modules).map((t) => t.id)).toEqual(['draft', 'architecture', 'mep']);
    expect(mergeTabs(modules, { mep: 5 }).map((t) => t.id)).toEqual(['mep', 'draft', 'architecture']);
  });
});
