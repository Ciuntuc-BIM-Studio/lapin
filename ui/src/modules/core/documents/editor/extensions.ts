/**
 * The editor's schema (TipTap over ProseMirror): StarterKit's paragraphs, headings 1–3, lists, bold, italic and history (no code,
 * quotes, strike, rules or line breaks: a document's blocks have none), every text block keeping the block's own keys (`src`) and a
 * paragraph its style; the inline `field` atom ({{key}}); the block atoms (`docAtom`: a table, a picture, key–value fields, an
 * input, the contents, a page break); the containers `docRepeat` and `docIf` of their zones (`docZone`, main and alt). The node
 * views are given by the editor (React); without them (a test) the schema is the same.
 */

import { Extension, Node, mergeAttributes, type NodeViewRenderer } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';

export interface DocNodeViews { field?: NodeViewRenderer; atom?: NodeViewRenderer; repeat?: NodeViewRenderer; cond?: NodeViewRenderer; zone?: NodeViewRenderer }

/** What a text block keeps of its block, and a paragraph's style. */
const BlockAttrs = Extension.create({
  name: 'docBlockAttrs',
  addGlobalAttributes() {
    return [
      { types: ['paragraph', 'heading', 'bulletList', 'orderedList'], attributes: { src: { default: null, rendered: false } } },
      {
        types: ['paragraph'],
        attributes: {
          docStyle: {
            default: null,
            parseHTML: (el) => el.getAttribute('data-doc-style'),
            renderHTML: (a) => (a.docStyle ? { 'data-doc-style': a.docStyle, class: `doc-p doc-${a.docStyle}` } : { class: 'doc-p' }),
          },
        },
      },
    ];
  },
});

const jsonAttr = (name: string) => ({
  default: null,
  parseHTML: (el: HTMLElement) => { try { return JSON.parse(el.getAttribute(`data-${name}`) ?? 'null'); } catch { return null; } },
  renderHTML: (a: Record<string, unknown>) => ({ [`data-${name}`]: JSON.stringify(a[name] ?? null) }),
});

export function docExtensions(views: DocNodeViews = {}) {
  const Field = Node.create({
    name: 'field',
    group: 'inline',
    inline: true,
    atom: true,
    selectable: true,
    addAttributes: () => ({ key: { default: '', parseHTML: (el) => el.getAttribute('data-key') ?? '', renderHTML: (a) => ({ 'data-key': a.key }) } }),
    parseHTML: () => [{ tag: 'span[data-doc-field]' }],
    renderHTML: ({ HTMLAttributes, node }) => ['span', mergeAttributes(HTMLAttributes, { 'data-doc-field': '' }), `{{${node.attrs.key}}}`],
    renderText: ({ node }) => `{{${node.attrs.key}}}`,
    ...(views.field ? { addNodeView: () => views.field! } : {}),
  });
  const Atom = Node.create({
    name: 'docAtom',
    group: 'block',
    atom: true,
    selectable: true,
    draggable: true,
    addAttributes: () => ({ block: jsonAttr('block') }),
    parseHTML: () => [{ tag: 'div[data-doc-atom]' }],
    renderHTML: ({ HTMLAttributes }) => ['div', mergeAttributes(HTMLAttributes, { 'data-doc-atom': '' })],
    ...(views.atom ? { addNodeView: () => views.atom! } : {}),
  });
  const Zone = Node.create({
    name: 'docZone',
    content: 'block+',
    defining: true,
    isolating: true,
    addAttributes: () => ({ role: { default: 'main', parseHTML: (el) => el.getAttribute('data-role') ?? 'main', renderHTML: (a) => ({ 'data-role': a.role }) } }),
    parseHTML: () => [{ tag: 'div[data-doc-zone]' }],
    renderHTML: ({ HTMLAttributes }) => ['div', mergeAttributes(HTMLAttributes, { 'data-doc-zone': '' }), 0],
    ...(views.zone ? { addNodeView: () => views.zone! } : {}),
  });
  const container = (name: 'docRepeat' | 'docIf', view?: NodeViewRenderer) => Node.create({
    name,
    group: 'block',
    content: 'docZone docZone?',
    defining: true,
    isolating: true,
    selectable: false,
    addAttributes: () => ({ src: jsonAttr('src') }),
    parseHTML: () => [{ tag: `div[data-${name}]` }],
    renderHTML: ({ HTMLAttributes }) => ['div', mergeAttributes(HTMLAttributes, { [`data-${name}`]: '' }), 0],
    ...(view ? { addNodeView: () => view } : {}),
  });
  return [
    StarterKit.configure({ heading: { levels: [1, 2, 3] }, blockquote: false, code: false, codeBlock: false, hardBreak: false, horizontalRule: false, strike: false }),
    BlockAttrs, Field, Atom, Zone, container('docRepeat', views.repeat), container('docIf', views.cond),
  ];
}
