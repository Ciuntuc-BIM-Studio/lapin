/**
 * The audit's third stage (2026-11-16): one Opening tool (S14), one Place catalogue (S15), Create similar for every class a tool
 * places, and the labels that named different tools alike. The old tools stay tools: their ids, letters and types resolve.
 */
import { beforeEach, describe, expect, it } from 'vitest';
import type { NodeDto, RelationDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { useUi } from '../app/store';

const OLD = [
  ['architecture.door', 'DR', 'door'], ['architecture.window', 'WN', 'window'], ['architecture.bayWindow', 'BW', 'bayWindow'],
  ['architecture.wallOpening', 'WO', 'opening'], ['architecture.slabOpening', 'SH', 'floor'],
  ['architecture.roofWindow', undefined, 'roofWindow'], ['architecture.roofDormer', undefined, 'dormer'],
] as const;

const node = (id: string, type: string, props: NodeDto['props'] = {}): NodeDto => ({ id, type, name: id, parentId: 's', px: 0, py: 0, props, editable: true, module: null, storeyId: 's' });
const rel = (id: string, cls: string, relating: string, related: string[]): [string, RelationDto] => [id, { id, class: cls, relating, related }];

beforeEach(() => {
  useUi.setState({ tool: null, selected: [], selection: null, dialog: null });
});

describe('one Opening tool (S14)', () => {
  it('is on the ribbon; the seven old tools are not, but keep their ids and letters', async () => {
    const { findTool, ribbonTabs } = await import('./modules');
    const shown = new Set(ribbonTabs().flatMap((t) => t.groups.flatMap((g) => [...g.tools, ...(g.small ?? [])].map((x) => x.id))));
    expect(shown.has('architecture.opening')).toBe(true);
    for (const [id, letters] of OLD) {
      expect(findTool(id), id).not.toBeNull();
      expect(shown.has(id), id).toBe(false);
      expect(findTool(id)?.shortcut).toBe(letters);
    }
  }, 120_000);

  it('an old tool starts the Opening tool set to its kind; the gesture follows the kind', async () => {
    const { findTool, activate } = await import('./modules');
    const { openingKind } = await import('../modules/architecture/openingTool');
    const opening = findTool('architecture.opening')!;
    for (const [id, , kind] of OLD) {
      activate(findTool(id)!);
      expect(useUi.getState().tool, id).toBe('architecture.opening');
      expect(openingKind()).toBe(kind);
      expect(opening.mode?.()).toBe(kind);
    }
    activate(findTool('architecture.door')!);
    expect(opening.gesture).toMatchObject({ kind: 'pick', types: ['IfcWall'] });
    expect(opening.typed?.cls).toBe('IfcDoorType');
    activate(findTool('architecture.window')!);
    expect(opening.typed?.cls).toBe('IfcWindowType');
    activate(findTool('architecture.slabOpening')!);
    expect(opening.gesture?.kind).toBe('polygon');
    expect(opening.typed).toBeUndefined();
    activate(findTool('architecture.roofWindow')!);
    expect(opening.gesture?.kind).toBe('point');
  }, 120_000);

  it('the catalog still offers door and window types under their own tools', async () => {
    const { catalogKinds } = await import('../shell/Catalog');
    const ids = catalogKinds().map((t) => t.id);
    expect(ids).toContain('architecture.door');
    expect(ids).toContain('architecture.window');
    expect(ids).not.toContain('architecture.opening');
  }, 120_000);
});

describe('Create similar for every class a tool places', () => {
  it('starts the element\'s tool, set up like it', async () => {
    const { similarFor } = await import('./modules');
    const { usePlaceRole } = await import('../modules/mep/placeRole');
    const { useTypePick } = await import('../modules/structure/typePick');
    const nodes = new Map([
      node('c', 'IfcColumn'), node('ct', 'IfcColumnType'), node('d', 'IfcDoor'), node('dt', 'IfcDoorType'),
      node('rad', 'IfcSpaceHeater', { role: 'radiator', systemId: 'sys' }), node('val', 'IfcValve', { role: 'valve', systemId: 'sys' }),
      node('m', 'LapinSteelMember'), node('mt', 'LapinSteelMemberType'), node('pv', 'IfcPavement', { template: 'road' }),
      node('bar', 'LapinRebar', { shape: 'stirrup' }), node('nothing', 'IfcAnnotation'),
    ].map((n) => [n.id, n]));
    useSession.setState({ nodes, relations: new Map([rel('t1', 'IfcRelDefinesByType', 'ct', ['c']), rel('t2', 'IfcRelDefinesByType', 'dt', ['d']), rel('t3', 'IfcRelDefinesByType', 'mt', ['m'])]) });
    const run = (id: string) => { const f = similarFor(nodes.get(id)!); expect(f, id).not.toBeNull(); f!(); return useUi.getState().tool; };
    expect(run('c')).toBe('structure.column');
    expect(useTypePick.getState().column).toBe('ct');
    expect(run('d')).toBe('architecture.opening');
    expect(useUi.getState().doorTypeId).toBe('dt');
    expect(run('rad')).toBe('mep.place');
    expect(usePlaceRole.getState().role).toBe('radiator');
    run('val');
    expect(usePlaceRole.getState().role).toBe('valve');
    // A class with no `similar` of its own: the tool placing its type's class, set to that type.
    expect(run('m')).toBe('steel.member');
    expect(useUi.getState().memberTypeId).toBe('mt');
    expect(run('pv')).toBe('civil.path');
    expect(run('bar')).toBe('rebar.stirrup');
    expect(similarFor(nodes.get('nothing')!)).toBeNull();
  }, 120_000);
});

describe('one Place catalogue (S15)', () => {
  it('gathers every module\'s source, and the old entry points open it on theirs', async () => {
    const { findTool, activate, placeSources } = await import('./modules');
    const { usePlaceCatalog } = await import('../shell/PlaceCatalog');
    const ids = placeSources().map((s) => s.id);
    for (const id of ['types', 'symbols', 'components', 'parts', 'furniture', 'mep']) expect(ids, id).toContain(id);
    for (const [tool, source] of [['manage.symbols', 'symbols'], ['drafting.symbols', 'symbols'], ['manage.components', 'components'], ['studio.families', 'parts'], ['furniture.library', 'furniture']] as const) {
      activate(findTool(tool)!);
      expect(useUi.getState().dialog, tool).toBe('placeCatalog');
      expect(usePlaceCatalog.getState().source, tool).toBe(source);
    }
    activate(findTool('manage.placeCatalog')!);
    expect(usePlaceCatalog.getState().source).toBe('all');
  }, 120_000);

  it('one search: every word in the name, the group or the few words', async () => {
    const { matches, grouped } = await import('../shell/PlaceCatalog');
    const e = { name: 'Radiator', group: 'Heating', about: 'panel' };
    expect(matches(e, '')).toBe(true);
    expect(matches(e, 'heat RAD')).toBe(true);
    expect(matches(e, 'heat sofa')).toBe(false);
    const place = () => undefined;
    expect(grouped([{ key: 'a', name: 'A', group: 'x', place }, { key: 'b', name: 'B', group: 'y', place }, { key: 'c', name: 'C', group: 'x', place }]).map((g) => [g.group, g.entries.length])).toEqual([['x', 2], ['y', 1]]);
  }, 120_000);
});

describe('one home for the types', () => {
  it('Manage → All types lists every tab\'s Types tools, once each', async () => {
    const { findTool } = await import('./modules');
    const ids = (findTool('manage.types')?.items ?? []).map((t) => t.id);
    for (const id of ['architecture.wallTypes', 'architecture.joineryTypes']) expect(ids, id).toContain(id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids).not.toContain('manage.types');
  }, 120_000);
});

describe('labels that named different tools alike', () => {
  it('Join walls and the linework edits say what they work on (their ids unchanged)', async () => {
    const { findTool } = await import('./modules');
    expect(findTool('architecture.join')?.label).toBe('Join walls');
    expect(findTool('architecture.join')?.shortcut).toBe('JW');
    for (const [id, label] of [['cad.lwJoin', 'Join linework'], ['cad.lwTrim', 'Trim linework'], ['cad.lwExtend', 'Extend linework']]) expect(findTool(id)?.label, id).toBe(label);
  }, 120_000);
});
