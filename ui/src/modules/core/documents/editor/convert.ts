/**
 * A document's blocks as kept (docs/plan-documents.md, Blocurile) and the editor's ProseMirror document, both ways, pure (no
 * schema, no DOM: TipTap's JSON). Headings, paragraphs and lists are text with the blocks' markup read into marks and nodes:
 * `**bold**`, `*italic*` and `{{key}}` as an inline `field` node; tables, pictures, key–value fields, inputs, contents and page
 * breaks are atoms (`docAtom`) holding their block whole; a repeat and an if are containers (`docRepeat`, `docIf`) of one or two
 * zones (`docZone`: `main` their blocks, `alt` the repeat's `empty` or the if's `else`). Every key of a block the editor does not
 * show is kept on its node (`src`), so a document read and written back without an edit is the same.
 */

/** A document block as kept: a kind and its own keys. */
export type Block = { kind: string; [key: string]: unknown };

/** TipTap's JSON of a node. */
export interface PmNode {
  type: string;
  attrs?: Record<string, unknown>;
  content?: PmNode[];
  text?: string;
  marks?: { type: string; attrs?: Record<string, unknown> }[];
}

/** The blocks held whole as atoms. */
export const ATOM_KINDS = ['table', 'image', 'fields', 'input', 'toc', 'pageBreak'] as const;

const clone = <T,>(v: T): T => (v == null ? v : JSON.parse(JSON.stringify(v)) as T);

/** A block's keys but those the node's content holds. */
function srcOf(b: Block, ...omit: string[]): Record<string, unknown> {
  const o: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(b)) if (k !== 'kind' && !omit.includes(k)) o[k] = clone(v);
  return o;
}

// ─── Text ↔ inline nodes ───────────────────────────────────────────────────

/** Text with the blocks' markup as inline nodes: text with bold and italic marks, a field node for each {{key}}. */
export function textToInline(text: string | null | undefined): PmNode[] {
  const out: PmNode[] = [];
  if (!text) return out;
  let bold = false, italic = false, buf = '';
  const marks = () => [...(bold ? [{ type: 'bold' }] : []), ...(italic ? [{ type: 'italic' }] : [])];
  const withMarks = (n: PmNode): PmNode => { const m = marks(); if (m.length) n.marks = m; return n; };
  const flush = () => { if (buf) out.push(withMarks({ type: 'text', text: buf })); buf = ''; };
  for (let i = 0; i < text.length; i++) {
    if (text[i] === '{' && text[i + 1] === '{') {
      const end = text.indexOf('}}', i + 2);
      if (end > 0) {
        flush();
        out.push(withMarks({ type: 'field', attrs: { key: text.slice(i + 2, end).trim() } }));
        i = end + 1;
        continue;
      }
    }
    if (text[i] === '*') {
      flush();
      if (text[i + 1] === '*') { bold = !bold; i++; } else italic = !italic;
      continue;
    }
    buf += text[i];
  }
  flush();
  return out;
}

/** Inline nodes as text with the markup: marks closed (italic first) and opened (bold first) where they change. */
export function inlineToText(nodes: PmNode[] | undefined): string {
  let s = '', bold = false, italic = false;
  const to = (b: boolean, i: boolean) => {
    if (italic && !i) { s += '*'; italic = false; }
    if (bold && !b) { s += '**'; bold = false; }
    if (!bold && b) { s += '**'; bold = true; }
    if (!italic && i) { s += '*'; italic = true; }
  };
  for (const n of nodes ?? []) {
    const m = new Set((n.marks ?? []).map((x) => x.type));
    if (n.type === 'text') { if (!n.text) continue; to(m.has('bold'), m.has('italic')); s += n.text; }
    else if (n.type === 'field') { to(m.has('bold'), m.has('italic')); s += `{{${String(n.attrs?.key ?? '')}}}`; }
    else if (n.type === 'hardBreak') s += ' ';
  }
  to(false, false);
  return s;
}

// ─── Blocks → ProseMirror ──────────────────────────────────────────────────

const emptyParagraph = (): PmNode => ({ type: 'paragraph', attrs: { docStyle: null, src: null } });

function zone(role: 'main' | 'alt', blocks: unknown): PmNode {
  const content = blocksToNodes(Array.isArray(blocks) ? (blocks as Block[]) : []);
  return { type: 'docZone', attrs: { role }, content: content.length ? content : [emptyParagraph()] };
}

function inline(text: unknown): PmNode[] | undefined {
  const c = textToInline(typeof text === 'string' ? text : '');
  return c.length ? c : undefined;
}

function withContent(n: PmNode, content: PmNode[] | undefined): PmNode {
  if (content) n.content = content;
  return n;
}

export function blockToNode(b: Block): PmNode {
  switch (b.kind) {
    case 'heading':
      return withContent({ type: 'heading', attrs: { level: Math.min(3, Math.max(1, Number(b.level ?? 1) || 1)), src: srcOf(b, 'text') } }, inline(b.text));
    case 'paragraph':
      return withContent({ type: 'paragraph', attrs: { docStyle: typeof b.style === 'string' ? b.style : null, src: srcOf(b, 'text') } }, inline(b.text));
    case 'list': {
      const items = Array.isArray(b.items) ? b.items : [];
      return {
        type: b.ordered === true ? 'orderedList' : 'bulletList', attrs: { src: srcOf(b, 'items') },
        content: (items.length ? items : ['']).map((it) => ({ type: 'listItem', content: [withContent(emptyParagraph(), inline(it))] })),
      };
    }
    case 'repeat': {
      const content = [zone('main', b.blocks)];
      if (Array.isArray(b.empty) && b.empty.length) content.push(zone('alt', b.empty));
      return { type: 'docRepeat', attrs: { src: srcOf(b, 'blocks', 'empty') }, content };
    }
    case 'if': {
      const content = [zone('main', b.blocks)];
      if (Array.isArray(b.else) && b.else.length) content.push(zone('alt', b.else));
      return { type: 'docIf', attrs: { src: srcOf(b, 'blocks', 'else') }, content };
    }
    default:
      return { type: 'docAtom', attrs: { block: clone(b) } };
  }
}

export function blocksToNodes(blocks: Block[]): PmNode[] {
  return blocks.filter((b) => b && typeof b === 'object' && typeof b.kind === 'string').map(blockToNode);
}

/** A document's blocks as the editor's document (one empty paragraph when there are none). */
export function blocksToDoc(blocks: Block[]): PmNode {
  const content = blocksToNodes(blocks);
  return { type: 'doc', content: content.length ? content : [emptyParagraph()] };
}

// ─── ProseMirror → blocks ──────────────────────────────────────────────────

const srcAttr = (n: PmNode): Record<string, unknown> => clone((n.attrs?.src as Record<string, unknown> | null) ?? {}) ?? {};

/** A paragraph with no text and nothing of its own kept (a zone's or a document's only line, left empty). */
const isBlank = (n: PmNode) => n.type === 'paragraph' && !(n.content?.length) && !n.attrs?.src && !n.attrs?.docStyle;

/** A zone's or the document's nodes as blocks: a lone empty paragraph is no block. */
export function nodesToBlocks(nodes: PmNode[] | undefined): Block[] {
  const list = nodes ?? [];
  if (list.length === 1 && isBlank(list[0])) return [];
  return list.flatMap((n) => { const b = nodeToBlock(n); return b ? [b] : []; });
}

function zoneBlocks(n: PmNode, role: 'main' | 'alt'): Block[] | null {
  const z = n.content?.find((c) => c.type === 'docZone' && (c.attrs?.role ?? 'main') === role)
    ?? (role === 'main' ? n.content?.find((c) => c.type === 'docZone') : undefined);
  return z ? nodesToBlocks(z.content) : null;
}

export function nodeToBlock(n: PmNode): Block | null {
  switch (n.type) {
    case 'heading': {
      const src = srcAttr(n);
      const level = Number(n.attrs?.level ?? 1);
      if ('level' in src || level !== 1) src.level = level;
      return { kind: 'heading', ...src, text: inlineToText(n.content) };
    }
    case 'paragraph': {
      const src = srcAttr(n);
      const style = n.attrs?.docStyle;
      if (typeof style === 'string' && style) src.style = style; else delete src.style;
      return { kind: 'paragraph', ...src, text: inlineToText(n.content) };
    }
    case 'bulletList': case 'orderedList': {
      const src = srcAttr(n);
      const ordered = n.type === 'orderedList';
      if (ordered || 'ordered' in src) src.ordered = ordered;
      // An item's text: its paragraphs one after the other (a list item holds one in a document).
      const items = (n.content ?? []).map((li) => (li.content ?? []).filter((p) => p.type === 'paragraph').map((p) => inlineToText(p.content)).join(' '));
      return { kind: 'list', ...src, items };
    }
    case 'docRepeat': case 'docIf': {
      const src = srcAttr(n);
      const kind = n.type === 'docRepeat' ? 'repeat' : 'if';
      const alt = kind === 'repeat' ? 'empty' : 'else';
      delete src[alt];
      const b: Block = { kind, ...src, blocks: zoneBlocks(n, 'main') ?? [] };
      const other = zoneBlocks(n, 'alt');
      if (other && other.length) b[alt] = other;
      return b;
    }
    case 'docAtom': {
      const b = n.attrs?.block as Block | null;
      return b && typeof b.kind === 'string' ? clone(b) : null;
    }
    case 'docZone':
      return null;
    default:
      return null;
  }
}

/** The editor's document as the blocks the engine keeps. */
export const docToBlocks = (doc: PmNode): Block[] => nodesToBlocks(doc.content);

// ─── Around a position ─────────────────────────────────────────────────────

/** A repeat the editor stands in: its scope for `document.block` (its collection, alias and where). */
export interface DocScopeArg { collection: string; as?: string; where?: Record<string, string>; index?: number }

/** A repeat container's attributes as the scope its main zone fills in. */
export function scopeOf(src: Record<string, unknown> | null | undefined): DocScopeArg | null {
  const over = typeof src?.over === 'string' ? src.over : '';
  if (!over) return null;
  const s: DocScopeArg = { collection: over };
  if (typeof src?.as === 'string' && src.as) s.as = src.as;
  if (src?.where && typeof src.where === 'object') s.where = src.where as Record<string, string>;
  return s;
}

/** A key for a scope chain (a cache's). */
export const scopeKey = (chain: DocScopeArg[]) => JSON.stringify(chain);

/**
 * An input's key from its label: the label's letters and digits in camelCase, without diacritics ("Situația existentă" →
 * "situatiaExistenta"), "text" when none, a number after it when another input has it.
 */
export function inputKey(label: string, taken: Iterable<string>): string {
  const words = label.normalize('NFD').replace(/[̀-ͯ]/g, '').split(/[^A-Za-z0-9]+/).filter(Boolean);
  let base = words.map((w, i) => (i === 0 ? w.toLowerCase() : w[0].toUpperCase() + w.slice(1).toLowerCase())).join('').slice(0, 48);
  if (!base) base = 'text';
  if (/^[0-9]/.test(base)) base = `t${base}`;
  const used = new Set(taken);
  if (!used.has(base)) return base;
  for (let i = 2; ; i++) if (!used.has(`${base}${i}`)) return `${base}${i}`;
}
