import { beforeEach, describe, expect, it } from 'vitest';
import { closePane, emptyPane, focusPane, setPane, splitPane, useUi } from './store';

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
