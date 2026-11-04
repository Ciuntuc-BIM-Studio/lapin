import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import type { GripStep } from './module';
import { hitsLabel, offsetOf, partGrips, reachOf, turnOf, voidLabelGrips, type PartDto } from './labelParts';

// A marker on a wall running along +y (u), its exterior towards −x (v), at 1:100 (a paper mm is 0.1 m).
const text: PartDto = { key: 'o0', kind: 'text', at: [1, 2], base: [1, 2], u: [0, 1], v: [-1, 0], mm: 0.1, angle: 90, from: 90, rotAt: [1, 2.5] };
const end: PartDto = { key: 'b', kind: 'end', at: [-0.5, 2], base: [-0.5, 2], dir: [-1, 0], mm: 0.1 };
const edit = (args: Record<string, unknown>): GripStep => ({ command: 'drafting.updateTag', args: { id: 't', ...args } });

describe('a label part dragged', () => {
  it('turns a text\'s drag into its offset in paper mm along and across its wall', () => {
    expect(offsetOf(text, { x: 0.8, y: 2.3 })).toEqual([3, 2]);
    expect(offsetOf(text, { x: 1, y: 2 })).toEqual([0, 0]);
  });

  it('turns a turning grip\'s drag into whole degrees from the way it reads by default', () => {
    // Straight up from the text: 90°, the default: no turn; towards −x: +90°; towards −y, past the half turn: −180 + …
    expect(turnOf(text, { x: 1, y: 3 })).toBe(0);
    expect(turnOf(text, { x: 0, y: 2 })).toBe(90);
    expect(turnOf(text, { x: 2, y: 2 })).toBe(-90);
    expect(turnOf(text, { x: 1.5, y: 2.5 })).toBe(-45);
  });

  it('turns a line end\'s drag into how far further it reaches, the part across its line ignored', () => {
    expect(reachOf(end, { x: -0.9, y: 2.7 })).toBe(4);
    expect(reachOf(end, { x: -0.3, y: 2 })).toBe(-2);
  });
});

describe('a label\'s grips', () => {
  it('are a move and a turn for each text, a reach for each end, each one command', () => {
    const grips = partGrips([text, end], edit);
    expect(grips.map((g) => [g.key, g.kind])).toEqual([['o0', 'centre'], ['o0:turn', 'rotate'], ['b', 'end']]);
    expect(grips[0].step!({ x: 0.8, y: 2.3 })).toEqual({ command: 'drafting.updateTag', args: { id: 't', part: 'o0', offset: [3, 2] } });
    expect(grips[1].step!({ x: 0, y: 2 })).toEqual({ command: 'drafting.updateTag', args: { id: 't', part: 'o0', rot: 90 } });
    expect(grips[2].step!({ x: -0.9, y: 2 })).toEqual({ command: 'drafting.updateTag', args: { id: 't', part: 'b', ext: 4 } });
  });

  it('put a part back on Ctrl/⌘-click only when it was edited', () => {
    expect(partGrips([text, end], edit).map((g) => g.remove!())).toEqual([null, null, null]);
    const edited = partGrips([{ ...text, moved: true, turned: true }, { ...end, moved: true }], edit).map((g) => g.remove!());
    expect(edited.map((s) => s?.args)).toEqual([{ id: 't', part: 'o0', offset: null }, { id: 't', part: 'o0', rot: null }, { id: 't', part: 'b', ext: null }]);
  });

  it('leave out what is not a part', () => {
    expect(partGrips([{ key: 'x' } as unknown as PartDto, { ...text, mm: 0 }], edit)).toEqual([]);
  });

  it('of a void label are its opening\'s, in the view that draws it, kept by the void\'s key', () => {
    const node = { id: 'op1', type: 'IfcOpeningElement', props: {} } as unknown as NodeDto;
    const label: PartDto = { ...text, key: 'label' };
    const marks = [
      { id: 'op1', segments: [], data: { void: 'V1', key: 'op1', view: 'plan1', label } },
      { id: 'op2', segments: [], data: { void: 'V2', key: 'op2', view: 'plan1', label } },
    ];
    const grips = voidLabelGrips(node, { marks });
    expect(grips.map((g) => [g.key, g.view])).toEqual([['void:op1:label', 'plan1'], ['void:op1:label:turn', 'plan1']]);
    expect(grips[0].step!({ x: 0.8, y: 2.3 })).toEqual({ command: 'view.voidLabel', args: { viewId: 'plan1', key: 'op1', offset: [3, 2] } });
    expect(voidLabelGrips(node, { marks: [] })).toEqual([]);
  });
});

describe('a label\'s texts', () => {
  it('pick their mark where they cover the plan, turned with them', () => {
    // A text 1 m long and 0.3 m high about (5, 5), turned 90°: it stands upright.
    const data = { label: { box: [5, 5, 0.5, 0.15, 90] }, parts: [{ box: [0, 0, 0.2, 0.1, 0] }] };
    expect(hitsLabel({ x: 5, y: 5.4 }, data)).toBe(true);
    expect(hitsLabel({ x: 5.4, y: 5 }, data)).toBe(false);
    expect(hitsLabel({ x: 0.15, y: 0.05 }, data)).toBe(true);
    expect(hitsLabel({ x: 0.25, y: 0 }, data, 0.06)).toBe(true);
    expect(hitsLabel({ x: 1, y: 1 }, undefined)).toBe(false);
  });
});
