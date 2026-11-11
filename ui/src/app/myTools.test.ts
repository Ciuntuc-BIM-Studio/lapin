import { beforeEach, describe, expect, it } from 'vitest';
import {
  stepTool, addGroup, addTool, addToNewGroup, changeMyTab, deleteGroup, emptyTab, exportTab, has, importMyTab, importTab, isEmpty, locate, moveGroup,
  myTab, nudgeGroup, nudgeTool, readMyTools, removeTool, renameGroup, resetMyTab, setSize, toolsOf, undoMyTools, useMyTools, visibleTab, FIRST_GROUP,
  type MyTab,
} from './myTools';

/** A tab with groups A (a1 a2 a3) and B (b1). */
function sample(): MyTab {
  let t = emptyTab();
  const a = addGroup(t, 'A'); t = a.tab;
  const b = addGroup(t, 'B'); t = b.tab;
  for (const x of ['a1', 'a2', 'a3']) t = addTool(t, x, { group: a.id });
  t = addTool(t, 'b1', { group: b.id });
  return t;
}
const shape = (t: MyTab) => t.groups.map((g) => `${g.label}:${g.items.map((i) => i.tool).join(',')}`);

describe('adding tools', () => {
  it('makes a first group when there is none, then fills the last', () => {
    let t = addTool(emptyTab(), 'wall');
    expect(shape(t)).toEqual([`${FIRST_GROUP}:wall`]);
    t = addTool(t, 'door');
    expect(shape(t)).toEqual([`${FIRST_GROUP}:wall,door`]);
  });
  it('puts a tool at a place, and a tool stands once', () => {
    const t = sample();
    const a = t.groups[0].id;
    expect(shape(addTool(t, 'x', { group: a, index: 1 }))).toEqual(['A:a1,x,a2,a3', 'B:b1']);
    expect(shape(addTool(t, 'b1', { group: a, index: 0 }))).toEqual(['A:b1,a1,a2,a3', 'B:']);
  });
  it('makes a new group for a tool', () => {
    expect(shape(addToNewGroup(sample(), 'x', 'C'))).toEqual(['A:a1,a2,a3', 'B:b1', 'C:x']);
  });
});

describe('moving tools', () => {
  it('reorders within a group, counting the place as drawn', () => {
    const t = sample(), a = t.groups[0].id;
    expect(shape(addTool(t, 'a1', { group: a, index: 3 }))).toEqual(['A:a2,a3,a1', 'B:b1']);
    expect(shape(addTool(t, 'a1', { group: a, index: 2 }))).toEqual(['A:a2,a1,a3', 'B:b1']);
    expect(shape(addTool(t, 'a3', { group: a, index: 0 }))).toEqual(['A:a3,a1,a2', 'B:b1']);
    // Dropped just before or just after itself: nothing changes.
    expect(shape(addTool(t, 'a2', { group: a, index: 1 }))).toEqual(shape(t));
    expect(shape(addTool(t, 'a2', { group: a, index: 2 }))).toEqual(shape(t));
  });
  it('moves between groups, keeping the size', () => {
    let t = setSize(sample(), 'a2', false);
    t = addTool(t, 'a2', { group: t.groups[1].id, index: 0 });
    expect(shape(t)).toEqual(['A:a1,a3', 'B:a2,b1']);
    expect(t.groups[1].items[0].large).toBe(false);
  });
  it('nudges with the keys: up, down, to the group before or after', () => {
    const t = sample();
    expect(shape(nudgeTool(t, 'a2', 'up'))).toEqual(['A:a2,a1,a3', 'B:b1']);
    expect(shape(nudgeTool(t, 'a2', 'down'))).toEqual(['A:a1,a3,a2', 'B:b1']);
    expect(nudgeTool(t, 'a1', 'up')).toBe(t);
    expect(nudgeTool(t, 'a3', 'down')).toBe(t);
    expect(shape(nudgeTool(t, 'a3', 'right'))).toEqual(['A:a1,a2', 'B:a3,b1']);
    expect(shape(nudgeTool(t, 'b1', 'left'))).toEqual(['A:a1,a2,a3,b1', 'B:']);
    expect(nudgeTool(t, 'a1', 'left')).toBe(t);
    expect(nudgeTool(t, 'zz', 'up')).toBe(t);
  });
});

describe('stepping through the reading order', () => {
  it('crosses into the neighbouring group at the ends', () => {
    const t = sample();
    expect(shape(stepTool(t, 'a2', 1))).toEqual(['A:a1,a3,a2', 'B:b1']);
    expect(shape(stepTool(t, 'a3', 1))).toEqual(['A:a1,a2', 'B:a3,b1']);
    expect(shape(stepTool(t, 'b1', -1))).toEqual(['A:a1,a2,a3,b1', 'B:']);
    expect(stepTool(t, 'a1', -1)).toBe(t);
    expect(stepTool(t, 'b1', 1)).toBe(t);
  });
});

describe('groups', () => {
  it('renames, ignoring an empty name', () => {
    const t = sample(), a = t.groups[0].id;
    expect(renameGroup(t, a, '  Walls ').groups[0].label).toBe('Walls');
    expect(renameGroup(t, a, '  ')).toBe(t);
  });
  it('moves and nudges', () => {
    const t = addGroup(sample(), 'C').tab, [a, b, c] = t.groups.map((g) => g.id);
    expect(moveGroup(t, a, 3).groups.map((g) => g.label)).toEqual(['B', 'C', 'A']);
    expect(moveGroup(t, c, 0).groups.map((g) => g.label)).toEqual(['C', 'A', 'B']);
    expect(moveGroup(t, b, 2).groups.map((g) => g.label)).toEqual(['A', 'B', 'C']);
    expect(nudgeGroup(t, b, -1).groups.map((g) => g.label)).toEqual(['B', 'A', 'C']);
    expect(nudgeGroup(t, b, 1).groups.map((g) => g.label)).toEqual(['A', 'C', 'B']);
    expect(nudgeGroup(t, a, -1)).toBe(t);
  });
  it('deletes a group with its tools', () => {
    const t = sample();
    const d = deleteGroup(t, t.groups[0].id);
    expect(shape(d)).toEqual(['B:b1']);
    expect(has(d, 'a1')).toBe(false);
  });
});

describe('removing and sizes', () => {
  it('removes a tool, and leaves the tab alone when it is not there', () => {
    const t = sample();
    expect(toolsOf(removeTool(t, 'a2'))).toEqual(['a1', 'a3', 'b1']);
    expect(removeTool(t, 'nope')).toBe(t);
  });
  it('makes a tool small or large', () => {
    const t = setSize(sample(), 'a1', false);
    expect(t.groups[0].items[0].large).toBe(false);
    expect(setSize(t, 'a1', true).groups[0].items[0].large).toBe(true);
    expect(locate(t, 'b1')).toEqual({ g: 1, i: 0 });
  });
});

describe('tools this edition lacks', () => {
  it('are hidden without breaking the tab, and stay in the data', () => {
    const t = sample();
    const shown = visibleTab(t, (id) => id !== 'a2' && id !== 'b1');
    expect(shape(shown)).toEqual(['A:a1,a3', 'B:']);
    expect(shape(t)).toEqual(['A:a1,a2,a3', 'B:b1']);
    // The module is back: the tool is where it was.
    expect(shape(visibleTab(t, () => true))).toEqual(shape(t));
    expect(isEmpty(visibleTab(t, () => false))).toBe(true);
  });
});

describe('reading what was kept', () => {
  it('reads a good file back, and survives anything else', () => {
    const t = sample();
    expect(readMyTools(JSON.parse(JSON.stringify({ version: 1, tabs: [t] })))).toEqual([t]);
    for (const bad of [null, undefined, 3, 'x', [], {}, { tabs: 4 }, { tabs: [null, 3, 'x', {}] }, { tabs: [{ groups: 'no' }] }]) expect(readMyTools(bad)).toEqual([]);
  });
  it('repairs: a tool once, names trimmed, a plain id read as a large tool, a missing group id made', () => {
    const [tab] = readMyTools({ tabs: [{ id: 'my-tools', label: ' Mine ', groups: [
      { id: 'g', label: ' One ', items: [{ tool: 'a' }, 'b', { tool: 'a' }, { tool: 5 }, { tool: 'c', large: false }] },
      { id: 'g', label: '', items: [{ tool: 'b' }, { tool: 'd' }], extra: 1 },
    ] }] });
    expect(tab.label).toBe('Mine');
    expect(tab.groups[0].label).toBe('One');
    expect(toolsOf(tab)).toEqual(['a', 'b', 'c', 'd']);
    expect(tab.groups[0].items.map((i) => i.large)).toEqual([true, true, false]);
    expect(tab.groups[1].label).toBe('New group');
    expect(tab.groups[0].id).not.toBe(tab.groups[1].id);
  });
});

describe('export and import', () => {
  it('round-trips a tab with new group ids', () => {
    const t = setSize(sample(), 'a1', false);
    const back = importTab(exportTab(t))!;
    expect(shape(back)).toEqual(shape(t));
    expect(back.groups[0].items[0].large).toBe(false);
    expect(back.groups[0].id).not.toBe(t.groups[0].id);
  });
  it('refuses what is not an exported tab', () => {
    expect(importTab('not json')).toBeNull();
    expect(importTab('{"groups":[]}')).toBeNull();
    expect(importTab('{"format":"other","groups":[]}')).toBeNull();
    expect(importTab('{"format":"lapin-my-tools"}')).toBeNull();
  });
});

describe('the store', () => {
  beforeEach(() => { useMyTools.setState({ tabs: [], past: [] }); });
  it('keeps a change, and undoes it step by step', () => {
    changeMyTab((t) => addTool(t, 'wall'));
    changeMyTab((t) => addTool(t, 'door'));
    expect(toolsOf(myTab())).toEqual(['wall', 'door']);
    expect(undoMyTools()).toBe(true);
    expect(toolsOf(myTab())).toEqual(['wall']);
    expect(undoMyTools()).toBe(true);
    expect(toolsOf(myTab())).toEqual([]);
    expect(undoMyTools()).toBe(false);
  });
  it('counts a change that changed nothing as no step', () => {
    changeMyTab((t) => removeTool(t, 'nope'));
    expect(useMyTools.getState().past).toHaveLength(0);
  });
  it('resets and imports as one undoable step each', () => {
    changeMyTab((t) => addTool(t, 'wall'));
    const file = exportTab(sample());
    expect(importMyTab('junk')).toBe(false);
    expect(importMyTab(file)).toBe(true);
    expect(toolsOf(myTab())).toEqual(['a1', 'a2', 'a3', 'b1']);
    resetMyTab();
    expect(toolsOf(myTab())).toEqual([]);
    undoMyTools(); undoMyTools();
    expect(toolsOf(myTab())).toEqual(['wall']);
  });
  it('holds several tabs apart', () => {
    changeMyTab((t) => addTool(t, 'a'));
    changeMyTab((t) => addTool(t, 'b'), 'second');
    expect(toolsOf(myTab(undefined, 'second'))).toEqual(['b']);
    expect(toolsOf(myTab())).toEqual(['a']);
  });
});
