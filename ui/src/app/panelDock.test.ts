import { beforeEach, describe, expect, it, vi } from 'vitest';
import { clampFloat, columns, dockPanel, dropChange, dropZone, layoutName, panelsOn, setFloatRect, setLayout, setPanelWidth, toggleFloat, togglePanel, usePanels } from './panels';

describe('dragging a panel by its header (decision 755)', () => {
  beforeEach(() => usePanels.setState({ browserSide: 'left', propsSide: 'right', propsFirst: false, browserFloat: null, propsFloat: null, stackRatio: 0.5, browserWidth: 250, propsWidth: 300, browserOpen: true, propsOpen: true, railsHidden: false, overlay: null }));
  const views = { left: 250, top: 100, right: 1300, bottom: 900 };
  const panels = { browser: { left: 0, top: 100, right: 250, bottom: 900 }, props: { left: 1300, top: 100, right: 1600, bottom: 900 } };
  const at = { x: 400, y: 200, w: 300, h: 400 };

  it('finds the zone: over a panel its half, the views edges, the views middle, nothing elsewhere', () => {
    expect(dropZone(1400, 200, views, panels, 'browser')).toEqual({ kind: 'stack', target: 'props', where: 'above' });
    expect(dropZone(1400, 800, views, panels, 'browser')).toEqual({ kind: 'stack', target: 'props', where: 'below' });
    expect(dropZone(1290, 300, views, panels, 'browser')).toEqual({ kind: 'edge', side: 'right', where: 'above' });
    expect(dropZone(260, 800, views, panels, 'props')).toEqual({ kind: 'edge', side: 'left', where: 'below' });
    expect(dropZone(700, 500, views, panels, 'browser')).toEqual({ kind: 'float' });
    expect(dropZone(100, 300, views, panels, 'browser')).toBeNull(); // its own place
    expect(dropZone(700, 40, views, panels, 'browser')).toBeNull(); // the ribbon
  });

  it('stacks above or below the other panel, on its side, and puts the order in the state', () => {
    dockPanel('browser', { kind: 'stack', target: 'props', where: 'above' }, at);
    expect(usePanels.getState()).toMatchObject({ browserSide: 'right', propsSide: 'right', propsFirst: false });
    expect(panelsOn(usePanels.getState(), 'right')).toEqual(['browser', 'props']);
    dockPanel('browser', { kind: 'stack', target: 'props', where: 'below' }, at);
    expect(panelsOn(usePanels.getState(), 'right')).toEqual(['props', 'browser']);
    expect(layoutName(usePanels.getState())).toBe('custom'); // the properties above the browser is no preset
  });

  it('docks to a side above or below what stands there, and the menu layouts reset everything', () => {
    dockPanel('props', { kind: 'edge', side: 'left', where: 'below' }, at);
    expect(panelsOn(usePanels.getState(), 'left')).toEqual(['browser', 'props']);
    dockPanel('props', { kind: 'edge', side: 'left', where: 'above' }, at);
    expect(panelsOn(usePanels.getState(), 'left')).toEqual(['props', 'browser']);
    dockPanel('browser', { kind: 'float' }, at);
    setLayout('swapped');
    expect(usePanels.getState()).toMatchObject({ browserSide: 'right', propsSide: 'left', propsFirst: false, browserFloat: null });
    expect(panelsOn(usePanels.getState(), 'right')).toEqual(['browser']);
  });

  it('floats a panel off its side: the side shrinks, the other keeps its width, and docking back restores', () => {
    dockPanel('browser', { kind: 'float' }, at);
    const s = usePanels.getState();
    expect(s.browserFloat).toEqual(at);
    expect(panelsOn(s, 'left')).toEqual([]);
    expect(columns(s)).toBe('0px 0px minmax(0, 1fr) 6px 300px');
    toggleFloat('browser', { w: 1600, h: 900 });
    expect(usePanels.getState().browserFloat).toBeNull();
    expect(columns(usePanels.getState())).toBe('250px 6px minmax(0, 1fr) 6px 300px');
  });

  it('a stacked side takes its width from its first open panel, whichever it is', () => {
    setLayout('bothLeft');
    usePanels.setState({ propsFirst: true });
    expect(columns(usePanels.getState())).toBe('300px 6px minmax(0, 1fr) 0px 0px');
    setPanelWidth('browser', 400);
    expect(usePanels.getState().propsWidth).toBe(400);
  });

  it('keeps a floating panel inside the window, with a minimum size, and persists it', () => {
    expect(clampFloat({ x: 1500, y: 800, w: 400, h: 500 }, { w: 1600, h: 900 })).toEqual({ x: 1200, y: 400, w: 400, h: 500 });
    expect(clampFloat({ x: -50, y: -9, w: 10, h: 10 }, { w: 1600, h: 900 })).toEqual({ x: 0, y: 0, w: 220, h: 160 });
    const store = new Map<string, string>();
    vi.stubGlobal('localStorage', { getItem: (k: string) => store.get(k) ?? null, setItem: (k: string, v: string) => void store.set(k, v) });
    setFloatRect('props', { x: 5000, y: 5000, w: 300, h: 300 }, { w: 1000, h: 800 });
    expect(usePanels.getState().propsFloat).toEqual({ x: 700, y: 500, w: 300, h: 300 });
    expect(JSON.parse(store.get('lapin.panels')!).propsFloat).toEqual({ x: 700, y: 500, w: 300, h: 300 });
    vi.unstubAllGlobals();
  });

  it('reads as custom with a float or a reversed stack, and a floating panel is never folded', () => {
    expect(layoutName(usePanels.getState())).toBe('default');
    dockPanel('props', { kind: 'float' }, at);
    expect(layoutName(usePanels.getState())).toBe('custom');
    togglePanel('props');
    expect(usePanels.getState().propsOpen).toBe(true);
    setLayout('bothLeft');
    expect(layoutName(usePanels.getState())).toBe('bothLeft');
    usePanels.setState({ propsFirst: true });
    expect(layoutName(usePanels.getState())).toBe('custom');
    usePanels.setState({ propsFirst: true, propsSide: 'right' });
    expect(layoutName(usePanels.getState())).toBe('default');
  });

  it('dropChange alone does not touch the store', () => {
    const before = JSON.stringify(usePanels.getState());
    dropChange(usePanels.getState(), 'props', { kind: 'edge', side: 'left' }, at);
    expect(JSON.stringify(usePanels.getState())).toBe(before);
  });
});
