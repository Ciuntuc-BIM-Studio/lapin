import { beforeEach, describe, expect, it } from 'vitest';
import { applyPanelsPart, clamp, columns, setPanelWidth, showOverlay, toggleBothPanels, togglePanel, usePanels } from './panels';

describe('the side panels', () => {
  beforeEach(() => usePanels.setState({ browserWidth: 250, propsWidth: 300, browserOpen: true, propsOpen: true, railsHidden: false, overlay: null }));

  it('lays out the browser, a separator, the views, a separator and the properties', () => {
    expect(columns(usePanels.getState())).toBe('250px 6px minmax(0, 1fr) 6px 300px');
  });

  it('resizes within bounds and docks a panel away to a rail', () => {
    setPanelWidth('browser', 400);
    setPanelWidth('props', 20);
    expect(usePanels.getState()).toMatchObject({ browserWidth: 400, propsWidth: clamp(20) });
    togglePanel('props');
    expect(columns(usePanels.getState())).toBe('400px 6px minmax(0, 1fr) 0px 36px');
    togglePanel('props');
    expect(usePanels.getState().propsOpen).toBe(true);
  });

  it('folds both and back with one toggle, and hides the rails in Focus', () => {
    toggleBothPanels();
    expect(usePanels.getState()).toMatchObject({ browserOpen: false, propsOpen: false });
    toggleBothPanels();
    expect(usePanels.getState()).toMatchObject({ browserOpen: true, propsOpen: true });
    applyPanelsPart('browserRail', 260);
    expect(columns(usePanels.getState())).toBe('36px 0px minmax(0, 1fr) 6px 260px');
    applyPanelsPart('hidden');
    expect(columns(usePanels.getState())).toBe('0px 0px minmax(0, 1fr) 0px 0px');
    showOverlay('browser', 'ifc');
    expect(usePanels.getState().overlay).toEqual({ side: 'browser', tab: 'ifc' });
    showOverlay('browser', 'ifc');
    expect(usePanels.getState().overlay).toBeNull();
    applyPanelsPart('docked');
  });
});
