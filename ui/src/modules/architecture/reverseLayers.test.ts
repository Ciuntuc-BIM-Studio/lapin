import { describe, expect, it } from 'vitest';
import { moveLayer, reverseLayers } from './reverseLayers';
import { dropStash, openMaterialsFrom, returnToDialog, takeStash } from '../core/dialogReturn';
import { useUi } from '../../app/store';

describe('Reverse layers', () => {
  it('runs the rows the other way, keeping each layer whole and the input untouched', () => {
    const rows = [{ material: 'Render', core: false }, { material: 'Brick', core: true }, { material: 'Plaster', core: false }];
    const r = reverseLayers(rows);
    expect(r.map((x) => x.material)).toEqual(['Plaster', 'Brick', 'Render']);
    expect(r.find((x) => x.core)).toBe(rows[1]);
    expect(rows.map((x) => x.material)).toEqual(['Render', 'Brick', 'Plaster']);
    expect(reverseLayers(r).map((x) => x.material)).toEqual(['Render', 'Brick', 'Plaster']);
  });
});

describe('the way out to Materials and back', () => {
  it('keeps the editor state for the dialog it came from, once', () => {
    openMaterialsFrom('wallTypes', 'Brick', { name: 'Mine' });
    expect(useUi.getState()).toMatchObject({ dialog: 'materials', returnTo: 'wallTypes', materialFocus: { name: 'Brick', tab: 'graphics' } });
    returnToDialog();
    expect(useUi.getState()).toMatchObject({ dialog: 'wallTypes', returnTo: null, materialFocus: null });
    expect(takeStash('wallTypes')).toEqual({ name: 'Mine' });
    expect(takeStash('wallTypes')).toBeNull();
  });

  it('drops it when Materials is closed instead', () => {
    openMaterialsFrom('slabTypes', 'Screed', { name: 'x' });
    dropStash(); // still Materials (a remount): kept
    expect(useUi.getState().returnTo).toBe('slabTypes');
    useUi.setState({ dialog: null });
    dropStash();
    expect(takeStash('slabTypes')).toBeNull();
    expect(useUi.getState().returnTo).toBeNull();
  });
});

describe('Move a layer', () => {
  const rows = ['Finish', 'Screed', 'Concrete'];
  it('swaps the layer with its neighbour and leaves the input untouched', () => {
    expect(moveLayer(rows, 1, -1)).toEqual(['Screed', 'Finish', 'Concrete']);
    expect(moveLayer(rows, 1, 1)).toEqual(['Finish', 'Concrete', 'Screed']);
    expect(rows).toEqual(['Finish', 'Screed', 'Concrete']);
  });
  it('does nothing past either end', () => {
    expect(moveLayer(rows, 0, -1)).toEqual(rows);
    expect(moveLayer(rows, 2, 1)).toEqual(rows);
    expect(moveLayer(rows, 5, 1)).toEqual(rows);
  });
});
