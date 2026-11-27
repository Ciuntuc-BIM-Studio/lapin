import { describe, expect, it } from 'vitest';
import { parseColourBook, parseColourFile, parseColourText, parseSwatchExchange } from './colourBooks';

const utf16 = (s: string) => { const b: number[] = []; for (const ch of s) b.push(0, ch.charCodeAt(0)); return b; };
const u32 = (n: number) => [(n >>> 24) & 255, (n >>> 16) & 255, (n >>> 8) & 255, n & 255];
const u16 = (n: number) => [(n >>> 8) & 255, n & 255];
const str = (s: string) => [...u32(s.length), ...utf16(s)];

function book(space: number, colours: { name: string; ch: number[] }[]): ArrayBuffer {
  const b = [0x38, 0x42, 0x43, 0x42, ...u16(1), ...u16(7), ...str('Test'), ...str(''), ...str(''), ...str(''), ...u16(colours.length), ...u16(7), ...u16(0), ...u16(space)];
  for (const c of colours) b.push(...str(c.name), ...[65, 66, 67, 68, 69, 70], ...c.ch);
  return new Uint8Array(b).buffer;
}

describe('colour books', () => {
  it('reads an RGB colour book in its order, without repeats', () => {
    const data = book(0, [{ name: 'Red', ch: [255, 0, 0] }, { name: 'Blue', ch: [0, 0, 255] }, { name: 'Red again', ch: [255, 0, 0] }]);
    expect(parseColourBook(data)).toEqual(['#ff0000', '#0000ff']);
    expect(parseColourFile(data)).toEqual(['#ff0000', '#0000ff']);
  });

  it('reads CMYK (stored inverted) and Lab books', () => {
    expect(parseColourBook(book(2, [{ name: 'Black', ch: [255, 255, 255, 0] }, { name: 'White', ch: [255, 255, 255, 255] }]))).toEqual(['#000000', '#ffffff']);
    expect(parseColourBook(book(7, [{ name: 'White', ch: [255, 128, 128] }]))).toEqual(['#ffffff']);
  });

  it('gives nothing for a file that is not a book, or a cut one', () => {
    expect(parseColourBook(new Uint8Array([1, 2, 3, 4, 5, 6, 7, 8, 9]).buffer)).toEqual([]);
    expect(parseColourBook(book(0, [{ name: 'Red', ch: [255, 0, 0] }]).slice(0, 40))).toEqual([]);
  });

  it('reads an ASE file', () => {
    const f32 = (x: number) => { const d = new DataView(new ArrayBuffer(4)); d.setFloat32(0, x); return [...new Uint8Array(d.buffer)]; };
    const name = [...u16(4), ...utf16('Sky'), 0, 0];
    const body = [...name, 0x52, 0x47, 0x42, 0x20, ...f32(0), ...f32(0.5), ...f32(1), ...u16(2)];
    const bytes = [0x41, 0x53, 0x45, 0x46, ...u16(1), ...u16(0), ...u32(1), ...u16(1), ...u32(body.length), ...body];
    expect(parseSwatchExchange(new Uint8Array(bytes).buffer)).toEqual(['#0080ff']);
  });
});

describe('colour text', () => {
  it('reads hex values, with or without #, in any separators', () => {
    expect(parseColourText('#FF0000, 00ff00\n#0000ff')).toEqual(['#ff0000', '#00ff00', '#0000ff']);
  });

  it('reads r,g,b rows, with a name or a header beside them', () => {
    expect(parseColourText('name,r,g,b\nRed,255,0,0\nGreen,0,255,0')).toEqual(['#ff0000', '#00ff00']);
  });

  it('reads a flat list of numbers three at a time', () => {
    expect(parseColourText('255,0,0,0,0,255')).toEqual(['#ff0000', '#0000ff']);
  });

  it('reads fractions as shares of 255 and drops what is out of range', () => {
    expect(parseColourText('1,0.5,0')).toEqual(['#ff8000']);
    expect(parseColourText('300,0,0\n0,0,255')).toEqual(['#0000ff']);
  });
});
