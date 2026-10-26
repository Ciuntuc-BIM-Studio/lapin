import { beforeEach, describe, expect, it } from 'vitest';
import { closePane, closeTab, emptyPane, focusPane, openTab, setPane, splitPane, tabKey, useUi } from './store';

describe('the panes', () => {
  beforeEach(() => { useUi.setState({ panes: [emptyPane('plan')], pane: 0, ...emptyPane('plan') }); });

  it('mirrors the active pane in the view fields, both ways', () => {
    useUi.setState({ view: 'drawing', drawingViewId: 'v1' });
    expect(useUi.getState().panes[0]).toMatchObject({ view: 'drawing', drawingViewId: 'v1' });
    setPane(0, { view: '3d' });
    expect(useUi.getState().view).toBe('3d');
    expect(useUi.getState().panes[0].drawingViewId).toBe('v1');
  });

  it('splits into two panes, the new one active with another view, and focusing a pane brings its view in front', () => {
    splitPane('row');
    const s = useUi.getState();
    expect(s.panes).toHaveLength(2);
    expect(s.pane).toBe(1);
    expect(s.view).toBe('3d');
    expect(s.panes[0].view).toBe('plan');
    // The inactive pane keeps its own ids.
    setPane(0, { view: 'sheet', sheetId: 's1' });
    expect(useUi.getState().view).toBe('3d');
    focusPane(0);
    expect(useUi.getState()).toMatchObject({ pane: 0, view: 'sheet', sheetId: 's1' });
    // A second split only changes the direction.
    splitPane('column');
    expect(useUi.getState().panes).toHaveLength(2);
    expect(useUi.getState().split).toBe('column');
  });

  it('closes a pane: the other stays, active, and a lone pane cannot close', () => {
    splitPane();
    closePane(1);
    expect(useUi.getState().panes).toHaveLength(1);
    expect(useUi.getState()).toMatchObject({ pane: 0, view: 'plan' });
    closePane(0);
    expect(useUi.getState().panes).toHaveLength(1);
  });

  it('leaves the other fields alone', () => {
    useUi.setState({ storeyId: 's2', view: '3d' });
    expect(useUi.getState().storeyId).toBe('s2');
    expect(useUi.getState().panes[0].view).toBe('3d');
  });
});

describe('the view tabs', () => {
  beforeEach(() => { useUi.setState({ panes: [emptyPane('plan')], pane: 0, ...emptyPane('plan'), viewTabs: [], storeyId: null, screen: 'start' }); });

  it('opens a tab for every view brought in front, once', () => {
    useUi.setState({ screen: 'workspace', storeyId: null });
    expect(useUi.getState().viewTabs.map(tabKey)).toEqual(['plan:']);
    useUi.setState({ view: '3d', view3dId: null });
    useUi.setState({ view: 'drawing', drawingViewId: 'v1' });
    useUi.setState({ view: '3d', view3dId: null });
    expect(useUi.getState().viewTabs.map(tabKey)).toEqual(['plan:', '3d', 'v1']);
  });

  it('the first storey chosen takes over the lowest-storey plan tab; the next storeys get tabs of their own', () => {
    useUi.setState({ screen: 'workspace', storeyId: null });
    useUi.setState({ storeyId: 's1' });
    expect(useUi.getState().viewTabs.map(tabKey)).toEqual(['plan:s1']);
    useUi.setState({ storeyId: 's2' });
    expect(useUi.getState().viewTabs.map(tabKey)).toEqual(['plan:s1', 'plan:s2']);
    // Choosing a storey while the 3D is in front opens no plan tab.
    useUi.setState({ view: '3d' });
    useUi.setState({ storeyId: 's3' });
    expect(useUi.getState().viewTabs.map(tabKey)).toEqual(['plan:s1', 'plan:s2', '3d']);
  });

  it('showing a tab sets the pane and, for a plan, the storey; closing one falls back to its neighbour', () => {
    useUi.setState({ screen: 'workspace', storeyId: 's1' });
    useUi.setState({ storeyId: 's2' });
    useUi.setState({ view: 'sheet', sheetId: 'sh1' });
    const tabs = useUi.getState().viewTabs;
    openTab(0, tabs[0]);
    expect(useUi.getState()).toMatchObject({ view: 'plan', storeyId: 's1' });
    openTab(0, tabs[2]);
    expect(useUi.getState()).toMatchObject({ view: 'sheet', sheetId: 'sh1' });
    closeTab('sh1');
    expect(useUi.getState().viewTabs.map(tabKey)).toEqual(['plan:s1', 'plan:s2']);
    expect(useUi.getState()).toMatchObject({ view: 'plan', storeyId: 's2' });
    closeTab('plan:s2');
    closeTab('plan:s1');
    // The last tab closed leaves a plan.
    expect(useUi.getState().viewTabs).toHaveLength(1);
    expect(useUi.getState().view).toBe('plan');
  });

  it('entering the workspace starts the tabs afresh', () => {
    useUi.setState({ screen: 'workspace' });
    useUi.setState({ view: '3d' });
    expect(useUi.getState().viewTabs).toHaveLength(2);
    useUi.setState({ screen: 'workspace', storeyId: null, tool: null });
    expect(useUi.getState().viewTabs.map(tabKey)).toEqual(['3d']);
  });
});
