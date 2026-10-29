/**
 * The engine's binary frames (engine/src/Host/Lapin.Host/BinaryFrame.cs):
 * "LPF1", the JSON header's length (uint32 LE), the header, then the buffers.
 * An array in the header is `{"$buf":offset,"n":count,"t":"f32"|"i32"}`; it
 * becomes a typed-array view on the frame, no copy. A small one is copied: a
 * view keeps its whole frame alive, and an instance's matrix (12 numbers) or
 * an empty list would keep a 20 MB part of a big import for as long as the
 * element lasts (docs/plan-instancing.md).
 */

const MARK = [0x4c, 0x50, 0x46, 0x31]; // "LPF1"
/** Arrays up to this many numbers are copied out of the frame. */
const COPY_UP_TO = 64;
/** Every empty array, one for all (nothing is written into an engine's array). */
const NO_FLOATS = new Float32Array(0);
const NO_INTS = new Int32Array(0);

interface BufRef { $buf: number; n: number; t: 'f32' | 'i32' }

const isRef = (v: unknown): v is BufRef =>
  typeof v === 'object' && v !== null && typeof (v as BufRef).$buf === 'number' && typeof (v as BufRef).n === 'number' && typeof (v as BufRef).t === 'string';

export function decodeFrame<T = unknown>(frame: ArrayBuffer): T {
  const bytes = new Uint8Array(frame);
  if (bytes.length < 8 || MARK.some((b, i) => bytes[i] !== b)) throw new Error('Not a Lapin frame.');
  const headerLength = new DataView(frame).getUint32(4, true);
  const base = 8 + headerLength;
  const text = new TextDecoder().decode(bytes.subarray(8, base));
  return JSON.parse(text, (_key, value: unknown) => {
    if (!isRef(value)) return value;
    if (value.n === 0 && COPY_UP_TO >= 0) return value.t === 'f32' ? NO_FLOATS : NO_INTS;
    if (value.t === 'f32') { const v = new Float32Array(frame, base + value.$buf, value.n); return value.n <= COPY_UP_TO ? v.slice() : v; }
    if (value.t === 'i32') { const v = new Int32Array(frame, base + value.$buf, value.n); return value.n <= COPY_UP_TO ? v.slice() : v; }
    throw new Error(`Unknown buffer type ${String(value.t)}.`);
  }) as T;
}

/** A frame as the engine writes it (for tests). */
export function encodeFrame(header: unknown, buffers: ArrayBufferView[]): ArrayBuffer {
  let text = JSON.stringify(header);
  while (new TextEncoder().encode(text).length % 4) text += ' ';
  const head = new TextEncoder().encode(text);
  const total = buffers.reduce((s, b) => s + b.byteLength, 0);
  const out = new Uint8Array(8 + head.length + total);
  out.set(MARK, 0);
  new DataView(out.buffer).setUint32(4, head.length, true);
  out.set(head, 8);
  let at = 8 + head.length;
  for (const b of buffers) { out.set(new Uint8Array(b.buffer, b.byteOffset, b.byteLength), at); at += b.byteLength; }
  return out.buffer;
}
