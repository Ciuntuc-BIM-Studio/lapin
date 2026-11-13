import { beforeEach, describe, expect, it } from 'vitest';
import { PREVIEW_DEFAULTS, fitBounds, keyMatches, layerKey, previewArgs, shownPanes, useTypePreview } from './typePreviewState';

const names = { storey: 'Storey', above: 'Storey above', section: 'A' };
const type = { name: 'Brick 300', layers: [{ material: 'Brick', thickness: 0.25, core: true }] };

describe('the type preview', () => {
  beforeEach(() => useTypePreview.setState({ ...PREVIEW_DEFAULTS }));

  it('starts at 1:50, fine, a 3 m sample of its type\'s own height, on the plan', () => {
    expect(useTypePreview.getState()).toMatchObject({ scale: 50, detail: 'fine', length: 3, height: null, pane: 'plan' });
  });

  it('asks the engine for the three views of the type as it is edited, with the sample\'s sizes', () => {
    const a = previewArgs('IfcWallType', type, useTypePreview.getState(), names);
    expect(a).toEqual({ class: 'IfcWallType', type, sample: { length: 3, names }, views: ['plan', 'section', '3d'], scale: 50, detail: 'fine' });
    useTypePreview.setState({ scale: 20, detail: 'coarse', height: 2.4, length: 5 });
    const b = previewArgs('IfcWallType', type, useTypePreview.getState(), names);
    expect(b).toMatchObject({ scale: 20, detail: 'coarse', sample: { length: 5, height: 2.4 } });
    // A height cleared: as the type's top says.
    useTypePreview.setState({ height: null });
    expect(previewArgs('IfcWallType', type, useTypePreview.getState(), names).sample).not.toHaveProperty('height');
  });

  it('lights the hovered row\'s layer by its part key, and nothing when no row is hovered', () => {
    expect(layerKey(0)).toBe('layer:0');
    expect(keyMatches('layer:1', 1)).toBe(true);
    expect(keyMatches('layer:1:zone', 1)).toBe(true);
    expect(keyMatches('layer:10', 1)).toBe(false);
    expect(keyMatches('layer:1', null)).toBe(false);
    expect(keyMatches(null, 1)).toBe(false);
  });

  it('shows the three panes side by side when wide, the picked one alone (tabs) when narrow', () => {
    expect(shownPanes(900, 'section')).toEqual(['plan', 'section', '3d']);
    expect(shownPanes(500, 'section')).toEqual(['section']);
  });

  it('fits a drawing into its pane at one scale, centred, y down', () => {
    const f = fitBounds([0, -1, 4, 1], 220, 120, 10);
    // 4 × 2 m into 200 × 100 px: 50 px a metre both ways.
    expect(f.k).toBe(50);
    expect(f.toScreen({ x: 0, y: 1 })).toEqual({ x: 10, y: 10 });
    expect(f.toScreen({ x: 4, y: -1 })).toEqual({ x: 210, y: 110 });
    // A tall drawing is centred across.
    const g = fitBounds([0, 0, 1, 4], 220, 120, 10);
    expect(g.k).toBe(25);
    expect(g.toScreen({ x: 0.5, y: 2 }).x).toBe(110);
  });
});
