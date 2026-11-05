/**
 * The editor's node views (docs/plan-documents.md, Etapa 2): a field as a chip with its label and its value now (an item's field,
 * inside a repeat, with the first item's value); the project's blocks as atoms drawn with their real content by
 * `document.block` (tables, pictures, key–value fields, the text to complete), the contents from the document's headings, the
 * page break as a line; repeats and conditions as zones with a band over them ("For each Storeys", "Only if … has a value") and
 * the other branch's band ("Otherwise", "When there are none"). The editor's own text is never translated (contenteditable): the
 * bands are, through t().
 */

import type { Editor } from '@tiptap/core';
import type { Node as PMNode } from '@tiptap/pm/model';
import { TextSelection } from '@tiptap/pm/state';
import { NodeViewContent, NodeViewWrapper, ReactNodeViewRenderer, type NodeViewProps } from '@tiptap/react';
import { ChevronLeft, ChevronRight, GitBranch, Repeat, SeparatorHorizontal, TextCursorInput } from 'lucide-react';
import { t } from '../../../../i18n';
import { Block as PreviewBlock } from '../DocumentPreview';
import { scopeKey, scopeOf, type Block, type DocScopeArg } from './convert';
import { useDocEditor, useFilled } from './editorStore';
import { labelOf, type DocFieldsInfo, type ScopeInfo } from './fields';
import type { DocNodeViews } from './extensions';

/** The repeats a position stands in (their main zones), outer first: their scopes, and the prefixes their items are read by. */
export function chainAt(doc: PMNode, pos: number, info: DocFieldsInfo | null): { chain: DocScopeArg[]; scopes: ScopeInfo[] } {
  const chain: DocScopeArg[] = [], scopes: ScopeInfo[] = [];
  if (pos < 0 || pos > doc.content.size) return { chain, scopes };
  const $p = doc.resolve(pos);
  for (let d = 1; d < $p.depth; d++) {
    const n = $p.node(d);
    if (n.type.name !== 'docRepeat' || $p.node(d + 1).attrs.role !== 'main') continue;
    const s = scopeOf(n.attrs.src as Record<string, unknown> | null);
    if (!s) continue;
    // The item the repeat is shown with, chosen on its band (the first by default).
    const pick = useDocEditor.getState().picks[pickKey(chain, s)];
    chain.push(pick ? { ...s, index: pick } : s);
    scopes.push({ collection: s.collection, prefix: s.as ?? info?.collections.find((c) => c.key === s.collection)?.itemPrefix ?? s.collection });
  }
  return { chain, scopes };
}

/** The key of a repeat's chosen item: its chain, without the items chosen. */
export const pickKey = (outer: DocScopeArg[], own: DocScopeArg) => scopeKey([...outer.map(({ index: _, ...s }) => s), own]);

const posOf = (getPos: NodeViewProps['getPos']) => (typeof getPos === 'function' ? getPos() ?? -1 : -1);

/** The cursor put at the start of a container's content (a click on its band). */
function enter(editor: Editor, pos: number) {
  editor.commands.command(({ tr }) => {
    tr.setSelection(TextSelection.near(tr.doc.resolve(Math.min(pos + 2, tr.doc.content.size))));
    return true;
  });
  editor.commands.focus();
}

function FieldChip({ node, editor, getPos, selected }: NodeViewProps) {
  const key = String(node.attrs.key ?? '');
  const info = useDocEditor((s) => s.info);
  useDocEditor((s) => s.tick);
  useDocEditor((s) => s.picks);
  const { chain, scopes } = chainAt(editor.state.doc, posOf(getPos), info);
  // An item's field reads the repeat it belongs to, and those around it.
  const at = scopes.findIndex((s) => key.startsWith(`${s.prefix}.`));
  const part = useFilled(null, at >= 0 ? chain.slice(0, at + 1) : [], at < 0);
  const page = key === 'page' || key === 'pages';
  const value = at >= 0 ? part?.scope[key] : info?.fields.find((f) => f.key === key)?.value;
  const label = page ? t(key === 'page' ? 'Page number' : 'Number of pages') : labelOf(info, key, scopes);
  const empty = !page && (value == null || value === '') && !(at >= 0 && !part);
  return (
    <NodeViewWrapper as="span" className={`doc-chip${empty ? ' empty' : ''}${at >= 0 ? ' item' : ''}${selected ? ' selected' : ''}`} data-chip={key} title={`${label} · {{${key}}}`}>
      <span className="doc-chip-label">{label}</span>
      {!page && <span className="doc-chip-value">{empty ? '—' : value ?? '…'}</span>}
    </NodeViewWrapper>
  );
}

/** The headings of the document edited (levels 2 and 3), for its contents: a field as its key. */
function headings(doc: PMNode): { level: number; text: string }[] {
  const o: { level: number; text: string }[] = [];
  doc.descendants((n) => {
    if (n.type.name !== 'heading') return true;
    if (n.attrs.level >= 2) {
      let text = '';
      n.forEach((c) => { text += c.isText ? c.text ?? '' : c.type.name === 'field' ? `{{${c.attrs.key}}}` : ''; });
      o.push({ level: n.attrs.level, text });
    }
    return false;
  });
  return o;
}

const ATOM_TAGS: Record<string, string> = { table: 'Table', image: 'Picture', fields: 'Key–value fields', input: 'Text to complete', toc: 'Contents', pageBreak: 'Page break' };

function AtomView({ node, editor, getPos, selected }: NodeViewProps) {
  const block = (node.attrs.block ?? { kind: 'unknown' }) as Block;
  const info = useDocEditor((s) => s.info);
  const k = useDocEditor((s) => s.k);
  const width = useDocEditor((s) => s.width);
  const defs = useDocEditor((s) => s.inputDefs);
  useDocEditor((s) => s.tick);
  useDocEditor((s) => s.picks);
  const { chain } = chainAt(editor.state.doc, posOf(getPos), info);
  const kind = block.kind;
  const filled = useFilled(block, chain, kind === 'pageBreak');
  const select = () => {
    const pos = posOf(getPos);
    if (pos >= 0) editor.chain().setNodeSelection(pos).run();
  };
  let body: React.ReactNode;
  if (kind === 'pageBreak') body = <div className="doc-atom-break"><SeparatorHorizontal size={12} /> <span>{t('Page break')}</span></div>;
  else if (kind === 'toc') {
    // The title in the document's language, as the engine gives it.
    const title = (filled?.blocks[0] as { title?: string } | undefined)?.title ?? (typeof block.title === 'string' ? block.title : '');
    body = (
      <div className="doc-toc" style={{ fontSize: 10.5 * (25.4 / 72) * k }}>
        <div className="doc-heading" style={{ fontWeight: 700 }}>{title}</div>
        {headings(editor.state.doc).map((h, i) => <div key={i} className="doc-toc-row" style={{ paddingLeft: (h.level - 2) * 4 * k }}><span>{h.text}</span><span className="doc-toc-dots" /><span>#</span></div>)}
      </div>
    );
  } else if (!filled) body = <p className="muted small">{t('Reading…')}</p>;
  else body = filled.blocks.map((b, i) => <PreviewBlock key={i} b={b} k={k} width={width} page={1} pages={1} toc={[]} />);
  const def = kind === 'input' ? defs.find((d) => d.key === block.key) : null;
  const tag = kind === 'table' && info ? info.tables.find((x) => x.source === block.source)?.label : kind === 'input' ? def?.label ?? String(block.key ?? '') : null;
  return (
    <NodeViewWrapper className={`doc-atom doc-atom-${kind}${selected ? ' selected' : ''}`} data-atom={kind} contentEditable={false} onClick={select}>
      {kind !== 'pageBreak' && (
        <div className="doc-atom-tag">
          {kind === 'input' && <TextCursorInput size={11} />}
          <span>{t(ATOM_TAGS[kind] ?? kind)}</span>{tag ? <> · <span>{tag}</span></> : null}
        </div>
      )}
      <div className="doc-atom-body">{body}</div>
    </NodeViewWrapper>
  );
}

function RepeatView({ node, editor, getPos }: NodeViewProps) {
  const info = useDocEditor((s) => s.info);
  useDocEditor((s) => s.tick);
  const picks = useDocEditor((s) => s.picks);
  const src = (node.attrs.src ?? {}) as Record<string, unknown>;
  const own = scopeOf(src);
  const { chain } = chainAt(editor.state.doc, posOf(getPos), info);
  const key = own ? pickKey(chain, own) : '';
  const pick = picks[key] ?? 0;
  const part = useFilled(null, own ? [...chain, pick ? { ...own, index: pick } : own] : [], !own);
  const c = info?.collections.find((x) => x.key === own?.collection);
  const n = part?.items[part.items.length - 1];
  // Another item shown in the repeat (its fields, its blocks and the repeats inside it).
  const step = (d: number) => (e: React.MouseEvent) => {
    e.stopPropagation();
    if (n == null || n < 1) return;
    useDocEditor.setState((s) => ({ picks: { ...s.picks, [key]: Math.min(n - 1, Math.max(0, pick + d)) } }));
  };
  const where = Object.entries((src.where ?? {}) as Record<string, string>);
  return (
    <NodeViewWrapper className="doc-zoned doc-repeat" data-repeat={own?.collection ?? ''}>
      <div className="doc-band" contentEditable={false} onClick={() => enter(editor, posOf(getPos))}>
        <Repeat size={12} /> <span>{t('For each')}</span> <b>{c?.label ?? own?.collection ?? '?'}</b>
        {where.length > 0 && <span className="doc-band-where">{t('where')} {where.map(([f, v]) => `${f} = ${v}`).join(', ')}</span>}
        {n != null && n >= 0 && <span className="doc-band-count">{n === 1 ? t('1 item') : `${n} ${t('items')}`}</span>}
        {n != null && n > 1 && (
          <span className="doc-band-step" title={t('The item shown while editing')}>
            <button type="button" className="icon" data-field="doc-repeat-prev" disabled={pick <= 0} onMouseDown={(e) => e.preventDefault()} onClick={step(-1)}><ChevronLeft size={12} /></button>
            <span>{Math.min(pick, n - 1) + 1}/{n}</span>
            <button type="button" className="icon" data-field="doc-repeat-next" disabled={pick >= n - 1} onMouseDown={(e) => e.preventDefault()} onClick={step(1)}><ChevronRight size={12} /></button>
          </span>
        )}
        {n === -1 && <span className="doc-band-count warn">{t('Not in this project')}</span>}
      </div>
      <NodeViewContent className="doc-zones" />
    </NodeViewWrapper>
  );
}

function CondView({ node, editor, getPos }: NodeViewProps) {
  const info = useDocEditor((s) => s.info);
  useDocEditor((s) => s.tick);
  const src = (node.attrs.src ?? {}) as Record<string, unknown>;
  const field = String(src.field ?? '');
  const { scopes } = chainAt(editor.state.doc, posOf(getPos), info);
  return (
    <NodeViewWrapper className="doc-zoned doc-if" data-if={field}>
      <div className="doc-band" contentEditable={false} onClick={() => enter(editor, posOf(getPos))}>
        <GitBranch size={12} /> <span>{t('Only if')}</span> <b>{labelOf(info, field, scopes)}</b> <span>{src.not === true ? t('is empty') : t('has a value')}</span>
      </div>
      <NodeViewContent className="doc-zones" />
    </NodeViewWrapper>
  );
}

function ZoneView({ node, editor, getPos }: NodeViewProps) {
  const alt = node.attrs.role === 'alt';
  const pos = posOf(getPos);
  const parent = alt && pos >= 0 ? editor.state.doc.resolve(pos).parent.type.name : null;
  return (
    <NodeViewWrapper className={`doc-zone${alt ? ' alt' : ''}`}>
      {alt && <div className="doc-band alt" contentEditable={false}>{parent === 'docRepeat' ? t('When there are none') : t('Otherwise')}</div>}
      <NodeViewContent />
    </NodeViewWrapper>
  );
}

export const docNodeViews: DocNodeViews = {
  field: ReactNodeViewRenderer(FieldChip, { as: 'span' }),
  atom: ReactNodeViewRenderer(AtomView),
  repeat: ReactNodeViewRenderer(RepeatView),
  cond: ReactNodeViewRenderer(CondView),
  zone: ReactNodeViewRenderer(ZoneView),
};
