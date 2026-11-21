import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { canPlace, foldsFor, matchingSheets, onSheetTitle, paperForNew, placedOn, sheetLabel, unfold, viewIdOfTab, viewSheetItems } from './browserFind';
import { groupItems, implicitPlan, itemOf, THREE_D, type BrowserSet } from './browserSets';

const node = (id: string, type: string, name: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type, name, props } as unknown as NodeDto);
const map = (...ns: NodeDto[]) => new Map(ns.map((n) => [n.id, n]));

const level = node('L1', 'IfcBuildingStorey', 'Ground floor', { elevation: 0 });
const section = node('S1', 'LapinView', 'Section 1', { kind: 'section', discipline: 'Architecture' });
const detail = node('D1', 'LapinView', 'Detail 1', { kind: 'section', calloutOf: 'S1', discipline: 'Architecture' });
const sitePlan = node('SP', 'LapinView', 'Site plan', { kind: 'plan', site: 'site', storeyId: 'L1' });
const schedule = node('Q1', 'LapinSchedule', 'Doors', {});
const a101 = node('A1', 'LapinSheet', 'Plans', { number: 'A-101', paper: 'A1', orientation: 'landscape', viewports: [{ id: 'v1', viewId: 'S1' }, { id: 'v2', viewId: 'plan:L1' }] });
const a102 = node('A2', 'LapinSheet', 'Sections', { number: 'A-102', paper: 'A2', orientation: 'portrait', viewports: [{ id: 'v3', viewId: 'S1', page: 2 }, { id: 'v4', viewId: 'S1', page: 3 }] });
const a1010 = node('A3', 'LapinSheet', 'Details', { number: 'A-1010', viewports: [] });

describe('the view a tab shows (Find in project browser)', () => {
  const nodes = map(level, section, a101);
  const pane = { drawingViewId: null, sheetId: null, scheduleId: null, planViewId: null, view3dId: null };
  it('is a level\'s default plan, a drawing, a sheet (its paper too), a schedule or the 3D', () => {
    expect(viewIdOfTab({ ...pane, view: 'plan', storeyId: 'L1' }, nodes)).toBe('plan:L1');
    expect(viewIdOfTab({ ...pane, view: 'drawing', drawingViewId: 'S1' }, nodes)).toBe('S1');
    expect(viewIdOfTab({ ...pane, view: 'drawing', drawingViewId: 'paper:A1' }, nodes)).toBe('A1');
    expect(viewIdOfTab({ ...pane, view: 'sheet', sheetId: 'A1' }, nodes)).toBe('A1');
    expect(viewIdOfTab({ ...pane, view: 'schedule', scheduleId: 'Q1' }, nodes)).toBe('Q1');
    expect(viewIdOfTab({ ...pane, view: '3d' }, nodes)).toBe('3d');
    expect(viewIdOfTab({ ...pane, view: '3d', view3dId: 'V3' }, nodes)).toBe('V3');
    expect(viewIdOfTab({ ...pane, view: 'gantt' }, nodes)).toBeNull();
  });

  it('is the level\'s own floor plan view once it has one', () => {
    const own = node('P1', 'LapinView', 'Ground floor', { kind: 'plan', storeyId: 'L1' });
    expect(viewIdOfTab({ ...pane, view: 'plan', storeyId: 'L1' }, map(level, own))).toBe('P1');
  });
});

describe('the folds that hide a row', () => {
  const sets: BrowserSet[] = [{ id: 'architecture', name: 'Architecture', filter: { disciplines: ['Architecture'] } }, { id: 'sheets', name: 'Sheets', filter: { kinds: ['sheet'] } }];
  const items = [implicitPlan(level), THREE_D, ...[section, detail, a101, schedule].map(itemOf).filter((i) => i !== null)];
  const groups = groupItems(sets, items);
  const site = [itemOf(sitePlan)].filter((i) => i !== null);

  it('are the set and the kind in it, the first set that holds it', () => {
    expect(foldsFor(groups, [], 'plan:L1')).toEqual(['set:architecture', 'architecture:plans']);
    expect(foldsFor(groups, [], 'S1')).toEqual(['set:architecture', 'architecture:sections']);
    expect(foldsFor(groups, [], '3d')).toEqual(['set:architecture', 'architecture:3d']);
  });

  it('put a callout\'s detail under Details, a sheet in the set that takes it, the rest under Other', () => {
    expect(foldsFor(groups, [], 'D1')).toEqual(['set:architecture', 'architecture:details']);
    expect(foldsFor(groups, [], 'A1')).toEqual(['set:architecture', 'architecture:sheets']);
    // A schedule names no discipline of its own: Architecture takes it as the default.
    expect(foldsFor(groupItems([{ id: 's', name: 'S', filter: { kinds: ['section'] } }], items), [], 'Q1')).toEqual(['set:other', 'other:schedules']);
  });

  it('are the Site\'s for a site drawing, and none for what the browser does not list', () => {
    expect(foldsFor(groups, site, 'SP')).toEqual(['site']);
    expect(foldsFor(groups, site, 'nowhere')).toBeNull();
  });

  it('open only those, and say so when none was closed', () => {
    expect([...unfold(new Set(['set:architecture', 'architecture:sections', 'site']), ['set:architecture', 'architecture:sections'])!]).toEqual(['site']);
    expect(unfold(new Set(['site']), ['set:architecture', 'architecture:sections'])).toBeNull();
  });
});

describe('where the views are placed', () => {
  const nodes = map(level, section, schedule, a102, a101, a1010);
  it('lists each view\'s sheets once, in number order', () => {
    const on = placedOn(nodes);
    expect(on.get('S1')!.map((s) => s.id)).toEqual(['A1', 'A2']);
    expect(on.get('plan:L1')!.map((s) => s.id)).toEqual(['A1']);
    expect(on.has('Q1')).toBe(false);
  });

  it('names them in the marker\'s tooltip', () => {
    expect(onSheetTitle([a101])).toBe('On sheet A-101 · Plans');
    expect(onSheetTitle([a101, a102])).toBe('On sheets A-101 · Plans, A-102 · Sections');
    expect(sheetLabel(node('X', 'LapinSheet', 'Unnumbered'))).toBe('Unnumbered');
  });

  it('finds the sheets by number or name, numbers in their natural order', () => {
    expect(matchingSheets(nodes, '').map((s) => s.id)).toEqual(['A1', 'A2', 'A3']);
    expect(matchingSheets(nodes, 'sect').map((s) => s.id)).toEqual(['A2']);
    expect(matchingSheets(nodes, ' a-10 ').map((s) => s.id)).toEqual(['A1', 'A2', 'A3']);
    expect(matchingSheets(nodes, 'zzz')).toEqual([]);
  });

  it('makes a new sheet on the last sheet\'s paper, else A3', () => {
    expect(paperForNew(map(a101, a102))).toEqual({ paper: 'A2', orientation: 'portrait' });
    expect(paperForNew(map(level))).toEqual({ paper: 'A3' });
    const custom = node('C', 'LapinSheet', 'Long', { number: 'Z-1', paper: 'custom', paperWidth: 1500, paperHeight: 600 });
    expect(paperForNew(map(custom))).toEqual({ paper: 'custom', orientation: 'landscape', paperWidth: 1500, paperHeight: 600 });
  });
});

describe('what goes on a sheet', () => {
  const nodes = map(level, section, schedule, a101);
  it('is a saved view, a level\'s plan or a schedule; not the 3D, a sheet or nothing', () => {
    expect(canPlace('S1', nodes)).toBe(true);
    expect(canPlace('plan:L1', nodes)).toBe(true);
    expect(canPlace('Q1', nodes)).toBe(true);
    expect(canPlace('3d', nodes)).toBe(false);
    expect(canPlace('A1', nodes)).toBe(false);
    expect(canPlace('plan:gone', nodes)).toBe(false);
    expect(canPlace(null, nodes)).toBe(false);
  });

  it('gives a view\'s menu Find in project browser, and Place on sheet… when it can go on one', () => {
    expect(viewSheetItems('S1', nodes).map((i) => i.label)).toEqual(['Find in project browser', 'Place on sheet…']);
    expect(viewSheetItems('3d', nodes).map((i) => i.label)).toEqual(['Find in project browser']);
    expect(viewSheetItems('A1', nodes).map((i) => i.label)).toEqual(['Find in project browser']);
    expect(viewSheetItems(null, nodes)).toEqual([]);
  });
});
