import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { EngineClient } from './client';

/** A WebSocket the test drives by hand: what was sent, and what comes back. */
class FakeSocket {
  static readonly OPEN = 1;
  static readonly CLOSED = 3;
  static all: FakeSocket[] = [];
  readyState = 0;
  binaryType = '';
  sent: { id: number; method: string; params: unknown }[] = [];
  onopen: (() => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onmessage: ((ev: { data: unknown }) => void) | null = null;
  constructor(public readonly url: string) { FakeSocket.all.push(this); }
  send(text: string) { this.sent.push(JSON.parse(text)); }
  close() { this.readyState = FakeSocket.CLOSED; this.onclose?.(); }
  open() { this.readyState = FakeSocket.OPEN; this.onopen?.(); }
  reply(msg: object) { this.onmessage?.({ data: JSON.stringify(msg) }); }
}

async function connected(): Promise<{ client: EngineClient; socket: FakeSocket; status: boolean[] }> {
  const client = new EngineClient(async () => 'ws://test', 50);
  const status: boolean[] = [];
  client.onStatus((c) => status.push(c));
  const started = client.start();
  await Promise.resolve();
  await Promise.resolve();
  const socket = FakeSocket.all[FakeSocket.all.length - 1];
  socket.open();
  await started;
  return { client, socket, status };
}

describe('EngineClient', () => {
  beforeEach(() => {
    FakeSocket.all = [];
    vi.stubGlobal('WebSocket', FakeSocket);
    vi.useFakeTimers();
  });
  afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

  it('matches answers to calls by id and ignores what it did not ask', async () => {
    const { client, socket } = await connected();
    const a = client.call<string>('doc.query', { name: 'a' });
    const b = client.call<string>('doc.query', { name: 'b' });
    expect(socket.sent.map((m) => m.method)).toEqual(['doc.query', 'doc.query']);
    socket.reply({ id: 99, result: 'nobody asked' });
    socket.reply({ id: socket.sent[1].id, result: 'B' });
    socket.reply({ id: socket.sent[0].id, result: 'A' });
    await expect(a).resolves.toBe('A');
    await expect(b).resolves.toBe('B');
    expect(client.waiting).toBe(0);
  });

  it('turns an engine error into a rejection and hands diffs to their listeners', async () => {
    const { client, socket } = await connected();
    const diffs: unknown[] = [];
    client.onDiff((d) => diffs.push(d));
    const call = client.call('doc.execute');
    socket.reply({ id: socket.sent[0].id, error: 'No such command.' });
    await expect(call).rejects.toThrow('No such command.');
    socket.reply({ event: 'diff', data: { version: 3 } });
    expect(diffs).toEqual([{ version: 3 }]);
  });

  it('gives up on a call the engine never answers, and says which', async () => {
    const { client, socket } = await connected();
    const slow = client.call('model.compute', {}, { timeoutMs: 5_000 });
    const quick = client.call('doc.query');
    socket.reply({ id: socket.sent[1].id, result: 1 });
    await expect(quick).resolves.toBe(1);
    const failed = expect(slow).rejects.toThrow(/did not answer model\.compute within 5 s/);
    vi.advanceTimersByTime(5_001);
    await failed;
    expect(client.waiting).toBe(0);
    // A late answer to a call given up on is dropped, not crashed on.
    socket.reply({ id: socket.sent[0].id, result: 'late' });
  });

  it('waits for an edit as long as the connection lives: said to be slow, its late result kept, never failed by the clock', async () => {
    const { client, socket } = await connected();
    const slow: [string, boolean][] = [];
    client.onSlow((method, still) => slow.push([method, still]));
    const edit = client.call<string>('doc.execute', { command: 'big' });
    const ask = client.call('doc.query');
    const askFailed = expect(ask).rejects.toThrow(/did not answer doc\.query within 30 s/);
    vi.advanceTimersByTime(30_001);
    await askFailed;
    expect(slow).toEqual([['doc.execute', true]]);
    vi.advanceTimersByTime(10 * 60_000);
    expect(client.waiting).toBe(1);
    socket.reply({ id: socket.sent[0].id, result: 'done' });
    await expect(edit).resolves.toBe('done');
    expect(slow).toEqual([['doc.execute', true], ['doc.execute', false]]);
    // An edit given a long wait (an import) is said to be slow only after it, and still never failed by the clock.
    const imported = client.call('doc.execute', { command: 'import' }, { timeoutMs: 5 * 60_000 });
    vi.advanceTimersByTime(60_000);
    expect(slow).toHaveLength(2);
    vi.advanceTimersByTime(5 * 60_000);
    expect(slow.at(-1)).toEqual(['doc.execute', true]);
    socket.reply({ id: socket.sent[socket.sent.length - 1].id, result: 'in' });
    await expect(imported).resolves.toBe('in');
    // Undo and redo the same; a connection that drops fails them.
    const undo = client.call('doc.undo');
    vi.advanceTimersByTime(60_000);
    const undoFailed = expect(undo).rejects.toThrow(/closed/);
    socket.close();
    await undoFailed;
    expect(slow.at(-1)).toEqual(['doc.undo', false]);
    client.stop();
  });

  it('refuses calls while disconnected, fails the pending ones when the socket closes, and reconnects', async () => {
    const { client, socket, status } = await connected();
    const pending = client.call('doc.query');
    socket.close();
    await expect(pending).rejects.toThrow(/closed/);
    await expect(client.call('doc.query')).rejects.toThrow(/not connected/);
    expect(status).toEqual([true, false]);
    vi.advanceTimersByTime(60);
    await Promise.resolve();
    await Promise.resolve();
    expect(FakeSocket.all).toHaveLength(2);
    FakeSocket.all[1].open();
    await Promise.resolve();
    expect(status).toEqual([true, false, true]);
    client.stop();
  });
});
