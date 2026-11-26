import { describe, expect, it } from 'vitest';
import { pythonToGraph } from './toGraph';
import type { Catalogue } from './logic';

const CAT: Catalogue = {
  commands: [
    {
      name: 'architecture.addWall',
      module: 'architecture',
      args: [
        { name: 'x1', type: 'float' },
        { name: 'y1', type: 'float' },
        { name: 'x2', type: 'float' },
        { name: 'y2', type: 'float' },
        { name: 'height', type: 'float' },
      ],
    },
    {
      name: 'structure.addColumn',
      module: 'structure',
      args: [
        { name: 'x', type: 'float' },
        { name: 'y', type: 'float' },
        { name: 'height', type: 'float' },
      ],
    },
  ],
  queries: [],
};

describe('pythonToGraph deterministic converter', () => {
  it('converts parametric variables, range loop, formula, and lapin commands into a valid GraphJson v2', () => {
    const code = [
      'count = 5',
      'spacing = 4.0',
      'h = 3.2',
      'for i in range(count):',
      '    x = i * spacing',
      '    lapin.structure.add_column(x=x, y=0, height=h)',
    ].join('\n');

    const res = pythonToGraph(code, CAT);
    expect(res.unsupported).toEqual([]);
    expect(res.convertedCommands).toBe(1);
    expect(res.graph.format).toBe(2);

    const kinds = res.graph.nodes.map((n) => n.kind);
    expect(kinds).toContain('integer');
    expect(kinds).toContain('number');
    expect(kinds).toContain('range');
    expect(kinds).toContain('formula');
    expect(kinds).toContain('command:structure.addColumn');

    const cmd = res.graph.nodes.find((n) => n.kind === 'command:structure.addColumn')!;
    // x and height are wired, y is a literal 0
    expect(cmd.values?.y).toBe(0);
    expect(res.graph.wires.some((w) => w.to === cmd.id && w.in === 'x')).toBe(true);
    expect(res.graph.wires.some((w) => w.to === cmd.id && w.in === 'height')).toBe(true);
    // Topological layout places command to the right of its inputs
    const inputs = res.graph.nodes.filter((n) => n.kind === 'integer' || n.kind === 'number');
    for (const inp of inputs) expect(cmd.x).toBeGreaterThan(inp.x);
  });

  it('reports unsupported complex Python constructs by line number', () => {
    const code = 'h = 3.0\nwhile h > 0:\n    h -= 1\n';
    const res = pythonToGraph(code, CAT);
    expect(res.unsupported.length).toBeGreaterThan(0);
    expect(res.unsupported[0].line).toBe(2);
  });
});
