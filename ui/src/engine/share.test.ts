/** A guest's side of a shared session: the key of both salts, and the relay transport's handshake and queues, with the socket faked. */

import { createDecipheriv, createCipheriv, hkdfSync } from 'node:crypto';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const notices: (string | null)[] = [];
vi.mock('../app/store', () => ({ notify: (m: string | null) => notices.push(m) }));

import { GuestCrypto, PROTOCOL, parseInvitation, relayTransport, useShare, type Invitation } from './share';

const inv = parseInvitation('abcdefgh23.AAECAwQFBgcICQoLDA0ODw')!;

/** The host's key, drawn as the engine draws it (HKDF-SHA256, salt room ‖ guest's ‖ host's, info lapin-share-v2). */
function hostKey(i: Invitation, guestSalt: Uint8Array, hostSalt: Uint8Array): Buffer {
  const salt = Buffer.concat([Buffer.from(i.room), guestSalt, hostSalt]);
  return Buffer.from(hkdfSync('sha256', i.secret, salt, 'lapin-share-v2', 32));
}

/** The host's frame: [direction 1][count][ciphertext + tag] of [kind][body]. */
function hostSeal(key: Buffer, count: number, kind: number, body: Uint8Array): Uint8Array {
  const nonce = Buffer.alloc(12);
  nonce.writeUInt32BE(1, 0);
  nonce.writeBigUInt64BE(BigInt(count), 4);
  const c = createCipheriv('aes-256-gcm', key, nonce);
  const sealed = Buffer.concat([c.update(Buffer.from([kind, ...body])), c.final(), c.getAuthTag()]);
  return new Uint8Array(Buffer.concat([nonce, sealed]));
}

function hostOpen(key: Buffer, frame: Uint8Array): string {
  const b = Buffer.from(frame);
  const d = createDecipheriv('aes-256-gcm', key, b.subarray(0, 12));
  d.setAuthTag(b.subarray(b.length - 16));
  const plain = Buffer.concat([d.update(b.subarray(12, b.length - 16)), d.final()]);
  return plain.subarray(1).toString('utf8');
}

class FakeSocket {
  static readonly OPEN = 1;
  static all: FakeSocket[] = [];
  readyState = 0;
  binaryType = '';
  sent: Uint8Array[] = [];
  onopen: (() => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onmessage: ((ev: { data: ArrayBuffer }) => void) | null = null;
  constructor(public readonly url: string) { FakeSocket.all.push(this); }
  send(data: Uint8Array) { this.sent.push(new Uint8Array(data)); }
  close() { if (this.readyState === 3) return; this.readyState = 3; this.onclose?.(); }
  open() { this.readyState = FakeSocket.OPEN; this.onopen?.(); }
  deliver(frame: Uint8Array) { this.onmessage?.({ data: frame.slice().buffer }); }
}

/** Until what the asynchronous crypto does has happened (2 s at most). */
async function until(done: () => boolean): Promise<void> {
  for (let i = 0; i < 200 && !done(); i++) await new Promise((r) => setTimeout(r, 10));
  expect(done()).toBe(true);
}

describe('the guest crypto', () => {
  it('seals with the key of both salts, as the host draws it', async () => {
    const g = crypto.getRandomValues(new Uint8Array(16));
    const h = crypto.getRandomValues(new Uint8Array(16));
    const box = await GuestCrypto.create(inv, g, h);
    const frame = await box.seal(1, new TextEncoder().encode('{"id":1}'));
    expect(hostOpen(hostKey(inv, g, h), frame)).toBe('{"id":1}');
    // Another host salt (a new connection, the same guest salt played again): the frame does not open.
    expect(() => hostOpen(hostKey(inv, g, crypto.getRandomValues(new Uint8Array(16))), frame)).toThrow();
    const plain = await box.open(hostSeal(hostKey(inv, g, h), 1, 1, new TextEncoder().encode('hi')));
    expect(new TextDecoder().decode(plain!.slice(1))).toBe('hi');
    // Replayed: refused.
    expect(await box.open(hostSeal(hostKey(inv, g, h), 1, 1, new TextEncoder().encode('hi')))).toBeNull();
  });
});

describe('the relay transport', () => {
  beforeEach(() => {
    FakeSocket.all = [];
    notices.length = 0;
    useShare.setState({ welcome: null, ended: null });
    vi.stubGlobal('WebSocket', FakeSocket);
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });
  afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); });

  /** Joins: the clear salts, the sealed hello, the welcome. */
  async function joined(onMessage: (d: ArrayBuffer | string) => void) {
    const t = relayTransport(inv, () => 'Ana');
    let link: { send(t: string): void } | null = null;
    const connected = t.connect({ onOpen: (l) => { link = l; }, onMessage, onClose: () => undefined });
    const ws = FakeSocket.all[0];
    ws.open();
    const opening = ws.sent[0];
    expect([...opening.slice(0, 2)]).toEqual([0, PROTOCOL]);
    expect(opening.length).toBe(18);
    const hostSalt = crypto.getRandomValues(new Uint8Array(16));
    ws.deliver(new Uint8Array([0, ...hostSalt]));
    await until(() => ws.sent.length > 1);
    const key = hostKey(inv, opening.slice(2), hostSalt);
    expect(JSON.parse(hostOpen(key, ws.sent[1])).hello.protocol).toBe(PROTOCOL);
    const welcome = { welcome: { id: 'p1', name: 'Ana', color: '#000', role: 'editor', token: 't', host: 'H', app: '0', product: 'lapin', modules: [] } };
    ws.deliver(hostSeal(key, 1, 1, new TextEncoder().encode(JSON.stringify(welcome))));
    await connected;
    return { ws, key, link: link! };
  }

  it('one frame that fails does not stop the frames after it, nor the calls', async () => {
    const got: string[] = [];
    const { ws, key, link } = await joined((d) => {
      if (d === 'boom') throw new Error('a listener failed');
      got.push(String(d));
    });
    ws.deliver(hostSeal(key, 2, 1, new TextEncoder().encode('boom')));
    ws.deliver(hostSeal(key, 3, 1, new TextEncoder().encode('after')));
    await until(() => got.length > 0);
    expect(got).toEqual(['after']);
    expect(notices).toContain('A frame from the session could not be read; the session goes on.');
    // Calls go on their own chain, after the failure too.
    const before = ws.sent.length;
    link.send('{"id":1}');
    await until(() => ws.sent.length > before);
    expect(hostOpen(key, ws.sent[ws.sent.length - 1])).toBe('{"id":1}');
  });

  it('a key that cannot be made fails the join instead of leaving it hanging', async () => {
    vi.spyOn(crypto.subtle, 'importKey').mockRejectedValueOnce(new Error('no WebCrypto here'));
    const t = relayTransport(inv, () => 'Ana');
    const connected = t.connect({ onOpen: () => undefined, onMessage: () => undefined, onClose: () => undefined });
    const ws = FakeSocket.all[0];
    ws.open();
    ws.deliver(new Uint8Array([0, ...crypto.getRandomValues(new Uint8Array(16))]));
    await expect(connected).rejects.toThrow(/no WebCrypto here/);
  });

  it('a host of another protocol: told to update, not retried', async () => {
    const t = relayTransport(inv, () => 'Ana');
    const connected = t.connect({ onOpen: () => undefined, onMessage: () => undefined, onClose: () => undefined });
    const ws = FakeSocket.all[0];
    ws.open();
    ws.deliver(new Uint8Array([1, 3]));
    await expect(connected).rejects.toThrow(/update Lapin/);
    expect(t.retries).toBe(false);
    expect(useShare.getState().ended).toMatch(/update Lapin/);
  });
});
