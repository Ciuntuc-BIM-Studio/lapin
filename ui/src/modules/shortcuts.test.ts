/**
 * The letters typed in a view: no two commands share a shortcut in either
 * preset (the first one would always win and the other never run), and no
 * shortcut is a command-line alias of another command (SC would start one
 * thing with Enter and another after a pause). Every module of this edition
 * and the command line's commands are read as the app reads them.
 */

import { describe, expect, it } from 'vitest';
import { commands } from '../app/commands';
import { isSequence, presetBindings, PRESETS, type PresetId } from '../app/shortcuts';
import { COMMANDS } from '../viewport/cad/session';

describe('the keyboard shortcuts', () => {
  const cmds = commands();
  const same = new Map(cmds.map((c) => [c.id, c.same]));
  const cli = COMMANDS.flatMap((c) => c.names.map((name) => ({ name, command: c.names[0] })));

  for (const preset of Object.keys(PRESETS) as PresetId[]) {
    const bindings = presetBindings(cmds, preset);
    const sequences = new Map<string, string[]>();
    for (const [id, list] of Object.entries(bindings)) for (const b of list) if (isSequence(b)) sequences.set(b, [...(sequences.get(b) ?? []), id]);

    it(`no two commands share letters (${preset})`, () => {
      const shared = [...sequences].filter(([, ids]) => ids.length > 1).map(([b, ids]) => `${b}: ${ids.join(', ')}`);
      expect(shared).toEqual([]);
    });

    it(`no letters are another command's command-line alias (${preset})`, () => {
      const clash = [...sequences].flatMap(([b, ids]) => ids.filter((id) => cli.some((n) => n.name === b && n.command !== same.get(id))).map((id) => `${b}: ${id} vs ${cli.find((n) => n.name === b)!.command}`));
      expect(clash).toEqual([]);
    });
  }
});
