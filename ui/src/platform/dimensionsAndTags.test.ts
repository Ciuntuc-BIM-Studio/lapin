/**
 * S9 (audit 2026-11-15): one Dimension tool and one Tag tool on the ribbon. The old dimension kinds and Tag a parameter stay
 * tools: their ids and letters resolve, and each starts the one tool set to its kind or source.
 */
import { beforeEach, describe, expect, it } from 'vitest';
import { useUi } from '../app/store';
import { cancel, useCad } from '../viewport/cad/session';

const OLD = [
  ['cad.dimLinear', 'linear'], ['cad.dimAngular', 'angular'], ['cad.dimRadius', 'radial'], ['cad.dimDiameter', 'diameter'],
  ['cad.dimArc', 'arc'], ['cad.dimSlope', 'slope'], ['cad.dimOrdinate', 'ordinate'], ['cad.dimCoord', 'coordinate'],
] as const;

beforeEach(() => {
  cancel(true);
  useUi.setState({ tool: null, selected: [], selection: null, dialog: null });
});

describe('one Dimension tool and one Tag tool (S9)', () => {
  it('are on the ribbon; the old kinds are not, but keep their ids', async () => {
    const { findTool, ribbonTabs } = await import('./modules');
    const shown = new Set(ribbonTabs().flatMap((t) => t.groups.flatMap((g) => [...g.tools, ...(g.small ?? [])].map((x) => x.id))));
    for (const id of ['drafting.dimension', 'cad.tag', 'drafting.roomDimensions', 'drafting.exteriorDimensions', 'level', 'tagOpenings']) expect(shown.has(id), id).toBe(true);
    for (const [id] of OLD) {
      expect(findTool(id), id).not.toBeNull();
      expect(shown.has(id), id).toBe(false);
    }
    expect(shown.has('drafting.tagParameter')).toBe(false);
    expect(findTool('drafting.tagParameter')).not.toBeNull();
    expect(findTool('drafting.dimension')?.shortcut).toBe('DI');
    expect(findTool('level')?.shortcut).toBe('LL');
  }, 120_000);

  it('an old kind starts the Dimension tool at its kind; the main button starts it smart', async () => {
    const { findTool, activate } = await import('./modules');
    const { useSmartDim } = await import('../modules/drafting/smartDimension');
    useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => null } });
    for (const [id, kind] of [...OLD, ['level', 'level'] as const]) {
      activate(findTool(id)!);
      await new Promise((r) => setTimeout(r, 0));
      expect(useCad.getState().command, id).toBe('DIMENSION');
      expect(useSmartDim.getState().kind, id).toBe(kind);
    }
    activate(findTool('drafting.dimension')!);
    await new Promise((r) => setTimeout(r, 0));
    expect(useSmartDim.getState().kind).toBe('smart');
  }, 120_000);

  it('Tag a parameter starts the Tag tool at the parameter source', async () => {
    const { findTool, activate } = await import('./modules');
    const { useTagPrefs } = await import('../modules/drafting/tag');
    useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => null } });
    activate(findTool('drafting.tagParameter')!);
    await new Promise((r) => setTimeout(r, 0));
    expect(useCad.getState().command).toBe('TAG');
    expect(useTagPrefs.getState().source).toBe('parameter');
  }, 120_000);
});
