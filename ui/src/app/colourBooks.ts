/**
 * Colours from a file (forum report 196): an Adobe / Autodesk colour book (.acb, "8BCB": RGB, CMYK or Lab), an Adobe swatch
 * exchange file (.ase) or text (.csv, .txt: `#rrggbb` values, `r,g,b` rows, or a flat list of numbers read three at a time).
 * Written after the formats' public descriptions; the result is a list of `#rrggbb`, in the file's order, no repeats.
 */

import { cleanColours } from './colours';

const hex2 = (v: number) => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0');
const rgbHex = (r: number, g: number, b: number) => `#${hex2(r)}${hex2(g)}${hex2(b)}`;

/** CMYK in 0…1 as #rrggbb (the plain conversion; a profile is not applied). */
function cmykHex(c: number, m: number, y: number, k: number): string {
  return rgbHex(255 * (1 - c) * (1 - k), 255 * (1 - m) * (1 - k), 255 * (1 - y) * (1 - k));
}

/** CIE Lab (D50, as Adobe's) as #rrggbb. */
function labHex(L: number, a: number, b: number): string {
  const fy = (L + 16) / 116, fx = fy + a / 500, fz = fy - b / 200;
  const inv = (t: number) => (t ** 3 > 0.008856 ? t ** 3 : (t - 16 / 116) / 7.787);
  const X = 0.9642 * inv(fx), Y = inv(fy), Z = 0.8249 * inv(fz);
  const lin = [3.1338561 * X - 1.6168667 * Y - 0.4906146 * Z, -0.9787684 * X + 1.9161415 * Y + 0.033454 * Z, 0.0719453 * X - 0.2289914 * Y + 1.4052427 * Z];
  const g = (v: number) => 255 * (v <= 0.0031308 ? 12.92 * Math.max(v, 0) : 1.055 * Math.max(v, 0) ** (1 / 2.4) - 0.055);
  return rgbHex(g(lin[0]), g(lin[1]), g(lin[2]));
}

/** A colour book (.acb): header, four strings, then the colours (name, 6-byte code, 3 or 4 channel bytes). */
export function parseColourBook(data: ArrayBuffer): string[] {
  const v = new DataView(data);
  if (v.byteLength < 8 || v.getUint32(0) !== 0x38424342) return []; // "8BCB"
  let at = 8; // signature, version, book id
  const text = () => { const n = v.getUint32(at); at += 4 + n * 2; };
  try {
    for (let i = 0; i < 4; i++) text(); // title, prefix, suffix, description
    const count = v.getUint16(at);
    const space = v.getUint16(at + 6);
    at += 8; // count, page size, page key offset, colour space
    const channels = space === 2 ? 4 : 3;
    if (space !== 0 && space !== 2 && space !== 7) return [];
    const out: string[] = [];
    for (let i = 0; i < count; i++) {
      text(); // the colour's name
      at += 6; // its code
      const c = [...Array(channels).keys()].map((k) => v.getUint8(at + k));
      at += channels;
      if (space === 0) out.push(rgbHex(c[0], c[1], c[2]));
      else if (space === 2) out.push(cmykHex(1 - c[0] / 255, 1 - c[1] / 255, 1 - c[2] / 255, 1 - c[3] / 255));
      else out.push(labHex((c[0] * 100) / 255, c[1] - 128, c[2] - 128));
    }
    return cleanColours(out);
  } catch { return []; }
}

/** A swatch exchange file (.ase): blocks; colour entries hold a name and 3 (RGB, Lab) or 4 (CMYK) or 1 (Gray) floats. */
export function parseSwatchExchange(data: ArrayBuffer): string[] {
  const v = new DataView(data);
  if (v.byteLength < 12 || v.getUint32(0) !== 0x41534546) return []; // "ASEF"
  const blocks = v.getUint32(8);
  let at = 12;
  const out: string[] = [];
  try {
    for (let i = 0; i < blocks && at + 6 <= v.byteLength; i++) {
      const type = v.getUint16(at), len = v.getUint32(at + 2);
      const body = at + 6;
      at = body + len;
      if (type !== 0x0001) continue;
      let p = body + 2 + v.getUint16(body) * 2; // the name
      const model = String.fromCharCode(v.getUint8(p), v.getUint8(p + 1), v.getUint8(p + 2), v.getUint8(p + 3)).trim();
      p += 4;
      const f = (k: number) => v.getFloat32(p + k * 4);
      if (model === 'RGB') out.push(rgbHex(f(0) * 255, f(1) * 255, f(2) * 255));
      else if (model === 'CMYK') out.push(cmykHex(f(0), f(1), f(2), f(3)));
      else if (model === 'LAB') out.push(labHex(f(0) * 100, f(1), f(2)));
      else if (model === 'Gray') out.push(rgbHex(f(0) * 255, f(0) * 255, f(0) * 255));
    }
  } catch { /* what was read stays */ }
  return cleanColours(out);
}

/** Text: `#rrggbb` / `rrggbb` words, else numbers (0–255) read three at a time (rows of `r,g,b`, or one long list). */
export function parseColourText(text: string): string[] {
  const words = text.split(/[\s,;]+/).filter(Boolean);
  const hexes = words.filter((w) => /^#?[0-9a-f]{6}$/i.test(w) && (w.startsWith('#') || /[a-f]/i.test(w)));
  if (hexes.length && hexes.length >= words.length / 2) return cleanColours(hexes.map((w) => (w.startsWith('#') ? w : `#${w}`)));
  // Each row's own numbers (a name or a header beside them is ignored); three on every row make a colour each, else the whole list is read three at a time.
  const rows = text.split(/\r?\n/).map((r) => r.split(/[\s,;]+/).filter((w) => w !== '' && Number.isFinite(Number(w))).map(Number)).filter((r) => r.length > 0);
  const use: number[][] = [];
  if (rows.length && rows.every((r) => r.length === 3)) use.push(...rows);
  else { const nums = rows.flat(); for (let i = 0; i + 2 < nums.length; i += 3) use.push(nums.slice(i, i + 3)); }
  // Fractions (0–1, some not whole) are shares of 255.
  const unit = use.length && use.every((t) => t.every((n) => n >= 0 && n <= 1)) && use.some((t) => t.some((n) => n % 1 !== 0)) ? 255 : 1;
  return cleanColours(use.filter((t) => t.every((n) => n >= 0 && n * unit <= 255)).map((t) => rgbHex(t[0] * unit, t[1] * unit, t[2] * unit)));
}

/** The colours of a file by what it is: its start says a colour book or swatch exchange file, else text. */
export function parseColourFile(data: ArrayBuffer): string[] {
  const head = new Uint8Array(data, 0, Math.min(4, data.byteLength));
  const sig = String.fromCharCode(...head);
  if (sig === '8BCB') return parseColourBook(data);
  if (sig === 'ASEF') return parseSwatchExchange(data);
  return parseColourText(new TextDecoder().decode(data));
}
