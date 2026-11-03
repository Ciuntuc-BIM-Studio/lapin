import { describe, expect, it, vi } from 'vitest';

vi.mock('virtual:lapin-modules', () => ({ default: [], product: { id: 't', name: 'T', brand: { name: 'T', short: 'Lapin' }, modules: [] } }));

import { catalogKinds } from './Catalog';
import type { ToolDef } from '../platform/module';

const tool = (id: string, label: string, cls?: string): ToolDef =>
  ({ id, label, icon: (() => null) as unknown as ToolDef['icon'], ...(cls ? { typed: { cls, get: () => null, set: () => {} } } : {}) });

describe('the catalog', () => {
  it('has one kind per type class, the shortest name standing for it, in the tools\' order (decision 263)', () => {
    const kinds = catalogKinds([
      tool('a.curvedWall', 'Curved wall', 'IfcWallType'), tool('a.select', 'Select'), tool('a.wall', 'Wall', 'IfcWallType'),
      tool('a.door', 'Door', 'IfcDoorType'), tool('a.skirtingInRoom', 'Skirting in room', 'LapinSkirtingType'), tool('a.skirting', 'Skirting', 'LapinSkirtingType'),
    ]);
    expect(kinds.map((k) => k.id)).toEqual(['a.wall', 'a.door', 'a.skirting']);
  });
});
