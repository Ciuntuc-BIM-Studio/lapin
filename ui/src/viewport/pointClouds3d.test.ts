import { describe, expect, it } from 'vitest';
import { chooseNodes, pointBudget, type NodeRow } from './pointClouds3d';
import { cloudSpecs, statusText, viewClouds } from './pointClouds';
import type { NodeDto } from '../engine/protocol';

/** A cube's octree two levels deep: the root (10 m), its eight children, the first child's eight. */
function tree(): NodeRow[] {
  const rows: NodeRow[] = [['r', 1000, 0, 0, 0, 10, 10, 10, 10 / 128]];
  for (let k = 0; k < 8; k++) {
    const [x, y, z] = [(k & 4) ? 5 : 0, (k & 2) ? 5 : 0, (k & 1) ? 5 : 0];
    rows.push([`r${k}`, 800, x, y, z, x + 5, y + 5, z + 5, 5 / 128]);
  }
  for (let k = 0; k < 8; k++) {
    const [x, y, z] = [(k & 4) ? 2.5 : 0, (k & 2) ? 2.5 : 0, (k & 1) ? 2.5 : 0];
    rows.push([`r0${k}`, 600, x, y, z, x + 2.5, y + 2.5, z + 2.5, 2.5 / 128]);
  }
  return rows;
}

const near = { kind: 'perspective' as const, eye: [-1, -1, 1] as const, fovDeg: 45, heightPx: 1000 };
const far = { kind: 'perspective' as const, eye: [-5000, -5000, 10] as const, fovDeg: 45, heightPx: 1000 };

describe('the nodes a frame draws', () => {
  it('takes the root alone from far away, where its sample is finer than a point', () => {
    expect(chooseNodes(tree(), far, () => true, 1e9, 2).names).toEqual(['r']);
  });

  it('refines near the eye, the nearest nodes first, within the budget', () => {
    const all = chooseNodes(tree(), near, () => true, 1e9, 2);
    expect(all.names[0]).toBe('r');
    expect(all.names).toContain('r00');
    // The budget stops it: the root, then the nearest children only.
    const some = chooseNodes(tree(), near, () => true, 1000 + 800 * 2, 2);
    expect(some.points).toBeLessThanOrEqual(2600);
    expect(some.names[1]).toBe('r0');
  });

  it('leaves out what is not in view, and its children with it', () => {
    const c = chooseNodes(tree(), near, (n) => !n[0].startsWith('r0'), 1e9, 2);
    expect(c.names.some((n) => n.startsWith('r0'))).toBe(false);
    expect(c.names).toContain('r7');
  });

  it('draws fewer points in a browser and at the fast quality', () => {
    expect(pointBudget('balanced', true)).toBeLessThan(pointBudget('balanced'));
    expect(pointBudget('fast')).toBeLessThan(pointBudget('precise'));
  });
});

const node = (id: string, type: string, props: Record<string, unknown>): NodeDto => ({ id, type, name: id, parentId: null, px: 0, py: 0, props, editable: true, module: null, storeyId: null } as unknown as NodeDto);

describe('how each cloud is drawn', () => {
  it('takes the link\'s settings, the view\'s rows over them', () => {
    const nodes = new Map<string, NodeDto>([
      ['c', node('c', 'LapinLink', { kind: 'pointcloud', key: 'k', colorMode: 'intensity', pointSize: 3, halftone: false, placement: { mode: 'origin' } })],
      ['i', node('i', 'LapinLink', { kind: 'ifc' })],
      ['v', node('v', 'LapinView', { graphics: { pointClouds: { c: { halftone: true, colorMode: 'classification', hiddenClasses: [2, 7] } } } })],
    ]);
    const [own] = cloudSpecs(nodes);
    expect(own).toMatchObject({ id: 'c', shown: true, halftone: false, colorMode: 'intensity', pointSize: 3, hiddenClasses: [] });
    const [inView] = cloudSpecs(nodes, viewClouds(nodes.get('v')));
    expect(inView).toMatchObject({ halftone: true, colorMode: 'classification', hiddenClasses: [2, 7] });
    // Hidden by its link everywhere, or by the view alone.
    nodes.set('c', node('c', 'LapinLink', { kind: 'pointcloud', visible: false }));
    expect(cloudSpecs(nodes)[0].shown).toBe(false);
    // The tiles are asked for again when its placement changes.
    const a = cloudSpecs(new Map([['c', node('c', 'LapinLink', { kind: 'pointcloud', placement: { mode: 'origin' } })]]))[0].rev;
    const b = cloudSpecs(new Map([['c', node('c', 'LapinLink', { kind: 'pointcloud', placement: { mode: 'center' } })]]))[0].rev;
    expect(a).not.toBe(b);
  });

  it('says how far the index is', () => {
    expect(statusText({ state: 'indexing', stage: 'Sorting', done: 0.5 })).toBe('Indexing: Sorting 50%');
    expect(statusText({ state: 'missing', error: 'The file is gone: x.' })).toBe('The file is gone: x.');
  });
});
