/**
 * The audit's ribbon simplification (2026-11-15, stage 2): tools whose button went into another stay resolvable by their id (quick slots,
 * My tools, shortcuts and saved interfaces name them), and a tab shows no tool twice.
 */

import { describe, expect, it } from 'vitest';

/** The tools that stopped having a button: their ids still work. */
const OLD_IDS = [
  'architecture.curvedWall', 'architecture.roomDividerArc', 'architecture.mitre', 'architecture.butt', 'architecture.swap',
  'structure.curvedBeam', 'structure.curvedStrip', 'structure.mitre', 'structure.butt', 'structure.swap',
  'civil.road', 'furniture.sofa', 'furniture.bed', 'furniture.casework', 'furniture.model', 'mep.node.radiator', 'mep.node.valve', 'mep.node.sprinkler',
  'manage.openingsMep',
];

describe('the tools merged into others', () => {
  it('stay tools: findTool finds each by its id, with its letters', async () => {
    const { findTool, allTools } = await import('./modules');
    for (const id of OLD_IDS) expect(findTool(id), id).not.toBeNull();
    expect(findTool('furniture.sofa')?.shortcut).toBe('SO');
    expect(findTool('furniture.bed')?.shortcut).toBe('BD');
    expect(findTool('architecture.curvedWall')?.shortcut).toBe('WC');
    expect(new Set(allTools().map((t) => t.id)).size).toBe(allTools().length);
  }, 120_000);

  it('no tab shows a tool twice, nor two buttons of one name in a group', async () => {
    const { ribbonTabs } = await import('./modules');
    for (const tab of ribbonTabs()) {
      const ids = tab.groups.flatMap((g) => [...g.tools, ...(g.small ?? [])].map((t) => t.id));
      expect(ids.filter((id, i) => ids.indexOf(id) !== i), tab.id).toEqual([]);
      for (const g of tab.groups) {
        const labels = [...g.tools, ...(g.small ?? [])].map((t) => t.label);
        expect(labels.filter((l, i) => labels.indexOf(l) !== i), `${tab.id} → ${g.label}`).toEqual([]);
      }
    }
  }, 120_000);

  it('have no button on the ribbon', async () => {
    const { ribbonTabs } = await import('./modules');
    const shown = new Set(ribbonTabs().flatMap((t) => t.groups.flatMap((g) => [...g.tools, ...(g.small ?? [])].map((x) => x.id))));
    for (const id of ['architecture.curvedWall', 'structure.curvedBeam', 'civil.road', 'furniture.sofa', 'mep.node.radiator', 'architecture.mitre']) expect(shown.has(id), id).toBe(false);
  }, 120_000);

  it('a chain tool draws straight pieces, or an arc once set to Arc, and the old Curved tool sets it', async () => {
    const { findTool, activate } = await import('./modules');
    const { chainMode, setChainMode } = await import('../modules/core/chainTool');
    const wall = findTool('architecture.wall')!;
    setChainMode(wall.id, 'line');
    expect(wall.gesture).toMatchObject({ kind: 'twoPoints', repeat: true });
    setChainMode(wall.id, 'arc');
    expect(wall.gesture).toMatchObject({ kind: 'threePoints', arc: true });
    expect(wall.mode?.()).toBe('arc');
    setChainMode(wall.id, 'line');
    activate(findTool('architecture.curvedWall')!);
    expect(chainMode(wall.id)).toBe('arc');
    setChainMode(wall.id, 'line');
  }, 120_000);
});
