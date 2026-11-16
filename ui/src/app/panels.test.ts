import { beforeEach, describe, expect, it } from 'vitest';
import { applyPanelsPart, clamp, clampRatio, columns, layoutName, panelsOn, setLayout, setStackRatio, setPanelWidth, nextPropsTabForTool, showOverlay, toggleBothPanels, togglePanel, usePanels } from './panels';

describe('the side panels', () => {
  beforeEach(() => usePanels.setState({ browserSide: 'left', propsSide: 'right', stackRatio: 0.5, browserWidth: 250, propsWidth: 300, browserOpen: true, propsOpen: true, railsHidden: false, overlay: null }));

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

  it('puts the panels on either side, or stacks them on one', () => {
    expect(layoutName(usePanels.getState())).toBe('default');
    setLayout('swapped');
    expect(columns(usePanels.getState())).toBe('300px 6px minmax(0, 1fr) 6px 250px');
    expect(panelsOn(usePanels.getState(), 'left')).toEqual(['props']);
    setLayout('bothLeft');
    expect(panelsOn(usePanels.getState(), 'left')).toEqual(['browser', 'props']);
    expect(columns(usePanels.getState())).toBe('250px 6px minmax(0, 1fr) 0px 0px');
    // stacked: the side keeps the browser's width, whichever is dragged
    setPanelWidth('props', 400);
    expect(usePanels.getState().browserWidth).toBe(400);
    // one folded: the other keeps the side; both folded: a rail
    togglePanel('browser');
    expect(columns(usePanels.getState())).toBe('300px 6px minmax(0, 1fr) 0px 0px');
    togglePanel('props');
    expect(columns(usePanels.getState())).toBe('36px 0px minmax(0, 1fr) 0px 0px');
    setLayout('bothRight');
    expect(columns(usePanels.getState())).toBe('0px 0px minmax(0, 1fr) 0px 36px');
    expect(layoutName(usePanels.getState())).toBe('bothRight');
  });

  it('shares the height within bounds and keeps the choice', () => {
    setStackRatio(0.01);
    expect(usePanels.getState().stackRatio).toBe(clampRatio(0.01));
    setLayout('swapped');
    expect(usePanels.getState()).toMatchObject({ browserSide: 'right', propsSide: 'left' });
  });
});

describe('nextPropsTabForTool', () => {
  it('switches from properties to tool when a tool activates and restores properties on end', () => {
    const active = nextPropsTabForTool('properties', undefined, true);
    expect(active).toEqual({ propsTab: 'tool', prevPropsTab: 'properties' });

    const ended = nextPropsTabForTool(active.propsTab, active.prevPropsTab, false);
    expect(ended).toEqual({ propsTab: 'properties', prevPropsTab: undefined });
  });

  it('switches from assistant to tool and restores assistant when tool ends', () => {
    const active = nextPropsTabForTool('assistant', undefined, true);
    expect(active).toEqual({ propsTab: 'tool', prevPropsTab: 'assistant' });

    const ended = nextPropsTabForTool(active.propsTab, active.prevPropsTab, false);
    expect(ended).toEqual({ propsTab: 'assistant', prevPropsTab: undefined });
  });

  it('preserves prevPropsTab when switching directly from one tool to another', () => {
    const next = nextPropsTabForTool('tool', 'assistant', true);
    expect(next).toEqual({ propsTab: 'tool', prevPropsTab: 'assistant' });
  });

  it('keeps current non-tool tab when no tool is active', () => {
    expect(nextPropsTabForTool('assistant', undefined, false)).toEqual({
      propsTab: 'assistant',
      prevPropsTab: undefined,
    });
    expect(nextPropsTabForTool('properties', undefined, false)).toEqual({
      propsTab: 'properties',
      prevPropsTab: undefined,
    });
  });

  it('falls back to properties if tool ends without prevPropsTab', () => {
    expect(nextPropsTabForTool('tool', undefined, false)).toEqual({
      propsTab: 'properties',
      prevPropsTab: undefined,
    });
  });
});
