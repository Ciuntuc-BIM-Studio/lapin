import { describe, expect, it } from 'vitest';
import { lastLine, runMessages, tracebackSite } from './messages';

const TRACE = `Traceback (most recent call last):
  File "Walls.py", line 7, in <module>
    build()
  File "Walls.py", line 4, in build
    lapin.model.walls[99]
IndexError: list index out of range`;

describe('the Script panel messages', () => {
  it('takes the last line of an error', () => {
    expect(lastLine(TRACE)).toBe('IndexError: list index out of range');
    expect(lastLine('  \n')).toBe('');
    expect(lastLine(null)).toBe('');
  });

  it('finds the script file and line in a traceback: the last frame of that file', () => {
    expect(tracebackSite(TRACE, 'Walls.py')).toEqual({ file: 'Walls.py', line: 4 });
    expect(tracebackSite(TRACE, 'Other.py')).toBeNull();
    expect(tracebackSite('  File "/usr/lib/python3/x.py", line 9, in f\nValueError', 'Walls.py')).toBeNull();
    expect(tracebackSite(undefined, 'Walls.py')).toBeNull();
  });

  it('makes a row for a start and a finished run', () => {
    expect(runMessages({ type: 'start', id: 1, name: 'Walls' }, 'Walls')).toMatchObject([{ kind: 'info', source: 'Walls', text: 'Started' }]);
    expect(runMessages({ type: 'done', id: 1, ok: true, kept: true, seconds: 0.42 }, 'Walls')).toMatchObject([{ kind: 'info', text: 'Done in 0.42 s: one undo step.' }]);
    expect(runMessages({ type: 'done', id: 1, ok: false, interrupted: true, error: 'Stopped.' }, 'Walls')).toMatchObject([{ kind: 'warning', text: 'Stopped.' }]);
  });

  it('a failed run says the error and where, and its click opens the script at its line', () => {
    const [m] = runMessages({ type: 'done', id: 2, ok: false, error: 'IndexError: list index out of range', traceback: TRACE, line: 4 }, 'Walls.py');
    expect(m).toMatchObject({ kind: 'error', source: 'Walls.py:4', text: 'IndexError: list index out of range', target: { script: 'Walls.py', line: 4 } });
  });

  it('a failure without a traceback or line is Lapin\'s own text with no line', () => {
    const [m] = runMessages({ type: 'done', id: 3, ok: false, error: 'Python was not found.' }, 'Walls');
    expect(m).toMatchObject({ kind: 'error', source: 'Walls', text: 'Python was not found.', target: { script: 'Walls' } });
    expect(m!.textPlain).toBe(false);
  });

  it('leaves out a console run, and tells that Python ended', () => {
    expect(runMessages({ type: 'start', id: 4, console: true }, null)).toEqual([]);
    expect(runMessages({ type: 'exit' }, null)).toMatchObject([{ kind: 'warning', source: 'Python' }]);
  });
});
