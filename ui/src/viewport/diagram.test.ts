import { beforeEach, vi, describe, expect, it } from 'vitest';
import { categoryOf, DEFAULT_PALETTE, diagramColour, diagramLooks, effectiveDiagram, palettePatch, projectPaletteOf, readDiagram, resetDiagramPalette, setDiagram, useDiagram, WHITE_MODEL, CATEGORIES } from './diagram';

const items = [
  { id: 'w', type: 'IfcWall' }, { id: 's', type: 'IfcSlab' }, { id: 'd', type: 'IfcDoor' }, { id: 'sp', type: 'IfcSpace' },
  { id: 't', type: 'IfcGeographicElement', predefinedType: 'TERRAIN' }, { id: 'tree', type: 'IfcGeographicElement', predefinedType: 'VEGETATION' },
  { id: 'x', type: 'IfcBuildingElementProxy' },
];

describe('the diagram palette', () => {
  it('sorts classes into categories, and leaves rooms and drafts alone', () => {
    expect(categoryOf('IfcWallStandardCase')).toBe('walls');
    expect(categoryOf('IfcRoof')).toBe('roofs');
    expect(categoryOf('IfcGeographicElement', 'TERRAIN')).toBe('site');
    expect(categoryOf('IfcGeographicElement', 'VEGETATION')).toBe('greenery');
    expect(categoryOf('IfcBuildingElementProxy')).toBe('other');
    expect(categoryOf('IfcSpace')).toBeNull();
    expect(categoryOf('IfcAnnotation')).toBeNull();
  });

  it('paints every element of a category with its pastel', () => {
    const out = diagramLooks({}, items, { variant: 'pastel', palette: {} });
    expect(out.w?.color).toBe(DEFAULT_PALETTE.walls);
    expect(out.s?.color).toBe(DEFAULT_PALETTE.floors);
    expect(out.d?.color).toBe(DEFAULT_PALETTE.openings);
    expect(out.t?.color).toBe(DEFAULT_PALETTE.site);
    expect(out.tree?.color).toBe(DEFAULT_PALETTE.greenery);
    expect(out.x?.color).toBe(DEFAULT_PALETTE.other);
    expect(out.sp).toBeUndefined();
  });

  it('takes the user’s colour for a category, the default for the rest', () => {
    const s = { variant: 'pastel' as const, palette: { walls: '#112233' } };
    expect(diagramColour('walls', s)).toBe('#112233');
    expect(diagramColour('roofs', s)).toBe(DEFAULT_PALETTE.roofs);
  });

  it('makes the white model one colour for all', () => {
    const out = diagramLooks({}, items, { variant: 'white', palette: { walls: '#112233' } });
    expect(new Set(['w', 's', 'd', 't', 'tree', 'x'].map((i) => out[i]?.color))).toEqual(new Set([WHITE_MODEL]));
  });

  it('lets a colour already set win, keeps hidden ones hidden and the rest of a look', () => {
    const looks = { w: { color: '#ff0000' }, s: { hidden: true }, d: { opacity: 0.3 } };
    const out = diagramLooks(looks, items, { variant: 'pastel', palette: {} });
    expect(out.w).toBe(looks.w);
    expect(out.s).toBe(looks.s);
    expect(out.d).toEqual({ opacity: 0.3, color: DEFAULT_PALETTE.openings });
  });

  it('gives the same look object for the same input (a view restyles only what changed)', () => {
    const s = { variant: 'pastel' as const, palette: {} };
    expect(diagramLooks({}, items, s).w).toBe(diagramLooks({}, items, s).w);
  });
});

describe('the diagram settings kept on this computer', () => {
  const kept = new Map<string, string>();
  beforeEach(() => {
    kept.clear();
    vi.stubGlobal('localStorage', { getItem: (k: string) => kept.get(k) ?? null, setItem: (k: string, v: string) => void kept.set(k, v), removeItem: (k: string) => void kept.delete(k) });
    useDiagram.setState(readDiagram(null));
  });

  it('starts as the white model with the default palette', () => {
    expect(readDiagram(null)).toEqual({ variant: 'white', palette: {} });
    expect(readDiagram('nonsense')).toEqual({ variant: 'white', palette: {} });
  });

  it('keeps only valid colours of known categories, and not those equal to the default', () => {
    const r = readDiagram({ variant: 'pastel', palette: { walls: '#ABCDEF', roofs: 'red', nope: '#000000', floors: DEFAULT_PALETTE.floors } });
    expect(r).toEqual({ variant: 'pastel', palette: { walls: '#abcdef' } });
  });

  it('writes and reads back, and resets the palette', () => {
    setDiagram({ variant: 'pastel', palette: { walls: '#123456' } });
    expect(JSON.parse(kept.get('lapin.diagram')!)).toEqual({ variant: 'pastel', palette: { walls: '#123456' } });
    expect(readDiagram(JSON.parse(kept.get('lapin.diagram')!))).toEqual(useDiagram.getState());
    setDiagram({ palette: { walls: null } });
    expect(useDiagram.getState().palette).toEqual({});
    setDiagram({ palette: { roofs: '#654321' } });
    resetDiagramPalette();
    expect(useDiagram.getState()).toEqual({ variant: 'pastel', palette: {} });
    expect(CATEGORIES.every((c) => /^#[0-9a-f]{6}$/.test(DEFAULT_PALETTE[c]))).toBe(true);
  });
});

describe('the palette kept in the project (decision 885)', () => {
  const project = (diagramPalette?: unknown) => new Map([['p', { type: 'IfcProject', props: diagramPalette === undefined ? {} : { diagramPalette } }], ['w', { type: 'IfcWall', props: {} }]]);

  it('reads the project’s own colours, none when it has none', () => {
    expect(projectPaletteOf(project())).toBeNull();
    expect(projectPaletteOf(project({ walls: DEFAULT_PALETTE.walls }))).toBeNull();
    expect(projectPaletteOf(project({ walls: '#ABCDEF', roofs: 'red', nope: '#000000' }))).toEqual({ walls: '#abcdef' });
  });

  it('draws the 3D with the project’s palette, else this computer’s; the variant stays this computer’s', () => {
    const local = { variant: 'pastel' as const, palette: { roofs: '#111111' } };
    expect(effectiveDiagram(local, null)).toBe(local);
    expect(effectiveDiagram(local, { walls: '#222222' })).toEqual({ variant: 'pastel', palette: { walls: '#222222' } });
  });

  it('sends the palette shown with the change, a colour back at its default as null', () => {
    expect(palettePatch({ roofs: '#111111' }, { walls: '#ABCDEF' })).toEqual({ roofs: '#111111', walls: '#abcdef' });
    expect(palettePatch({ roofs: '#111111' }, { roofs: DEFAULT_PALETTE.roofs })).toEqual({ roofs: null });
    expect(palettePatch({}, { walls: null })).toEqual({ walls: null });
  });
});
