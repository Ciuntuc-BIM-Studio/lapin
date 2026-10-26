import { beforeEach, describe, expect, it } from 'vitest';
import { clamp, columns, setPanelWidth, togglePanel, usePanels } from './panels';

describe('the side panels', () => {
  beforeEach(() => usePanels.setState({ browserWidth: 250, propsWidth: 300, browserOpen: true, propsOpen: true }));

  it('lays out the browser, a separator, the views, a separator and the properties', () => {
    expect(columns(usePanels.getState())).toBe('250px 6px minmax(0, 1fr) 6px 300px');
  });

  it('resizes within bounds and docks a panel away to a rail', () => {
    setPanelWidth('browser', 400);
    setPanelWidth('props', 20);
    expect(usePanels.getState()).toMatchObject({ browserWidth: 400, propsWidth: clamp(20) });
    togglePanel('props');
    expect(columns(usePanels.getState())).toBe('400px 6px minmax(0, 1fr) 0px 30px');
    togglePanel('props');
    expect(usePanels.getState().propsOpen).toBe(true);
  });
});
