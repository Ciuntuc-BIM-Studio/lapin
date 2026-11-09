import { describe, expect, it } from 'vitest';
import type { RenderItem, RenderPart } from '../engine/protocol';
import { fineShown, keepFine, notAsked } from './fineBodies';

const item = (id: string, parts: RenderPart[] = []): RenderItem => ({ nodeId: id, type: 'IfcColumn', storeyId: null, editable: true, color: '#fff', positions: new Float32Array(), normals: new Float32Array(), indices: new Int32Array(), edges: new Float32Array(), plan: [], snaps: [], diagnostics: [], parts });
const part = (name: string): RenderPart => ({ name, color: '#fff', positions: new Float32Array(), normals: new Float32Array(), indices: new Int32Array(), edges: new Float32Array() });

describe('fine bodies at LOD 400', () => {
  it('draws the sharp body until the view is on and the fine one came, and keeps one object per item', () => {
    const a = item('a', [part('sharp')]);
    const render = new Map([['a', a]]);
    expect(fineShown(a, true)).toBe(a);
    keepFine(render, { lod: 400, items: [{ nodeId: 'a', parts: [part('fine')] }] }, [a]);
    expect(fineShown(a, false)).toBe(a);
    expect(fineShown(a, true).parts?.[0].name).toBe('fine');
    expect(fineShown(a, true)).toBe(fineShown(a, true));
  });

  it('asks once per item as built: a changed element is a new item, one without a fine body is not asked again', () => {
    const a = item('a'), b = item('b');
    const render = new Map([['a', a], ['b', b]]);
    expect(notAsked(render)).toEqual([a, b]);
    keepFine(render, { lod: 400, items: [] }, [a, b]);
    expect(notAsked(render)).toEqual([]);
    const a2 = item('a');
    render.set('a', a2);
    expect(notAsked(render)).toEqual([a2]);
    expect(fineShown(a2, true)).toBe(a2);
  });

  it('leaves instances of shared meshes alone', () => {
    const shared = { ...item('s'), instance: { mesh: 'm', matrix: new Float32Array(12) } };
    expect(notAsked(new Map([['s', shared]]))).toEqual([]);
  });
});
