import { describe, expect, it } from 'vitest';
import type { NodeDto, RenderItem, RenderPart } from '../engine/protocol';
import { everyLayerPatch, layers3dOf, layersChip, shownIn } from './layers3d';

const node = (id: string, type: string, props: Record<string, unknown> = {}): NodeDto =>
  ({ id, type, name: id, parentId: null, px: 0, py: 0, props: props as NodeDto['props'], editable: true, module: null, storeyId: null });

const model = (project: Record<string, unknown>, ...views: NodeDto[]) => new Map<string, NodeDto>([['p', node('p', 'IfcProject', project)], ...views.map((v) => [v.id, v] as [string, NodeDto])]);

const part = (name: string, hiddenBy?: RenderPart['hiddenBy']): RenderPart =>
  ({ name, color: '#ccc', positions: new Float32Array(), normals: new Float32Array(), indices: new Int32Array(), edges: new Float32Array(), hiddenBy });

describe('the layers a 3D view shows', () => {
  it('takes every layer by default, the cores alone for a Structural 3D view', () => {
    expect(layers3dOf(model({}), '3d')).toEqual({ mode: 'all', style: 'all', own: false });
    expect(layers3dOf(model({ view3d: { discipline: 'Structure' } }), null)).toEqual({ mode: 'core', style: 'core', own: false });
    expect(layers3dOf(model({ view3d: { discipline: 'structure' } }), '3d').mode).toBe('core');
    // A style named wins over the discipline's.
    expect(layers3dOf(model({ view3d: { discipline: 'Structure', viewStyle: 'mep' } }), '3d').mode).toBe('all');
  });

  it('lets a view set its own over its style, and reads a saved view from itself', () => {
    const saved = node('v', 'LapinView', { kind: '3d', discipline: 'Structure', styleOverrides: { layers: 'noFinishes' } });
    const nodes = model({ view3d: { styleOverrides: { layers: 'core' } } }, saved);
    expect(layers3dOf(nodes, '3d')).toEqual({ mode: 'core', style: 'all', own: true });
    expect(layers3dOf(nodes, 'v')).toEqual({ mode: 'noFinishes', style: 'core', own: true });
    // A mode the engine does not know reads as the style's.
    expect(layers3dOf(model({ view3d: { styleOverrides: { layers: 'half' } } }), '3d').mode).toBe('all');
  });

  it('reads the project\'s own styles', () => {
    const nodes = model({ viewStyles: [{ id: 'arch', name: 'Arch', discipline: 'Architecture', layers: 'noFinishes' }, { id: 'x', name: 'X', discipline: null, layers: 'core' }] });
    expect(layers3dOf(nodes, '3d').mode).toBe('noFinishes');
    expect(layers3dOf(model({ viewStyles: [{ id: 'arch', discipline: 'Architecture', layers: 'noFinishes' }], view3d: { discipline: 'Landscape' } }), '3d').mode).toBe('noFinishes');
  });

  it('says so on the chip, and goes back to every layer', () => {
    expect(layersChip({ mode: 'all', style: 'all', own: false })).toBeNull();
    expect(layersChip({ mode: 'core', style: 'core', own: false })).toBe('the core only');
    expect(layersChip({ mode: 'noFinishes', style: 'all', own: true })).toBe('without finishes');
    // Its style shows every layer: its own choice goes; else every layer becomes its own.
    expect(everyLayerPatch({ mode: 'noFinishes', style: 'all', own: true })).toEqual({ layers: null });
    expect(everyLayerPatch({ mode: 'core', style: 'core', own: false })).toEqual({ layers: 'all' });
  });

  it('leaves out the parts the mode hides, the same item when none go', () => {
    const item = { nodeId: 'w', type: 'IfcWall', parts: [part('Plaster', ['noFinishes', 'core']), part('Brick'), part('Wool', ['core'])] } as unknown as RenderItem;
    expect(shownIn(item, 'all')).toBe(item);
    expect(shownIn(item, 'noFinishes').parts!.map((p) => p.name)).toEqual(['Brick', 'Wool']);
    expect(shownIn(item, 'core').parts!.map((p) => p.name)).toEqual(['Brick']);
    // Kept per item and mode, so the 3D rebuilds nothing until the item or the mode changes.
    expect(shownIn(item, 'core')).toBe(shownIn(item, 'core'));
    const plain = { nodeId: 'c', type: 'IfcColumn', parts: [part('Concrete')] } as unknown as RenderItem;
    expect(shownIn(plain, 'core')).toBe(plain);
  });
});
