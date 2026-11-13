import { describe, expect, it } from 'vitest';
import { complete, foldOutput, namespaces, remember, scriptName, snake, stepName, validScriptName, walk, type Catalogue, type ConsoleLine } from './logic';

describe('the Script panel’s console (decision 489)', () => {
  it('joins a print’s pieces into its line and keeps runs apart', () => {
    let lines: ConsoleLine[] = [];
    lines = foldOutput(lines, { type: 'out', id: 1, text: 'hello' });
    lines = foldOutput(lines, { type: 'out', id: 1, text: '\n' });
    lines = foldOutput(lines, { type: 'out', id: 1, text: 'two\n' });
    lines = foldOutput(lines, { type: 'err', id: 1, text: 'warn\n' });
    lines = foldOutput(lines, { type: 'out', id: 2, text: 'x' });
    expect(lines.map((l) => [l.kind, l.text])).toEqual([['out', 'hello\n'], ['out', 'two\n'], ['err', 'warn\n'], ['out', 'x']]);
  });

  it('shows Lapin’s note that a watching project script was not run again (decision 541)', () => {
    const text = '„truss.py” was not run again: it is not trusted on this computer. Run it once from the Script panel and choose Trust.';
    expect(foldOutput([], { type: 'note', level: 'err', text })).toEqual([{ kind: 'err', text, run: undefined, own: true }]);
    expect(foldOutput([], { type: 'note', text: 'x' })[0].kind).toBe('info');
    expect(foldOutput([], { type: 'note' })).toEqual([]);
  });

  it('says how a run ended: one undo step, nothing changed, its error with nothing kept, stopped', () => {
    // Lapin's own words are marked so (translated as the interface is); a traceback is the script's, shown as it is.
    expect(foldOutput([], { type: 'done', id: 1, ok: true, kept: true, seconds: 0.123 })).toEqual([{ kind: 'ok', text: 'Done in 0.12 s: one undo step.', run: 1, own: true }]);
    expect(foldOutput([], { type: 'done', id: 1, ok: true, kept: false, seconds: 12.4 })[0].text).toBe('Done in 12 s.');
    // A console line that ended well says nothing more.
    expect(foldOutput([], { type: 'done', id: 1, ok: true, console: true })).toEqual([]);
    const failed = foldOutput([], { type: 'done', id: 3, ok: false, error: 'ZeroDivisionError: division by zero', traceback: 'Traceback…\nZeroDivisionError: division by zero\n', line: 2 });
    expect(failed).toEqual([
      { kind: 'err', text: 'Traceback…\nZeroDivisionError: division by zero', run: 3 },
      { kind: 'err', text: 'Nothing the script did is kept.', run: 3, own: true },
    ]);
    expect(foldOutput([], { type: 'done', id: 4, ok: false, interrupted: true, error: 'Stopped.' }).map((l) => [l.text, l.own])).toEqual([['Stopped.', true], ['Nothing the script did is kept.', true]]);
    expect(foldOutput([], { type: 'done', id: 5, ok: false, killed: true, error: 'ended' })).toEqual([{ kind: 'err', text: 'ended', run: 5, own: true }]);
    expect(foldOutput([], { type: 'exit', code: 0 })[0]).toMatchObject({ kind: 'info', own: true });
    // A script's print never joins a line of Lapin's.
    const joined = foldOutput(foldOutput([], { type: 'done', id: 6, ok: false, error: 'x' }), { type: 'err', id: 6, text: 'more' });
    expect(joined.map((l) => l.text)).toEqual(['x', 'Nothing the script did is kept.', 'more']);
  });

  it('keeps at most its last 2000 lines', () => {
    let lines: ConsoleLine[] = [];
    for (let i = 0; i < 2100; i++) lines = foldOutput(lines, { type: 'out', id: i, text: `${i}\n` });
    expect(lines.length).toBe(2000);
    expect(lines[0].text).toBe('100\n');
  });

  it('walks its history with Up and Down, the newest first', () => {
    let h = { items: [] as string[], at: null as number | null };
    h = remember(remember(remember(h, 'a = 1'), 'print(a)'), '  ');
    h = remember(h, 'a = 1');
    expect(h.items).toEqual(['print(a)', 'a = 1']);
    let r = walk(h, -1, '');
    expect(r.text).toBe('a = 1');
    r = walk(r.history, -1, '');
    expect(r.text).toBe('print(a)');
    r = walk(r.history, -1, '');
    expect(r.text).toBe('print(a)');
    r = walk(r.history, 1, '');
    expect(r.text).toBe('a = 1');
    r = walk(r.history, 1, '');
    expect(r.text).toBe('');
    expect(r.history.at).toBeNull();
  });

  it('names a run’s undo step after its script', () => {
    expect(stepName('Pereți perimetrali.py', false)).toBe('Pereți perimetrali');
    expect(stepName(null, false)).toBe('Script');
    expect(stepName('x.py', true)).toBe('Python console');
  });
});

describe('the user’s scripts (Mine)', () => {
  it('takes a file name ending .py and nothing that leaves the folder, as the engine does', () => {
    expect(validScriptName('walls.py')).toBe(true);
    expect(validScriptName('Pereți perimetrali.py')).toBe(true);
    for (const bad of ['../a.py', 'a/b.py', '.hidden.py', 'notes.txt', 'C:\\x.py', '.py', '']) expect(validScriptName(bad)).toBe(false);
    expect(scriptName('Room walls')).toBe('Room walls.py');
    expect(scriptName(' ../x.PY ')).toBe('x.py');
    expect(scriptName('a/b:c')).toBe('a b c.py');
    expect(scriptName('   ')).toBeNull();
  });
});

describe('completion from the command catalogue', () => {
  const cat: Catalogue = {
    commands: [
      { name: 'architecture.addWall', label: 'Wall', args: [{ name: 'storeyId', type: 'str' }, { name: 'x1', type: 'float' }] },
      { name: 'architecture.addSlab', label: 'Floor', args: [] },
      { name: 'cutOrder.reset', label: 'Reset', args: [] },
      { name: 'project.location', label: 'Location', args: [] },
      { name: 'element.import', label: 'Import', args: [{ name: 'from', type: 'str' }] },
    ],
    queries: [{ name: 'schedule.csv', args: [{ name: 'id' }] }, { name: 'project.location', args: [] }],
  };
  const spaces = namespaces(cat);

  it('names the calls as lapinbim does', () => {
    expect(snake('addWall')).toBe('add_wall');
    expect(snake('exportIFC')).toBe('export_ifc');
    expect([...spaces.keys()].sort()).toEqual(['architecture', 'cut_order', 'element', 'project', 'schedule']);
    expect(spaces.get('architecture')!.map((c) => c.label)).toEqual(['add_slab', 'add_wall']);
    expect(spaces.get('architecture')!.find((c) => c.label === 'add_wall')!.detail).toBe('(storey_id=…, x1=…)');
    expect(spaces.get('project')!.map((c) => c.label)).toEqual(['location', 'location_query']);
    expect(spaces.get('element')![0]).toMatchObject({ label: 'import_', detail: '(from_=…)' });
  });

  it('offers the namespaces after lapin. and a namespace’s calls after lapin.<namespace>.', () => {
    const top = complete(spaces, 'w = lapin.')!;
    expect(top.from).toBe(10);
    expect(top.options.map((o) => o.label)).toEqual(expect.arrayContaining(['architecture', 'batch', 'model', 'undo']));
    const some = complete(spaces, '    lapin.arch')!;
    expect(some.options.map((o) => o.label)).toEqual(['architecture']);
    expect(some.from).toBe('    lapin.'.length);
    const calls = complete(spaces, 'lapin.architecture.add_w')!;
    expect(calls.options.map((o) => o.label)).toEqual(['add_wall']);
    expect(calls.options[0].apply).toBe('add_wall(');
    expect(complete(spaces, 'lapin.nowhere.x')).toBeNull();
    expect(complete(spaces, 'mylapin.')).toBeNull();
    expect(complete(spaces, 'print(1)')).toBeNull();
  });
});
