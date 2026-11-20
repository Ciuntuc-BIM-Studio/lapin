/** Ctrl+D drags, Ctrl+Shift+D drags a copy, Ctrl+E rotates, Ctrl+Shift+E rotates a copy (forum, issue 86): free in both presets. */

import { describe, expect, it } from 'vitest';
import { MODIFY_CHORDS, commands } from './commands';
import { conflicts, effectiveBindings, presetBindings, withPreset, type CommandDef } from './shortcuts';
import { COMMANDS as CAD } from '../viewport/cad/session';

/** What the ribbon's tools add (commands.ts reads their chords the same way). */
const tool = (id: string): CommandDef => ({ id, label: id, category: 'Modify', defaults: [], chords: MODIFY_CHORDS[id], run: () => undefined });
const ALL = [...commands(), tool('modify.move'), tool('modify.copy'), tool('modify.rotate')];

describe('the four modify chords', () => {
  for (const preset of ['classic', 'revit'] as const) {
    it(`are bound once and clash with nothing in the ${preset} preset`, () => {
      const b = effectiveBindings(ALL, withPreset(preset));
      expect(b['modify.move']).toContain('Ctrl+D');
      expect(b['modify.copy']).toContain('Ctrl+Shift+D');
      expect(b['modify.rotate']).toContain('Ctrl+E');
      expect(b['app.rotateCopy']).toEqual(['Ctrl+Shift+E']);
      const clash = conflicts(b);
      for (const chord of ['Ctrl+D', 'Ctrl+Shift+D', 'Ctrl+E', 'Ctrl+Shift+E']) expect(clash[chord], chord).toBeUndefined();
    });
  }

  it('leave the letters of the presets as they were', () => {
    expect(presetBindings(ALL, 'revit')['modify.move']).toEqual(['MV', 'Ctrl+D']);
    expect(presetBindings(ALL, 'revit')['modify.rotate']).toEqual(['RO', 'Ctrl+E']);
    expect(presetBindings(ALL, 'classic')['modify.copy']).toEqual(['Ctrl+Shift+D']);
  });

  it('Rotate a copy is a command line command that starts with Copy on', () => {
    expect(CAD.some((c) => c.names.includes('ROTATECOPY'))).toBe(true);
  });
});
