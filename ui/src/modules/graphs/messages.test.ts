import { describe, expect, it } from 'vitest';
import { bakeMessage, evaluationMessages, isMissingInput, pathLabel } from './messages';
import type { Evaluation } from './store';
import type { GraphJson } from './graph';

const graph: GraphJson = {
  format: 2, wires: [],
  nodes: [
    { id: 'a', kind: 'number', x: 0, y: 0 },
    { id: 'b', kind: 'range', x: 0, y: 0, label: 'My range' },
    { id: 'g', kind: 'group', x: 0, y: 0, label: 'Box', graph: { nodes: [{ id: 'w', kind: 'point', x: 0, y: 0 }], wires: [] } },
  ],
};
const label = (k: string) => ({ number: 'Number', range: 'Range', point: 'Point' }[k] ?? k);
const ghost = { parts: [], plan: [], points: [], lines: [] };
const evaluation = (nodes: Evaluation['nodes'], extra: Partial<Evaluation> = {}): Evaluation => ({ nodes, ghost, ms: 38.4, calls: 14, ...extra });

describe('the graph editor messages', () => {
  it('labels a node by its own label, else its kind, and a nested one by its path', () => {
    expect(pathLabel(graph, 'a', label)).toBe('Number');
    expect(pathLabel(graph, 'b', label)).toBe('My range');
    expect(pathLabel(graph, 'g/w', label)).toBe('Box / Point');
    expect(pathLabel(graph, 'zz', label)).toBe('zz');
  });

  it('makes a row per failed node, the cause an error and what only has nothing to work on a warning, then the timing', () => {
    const rows = evaluationMessages(evaluation({
      a: { outputs: {} },
      b: { outputs: {}, error: 'The input „end” has nothing: „Number” failed.' },
      g: { outputs: {}, error: 'Division by zero.' },
      'g/w': { outputs: {}, warning: '1 of 2 failed: x' },
    }), graph, label);
    expect(rows.map((r) => [r.kind, r.source, r.text])).toEqual([
      ['error', 'Box', 'Division by zero.'],
      ['warning', 'My range', 'The input „end” has nothing: „Number” failed.'],
      ['warning', 'Box / Point', '1 of 2 failed: x'],
      ['info', 'Preview', 'Commands: 14, 38 ms'],
    ]);
    expect(rows[0]!.target).toEqual({ node: 'g' });
    expect(rows[2]!.target).toEqual({ node: 'g' });
    expect(rows.every((r) => r.group === 'evaluation')).toBe(true);
  });

  it('says the limits reached: a cut ghost, the calls', () => {
    const rows = evaluationMessages(evaluation({}, { ghost: { ...ghost, cut: true }, calls: 2000 }), graph, label);
    expect(rows.filter((r) => r.kind === 'warning').map((r) => r.text)).toEqual([
      'The preview was cut: the ghost holds too many coordinates.', 'One evaluation reached its limit of command calls.']);
  });

  it('says nothing for no evaluation, and recognises the missing-input messages', () => {
    expect(evaluationMessages(null, graph, label)).toEqual([]);
    expect(isMissingInput('Not reached: the graph was stopped before it.')).toBe(true);
    expect(isMissingInput('Division by zero.')).toBe(false);
  });

  it('tells the bake: its elements and one undo step, or the refusal', () => {
    expect(bakeMessage(5, null)).toMatchObject({ kind: 'info', source: 'Bake', text: 'Elements made: 5, one undo step' });
    expect(bakeMessage(null, 'The graph was not baked: „Wall” failed: x')).toMatchObject({ kind: 'error', text: 'The graph was not baked: „Wall” failed: x' });
  });
});
