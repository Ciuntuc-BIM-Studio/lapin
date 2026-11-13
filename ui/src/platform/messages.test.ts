import { describe, expect, it } from 'vitest';
import { addMessages, countsOf, filterMessages, messagesText, readStrip, replaceGroup, type MessageInput } from './messages';

const err = (text: string, extra: Partial<MessageInput> = {}): MessageInput => ({ kind: 'error', source: 'Node', text, ...extra });

describe('the messages log', () => {
  it('counts a repeat in a row instead of listing it again', () => {
    let r = addMessages([], [err('boom'), err('boom'), err('boom')], 1000, 1);
    expect(r.list).toHaveLength(1);
    expect(r.list[0]).toMatchObject({ runs: 3, time: 1000 });
    r = addMessages(r.list, [err('other'), err('boom')], 2000, r.nextId);
    expect(r.list.map((m) => [m.text, m.runs])).toEqual([['boom', 3], ['other', 1], ['boom', 1]]);
  });

  it('keeps no more than the cap, the newest', () => {
    const r = addMessages([], Array.from({ length: 12 }, (_, i) => err(`e${i}`)), 0, 1, 5);
    expect(r.list.map((m) => m.text)).toEqual(['e7', 'e8', 'e9', 'e10', 'e11']);
  });

  it('replaces a group by its new rows; a row that stays keeps its id and time and is not fresh', () => {
    const first = replaceGroup([], 'evaluation', [err('a'), err('b')], 100, 1);
    expect(first.fresh).toHaveLength(2);
    const info = addMessages(first.list, [{ kind: 'info', source: 'Bake', text: 'done' }], 150, first.nextId);
    const second = replaceGroup(info.list, 'evaluation', [err('b'), err('c')], 200, info.nextId);
    expect(second.list.map((m) => m.text)).toEqual(['done', 'b', 'c']);
    expect(second.fresh.map((m) => m.text)).toEqual(['c']);
    expect(second.list.find((m) => m.text === 'b')).toMatchObject({ time: 100, id: first.list[1]!.id });
    expect(replaceGroup(second.list, 'evaluation', [], 300, second.nextId).list.map((m) => m.text)).toEqual(['done']);
  });

  it('filters and counts', () => {
    const list = addMessages([], [err('x'), { kind: 'warning', source: '', text: 'w' }, { kind: 'info', source: '', text: 'i' }], 0, 1).list;
    expect(countsOf(list)).toEqual({ errors: 1, warnings: 1, all: 3 });
    expect(filterMessages(list, 'errors').map((m) => m.text)).toEqual(['x']);
    expect(filterMessages(list, 'warnings').map((m) => m.text)).toEqual(['w']);
    expect(filterMessages(list, 'all')).toHaveLength(3);
  });

  it('writes plain text, a line a message', () => {
    const list = addMessages([], [err('boom'), err('boom'), { kind: 'info', source: '', text: 'ok' }], new Date(2026, 10, 13, 9, 5, 7).getTime(), 1).list;
    expect(messagesText(list)).toBe('09:05:07  error    Node: boom (x2)\n09:05:07  info     ok');
  });

  it('reads the remembered state, anything wrong being the default', () => {
    expect(readStrip(null)).toEqual({ open: false, height: 150, filter: 'all' });
    expect(readStrip('{"open":true,"height":9999,"filter":"errors"}')).toEqual({ open: true, height: 600, filter: 'errors' });
    expect(readStrip('not json')).toEqual({ open: false, height: 150, filter: 'all' });
    expect(readStrip('{"height":"x","filter":"zzz"}').filter).toBe('all');
  });
});
