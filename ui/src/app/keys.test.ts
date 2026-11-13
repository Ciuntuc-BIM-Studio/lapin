import { describe, expect, it } from 'vitest';
import { carryOut, handleKeydown, keyIntent, openDialog, valueKey, type KeyIntent, type KeyPress, type KeySnapshot } from './keys';
import { claimValue, valueOwner } from './valueOwner';
import { resolveKeymap, type CommandDef } from './shortcuts';
import { useCad } from '../viewport/cad/session';

const cmd = (id: string, defaults: string[], same?: string): CommandDef => ({ id, label: id, category: 'test', defaults, same, run: () => undefined });
const COMMANDS = [
  cmd('app.undo', ['Ctrl+Z']), cmd('app.redo', ['Ctrl+Y', 'Ctrl+Shift+Z']), cmd('app.new', ['Ctrl+N']), cmd('app.open', ['Ctrl+O']),
  cmd('app.save', ['Ctrl+S']), cmd('app.saveAs', ['Ctrl+Shift+S']), cmd('app.delete', ['Delete', 'Backspace']),
  cmd('app.ortho', ['F8']), cmd('app.zoomIn', ['Plus', '=']), cmd('app.zoomOut', ['-']), cmd('app.zoomSelection', ['Shift+Z']),
  ...Array.from({ length: 9 }, (_, i) => cmd(`hotbar.${i + 1}`, [String(i + 1)])),
  cmd('app.commandLine', ['Backquote']),
  cmd('architecture.wall', ['WA']), cmd('view.layers', ['VV']), cmd('drafting.dimension', ['DI'], 'DIMALIGNED'), cmd('modify.copy', ['CO'], 'COPY'),
];
const bindings = Object.fromEntries(COMMANDS.map((c) => [c.id, c.defaults ?? []]));
const KEYMAP = resolveKeymap(COMMANDS, bindings, [
  { name: 'DIMALIGNED', command: 'DIMALIGNED' }, { name: 'DIMADD', command: 'DIMADD' }, { name: 'COPY', command: 'COPY' }, { name: 'CO', command: 'COPY' },
]);

const press = (key: string, more: Partial<KeyPress> = {}): KeyPress => ({ key, ctrlKey: false, metaKey: false, shiftKey: false, inField: false, inDialog: false, ...more });
const state = (more: Partial<KeySnapshot> = {}): KeySnapshot => ({
  screen: 'workspace', dialog: null, tool: null, selected: [], buffer: '', step: null, hasLast: false, hotbar: 5,
  keymap: KEYMAP, ...more,
});

describe('keyIntent', () => {
  it('leaves fields, the start screen and unknown keys alone', () => {
    expect(keyIntent(press('Delete', { inField: true }), state({ selected: ['w1'] }))).toBeNull();
    expect(keyIntent(press('Delete'), state({ screen: 'start', selected: ['w1'] }))).toBeNull();
    expect(keyIntent(press('F7'), state())).toBeNull();
    expect(keyIntent(press('ArrowUp'), state())).toBeNull();
  });

  it('gives the keys to an open dialog: Escape closes it, saving still works, nothing reaches the model', () => {
    const open = state({ dialog: 'materials', selected: ['w1'] });
    expect(keyIntent(press('Escape'), open)).toEqual({ kind: 'closeDialog' });
    expect(keyIntent(press('Delete'), open)).toBeNull();
    expect(keyIntent(press('Backspace'), open)).toBeNull();
    expect(keyIntent(press('w'), open)).toBeNull();
    expect(keyIntent(press('Enter'), open)).toBeNull();
    expect(keyIntent(press('s', { metaKey: true }), open)).toEqual({ kind: 'command', id: 'app.save' });
    expect(keyIntent(press('z', { metaKey: true }), open)).toBeNull();
    // A dialog that keeps its own state (not the store's): the keys still stay with it.
    expect(keyIntent(press('Delete', { inDialog: true }), state({ selected: ['w1'] }))).toBeNull();
    expect(keyIntent(press('Escape', { inDialog: true }), state())).toBeNull();
  });

  it('does not let a dialog id with nothing on the screen hold the keys', () => {
    expect(openDialog('materials', true)).toBe('materials');
    expect(openDialog('materials', false)).toBeNull();
    expect(openDialog(null, true)).toBeNull();
    expect(keyIntent(press('Delete'), state({ dialog: openDialog('gone', false), selected: ['w1'] }))).toEqual({ kind: 'command', id: 'app.delete' });
  });

  it('runs the Ctrl/Cmd shortcuts', () => {
    expect(keyIntent(press('z', { ctrlKey: true }), state())).toEqual({ kind: 'command', id: 'app.undo' });
    expect(keyIntent(press('Z', { metaKey: true, shiftKey: true }), state())).toEqual({ kind: 'command', id: 'app.redo' });
    expect(keyIntent(press('y', { ctrlKey: true }), state())).toEqual({ kind: 'command', id: 'app.redo' });
    expect(keyIntent(press('n', { metaKey: true }), state())).toEqual({ kind: 'command', id: 'app.new' });
    expect(keyIntent(press('o', { metaKey: true }), state())).toEqual({ kind: 'command', id: 'app.open' });
    expect(keyIntent(press('s', { metaKey: true, shiftKey: true }), state())).toEqual({ kind: 'command', id: 'app.saveAs' });
    expect(keyIntent(press('k', { metaKey: true }), state())).toBeNull();
  });

  it('deletes the selection, but Backspace edits the command line first', () => {
    expect(keyIntent(press('Delete'), state({ selected: ['w1', 'w2'] }))).toEqual({ kind: 'command', id: 'app.delete' });
    expect(keyIntent(press('Delete'), state())).toBeNull();
    expect(keyIntent(press('Backspace'), state({ selected: ['w1'] }))).toEqual({ kind: 'command', id: 'app.delete' });
    expect(keyIntent(press('Backspace'), state({ selected: ['w1'], buffer: '12' }))).toEqual({ kind: 'backspace' });
  });

  it('types into the command line and starts a tool on its two letters', () => {
    expect(keyIntent(press('w'), state())).toEqual({ kind: 'type', buffer: 'w' });
    expect(keyIntent(press('a'), state({ buffer: 'w' }))).toEqual({ kind: 'command', id: 'architecture.wall' });
    expect(keyIntent(press('A'), state({ buffer: 'W' }))).toEqual({ kind: 'command', id: 'architecture.wall' });
    expect(keyIntent(press('x'), state({ buffer: 'w' }))).toEqual({ kind: 'type', buffer: 'wx' });
    // While a command runs, its letters are input, not shortcuts.
    expect(keyIntent(press('a'), state({ buffer: 'w', step: { want: 'point' } }))).toEqual({ kind: 'type', buffer: 'wa' });
    expect(keyIntent(press('@'), state({ step: { want: 'point' } }))).toEqual({ kind: 'type', buffer: '@' });
    expect(keyIntent(press(' '), state({ step: { want: 'text' }, buffer: 'ab' }))).toEqual({ kind: 'type', buffer: 'ab ' });
    expect(keyIntent(press('!'), state())).toBeNull();
  });

  it('waits on letters a longer command starts with, and runs at once when only its own command does', () => {
    // DI: DIMADD starts with it too, so it waits (typing on writes DIMADD).
    expect(keyIntent(press('i'), state({ buffer: 'd' }))).toEqual({ kind: 'pending', id: 'drafting.dimension', buffer: 'di' });
    expect(keyIntent(press('m'), state({ buffer: 'di' }))).toEqual({ kind: 'type', buffer: 'dim' });
    // CO: only COPY, which is the command itself.
    expect(keyIntent(press('o'), state({ buffer: 'c' }))).toEqual({ kind: 'command', id: 'modify.copy' });
    // After something else was typed, the letters are not a shortcut.
    expect(keyIntent(press('a'), state({ buffer: '2w' }))).toEqual({ kind: 'type', buffer: '2wa' });
  });

  it('submits with Enter or Space, except when a plan gesture or nothing is waiting', () => {
    expect(keyIntent(press('Enter'), state({ buffer: 'line' }))).toEqual({ kind: 'submit' });
    expect(keyIntent(press(' '), state({ hasLast: true }))).toEqual({ kind: 'submit' });
    expect(keyIntent(press('Enter'), state())).toBeNull();
    expect(keyIntent(press('Enter'), state({ hasLast: true, tool: 'architecture.slab' }))).toBeNull();
    expect(keyIntent(press('Enter'), state({ step: { want: 'point' }, tool: 'architecture.slab' }))).toEqual({ kind: 'submit' });
  });

  it('zooms the view in front with + and −, and to the selection with Shift+Z, only while nothing is typed', () => {
    expect(keyIntent(press('+'), state())).toEqual({ kind: 'command', id: 'app.zoomIn' });
    expect(keyIntent(press('='), state())).toEqual({ kind: 'command', id: 'app.zoomIn' });
    expect(keyIntent(press('-'), state())).toEqual({ kind: 'command', id: 'app.zoomOut' });
    expect(keyIntent(press('Z', { shiftKey: true }), state({ selected: ['w1'] }))).toEqual({ kind: 'command', id: 'app.zoomSelection' });
    // A minus inside a running command is the start of a coordinate; a Z is typed.
    expect(keyIntent(press('-'), state({ step: { want: 'point' } }))).toEqual({ kind: 'type', buffer: '-' });
    expect(keyIntent(press('+'), state({ buffer: '@2' }))).toEqual({ kind: 'type', buffer: '@2+' });
    expect(keyIntent(press('z'), state())).toEqual({ kind: 'type', buffer: 'z' });
  });

  it('sends digits to the live length while a segment is drawn and the command line is empty', () => {
    const live = (text = '', typed = false) => state({ live: { active: true, text, typed }, hotbar: 9 });
    expect(keyIntent(press('3'), live())).toEqual({ kind: 'liveType', key: '3' });
    expect(keyIntent(press('.'), live('3'))).toEqual({ kind: 'liveType', key: '.' });
    expect(keyIntent(press('m'), live('3'))).toEqual({ kind: 'liveType', key: 'm' });
    expect(keyIntent(press('m'), live())).toEqual({ kind: 'type', buffer: 'm' });
    expect(keyIntent(press('-'), live())).toEqual({ kind: 'command', id: 'app.zoomOut' });
    expect(keyIntent(press('Tab'), live())).toEqual({ kind: 'liveFocus' });
    // A rectangle: Space switches length/angle with width/height; a comma in width/height goes on to the height; elsewhere Space is not the box's.
    const rect = (mode: 'polar' | 'rect') => state({ live: { active: true, text: '2000', typed: true, rect: true, mode }, hotbar: 9 });
    expect(keyIntent(press(' '), rect('polar'))).toEqual({ kind: 'liveMode' });
    expect(keyIntent(press(','), rect('rect'))).toEqual({ kind: 'liveFocus', field: 'ang' });
    expect(keyIntent(press(','), rect('polar'))).toEqual({ kind: 'liveType', key: ',' });
    expect(keyIntent(press(' '), live())).not.toEqual({ kind: 'liveMode' });
    // A negative width or height can be typed from an empty field in width/height (elsewhere a minus is zoom out).
    expect(keyIntent(press('-'), state({ live: { active: true, text: '', typed: false, rect: true, mode: 'rect' }, hotbar: 9 }))).toEqual({ kind: 'liveType', key: '-' });
    expect(keyIntent(press('<'), live('2400'))).toEqual({ kind: 'liveFocus', field: 'ang' });
    expect(keyIntent(press('Enter'), live('2400', true))).toEqual({ kind: 'liveCommit' });
    expect(keyIntent(press('Escape'), live('2400', true))).toEqual({ kind: 'liveClear' });
    expect(keyIntent(press('Backspace'), live('24', true))).toEqual({ kind: 'liveBack' });
    // A unit after the length, typed out in full.
    for (const typed of ['2400mm', '240cm', '24dm', '2.4m', '96in', '10ft', "10'", '42"', "3'6\""]) {
      let text = '';
      for (const k of typed) {
        expect(keyIntent(press(k), live(text, !!text)), `${typed} at ${text}`).toEqual({ kind: 'liveType', key: k });
        text += k;
      }
    }
    expect(keyIntent(press('M'), live('2400m'))).toEqual({ kind: 'liveType', key: 'm' });
    // Not a unit: the command line's.
    expect(keyIntent(press('x'), live('2400'))).toEqual({ kind: 'type', buffer: 'x' });
    expect(keyIntent(press('m'), live('2400mm'))).toEqual({ kind: 'type', buffer: 'm' });
    expect(keyIntent(press('"'), live("3'"))).toBeNull();
    // Something on the command line (@2,3): the digits stay there.
    expect(keyIntent(press('3'), state({ live: { active: true, text: '', typed: false }, buffer: '@2,' }))).toEqual({ kind: 'type', buffer: '@2,3' });
  });

  it('cancels a running command with Escape, toggles with the F keys, picks the hotbar with digits', () => {
    expect(keyIntent(press('Escape'), state({ step: { want: 'point' } }))).toEqual({ kind: 'cancel' });
    expect(keyIntent(press('Escape'), state())).toBeNull();
    expect(keyIntent(press('F8'), state())).toEqual({ kind: 'command', id: 'app.ortho' });
    expect(keyIntent(press('3'), state())).toEqual({ kind: 'command', id: 'hotbar.3' });
    expect(keyIntent(press('9'), state())).toBeNull();
    expect(keyIntent(press('3'), state({ buffer: '1' }))).toEqual({ kind: 'type', buffer: '13' });
  });
});

describe('a value a 3D tool waits for (decision 378)', () => {
  /** A keydown on the window with no field focused (target: the page), through the real handler. */
  const keydown = (key: string, did: KeyIntent[]) => {
    let prevented = false;
    handleKeydown({ key, code: '', ctrlKey: false, metaKey: false, shiftKey: false, altKey: false, defaultPrevented: false, target: null, preventDefault: () => { prevented = true; } },
      () => state({ hotbar: 9, value: (() => { const o = valueOwner(); return o ? { text: o.text() } : null; })() }),
      (i) => { did.push(i); if (i.kind.startsWith('value')) carryOut(i); });
    return prevented;
  };

  it('takes "600" after a Line\'s first point: the value has it, no quick tool starts, no character is lost', () => {
    let text = '';
    let entered = 0;
    const release = claimValue({ text: () => text, set: (t) => { text = t; }, enter: () => { entered++; } });
    const did: KeyIntent[] = [];
    for (const k of ['6', '0', '0']) expect(keydown(k, did)).toBe(true);
    expect(text).toBe('600');
    expect(did.some((i) => i.kind === 'command')).toBe(false);
    // A length with its unit, a pair, an angle, a CAD point, a radius; Backspace and Enter.
    for (const k of ['m', 'm', ',', '3', '0', '0']) keydown(k, did);
    expect(text).toBe('600mm,300');
    keydown('Backspace', did);
    expect(text).toBe('600mm,30');
    keydown('Enter', did);
    expect(entered).toBe(1);
    release();
    // Let go: the digit is a quick tool again.
    const after: KeyIntent[] = [];
    keydown('6', after);
    expect(after).toEqual([{ kind: 'command', id: 'hotbar.6' }]);
  });

  it('knows a value\'s keys', () => {
    for (const k of ['0', '9', '.', ',', '-', '+', '<', '@', ';']) expect(valueKey('', k), k).toBe(true);
    expect(valueKey('', 'r')).toBe(true);
    expect(valueKey('300', 'r')).toBe(false);
    expect(valueKey('1200', 'x')).toBe(true);
    expect(valueKey('', 'x')).toBe(false);
    expect(valueKey('25', 'c')).toBe(true);
    expect(valueKey('', 'l')).toBe(false);
    expect(valueKey('', 'Enter')).toBe(false);
  });

  it('leaves a focused box its own keys, and the command line its typed text', () => {
    const release = claimValue({ text: () => '', set: () => undefined, enter: () => undefined });
    expect(keyIntent(press('6', { inField: true }), state({ value: { text: '' } }))).toBeNull();
    expect(keyIntent(press('6'), state({ value: { text: '' }, buffer: 'L' }))).toEqual({ kind: 'type', buffer: 'L6' });
    release();
  });
  it('gives Space to a tool that takes it, unless something is typed', () => {
    expect(keyIntent(press(' '), state({ tool: 'architecture.door', toolSpace: true }))).toEqual({ kind: 'toolSpace' });
    expect(keyIntent(press(' '), state({ tool: 'architecture.door', toolSpace: true, buffer: 'WA' }))?.kind).toBe('submit');
    expect(keyIntent(press(' '), state({ tool: 'architecture.wall', toolSpace: false }))).toBeNull();
  });
});

describe('the command line, letters off, and the user\'s aliases (decision 473)', () => {
  it('with the letter shortcuts off, letters are typed and never run a tool by themselves', () => {
    expect(keyIntent(press('w'), state({ letters: false }))).toEqual({ kind: 'type', buffer: 'w' });
    expect(keyIntent(press('a'), state({ letters: false, buffer: 'w' }))).toEqual({ kind: 'type', buffer: 'wa' });
    expect(keyIntent(press('Enter'), state({ letters: false, buffer: 'wa' }))).toEqual({ kind: 'submit' });
    expect(keyIntent(press('w'), state())).toEqual({ kind: 'type', buffer: 'w' });
    expect(keyIntent(press('a'), state({ buffer: 'w' }))).toEqual({ kind: 'command', id: 'architecture.wall' });
  });

  it('leaves the chords and the quick tools alone when the letters are off', () => {
    expect(keyIntent(press('z', { ctrlKey: true }), state({ letters: false }))).toEqual({ kind: 'command', id: 'app.undo' });
    expect(keyIntent(press('3'), state({ letters: false }))).toEqual({ kind: 'command', id: 'hotbar.3' });
    expect(keyIntent(press('F8'), state({ letters: false }))).toEqual({ kind: 'command', id: 'app.ortho' });
  });

  it('the ` key is found by its place, whatever character the layout types, and toggles the mode', () => {
    expect(keyIntent(press('`', { code: 'Backquote' }), state())).toEqual({ kind: 'cmdMode' });
    expect(keyIntent(press('Dead', { code: 'Backquote' }), state())).toEqual({ kind: 'cmdMode' });
    expect(keyIntent(press('~', { code: 'Backquote', shiftKey: true }), state())).toEqual({ kind: 'cmdMode' });
    expect(keyIntent(press('`', { code: 'Backquote' }), state({ takeAll: true, buffer: 'DI' }))).toEqual({ kind: 'cmdMode' });
    expect(keyIntent(press('`', { code: 'Backquote', inField: true }), state())).toBeNull();
  });

  it('in the mode, every key goes to the command line: no sequence, no quick tool', () => {
    const on = { takeAll: true };
    expect(keyIntent(press('w'), state(on))).toEqual({ kind: 'type', buffer: 'w' });
    expect(keyIntent(press('a'), state({ ...on, buffer: 'w' }))).toEqual({ kind: 'type', buffer: 'wa' });
    expect(keyIntent(press('3'), state(on))).toEqual({ kind: 'type', buffer: '3' });
    expect(keyIntent(press('-'), state(on))).toEqual({ kind: 'type', buffer: '-' });
    expect(keyIntent(press('Enter'), state({ ...on, buffer: 'wa' }))).toEqual({ kind: 'submit' });
    expect(keyIntent(press('Backspace'), state({ ...on, selected: ['w1'] }))).toBeNull();
    expect(keyIntent(press('Escape'), state(on))).toEqual({ kind: 'cancel' });
    expect(keyIntent(press('Enter'), state(on))).toEqual({ kind: 'cancel' });
    // Chords are the app's still.
    expect(keyIntent(press('z', { ctrlKey: true }), state(on))).toEqual({ kind: 'command', id: 'app.undo' });
  });

  it('the mode stays while a command asks for input, and a value or live length goes to the line', () => {
    expect(keyIntent(press('5'), state({ takeAll: true, step: { want: 'point' }, value: { text: '' } }))).toEqual({ kind: 'type', buffer: '5' });
    expect(keyIntent(press('5'), state({ takeAll: true, step: { want: 'point' }, live: { active: true, text: '', typed: false } }))).toEqual({ kind: 'type', buffer: '5' });
  });

  it('carries the mode out: toggled by the key, left by Esc', () => {
    useCad.setState({ takeAll: false, buffer: '', step: null });
    carryOut({ kind: 'cmdMode' });
    expect(useCad.getState().takeAll).toBe(true);
    carryOut({ kind: 'cancel' });
    expect(useCad.getState().takeAll).toBe(false);
  });

  it('a user alias that is a shortcut, or starts one, makes the shortcut wait', () => {
    const aliases = { WAL: 'cad:LINE', VV: 'cad:LAYER', ON: 'cad:MOVE' };
    const map = resolveKeymap(COMMANDS, bindings, [{ name: 'COPY', command: 'COPY' }], aliases);
    expect(map.waits.WA).toBe(true);
    expect(map.waits.VV).toBe(true);
    expect(map.waits.DI).toBeUndefined();
    expect(keyIntent(press('a'), state({ buffer: 'w', keymap: map }))).toEqual({ kind: 'pending', id: 'architecture.wall', buffer: 'wa' });
    // Typing on reaches the alias.
    expect(keyIntent(press('l'), state({ buffer: 'wa', keymap: map }))).toEqual({ kind: 'type', buffer: 'wal' });
    // An alias that is the same command as the shortcut does not make it wait.
    const same = resolveKeymap(COMMANDS, bindings, [{ name: 'COPY', command: 'COPY' }], { CO: 'cad:COPY' });
    expect(same.waits.CO).toBeUndefined();
    // Another command with those letters does.
    expect(resolveKeymap(COMMANDS, bindings, [], { CO: 'cad:LINE' }).waits.CO).toBe(true);
  });
});
