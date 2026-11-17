/**
 * Spans typed in one field (issue #83): the Axis grid's spacings and the storeys' heights of New levels read them alike.
 * Spans are separated by spaces (or semicolons); `N*span` stands for N equal spans, and the two mix: `100 2*300 200`.
 * Each span is a length in the project's units unless it names its own (`5.4m`, `20'`), with a comma or a dot for the
 * decimals; a feet-and-inches length keeps its own spaces (`20' 6"`). One parser, so both fields take the same text.
 */

import { parseLength, type Unit } from '../../engine/unitsStore';

/** The most equal spans one `N*` stands for. */
export const MAX_REPEAT = 200;

export type SpansRead =
  | { ok: true; spans: number[] }
  /** What is wrong (an interface phrase) and the piece of the text it is about. */
  | { ok: false; error: string; token: string };

/** The pieces of a spans field: spaces and semicolons split them, `N * span` is one piece, and a feet piece takes its inches. */
export function spanTokens(text: string): string[] {
  const t = text.replace(/[″]/g, '"').replace(/[′]/g, "'").replace(/(^|[\s;])(\d+)\s*[*x×]\s*/gi, '$1$2*');
  const raw = t.split(/[\s;]+/).filter(Boolean);
  const out: string[] = [];
  for (let i = 0; i < raw.length; i++) {
    let piece = raw[i];
    // 20' 6" or 20' 6 1/2": the inches after the feet are the same length.
    if (piece.endsWith("'")) {
      const end = raw.findIndex((r, j) => j > i && r.endsWith('"'));
      if (end > i && raw.slice(i + 1, end + 1).every((r) => /^[\d./]+"?$/.test(r)) && end - i <= 2) {
        piece = [piece, ...raw.slice(i + 1, end + 1)].join(' ');
        i = end;
      }
    }
    out.push(piece);
  }
  return out;
}

/** Spans typed in one field, in metres: `6000 6000 4500`, `3*6000`, `100 2*300 200`, `5,4 3*3.0`; or what does not read. */
export function parseSpans(text: string, u?: Unit): SpansRead {
  const spans: number[] = [];
  for (const token of spanTokens(text)) {
    const m = /^(\d+)\*(.*)$/.exec(token);
    const count = m ? Number(m[1]) : 1;
    if (m && !(count >= 1 && count <= MAX_REPEAT)) return { ok: false, error: 'The count before * is a whole number from 1 to 200:', token };
    const body = m ? m[2] : token;
    const v = body ? parseLength(body, u) : null;
    if (v == null || !Number.isFinite(v)) return { ok: false, error: 'Not a length:', token };
    if (!(v > 0)) return { ok: false, error: 'A span is a length above zero:', token };
    for (let i = 0; i < count; i++) spans.push(v);
  }
  return { ok: true, spans };
}

/** The message of a spans field that does not read, naming its piece: `Not a length: abc`. */
export const spansError = (r: SpansRead): string | null => (r.ok ? null : `${r.error} ${r.token}`);
