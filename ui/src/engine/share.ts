/**
 * Working together (docs/plan-multiplayer.md). The host shares the open
 * project from the desktop app (its engine connects out to a relay); a guest
 * joins with the invitation, from the desktop app or the browser, as a thin
 * client: this interface talks to the host's engine instead of a local one,
 * through the relay, every frame sealed with a key drawn from the
 * invitation's secret (the relay never has it). Presence — who is in, where
 * each looks, what each has selected — goes round beside the model.
 *
 * The crypto mirrors the engine's ShareCrypto: HKDF-SHA256 over the secret,
 * salted with the room and the connection's own salt, gives an AES-256-GCM
 * key; a frame is [direction 4][count 8][ciphertext + tag], its plaintext
 * [kind 1][body] (1: JSON text, 2: an engine frame).
 */

import { create } from 'zustand';
import type { EngineLink, EngineTransport } from './client';

export const PROTOCOL = 1;
export const DEFAULT_RELAY = 'wss://lapin.ciuntucbimstudio.ro/relay';

export interface Participant {
  id: string; name: string; color: string; role: 'owner' | 'editor' | 'viewer'; host: boolean;
  presence: Presence;
}
export interface Presence { view?: string; storeyId?: string | null; planViewId?: string | null; cursor?: [number, number] | null; selection?: string[] }
export interface ShareStatus { active: boolean; code: string | null; link: string | null; role: 'editor' | 'viewer'; host: string; relay: string; guests: Participant[] }
export interface Welcome { id: string; name: string; color: string; role: 'editor' | 'viewer'; token: string; host: string; app: string; product: string; modules: string[] }

interface ShareState {
  /** The participants now (the host first), while the project is shared or joined. */
  participants: Participant[];
  /** The host's own sharing, as its engine last said. */
  status: ShareStatus | null;
  /** A guest's welcome: who they are in the host's session. */
  welcome: Welcome | null;
  /** Why a guest's session ended (the host stopped, asked them to leave, another version). */
  ended: string | null;
}

export const useShare = create<ShareState>(() => ({ participants: [], status: null, welcome: null, ended: null }));

// ------------------------------------------------------------ the invitation

export interface Invitation { room: string; secret: Uint8Array; relay: string; code: string }

const unb64url = (s: string) => Uint8Array.from(atob(s.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - (s.length % 4)) % 4)), (c) => c.charCodeAt(0));

/** An invitation from a link (…#join=<code>) or its code alone; null when it is neither. */
export function parseInvitation(text: string): Invitation | null {
  const code = (text.match(/[#&?]join=([^&\s]+)/)?.[1] ?? text).trim();
  const [room, secret, relay] = code.split('.');
  if (!room || !/^[a-z0-9]{8,32}$/i.test(room) || !secret) return null;
  try {
    const key = unb64url(secret);
    if (key.length < 16) return null;
    return { room, secret: key, relay: relay ? new TextDecoder().decode(unb64url(relay)) : DEFAULT_RELAY, code };
  } catch { return null; }
}

const JOIN_KEY = 'lapin.join';
const NAME_KEY = 'lapin.name';

/** The invitation this window joins with: from its address (the link opened), else from a join made before a reload. */
export function currentInvitation(): Invitation | null {
  const fromUrl = typeof location !== 'undefined' ? parseInvitation(location.hash) : null;
  if (fromUrl) { try { sessionStorage.setItem(JOIN_KEY, fromUrl.code); } catch { /* this window only */ } return fromUrl; }
  try { const kept = sessionStorage.getItem(JOIN_KEY); return kept ? parseInvitation(kept) : null; } catch { return null; }
}

export const isGuest = (): boolean => joined !== null;
const joined = currentInvitation();
export const invitation = (): Invitation | null => joined;

export function savedName(): string {
  try { return localStorage.getItem(NAME_KEY) ?? ''; } catch { return ''; }
}
export function saveName(name: string): void {
  try { localStorage.setItem(NAME_KEY, name.trim()); } catch { /* not kept */ }
}

/** Join with an invitation: this window becomes the host's guest (it reloads into their session). */
export function join(inv: Invitation, name: string): void {
  saveName(name);
  try { sessionStorage.setItem(JOIN_KEY, inv.code); } catch { /* kept in the address */ }
  location.hash = `join=${inv.code}`;
  location.reload();
}

/** Leave the host's session: back to this computer's own engine. */
export function leave(): void {
  try { sessionStorage.removeItem(JOIN_KEY); sessionStorage.removeItem(`lapin.token.${joined?.room}`); } catch { /* gone with the window */ }
  history.replaceState(null, '', location.pathname + location.search);
  location.reload();
}

// ------------------------------------------------------------ the crypto

async function connectionKey(inv: Invitation, salt: Uint8Array): Promise<CryptoKey> {
  const base = await crypto.subtle.importKey('raw', inv.secret as BufferSource, 'HKDF', false, ['deriveKey']);
  const room = new TextEncoder().encode(inv.room);
  const s = new Uint8Array(room.length + salt.length);
  s.set(room); s.set(salt, room.length);
  return crypto.subtle.deriveKey({ name: 'HKDF', hash: 'SHA-256', salt: s, info: new TextEncoder().encode('lapin-share-v1') }, base, { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
}

/** One connection's sealing and opening, as a guest: out on direction 0, in on 1, counts rising. */
export class GuestCrypto {
  private sent = 0n;
  private received = 0n;
  constructor(private readonly key: CryptoKey) {}

  static async create(inv: Invitation, salt: Uint8Array): Promise<GuestCrypto> { return new GuestCrypto(await connectionKey(inv, salt)); }

  async seal(kind: number, body: Uint8Array): Promise<Uint8Array> {
    const nonce = new Uint8Array(12);
    const v = new DataView(nonce.buffer);
    v.setUint32(0, 0);
    v.setBigUint64(4, ++this.sent);
    const plain = new Uint8Array(1 + body.length);
    plain[0] = kind; plain.set(body, 1);
    const sealed = new Uint8Array(await crypto.subtle.encrypt({ name: 'AES-GCM', iv: nonce }, this.key, plain));
    const frame = new Uint8Array(12 + sealed.length);
    frame.set(nonce); frame.set(sealed, 12);
    return frame;
  }

  async open(frame: Uint8Array): Promise<Uint8Array | null> {
    if (frame.length < 12 + 16 + 1) return null;
    const v = new DataView(frame.buffer, frame.byteOffset, 12);
    if (v.getUint32(0) !== 1) return null;
    const count = v.getBigUint64(4);
    if (count <= this.received) return null;
    try {
      const plain = new Uint8Array(await crypto.subtle.decrypt({ name: 'AES-GCM', iv: frame.slice(0, 12) }, this.key, frame.slice(12)));
      this.received = count;
      return plain;
    } catch { return null; }
  }
}

// ------------------------------------------------------------ the guest's transport

/**
 * The host's engine through the relay, as a guest: the connection's salt
 * first, then hello (name, protocol, the token of an earlier connection so
 * one comes back as the same participant), the welcome, then the engine
 * protocol as with a local engine. Frames are opened in order (decryption is
 * asynchronous; a queue keeps them as they came).
 */
export function relayTransport(inv: Invitation, name: () => string): EngineTransport {
  let fatal = false;
  // Once in: how many times in a row the session could not be reached again (the host gone without a word).
  let welcomedOnce = false;
  let misses = 0;
  return {
    get retries() { return !fatal; },
    connect: ({ onOpen, onMessage, onClose }) => new Promise<void>((resolve, reject) => {
      const ws = new WebSocket(`${inv.relay.replace(/\/$/, '')}/join?room=${inv.room}`);
      ws.binaryType = 'arraybuffer';
      const salt = crypto.getRandomValues(new Uint8Array(16));
      let box: GuestCrypto | null = null;
      let welcomed = false;
      let queue = Promise.resolve();
      const tokenKey = `lapin.token.${inv.room}`;
      const fail = (why: string, final = false) => {
        if (final) { fatal = true; useShare.setState({ ended: why }); }
        if (!welcomed && welcomedOnce && !final && ++misses >= 8) {
          fatal = true;
          useShare.setState({ ended: 'The session closed: the host stopped sharing, quit Lapin or lost the connection.' });
        }
        if (!welcomed) reject(new Error(why));
        try { ws.close(); } catch { /* closed */ }
      };
      const sendText = async (text: string) => { if (box && ws.readyState === WebSocket.OPEN) ws.send(await box.seal(1, new TextEncoder().encode(text))); };
      ws.onopen = async () => {
        ws.send(new Uint8Array([0, ...salt]));
        box = await GuestCrypto.create(inv, salt);
        let token: string | null = null;
        try { token = sessionStorage.getItem(tokenKey); } catch { /* new each time */ }
        await sendText(JSON.stringify({ hello: { name: name() || 'Guest', protocol: PROTOCOL, token } }));
      };
      ws.onmessage = (ev) => {
        const data = new Uint8Array(ev.data as ArrayBuffer);
        queue = queue.then(async () => {
          const plain = box ? await box.open(data) : null;
          if (!plain) { fail('A frame from the session did not open: the link may be wrong.'); return; }
          if (plain[0] === 2) { onMessage(plain.slice(1).buffer); return; }
          const text = new TextDecoder().decode(plain.slice(1));
          if (!welcomed) {
            const msg = JSON.parse(text) as { welcome?: Welcome; refused?: string };
            if (msg.refused) { fail(msg.refused, true); return; }
            if (!msg.welcome) return;
            welcomed = true;
            welcomedOnce = true;
            misses = 0;
            try { sessionStorage.setItem(tokenKey, msg.welcome.token); } catch { /* a new participant next time */ }
            useShare.setState({ welcome: msg.welcome, ended: null });
            const link: EngineLink = { send: (t) => void (queue = queue.then(() => sendText(t))), close: () => ws.close() };
            onOpen(link);
            resolve();
            return;
          }
          const ended = text.startsWith('{"event":"ended"') ? (JSON.parse(text) as { data: string }).data : null;
          if (ended) { fatal = true; useShare.setState({ ended }); }
          onMessage(text);
        });
      };
      ws.onerror = () => { if (!welcomed) fail('The session could not be reached: the host may have stopped sharing.'); };
      ws.onclose = () => {
        if (!welcomed) { fail('The session is not open: the host may have stopped sharing.'); return; }
        onClose();
      };
    }),
  };
}

// ------------------------------------------------------------ presence

let last = '';
let timer: ReturnType<typeof setTimeout> | null = null;
let pending: Presence = {};
let sender: ((p: Presence) => void) | null = null;

/** Where presence goes (the engine client, once connected). */
export function setPresenceSender(send: (p: Presence) => void): void { sender = send; }

/** What this participant shows the others, merged and sent at most ten times a second, only while others are there. */
export function reportPresence(p: Presence): void {
  pending = { ...pending, ...p };
  if (timer || !sender) return;
  timer = setTimeout(() => {
    timer = null;
    const others = useShare.getState().participants.length > 1;
    const text = JSON.stringify(pending);
    if (!others || text === last) return;
    last = text;
    sender?.(pending);
  }, 100);
}

/** The others (not this window's participant), for drawing their cursors and selections. */
export function others(list: Participant[], welcome: Welcome | null): Participant[] {
  const me = welcome?.id ?? 'me';
  return list.filter((p) => p.id !== me);
}
