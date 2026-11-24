import { beforeEach, describe, expect, it } from 'vitest';
import { begin, callLabel, commandLabel, humanize, REPEAT_AFTER_MS, resetRunning, setCommandLabels, SlowCalls } from './running';

beforeEach(() => { resetRunning(); setCommandLabels([]); });

describe('what the engine is working on, by name (app report #187)', () => {
  it('names a command by the catalogue\'s label, else by its name made readable', () => {
    expect(humanize('wall.moveVertex')).toBe('Move vertex');
    expect(humanize('project.importIfc')).toBe('Import ifc');
    expect(commandLabel('ifc.import')).toBe('Import');
    setCommandLabels([{ name: 'ifc.import', label: 'Import IFC' }, { name: 'x.y', label: null }]);
    expect(commandLabel('ifc.import')).toBe('Import IFC');
    expect(commandLabel('x.y')).toBe('Y');
    expect(callLabel('doc.execute', { command: 'ifc.import' })).toBe('Import IFC');
    expect(callLabel('doc.undo', {})).toBe('Undo');
    expect(callLabel('doc.query', { name: 'plan.drawing' })).toBe('Drawing');
  });

  it('names the newest slow edit still running, and clears when the last lands', () => {
    const slow = new SlowCalls();
    expect(slow.change('Import IFC', true)).toBe('Still working on: Import IFC…');
    expect(slow.change('Move', true)).toBe('Still working on: Move…');
    expect(slow.change('Move', false)).toBe('Still working on: Import IFC…');
    expect(slow.change('Import IFC', false)).toBeNull();
  });

  it('sends a quick second press, but not the same edit again once the first has run a while', () => {
    const args = { ids: ['a'], dx: 1 };
    const first = begin('element.move', args, 0);
    expect(first).not.toBeNull();
    const quick = begin('element.move', { ids: ['a'], dx: 1 }, 500);
    expect(quick).not.toBeNull();
    expect(begin('element.move', args, REPEAT_AFTER_MS)).toBeNull();
    // Another edit, or the same command with other arguments, goes.
    expect(begin('element.move', { ids: ['b'], dx: 1 }, REPEAT_AFTER_MS)).not.toBeNull();
    first!(); quick!();
    // Once it landed, the same edit goes again.
    expect(begin('element.move', args, 10 * REPEAT_AFTER_MS)).not.toBeNull();
  });
});
