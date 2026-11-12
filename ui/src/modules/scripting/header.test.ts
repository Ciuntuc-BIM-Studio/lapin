import { describe, expect, it } from 'vitest';
import { missingRequirements, parseHeader, parsePairs } from './header';

describe('a script\'s plug-in header (decision 489, stage 4)', () => {
  it('reads its # lapin: lines among the first comments, quoted or bare', () => {
    const code = [
      '#!/usr/bin/env python3',
      '# lapin: name="Perimeter walls", version="1.1.0", requires="scripting, lapin>=0.1.900"',
      '# SPDX-License-Identifier: MIT',
      '',
      '# lapin: tool="Perimeter walls", icon=square, tooltip="Four walls, \\"one\\" undo step", run=perimeter_tool',
      '"""Docstring."""',
      '# lapin: name="Not this one"',
    ].join('\n');
    const h = parseHeader(code)!;
    expect(h.name).toBe('Perimeter walls');
    expect(h.version).toBe('1.1.0');
    expect(h.requires).toEqual(['scripting', 'lapin>=0.1.900']);
    expect(h.tool).toBe('Perimeter walls');
    expect(h.icon).toBe('square');
    expect(h.tooltip).toBe('Four walls, "one" undo step');
    expect(h.run).toBe('perimeter_tool');
  });

  it('takes the stage-3 header of the extensions\' example as it is, with no tool', () => {
    const h = parseHeader('# lapin: name="Perimeter walls", version="1.0.0", requires="scripting"\n# SPDX-License-Identifier: MIT\n')!;
    expect(h).toMatchObject({ name: 'Perimeter walls', version: '1.0.0', requires: ['scripting'] });
    expect(h.tool).toBeUndefined();
  });

  it('names a tool=yes after the script, ignores tool=no and a run that is not a Python name', () => {
    expect(parseHeader('# lapin: name="Walls", tool=yes')!.tool).toBe('Walls');
    expect(parseHeader('# lapin: tool=true', 'Room walls.py')!.tool).toBe('Room walls');
    expect(parseHeader('# lapin: name=x, tool=no')!.tool).toBeUndefined();
    expect(parseHeader('# lapin: tool=x, run="os.system(\'rm\')"')!.run).toBeUndefined();
    expect(parseHeader('# lapin: requires=lapin>=0.1.5')!.requires).toEqual(['lapin>=0.1.5']);
  });

  it('has none when the code starts before any # lapin: line', () => {
    expect(parseHeader('import lapinbim as lapin\n# lapin: tool="Late"\n')).toBeNull();
    expect(parseHeader('print(1)')).toBeNull();
    expect(parseHeader('')).toBeNull();
  });

  it('splits a line into its pairs, commas inside quotes kept', () => {
    expect(parsePairs(' a="x, y", B=2 ,c=')).toEqual([['a', 'x, y'], ['b', '2'], ['c', '']]);
  });

  it('says what this Lapin lacks of what a script requires', () => {
    const facts = { version: '0.1.950', hasModule: (m: string) => m === 'scripting' || m === 'architecture' };
    expect(missingRequirements(['scripting', 'lapin>=0.1.900', 'architecture'], facts)).toEqual([]);
    expect(missingRequirements(['lapin>=0.1.999', 'urban'], facts)).toEqual(['Lapin 0.1.999 or newer', 'the urban module']);
  });
});
