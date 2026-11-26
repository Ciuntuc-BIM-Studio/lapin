import { describe, expect, it } from 'vitest';
import { formatRepairPrompt, validateLapinCode } from './validate';
import type { Catalogue } from './logic';

const CAT: Catalogue = {
  commands: [
    {
      name: 'architecture.addWall',
      module: 'architecture',
      args: [
        { name: 'storeyId', type: 'str' },
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
      args: [{ name: 'storeyId', type: 'str' }, { name: 'x', type: 'float' }, { name: 'y', type: 'float' }],
    },
  ],
  queries: [],
};

describe('static pre-flight lapinbim validator', () => {
  it('accepts valid lapin calls and ignores built-in namespaces like lapin.model / lapin.ui', () => {
    const code = [
      's = lapin.model.storeys[0].id',
      'lapin.architecture.add_wall(storey_id=s, x1=0, y1=0, x2=5, y2=0, height=3.0)',
      'lapin.ui.ask("Title", fields={"h": 3})',
    ].join('\n');
    expect(validateLapinCode(code, CAT)).toEqual([]);
  });

  it('catches hallucinated commands and suggests the closest real command', () => {
    const code = 'lapin.architecture.create_wall(x1=0, y1=0)\n';
    const issues = validateLapinCode(code, CAT);
    expect(issues).toHaveLength(1);
    expect(issues[0].kind).toBe('unknown_command');
    expect(issues[0].suggestion).toContain('lapin.architecture.add_wall');
  });

  it('catches wrong keyword arguments and suggests the real snake_case parameter', () => {
    const code = 'lapin.structure.add_column(storey=s, x=1, y=2)\n';
    const issues = validateLapinCode(code, CAT);
    expect(issues).toHaveLength(1);
    expect(issues[0].kind).toBe('unknown_arg');
    expect(issues[0].suggestion).toContain('storey_id');

    const prompt = formatRepairPrompt(code, issues);
    expect(prompt).toContain('storey_id');
  });
});
