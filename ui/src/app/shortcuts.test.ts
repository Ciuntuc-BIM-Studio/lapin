import { describe, expect, it } from 'vitest';
import {
  aliasClash, captureKey, chordOf, normalizeAlias, resolveAlias, conflicts, effectiveBindings, exportKeymap, formatBinding, importKeymap, isBareKey, isSequence, matchSequence, normalizeBinding,
  PendingSequence, presetBindings, resetAll, resetCommand, resolveKeymap, usersOf, withBinding, withoutBinding, withPreset, type CommandDef,
} from './shortcuts';
import { keyIntent, type KeyPress, type KeySnapshot } from './keys';

const cmd = (id: string, defaults: string[] = [], same?: string): CommandDef => ({ id, label: id, category: 'test', defaults, same, run: () => undefined });
const COMMANDS = [
  cmd('app.save', ['Ctrl+S']), cmd('app.saveAs', ['Ctrl+Shift+S']), cmd('app.interface', ['Ctrl+.']),
  cmd('architecture.wall', ['WA']), cmd('architecture.door', ['DR']), cmd('modify.move', [], 'MOVE'), cmd('modify.copy', [], 'COPY'),
  cmd('civil.corridor', ['CO']), cmd('drafting.dimension', ['DI'], 'DIMALIGNED'), cmd('app.zoomFit'),
  cmd('app.nextTab', ['Ctrl+Tab', 'Ctrl+PageDown']), cmd('app.previousTab', ['Ctrl+Shift+Tab', 'Ctrl+PageUp']),
];

describe('bindings', () => {
  it('reads chords and sequences in one spelling', () => {
    expect(normalizeBinding('ctrl+shift+s')).toBe('Ctrl+Shift+S');
    expect(normalizeBinding('Shift + Cmd + s')).toBe('Ctrl+Shift+S');
    expect(normalizeBinding('esc')).toBe('Escape');
    expect(normalizeBinding('f3')).toBe('F3');
    expect(normalizeBinding('wa')).toBe('WA');
    expect(normalizeBinding('Ctrl++')).toBe('Ctrl+Plus');
    expect(normalizeBinding('+')).toBe('Plus');
    expect(normalizeBinding('Ctrl+A+B')).toBeNull();
    expect(normalizeBinding('')).toBeNull();
    expect(isSequence('WA')).toBe(true);
    expect(isSequence('Ctrl+S')).toBe(false);
    expect(isBareKey('1')).toBe(true);
    expect(isBareKey('Shift+Z')).toBe(true);
    expect(isBareKey('Ctrl+Z')).toBe(false);
    expect(isBareKey('F3')).toBe(false);
  });

  it('reads a key press as a chord: Cmd is Ctrl, Shift goes with the character it made', () => {
    const k = (key: string, more: Partial<Parameters<typeof chordOf>[0]> = {}) => chordOf({ key, ctrlKey: false, metaKey: false, shiftKey: false, ...more });
    expect(k('s', { metaKey: true, shiftKey: true })).toBe('Ctrl+Shift+S');
    expect(k('.', { ctrlKey: true })).toBe('Ctrl+.');
    expect(k('+', { shiftKey: true })).toBe('Plus');
    expect(k('Z', { shiftKey: true })).toBe('Shift+Z');
    expect(k('∫', { altKey: true, code: 'KeyB' })).toBe('Alt+B');
    expect(k('F8')).toBe('F8');
    expect(k('Shift', { shiftKey: true })).toBeNull();
    // The draw order's brackets (decision 339): by the key's place, Shift kept though it typed a brace.
    expect(k(']', { ctrlKey: true, code: 'BracketRight' })).toBe('Ctrl+]');
    expect(k('}', { metaKey: true, shiftKey: true, code: 'BracketRight' })).toBe('Ctrl+Shift+]');
    expect(k('ă', { ctrlKey: true, code: 'BracketLeft' })).toBe('Ctrl+[');
    expect(normalizeBinding('ctrl+shift+[')).toBe('Ctrl+Shift+[');
    expect(formatBinding('Ctrl+Shift+S', false)).toBe('Ctrl ⇧ S');
    expect(formatBinding('Ctrl+Shift+S', true)).toBe('⌘⇧S');
    expect(formatBinding('WA', false)).toBe('WA');
  });
});

describe('the keymap', () => {
  const cli = [{ name: 'DIMALIGNED', command: 'DIMALIGNED' }, { name: 'DIMADD', command: 'DIMADD' }, { name: 'MOVE', command: 'MOVE' }, { name: 'MV', command: 'MOVE' }, { name: 'COPY', command: 'COPY' }];

  it('takes the preset, then the user\'s own bindings over it', () => {
    const classic = presetBindings(COMMANDS, 'classic');
    expect(classic['civil.corridor']).toEqual(['CO']);
    expect(classic['modify.copy']).toEqual([]);
    const revit = presetBindings(COMMANDS, 'revit');
    expect(revit['modify.copy']).toEqual(['CO']);
    // A command Revit has no letters for keeps Lapin's.
    expect(revit['civil.corridor']).toEqual(['CO']);
    expect(revit['app.zoomFit']).toEqual(['ZF', 'ZE']);
    const mine = withBinding(COMMANDS, withPreset('revit'), 'architecture.wall', 'Ctrl+Shift+W');
    expect(effectiveBindings(COMMANDS, mine)['architecture.wall']).toEqual(['WA', 'Ctrl+Shift+W']);
  });

  it('finds conflicts, and reassigns a binding from the command that had it', () => {
    const s = withBinding(COMMANDS, withPreset('classic'), 'modify.copy', 'CO');
    expect(conflicts(effectiveBindings(COMMANDS, s))).toEqual({ CO: ['modify.copy', 'civil.corridor'] });
    expect(usersOf(effectiveBindings(COMMANDS, s), 'CO', 'modify.copy')).toEqual(['civil.corridor']);
    const taken = withBinding(COMMANDS, withPreset('classic'), 'modify.copy', 'CO', true);
    const b = effectiveBindings(COMMANDS, taken);
    expect(b['modify.copy']).toEqual(['CO']);
    expect(b['civil.corridor']).toEqual([]);
    expect(conflicts(b)).toEqual({});
  });

  it('removes a binding, resets one command or all of them to the preset', () => {
    let s = withoutBinding(COMMANDS, withPreset('revit'), 'architecture.wall', 'WA');
    s = withBinding(COMMANDS, s, 'architecture.door', 'Ctrl+D');
    expect(effectiveBindings(COMMANDS, s)['architecture.wall']).toEqual([]);
    s = resetCommand(s, 'architecture.wall');
    expect(effectiveBindings(COMMANDS, s)['architecture.wall']).toEqual(['WA']);
    expect(effectiveBindings(COMMANDS, s)['architecture.door']).toEqual(['DR', 'Ctrl+D']);
    s = resetAll(s);
    expect(s.overrides).toEqual({});
    expect(s.preset).toBe('revit');
  });

  it('MA starts Match properties at once: its own MATCHPROP names never make it wait, so a following Enter ends the tool (the guide scene)', () => {
    const match: CommandDef = { id: 'modify.match', label: 'Match properties', category: 'Modify', defaults: ['MA'], same: 'MATCHPROP', run: () => {} };
    const map = resolveKeymap([match], { 'modify.match': ['MA'] }, [{ name: 'MATCHPROP', command: 'MATCHPROP' }, { name: 'MA', command: 'MATCHPROP' }, { name: 'MATCHTYPE', command: 'MATCHPROP' }]);
    expect(matchSequence('MA', map)).toEqual({ kind: 'fire', id: 'modify.match' });
  });

  it('runs a sequence at once unless something longer starts with it; its own command never makes it wait', () => {
    const bindings = { ...effectiveBindings(COMMANDS, withPreset('revit')), 'modify.move': ['MV'] };
    const map = resolveKeymap(COMMANDS, bindings, cli);
    expect(matchSequence('wa', map)).toEqual({ kind: 'fire', id: 'architecture.wall' });
    expect(matchSequence('MV', map)).toEqual({ kind: 'fire', id: 'modify.move' });
    expect(matchSequence('CO', map)).toEqual({ kind: 'fire', id: 'modify.copy' });
    expect(matchSequence('DI', map)).toEqual({ kind: 'wait', id: 'drafting.dimension' });
    expect(matchSequence('W', map)).toEqual({ kind: 'none' });
    expect(matchSequence('W1', map)).toEqual({ kind: 'none' });
    // Two commands with one binding: the first listed has it.
    const clash = resolveKeymap(COMMANDS, { ...bindings, 'architecture.door': ['WA'] }, cli);
    expect(clash.sequences.WA).toBe('architecture.wall');
  });

  it('runs a waiting sequence when its time is up and the command line still holds its letters', () => {
    const p = new PendingSequence(800);
    p.arm('drafting.dimension', 'DI', 1000);
    expect(p.due(1500, 'DI', false)).toBeNull();
    expect(p.due(1800, 'DI', false)).toBe('drafting.dimension');
    expect(p.due(2000, 'DI', false)).toBeNull();
    p.arm('drafting.dimension', 'DI', 1000);
    expect(p.due(1900, 'DIM', false)).toBeNull();
    p.arm('drafting.dimension', 'DI', 1000);
    expect(p.due(1900, 'DI', true)).toBeNull();
  });

  it('leaves the keys to a field: a binding acts only outside inputs', () => {
    const map = resolveKeymap(COMMANDS, effectiveBindings(COMMANDS, withPreset('revit')), cli);
    const press = (key: string, more: Partial<KeyPress> = {}): KeyPress => ({ key, ctrlKey: false, metaKey: false, shiftKey: false, inField: false, inDialog: false, ...more });
    const state: KeySnapshot = { screen: 'workspace', dialog: null, tool: null, selected: [], buffer: 'w', step: null, hasLast: false, hotbar: 9, keymap: map };
    expect(keyIntent(press('a'), state)).toEqual({ kind: 'command', id: 'architecture.wall' });
    expect(keyIntent(press('a', { inField: true }), state)).toBeNull();
    expect(keyIntent(press('.', { ctrlKey: true }), { ...state, buffer: '' })).toEqual({ kind: 'command', id: 'app.interface' });
    expect(keyIntent(press('.', { ctrlKey: true, inField: true }), { ...state, buffer: '' })).toBeNull();
  });

  it('Ctrl+Tab, Ctrl+Shift+Tab and Ctrl+Page Down/Up switch the view tabs, but not in a field (forum topic 63)', () => {
    const map = resolveKeymap(COMMANDS, effectiveBindings(COMMANDS, withPreset('classic')), cli);
    const press = (key: string, more: Partial<KeyPress> = {}): KeyPress => ({ key, ctrlKey: true, metaKey: false, shiftKey: false, inField: false, inDialog: false, ...more });
    const state: KeySnapshot = { screen: 'workspace', dialog: null, tool: null, selected: [], buffer: '', step: null, hasLast: false, hotbar: 9, keymap: map };
    expect(keyIntent(press('Tab'), state)).toEqual({ kind: 'command', id: 'app.nextTab' });
    expect(keyIntent(press('Tab', { shiftKey: true }), state)).toEqual({ kind: 'command', id: 'app.previousTab' });
    expect(keyIntent(press('PageDown'), state)).toEqual({ kind: 'command', id: 'app.nextTab' });
    expect(keyIntent(press('PageUp'), state)).toEqual({ kind: 'command', id: 'app.previousTab' });
    expect(keyIntent(press('Tab', { inField: true }), state)).toBeNull();
  });

  it('goes to a file and back the same', () => {
    let s = withBinding(COMMANDS, withPreset('classic'), 'architecture.wall', 'Ctrl+Shift+W');
    s = withoutBinding(COMMANDS, s, 'architecture.door', 'DR');
    const back = importKeymap(exportKeymap(s));
    expect(back).toEqual(s);
    expect(importKeymap('{"format":"other"}')).toBeNull();
    expect(importKeymap('not json')).toBeNull();
    expect(importKeymap(JSON.stringify({ format: 'lapin-shortcuts', preset: 'revit', overrides: { 'architecture.wall': ['ctrl+w', 'nonsense+key+x', 'wa'] } })))
      .toEqual({ preset: 'revit', overrides: { 'architecture.wall': ['Ctrl+W', 'WA'] }, aliases: {}, letters: true });
  });
});

/** Report #40: "Not all keys are recognized when trying to assign them to functions. At least Shift, Ctrl, Alt." */
describe('listening for a new shortcut', () => {
  const press = (key: string, more: Partial<Parameters<typeof captureKey>[1]> = {}) => ({ key, ctrlKey: false, metaKey: false, altKey: false, shiftKey: false, ...more });
  it('keeps Shift with a letter: a chord, not the letter alone', () => {
    expect(captureKey('', press('W', { shiftKey: true, code: 'KeyW' }))).toEqual({ kind: 'done', binding: 'Shift+W' });
  });
  it('takes Ctrl, Alt and Shift together, the key by its place', () => {
    expect(captureKey('', press('W', { ctrlKey: true, shiftKey: true, code: 'KeyW' }))).toEqual({ kind: 'done', binding: 'Ctrl+Shift+W' });
    expect(captureKey('', press('∫', { altKey: true, code: 'KeyB' }))).toEqual({ kind: 'done', binding: 'Alt+B' });
    expect(captureKey('', press('1', { altKey: true, code: 'Digit1' }))).toEqual({ kind: 'done', binding: 'Alt+1' });
  });
  it('shows a modifier held alone and waits for its key', () => {
    expect(captureKey('', press('Control', { ctrlKey: true }))).toEqual({ kind: 'held', text: 'Ctrl+…' });
    expect(captureKey('', press('Shift', { ctrlKey: true, shiftKey: true }))).toEqual({ kind: 'held', text: 'Ctrl+Shift+…' });
    expect(captureKey('', press('Alt', { altKey: true }))).toEqual({ kind: 'held', text: 'Alt+…' });
  });
  it('still makes sequences of plain letters, ended by Enter or the third', () => {
    expect(captureKey('', press('w'))).toEqual({ kind: 'letters', letters: 'W' });
    expect(captureKey('W', press('Enter'))).toEqual({ kind: 'done', binding: 'W' });
    expect(captureKey('WA', press('x'))).toEqual({ kind: 'done', binding: 'WAX' });
    expect(captureKey('', press('Escape'))).toEqual({ kind: 'done', binding: null });
    expect(captureKey('', press('F4'))).toEqual({ kind: 'done', binding: 'F4' });
  });
});

describe('aliases and the command line key (decision 473)', () => {
  it('keeps an alias in its one spelling', () => {
    expect(normalizeAlias(' wl ')).toBe('WL');
    expect(normalizeAlias('A1b2')).toBe('A1B2');
    expect(normalizeAlias('1A')).toBeNull();
    expect(normalizeAlias('')).toBeNull();
    expect(normalizeAlias('ABCDEFGHIJKLM')).toBeNull();
    expect(normalizeAlias('a b')).toBeNull();
  });

  it('resolves what is typed to the alias\'s command, ignoring case', () => {
    expect(resolveAlias('wl', { WL: 'cad:LINE' })).toBe('cad:LINE');
    expect(resolveAlias(' WL ', { WL: 'architecture.wall' })).toBe('architecture.wall');
    expect(resolveAlias('XX', { WL: 'cad:LINE' })).toBeNull();
    expect(resolveAlias('constructor', {})).toBeNull();
  });

  it('shows what an alias shadows', () => {
    const map = resolveKeymap(COMMANDS, effectiveBindings(COMMANDS, withPreset('revit')), []);
    const wa = aliasClash('WA', [{ name: 'LINE', command: 'LINE' }, { name: 'L', command: 'LINE' }], map);
    expect(wa.sequence).toBe('architecture.wall');
    expect(wa.builtIn).toBeNull();
    expect(aliasClash('l', [{ name: 'L', command: 'LINE' }], map).builtIn).toBe('LINE');
    expect(aliasClash('WAX', [], map).waits).toContain('WA');
    expect(aliasClash('QQ', [], map)).toEqual({ builtIn: null, sequence: null, waits: [] });
  });

  it('keeps the aliases and the letters switch in a file, and over a change of preset', () => {
    const s = { ...withPreset('classic'), aliases: { WL: 'cad:LINE' }, letters: false };
    const back = importKeymap(exportKeymap(s));
    expect(back?.aliases).toEqual({ WL: 'cad:LINE' });
    expect(back?.letters).toBe(false);
    expect(withPreset('revit', s).aliases).toEqual({ WL: 'cad:LINE' });
    expect(withPreset('revit', s).letters).toBe(false);
    // An older file has neither.
    const old = importKeymap(JSON.stringify({ format: 'lapin-shortcuts', version: 1, preset: 'revit', overrides: {} }));
    expect(old).toEqual({ preset: 'revit', overrides: {}, aliases: {}, letters: true });
    expect(importKeymap(JSON.stringify({ format: 'lapin-shortcuts', aliases: { '1x': 'a', ok: 'cad:LINE', bad: 5 } }))?.aliases).toEqual({ OK: 'cad:LINE' });
  });

  it('reads the ` key by its place', () => {
    expect(chordOf({ key: '`', code: 'Backquote', ctrlKey: false, metaKey: false, shiftKey: false })).toBe('Backquote');
    expect(chordOf({ key: 'Dead', code: 'Backquote', ctrlKey: false, metaKey: false, shiftKey: false })).toBe('Backquote');
    expect(chordOf({ key: '~', code: 'Backquote', ctrlKey: false, metaKey: false, shiftKey: true })).toBe('Backquote');
    expect(normalizeBinding('`')).toBe('Backquote');
    expect(formatBinding('Backquote')).toBe('`');
    expect(captureKey('', { key: '`', code: 'Backquote', ctrlKey: false, metaKey: false, shiftKey: false })).toEqual({ kind: 'done', binding: 'Backquote' });
  });
});
