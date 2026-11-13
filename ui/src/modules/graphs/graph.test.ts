import { describe, expect, it } from 'vitest';
import {
  numberText, rowValue, valueEnd,
  addNode, argLabel, argPorts, connect, emptyGraph, fits, fitView, graphOf, moveNodes, nodeHeight, nodeSpecs, nodesIn, portAt, removeNodes, removeWire,
  searchNodes, setLabel, setValue, showValue, wireKey, wouldCycle, zoomAt, HEAD_H, NODE_W, ROW_H, type EngineNodes, type GraphJson, type NodeSpec,
} from './graph';

const engineNodes: EngineNodes = {
  nodes: [
    { kind: 'number', label: 'Number', group: 'Inputs', inputs: [{ name: 'value', label: 'Value', type: 'number', setting: true, default: 0 }], outputs: [{ name: 'value', label: 'Value', type: 'number' }] },
    { kind: 'range', label: 'Range', group: 'Lists', inputs: [{ name: 'start', label: 'Start', type: 'number' }, { name: 'end', label: 'End', type: 'number' }, { name: 'step', label: 'Step', type: 'number' }], outputs: [{ name: 'list', label: 'Numbers', type: 'number' }] },
    { kind: 'point', label: 'Point', group: 'Geometry', inputs: [{ name: 'x', label: 'X', type: 'number' }, { name: 'y', label: 'Y', type: 'number' }, { name: 'z', label: 'Z', type: 'number' }], outputs: [{ name: 'point', label: 'Point', type: 'point' }] },
    { kind: 'join', label: 'Join texts', group: 'Text', inputs: [{ name: 'a', label: 'A', type: 'text' }], outputs: [{ name: 'text', label: 'Text', type: 'text' }] },
  ],
  commands: [{ name: 'architecture.addWall', label: 'Wall', module: 'architecture' }, { name: 'architecture.addSlab', label: 'Floor', module: 'architecture' }],
  queries: ['schedule.rows'],
};

const catalogue = {
  commands: [
    { name: 'architecture.addWall', module: 'architecture', label: 'Wall', args: [{ name: 'storeyId', type: 'str' }, { name: 'height', type: 'float' }, { name: 'x1', type: 'float' }, { name: 'y1', type: 'float' }, { name: 'x2', type: 'float' }, { name: 'y2', type: 'float' }] },
    { name: 'architecture.addSlab', module: 'architecture', label: 'Floor', args: [{ name: 'points', type: 'list' }] },
  ],
  queries: [{ name: 'schedule.rows', module: 'schedule', args: [{ name: 'id', type: 'str' }] }],
};

const specs = nodeSpecs(engineNodes, catalogue);
const spec = (kind: string) => specs.get(kind) as NodeSpec;

function sample(): GraphJson {
  let g = emptyGraph();
  g = addNode(g, spec('range'), 0, 0).graph;
  g = addNode(g, spec('point'), 220, 0).graph;
  g = addNode(g, spec('command:architecture.addWall'), 440, 0).graph;
  return g;
}

describe('the nodes a graph may hold', () => {
  it('takes the engine\'s own nodes, and the commands and queries with their arguments as ports', () => {
    expect(spec('range').group).toBe('Lists');
    const wall = spec('command:architecture.addWall');
    expect(wall.label).toBe('Wall');
    expect(wall.group).toBe('Architecture');
    // x1, y1 / x2, y2: two point ports, Start and End, instead of four numbers.
    expect(wall.inputs.map((p) => [p.name, p.label, p.type])).toEqual([['storeyId', 'Storey', 'storey'], ['height', 'Height', 'number'], ['pt:1', 'Start', 'point'], ['pt:2', 'End', 'point']]);
    expect(wall.outputs.map((p) => p.name)).toEqual(['ids', 'result']);
    expect(spec('command:architecture.addSlab').inputs[0]).toMatchObject({ name: 'points', type: 'list', whole: true });
    expect(spec('query:schedule.rows').outputs[0]!.name).toBe('result');
  });

  it('names arguments in words', () => {
    expect(argLabel('baseOffset')).toBe('Base offset');
    expect(argLabel('typeIds')).toBe('Types');
    expect(argPorts([{ name: 'x', type: 'float' }, { name: 'y', type: 'float' }])).toEqual([{ name: 'pt:', label: 'Point', type: 'point' }]);
  });

  it('are found by their name, group or engine name, the name starting so first', () => {
    expect(searchNodes(specs.values(), 'wall')[0]!.kind).toBe('command:architecture.addWall');
    expect(searchNodes(specs.values(), 'po')[0]!.kind).toBe('point');
    expect(searchNodes(specs.values(), 'addslab').map((s) => s.kind)).toEqual(['command:architecture.addSlab']);
    expect(searchNodes(specs.values(), 'nothing like it')).toEqual([]);
    // Nothing typed: the module's own nodes, not the hundreds of commands.
    expect(searchNodes(specs.values(), '').every((s) => !s.kind.includes(':'))).toBe(true);
  });
});

describe('editing a graph', () => {
  it('adds nodes with fresh ids, and a catalogue node notes the inputs that take a list whole', () => {
    const g = addNode(addNode(emptyGraph(), spec('range'), 10.4, 20.6).graph, spec('command:architecture.addSlab'), 0, 0);
    expect(g.graph.nodes.map((n) => n.id)).toEqual(['n1', 'n2']);
    expect(g.graph.nodes[0]).toMatchObject({ x: 10, y: 21 });
    expect(g.graph.nodes[1]!.whole).toEqual(['points']);
  });

  it('wires an output to an input, one wire per input, refusing a circle and kinds that do not meet', () => {
    let g = sample();
    const r1 = connect(g, spec, { from: 'n1', out: 'list', to: 'n2', in: 'x' });
    expect('graph' in r1).toBe(true);
    g = (r1 as { graph: GraphJson }).graph;
    const r2 = connect(g, spec, { from: 'n1', out: 'list', to: 'n2', in: 'x' });
    expect((r2 as { graph: GraphJson }).graph.wires).toHaveLength(1);
    g = (connect(g, spec, { from: 'n2', out: 'point', to: 'n3', in: 'pt:1' }) as { graph: GraphJson }).graph;
    expect(wouldCycle(g, 'n3', 'n1')).toBe(true);
    expect(connect(g, spec, { from: 'n2', out: 'point', to: 'n1', in: 'start' })).toEqual({ refused: 'A node cannot feed itself, even through others.' });
    const other = addNode(g, spec('point'), 0, 200).graph;
    expect(connect(other, spec, { from: 'n4', out: 'point', to: 'n1', in: 'start' })).toEqual({ refused: 'A point does not go into a number.' });
    // A point into a range's input: refused by kind; a number into the wall's height is fine.
    expect(fits('point', 'number')).toBe(false);
    expect(fits('number', 'integer')).toBe(true);
    expect(fits('element', 'text')).toBe(true);
    expect(fits('any', 'point')).toBe(true);
  });

  it('moves, removes, labels and sets constants', () => {
    let g = (connect(sample(), spec, { from: 'n1', out: 'list', to: 'n2', in: 'x' }) as { graph: GraphJson }).graph;
    g = moveNodes(g, new Set(['n1', 'n2']), 5, -5);
    expect(g.nodes.map((n) => [n.x, n.y])).toEqual([[5, -5], [225, -5], [440, 0]]);
    g = setValue(g, 'n2', 'y', 4);
    expect(g.nodes[1]!.values).toEqual({ y: 4 });
    g = setValue(g, 'n2', 'y', '');
    expect(g.nodes[1]!.values).toBeUndefined();
    g = setLabel(g, 'n3', '  Facade  ');
    expect(g.nodes[2]!.label).toBe('Facade');
    expect(removeWire(g, wireKey(g.wires[0]!)).wires).toEqual([]);
    const r = removeNodes(g, new Set(['n2']));
    expect(r.nodes.map((n) => n.id)).toEqual(['n1', 'n3']);
    expect(r.wires).toEqual([]);
  });

  it('reads a graph from an entity\'s props, an unreadable one as empty', () => {
    expect(graphOf(undefined)).toEqual(emptyGraph());
    expect(graphOf({ graph: { nodes: [{ id: 'n1', kind: 'range', x: 0, y: 0 }] } }).wires).toEqual([]);
  });
});

describe('drawing a graph', () => {
  it('puts the sockets down the sides, settings in the inspector', () => {
    const g = sample();
    expect(portAt(g.nodes[1]!, spec('point'), 'in', 'y')).toEqual({ x: 220, y: HEAD_H + ROW_H * 1.5 });
    expect(portAt(g.nodes[1]!, spec('point'), 'out', 'point')).toEqual({ x: 220 + NODE_W, y: HEAD_H + ROW_H / 2 });
    expect(portAt(g.nodes[1]!, spec('point'), 'in', 'nope')).toBeNull();
    expect(nodeHeight(spec('number'))).toBe(HEAD_H + ROW_H + 6);
    expect(nodesIn(g, spec, { x: -10, y: -10 }, { x: 250, y: 10 })).toEqual(['n1', 'n2']);
  });

  it('zooms about the pointer, within its bounds', () => {
    const v = zoomAt({ x: 0, y: 0, zoom: 1 }, 100, 50, 2);
    expect(v).toEqual({ x: 50, y: 25, zoom: 2 });
    expect(zoomAt(v, 0, 0, 100).zoom).toBe(2.5);
  });

  it('shows values in a few words', () => {
    expect(showValue(2.34567)).toBe('2.346');
    expect(showValue({ $pt: [1, 2, 0] })).toBe('(1, 2, 0)');
    expect(showValue([1, 2, 3, 4, 5, { $more: 10 }])).toBe('15 × [1, 2, 3, 4, …]');
    expect(showValue(true)).toBe('yes');
    expect(showValue(null)).toBe('—');
  });
});

describe('opening a graph', () => {
  it('fits every node in sight, never zoomed in past 1×', () => {
    const g = sample();
    const v = fitView(g, spec, 400, 300);
    expect(v.zoom).toBeLessThan(1);
    // The leftmost node's left edge and the rightmost's right edge are inside the canvas.
    expect((0 - v.x) * v.zoom).toBeGreaterThanOrEqual(0);
    expect((440 + NODE_W - v.x) * v.zoom).toBeLessThanOrEqual(400.001);
    expect(fitView(emptyGraph(), spec, 400, 300)).toEqual({ x: -40, y: -40, zoom: 1 });
    expect(fitView({ ...g, nodes: [g.nodes[0]!] }, spec, 2000, 2000).zoom).toBe(1);
  });
});

describe('what the nodes and the inspector show', () => {
  it('takes an untyped ids or points argument as a list, whole, as the engine does', () => {
    const ports = argPorts([{ name: 'ids' }, { name: 'points' }, { name: 'storeyIds' }, { name: 'id', type: 'str' }, { name: 'idsx' }]);
    expect(ports.map((p) => [p.name, p.type, p.whole ?? false])).toEqual([['ids', 'list', true], ['points', 'list', true], ['storeyIds', 'list', true], ['id', 'text', false], ['idsx', 'any', false]]);
  });

  it("shows a storey by its name, picked elements by their count, and cuts a value to the room its label leaves", () => {
    const names = (id: string) => (id === 's1' ? 'Ground floor' : undefined);
    expect(rowValue('s1', 'storey', 'Storey', names)).toBe('Ground floor');
    expect(rowValue('', 'storey', 'Storey', names)).toBe('');
    expect(rowValue('1ylyWM9x8CnC$zlUb', 'storey', 'Storey', names)).toBe('?');
    expect(rowValue(['a', 'b', 'c'], 'element', 'Elements', names)).toBe('3');
    expect(rowValue(2.8, 'number', 'Height', names)).toBe('2.8');
    const long = rowValue('a very long text that does not fit the node at all', 'text', 'Name', names);
    expect(long.endsWith('…')).toBe(true);
    expect(long.length).toBeLessThan(30);
    expect(rowValue('x', 'text', 'A label far too long for the row', names)).toBe('');
    // Beside an output's label, only the room before it: the storey's name cut, never over Elements made.
    const beside = rowValue('s1', 'storey', 'Storey', names, valueEnd('Elements made'));
    expect(beside.length).toBeLessThan('Ground floor'.length);
    expect(valueEnd('Elements made')).toBeLessThan(NODE_W - 10 - 'Elements made'.length * 6 + 1);
  });

  it('writes numbers with a decimal point whatever the language, as the nodes show them', () => {
    expect(numberText(2.8)).toBe('2.8');
    expect(numberText(0.1 + 0.2)).toBe('0.3');
    expect(numberText(undefined)).toBe('');
    expect(showValue(2.8)).toBe(numberText(2.8));
  });
});
