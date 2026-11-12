import { describe, expect, it } from 'vitest';
import { emptyTab, visibleTab, toolsOf as toolsInTab } from '../../app/myTools';
import { addNewTools, readRegistered, scriptTools, toolId, toolsOf, type ToolInputs } from './tools';

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
});
