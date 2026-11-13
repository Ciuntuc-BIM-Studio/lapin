import { describe, expect, it } from 'vitest';
import { addGroup, emptyTab, visibleTab, toolsOf as toolsInTab } from '../../app/myTools';
import { addNewTools, placed, readRegistered, scriptTools, toolId, toolsOf, type ToolInputs } from './tools';
import { fieldsOf, readMemory, readRows } from './palette';

const facts = { version: '0.1.950', hasModule: (m: string) => m === 'scripting' };
const HEADER = '# lapin: name="Walls", tool="Perimeter walls", icon="square", tooltip="Four walls", run="perimeter"\nimport lapinbim as lapin\n';

const inputs = (over: Partial<ToolInputs> = {}): ToolInputs => ({
  mine: [{ name: 'walls.py', head: HEADER }, { name: 'plain.py', head: 'print(1)\n' }],
  project: [
    { id: 'p1', name: 'room.py', code: '# lapin: tool="Room"\n', sha256: 'trusted' },
    { id: 'p2', name: 'stranger.py', code: '# lapin: tool="Save"\n', sha256: 'unknown' },
  ],
  extensions: [{ id: 'ro.x.pereti', name: 'Perimeter walls', text: '# lapin: name="Perimeter walls", tool=yes, requires="scripting, lapin>=0.1.900"\n' }],
  trusted: new Set(['trusted']),
  registered: { 'mine:plain.py': [{ name: 'Registered', icon: 'ruler', call: 'go' }, { name: 'Bad', call: 'os.system' }] },
  facts,
  ...over,
});

describe('a script\'s tools (decision 489, stage 4)', () => {
  it('come from the header and from what lapin.ui.tool registered, with stable ids', () => {
    const [t] = toolsOf({ kind: 'mine', key: 'mine:walls.py', label: 'walls.py' }, HEADER, undefined, facts);
    expect(t).toMatchObject({ id: 'script:mine:walls.py#Perimeter walls', name: 'Perimeter walls', icon: 'square', tooltip: 'Four walls', call: 'perimeter', main: false, from: 'header' });
    const byScript = scriptTools(inputs());
    expect(Object.keys(byScript)).toEqual(['mine:walls.py', 'mine:plain.py', 'project:room.py', 'extension:ro.x.pereti']);
    expect(byScript['mine:plain.py'].map((x) => [x.name, x.call])).toEqual([['Registered', 'go'], ['Bad', null]]);
    expect(byScript['project:room.py'][0]).toMatchObject({ main: true, call: null });
    expect(byScript['extension:ro.x.pereti'][0].name).toBe('Perimeter walls');
  });

  it('never come from a project\'s script this computer does not trust, nor from a script needing what this Lapin lacks', () => {
    const byScript = scriptTools(inputs());
    expect(byScript['project:stranger.py']).toBeUndefined();
    const older = scriptTools(inputs({ facts: { ...facts, version: '0.1.800' } }));
    expect(older['extension:ro.x.pereti']).toBeUndefined();
    // Trust taken back: the tool goes.
    expect(scriptTools(inputs({ trusted: new Set() }))['project:room.py']).toBeUndefined();
  });

  it('go into My tools once, in the group Scripts, and a tool taken out stays out', () => {
    const ids = Object.values(scriptTools(inputs())).flat().map((t) => t.id);
    const first = addNewTools(emptyTab(), ids, new Set());
    expect(first.tab.groups.map((g) => g.label)).toEqual(['Scripts']);
    expect(toolsInTab(first.tab)).toEqual(ids);
    const out = { ...first.tab, groups: first.tab.groups.map((g) => ({ ...g, items: g.items.filter((i) => i.tool !== ids[0]) })) };
    const again = addNewTools(out, ids, first.added);
    expect(again.tab).toBe(out);
    expect(toolsInTab(again.tab)).not.toContain(ids[0]);
    // A script gone hides its tool, which stays in the tab and comes back with it.
    const shown = visibleTab(first.tab, (id) => id !== toolId('mine:walls.py', 'Perimeter walls'));
    expect(toolsInTab(shown)).not.toContain(toolId('mine:walls.py', 'Perimeter walls'));
    expect(toolsInTab(first.tab)).toContain(toolId('mine:walls.py', 'Perimeter walls'));
  });

  it('keep what scripts registered, as read back, Mine\'s and the project\'s only', () => {
    expect(readRegistered({ 'mine:a.py': [{ name: 'A' }, 3, { nope: 1 }], 'extension:x': [{ name: 'B' }], junk: 1 })).toEqual({ 'mine:a.py': [{ name: 'A' }] });
    expect(readRegistered(null)).toEqual({});
  });

  it('keep older stored entries, and the panels, menus and palettes of decision 526', () => {
    const kept = readRegistered({ 'mine:a.py': [{ name: 'Old', call: 'f', main: false }, { kind: 'palette', name: 'P', rows: [{ row: 'separator' }] }] });
    expect(kept['mine:a.py'].map((x) => x.name)).toEqual(['Old', 'P']);
    const [old] = toolsOf({ kind: 'mine', key: 'mine:a.py', label: 'a.py' }, '', kept['mine:a.py'], facts);
    expect(old).toMatchObject({ kind: 'tool', small: false, call: 'f' });
    expect(old.panel).toBeUndefined();
  });
});

const SRC = { kind: 'mine' as const, key: 'mine:walls.py', label: 'walls.py' };

describe('panels, sizes, menus and palettes of a script (decision 526)', () => {
  const registered = [
    { name: 'Perimeter', call: 'perimeter', panel: 'Walls', size: 'small' },
    { kind: 'menu', name: 'Openings', icon: 'door', panel: 'Walls' },
    { name: 'Door', call: 'door', panel: 'Walls', menu: 'Openings' },
    { name: 'Window', call: 'window', panel: 'Walls', menu: 'Openings' },
    { kind: 'menu', name: 'Empty', panel: 'Walls' },
    { name: 'Stray', call: 'stray', menu: 'Nowhere' },
    { kind: 'palette', name: 'Wall tools', panel: 'Roofs', rows: [{ row: 'heading', text: 'New wall' }, { row: 'button', name: 'Make', call: 'make' }], call: 'evil' },
    { name: 'Loose', call: 'loose' },
  ];

  it('read their kind, panel, size and menu; a menu with no tool, and a menu no script has, are dropped', () => {
    const list = toolsOf(SRC, '# lapin: tool="Header tool", panel="Roofs", size="small"\n', registered, facts);
    const by = Object.fromEntries(list.map((t) => [t.name, t]));
    expect(list.map((t) => t.name)).toEqual(['Header tool', 'Perimeter', 'Openings', 'Door', 'Window', 'Stray', 'Wall tools', 'Loose']);
    expect(by['Header tool']).toMatchObject({ panel: 'Roofs', small: true, kind: 'tool' });
    expect(by.Perimeter).toMatchObject({ panel: 'Walls', small: true });
    expect(by.Openings).toMatchObject({ kind: 'menu', call: null, main: false, icon: 'door' });
    expect(by.Door.menu).toBe('Openings');
    expect(by.Stray.menu).toBeUndefined();
    expect(by['Wall tools']).toMatchObject({ kind: 'palette', panel: 'Roofs', call: null });
    expect(by['Wall tools'].rows).toEqual([{ row: 'heading', text: 'New wall' }, { row: 'buttons', buttons: [{ name: 'Make', call: 'make' }] }]);
  });

  it('go into My tools in their panels\' groups, in the panels\' order, small ones small, a menu\'s tools through their menu only', () => {
    const list = toolsOf(SRC, '', registered, facts);
    const r = addNewTools(emptyTab(), list.map(placed), new Set());
    expect(r.tab.groups.map((g) => g.label)).toEqual(['Walls', 'Scripts', 'Roofs']);
    const walls = r.tab.groups[0].items;
    expect(walls.map((i) => i.tool)).toEqual([toolId(SRC.key, 'Perimeter'), toolId(SRC.key, 'Openings')]);
    expect(walls.map((i) => i.large)).toEqual([false, true]);
    expect(toolsInTab(r.tab)).not.toContain(toolId(SRC.key, 'Door'));
    expect(r.added.has(toolId(SRC.key, 'Door'))).toBe(false);
    // A panel made later goes among the others in the script's order; an existing group of that name is used.
    const user = addGroup(emptyTab(), 'Roofs').tab;
    const again = addNewTools(user, list.map(placed), new Set());
    expect(again.tab.groups.map((g) => g.label)).toEqual(['Walls', 'Scripts', 'Roofs']);
    expect(again.tab.groups[2].items.map((i) => i.tool)).toEqual([toolId(SRC.key, 'Wall tools')]);
  });
});

describe('a palette\'s rows (decision 526)', () => {
  it('keep what the window draws only: texts, one separator at a time, fields once by key, buttons once by name', () => {
    const rows = readRows([
      { row: 'separator' },
      { row: 'heading', text: '  New wall ' },
      { row: 'text', text: '' },
      { row: 'separator' }, { row: 'separator' },
      { row: 'field', field: { key: 'height', kind: 'number', label: 'Height', value: 2.8, unit: 'm' } },
      { row: 'field', field: { key: 'height', kind: 'text' } },
      { row: 'field', field: { key: 'bad', kind: 'html' } },
      { row: 'button', name: 'Make', call: 'make', icon: 'hammer' },
      { row: 'buttons', buttons: [{ name: 'Make' }, { name: 'Two', call: 'os.system' }, 7] },
      { row: 'script', code: 'alert(1)' },
      'junk',
    ]);
    expect(rows).toEqual([
      { row: 'heading', text: 'New wall' },
      { row: 'separator' },
      { row: 'field', field: { key: 'height', kind: 'number', label: 'Height', value: 2.8, unit: 'm' } },
      { row: 'buttons', buttons: [{ name: 'Make', call: 'make', icon: 'hammer' }] },
      { row: 'buttons', buttons: [{ name: 'Two', call: null }] },
    ]);
    expect(fieldsOf(rows).map((f) => f.key)).toEqual(['height']);
    expect(readRows(null)).toEqual([]);
    expect(readRows(Array.from({ length: 500 }, (_, i) => ({ row: 'text', text: `t${i}` })))).toHaveLength(200);
  });

  it('remember where it floats and what its fields hold, only what still fits them', () => {
    const fields = fieldsOf(readRows([
      { row: 'field', field: { key: 'h', kind: 'number', value: 1 } },
      { row: 'field', field: { key: 'ok', kind: 'bool' } },
      { row: 'field', field: { key: 'k', kind: 'choice', choices: [{ value: 'a' }, { value: 'b' }] } },
    ]));
    expect(readMemory({ x: 10, y: 'no', state: { h: '2,5', ok: 'yes', k: 1, gone: 'x' } }, fields)).toEqual({ x: 10, state: { h: '2,5', k: 1 } });
    expect(readMemory({ state: { k: 5 } }, fields)).toEqual({ state: {} });
    expect(readMemory('junk', fields)).toEqual({});
  });
});
