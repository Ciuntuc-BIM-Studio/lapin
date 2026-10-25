import { describe, expect, it } from 'vitest';
import { keyIntent, type KeyPress, type KeySnapshot } from './keys';

const press = (key: string, more: Partial<KeyPress> = {}): KeyPress => ({ key, ctrlKey: false, metaKey: false, shiftKey: false, inField: false, inDialog: false, ...more });
const state = (more: Partial<KeySnapshot> = {}): KeySnapshot => ({
  screen: 'workspace', dialog: null, tool: null, selected: [], buffer: '', step: null, hasLast: false, hotbar: 5,
  shortcuts: { wa: 'architecture.wall', vv: 'view.layers' }, ...more,
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
    expect(keyIntent(press('s', { metaKey: true }), open)).toEqual({ kind: 'save', as: false });
    expect(keyIntent(press('z', { metaKey: true }), open)).toBeNull();
    // A dialog that keeps its own state (not the store's): the keys still stay with it.
    expect(keyIntent(press('Delete', { inDialog: true }), state({ selected: ['w1'] }))).toBeNull();
    expect(keyIntent(press('Escape', { inDialog: true }), state())).toBeNull();
  });

  it('runs the Ctrl/Cmd shortcuts', () => {
    expect(keyIntent(press('z', { ctrlKey: true }), state())).toEqual({ kind: 'undo' });
    expect(keyIntent(press('Z', { metaKey: true, shiftKey: true }), state())).toEqual({ kind: 'redo' });
    expect(keyIntent(press('y', { ctrlKey: true }), state())).toEqual({ kind: 'redo' });
    expect(keyIntent(press('n', { metaKey: true }), state())).toEqual({ kind: 'new' });
    expect(keyIntent(press('o', { metaKey: true }), state())).toEqual({ kind: 'open' });
    expect(keyIntent(press('s', { metaKey: true, shiftKey: true }), state())).toEqual({ kind: 'save', as: true });
    expect(keyIntent(press('k', { metaKey: true }), state())).toBeNull();
  });

  it('deletes the selection, but Backspace edits the command line first', () => {
    expect(keyIntent(press('Delete'), state({ selected: ['w1', 'w2'] }))).toEqual({ kind: 'delete', ids: ['w1', 'w2'] });
    expect(keyIntent(press('Delete'), state())).toBeNull();
    expect(keyIntent(press('Backspace'), state({ selected: ['w1'] }))).toEqual({ kind: 'delete', ids: ['w1'] });
    expect(keyIntent(press('Backspace'), state({ selected: ['w1'], buffer: '12' }))).toEqual({ kind: 'backspace' });
  });

  it('types into the command line and starts a tool on its two letters', () => {
    expect(keyIntent(press('w'), state())).toEqual({ kind: 'type', buffer: 'w' });
    expect(keyIntent(press('a'), state({ buffer: 'w' }))).toEqual({ kind: 'tool', id: 'architecture.wall' });
    expect(keyIntent(press('A'), state({ buffer: 'W' }))).toEqual({ kind: 'tool', id: 'architecture.wall' });
    expect(keyIntent(press('x'), state({ buffer: 'w' }))).toEqual({ kind: 'type', buffer: 'wx' });
    // While a command runs, its letters are input, not shortcuts.
    expect(keyIntent(press('a'), state({ buffer: 'w', step: { want: 'point' } }))).toEqual({ kind: 'type', buffer: 'wa' });
    expect(keyIntent(press('@'), state({ step: { want: 'point' } }))).toEqual({ kind: 'type', buffer: '@' });
    expect(keyIntent(press(' '), state({ step: { want: 'text' }, buffer: 'ab' }))).toEqual({ kind: 'type', buffer: 'ab ' });
    expect(keyIntent(press('!'), state())).toBeNull();
  });

  it('submits with Enter or Space, except when a plan gesture or nothing is waiting', () => {
    expect(keyIntent(press('Enter'), state({ buffer: 'line' }))).toEqual({ kind: 'submit' });
    expect(keyIntent(press(' '), state({ hasLast: true }))).toEqual({ kind: 'submit' });
    expect(keyIntent(press('Enter'), state())).toBeNull();
    expect(keyIntent(press('Enter'), state({ hasLast: true, tool: 'architecture.slab' }))).toBeNull();
    expect(keyIntent(press('Enter'), state({ step: { want: 'point' }, tool: 'architecture.slab' }))).toEqual({ kind: 'submit' });
  });

  it('zooms the view in front with + and −, and to the selection with Shift+Z, only while nothing is typed', () => {
    expect(keyIntent(press('+'), state())).toEqual({ kind: 'zoom', factor: 1.25 });
    expect(keyIntent(press('='), state())).toEqual({ kind: 'zoom', factor: 1.25 });
    expect(keyIntent(press('-'), state())).toEqual({ kind: 'zoom', factor: 0.8 });
    expect(keyIntent(press('Z', { shiftKey: true }), state({ selected: ['w1'] }))).toEqual({ kind: 'zoomSelection' });
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
    expect(keyIntent(press('-'), live())).toEqual({ kind: 'zoom', factor: 0.8 });
    expect(keyIntent(press('Tab'), live())).toEqual({ kind: 'liveFocus' });
    expect(keyIntent(press('<'), live('2400'))).toEqual({ kind: 'liveFocus', field: 'ang' });
    expect(keyIntent(press('Enter'), live('2400', true))).toEqual({ kind: 'liveCommit' });
    expect(keyIntent(press('Escape'), live('2400', true))).toEqual({ kind: 'liveClear' });
    expect(keyIntent(press('Backspace'), live('24', true))).toEqual({ kind: 'liveBack' });
    // Something on the command line (@2,3): the digits stay there.
    expect(keyIntent(press('3'), state({ live: { active: true, text: '', typed: false }, buffer: '@2,' }))).toEqual({ kind: 'type', buffer: '@2,3' });
  });

  it('cancels a running command with Escape, toggles with the F keys, picks the hotbar with digits', () => {
    expect(keyIntent(press('Escape'), state({ step: { want: 'point' } }))).toEqual({ kind: 'cancel' });
    expect(keyIntent(press('Escape'), state())).toBeNull();
    expect(keyIntent(press('F8'), state())).toEqual({ kind: 'toggle', what: 'ortho' });
    expect(keyIntent(press('3'), state())).toEqual({ kind: 'hotbar', slot: 3 });
    expect(keyIntent(press('9'), state())).toBeNull();
    expect(keyIntent(press('3'), state({ buffer: '1' }))).toEqual({ kind: 'type', buffer: '13' });
  });
});
