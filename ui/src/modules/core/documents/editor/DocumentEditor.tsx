/**
 * The document editor (docs/plan-documents.md, Etapa 2): the document's own blocks edited on a page as wide as the paper's text
 * (TipTap over ProseMirror). The toolbar has the text's styles (paragraph, headings 1–3, bold, italic, lists, undo and redo; the
 * usual keys too) and Insert: a field from `document.fields` (with the items' fields of the repeats around the cursor), a table
 * of the project, a picture of a view, key–value fields, a repeat over a collection, a condition on a field, a text to complete
 * (an input the document declares), the contents and a page break. What is typed is saved a moment after the typing stops, and
 * when the editor is left, as one undo step of the engine's (`document.update {blocks, inputDefs}`).
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { Editor } from '@tiptap/core';
import { NodeSelection } from '@tiptap/pm/state';
import { EditorContent, useEditor } from '@tiptap/react';
import {
  Bold, Braces, GitBranch, Heading1, Heading2, Heading3, Image as ImageIcon, Italic, KeyRound, List, ListOrdered, ListTree, Pilcrow, Plus, Redo2, Repeat, SeparatorHorizontal, Table2, TextCursorInput, Undo2,
} from 'lucide-react';
import { execute, query, useSession } from '../../../../engine/session';
import { blocksToDoc, docToBlocks, inputKey, type Block, type PmNode } from './convert';
import { useDocEditor, useFilled, type InputDef, type SelectedPart } from './editorStore';
import { docExtensions } from './extensions';
import { searchFields, type DocFieldsInfo, type ScopeInfo } from './fields';
import { chainAt, docNodeViews } from './nodeViews';

/** How long the typing rests before the document is saved. */
const SAVE_MS = 800;

interface DocSource { id: string; blocks: Block[]; inputDefs: InputDef[] }

/** The selected atom, else the innermost repeat or condition around the cursor. */
function selectedPart(editor: Editor): SelectedPart | null {
  const s = editor.state.selection;
  if (s instanceof NodeSelection && s.node.type.name === 'docAtom') return { pos: s.from, type: 'docAtom', attrs: { ...s.node.attrs }, alt: false };
  const $f = s.$from;
  for (let d = $f.depth; d >= 1; d--) {
    const n = $f.node(d);
    if (n.type.name === 'docRepeat' || n.type.name === 'docIf') return { pos: $f.before(d), type: n.type.name, attrs: { ...n.attrs }, alt: n.childCount > 1 };
  }
  return null;
}

/** The repeats the cursor stands in, and their items' values (the first item's). */
export function useCursorScopes(): { scopes: ScopeInfo[]; values: Record<string, string> } {
  const editor = useDocEditor((s) => s.editor);
  const info = useDocEditor((s) => s.info);
  useDocEditor((s) => s.tick);
  useDocEditor((s) => s.sel);
  const { chain, scopes } = editor ? chainAt(editor.state.doc, editor.state.selection.from, info) : { chain: [], scopes: [] };
  const part = useFilled(null, chain, chain.length === 0);
  return { scopes, values: part?.scope ?? {} };
}

/** A searchable list of the fields a document can read where the cursor is, with their values. */
export function FieldPicker({ onPick, autoFocus = true }: { onPick: (key: string) => void; autoFocus?: boolean }) {
  const [q, setQ] = useState('');
  const info = useDocEditor((s) => s.info);
  const { scopes, values } = useCursorScopes();
  const groups = useMemo(() => searchFields(info, scopes, q, values), [info, scopes, q, values]);
  return (
    <div className="doc-picker" data-field="doc-field-picker">
      <input type="search" autoFocus={autoFocus} placeholder="Search fields…" value={q} onChange={(e) => setQ(e.target.value)} data-field="doc-field-search" />
      <div className="doc-picker-list">
        {groups.length === 0 && <p className="muted small">No field matches.</p>}
        {groups.map((g) => (
          <section key={g.id}>
            <h5>{g.item ? <><Repeat size={11} /> <span data-no-i18n="">{g.title}</span></> : <span>{g.title}</span>}</h5>
            {g.fields.map((f) => (
              <button type="button" key={f.key} className="doc-picker-item" data-field={`doc-field-${f.key}`} onMouseDown={(e) => e.preventDefault()} onClick={() => onPick(f.key)}>
                <span className="doc-picker-label" data-no-i18n="">{f.label}</span>
                <span className={`doc-picker-value${f.value ? '' : ' muted'}`} data-no-i18n="">{f.value || '—'}</span>
              </button>
            ))}
          </section>
        ))}
      </div>
    </div>
  );
}

/** The views a picture can show: the project's plans, sections, elevations and 3D views (not the reference planes). */
export function useViews(): { id: string; name: string; kind: string }[] {
  const nodes = useSession((s) => s.nodes);
  return useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinView' && n.props.kind !== 'plane')
    .map((n) => ({ id: n.id, name: n.name, kind: String(n.props.kind ?? '') })).sort((a, b) => a.kind.localeCompare(b.kind) || a.name.localeCompare(b.name)), [nodes]);
}

type Menu = null | 'root' | 'field' | 'table' | 'image' | 'repeat' | 'if' | 'input';

function InputForm({ onDone }: { onDone: (d: InputDef) => void }) {
  const [label, setLabel] = useState('');
  const [group, setGroup] = useState('');
  const [def, setDef] = useState('');
  const [multiline, setMultiline] = useState(true);
  const defs = useDocEditor((s) => s.inputDefs);
  const key = inputKey(label, defs.map((d) => d.key));
  return (
    <form className="doc-picker doc-form" onSubmit={(e) => { e.preventDefault(); if (label.trim()) onDone({ key, label: label.trim(), group: group.trim() || null, default: def || null, multiline }); }}>
      <label className="doc-input"><span className="small">Label</span><input autoFocus value={label} onChange={(e) => setLabel(e.target.value)} data-field="doc-input-label" /></label>
      <label className="doc-input"><span className="small">Group</span><input value={group} list="doc-input-groups" onChange={(e) => setGroup(e.target.value)} data-field="doc-input-group" /></label>
      <datalist id="doc-input-groups">{[...new Set(defs.map((d) => d.group).filter(Boolean))].map((g) => <option key={g!} value={g!} />)}</datalist>
      <label className="doc-input"><span className="small">Default text</span><textarea rows={3} value={def} onChange={(e) => setDef(e.target.value)} data-field="doc-input-default" /></label>
      <label className="doc-check"><input type="checkbox" checked={multiline} onChange={(e) => setMultiline(e.target.checked)} /> <span>Several lines</span></label>
      <p className="muted small"><span>Key</span> <code data-no-i18n="">{label.trim() ? key : '—'}</code></p>
      <button type="submit" className="btn primary" disabled={!label.trim()} data-field="doc-input-insert">Insert</button>
    </form>
  );
}

function InsertMenu({ editor }: { editor: Editor }) {
  const [menu, setMenu] = useState<Menu>(null);
  const info = useDocEditor((s) => s.info);
  const views = useViews();
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!menu) return;
    const away = (e: MouseEvent) => { if (!ref.current?.contains(e.target as Node)) setMenu(null); };
    const esc = (e: KeyboardEvent) => { if (e.key === 'Escape') setMenu(null); };
    document.addEventListener('mousedown', away);
    document.addEventListener('keydown', esc);
    return () => { document.removeEventListener('mousedown', away); document.removeEventListener('keydown', esc); };
  }, [menu]);
  const put = (content: PmNode | PmNode[]) => { editor.chain().focus().insertContent(content).run(); setMenu(null); };
  const atom = (block: Block) => put({ type: 'docAtom', attrs: { block } });
  const zone = (role: string, content: PmNode[]): PmNode => ({ type: 'docZone', attrs: { role }, content });
  const para = (content?: PmNode[]): PmNode => ({ type: 'paragraph', ...(content ? { content } : {}) });
  const repeat = (key: string) => {
    const c = info?.collections.find((x) => x.key === key);
    const first = c?.fields.includes('name') ? `${c.itemPrefix}.name` : null;
    put({ type: 'docRepeat', attrs: { src: { over: key } }, content: [zone('main', [para(first ? [{ type: 'field', attrs: { key: first } }] : undefined)])] });
  };
  const cond = (field: string) => put({ type: 'docIf', attrs: { src: { field } }, content: [zone('main', [para()])] });
  const input = (d: InputDef) => {
    useDocEditor.setState((s) => ({ inputDefs: [...s.inputDefs, d] }));
    atom({ kind: 'input', key: d.key });
  };
  const item = (id: string, icon: React.ReactNode, label: string, go: () => void, more = false) => (
    <button type="button" className="doc-menu-item" data-field={id} onMouseDown={(e) => e.preventDefault()} onClick={go}>{icon}<span>{label}</span>{more && <span className="muted">›</span>}</button>
  );
  return (
    <div className="doc-insert" ref={ref}>
      <button type="button" className={`btn${menu ? ' on' : ''}`} data-field="doc-insert" onMouseDown={(e) => e.preventDefault()} onClick={() => setMenu(menu ? null : 'root')}><Plus size={13} /> Insert</button>
      {menu && (
        <div className="doc-menu" role="menu">
          {menu === 'root' && <>
            {item('doc-insert-field', <Braces size={13} />, 'Field…', () => setMenu('field'), true)}
            {item('doc-insert-table', <Table2 size={13} />, 'Table from project…', () => setMenu('table'), true)}
            {item('doc-insert-image', <ImageIcon size={13} />, 'Picture of a view…', () => setMenu('image'), true)}
            {item('doc-insert-fields', <KeyRound size={13} />, 'Key–value fields', () => atom({ kind: 'fields', rows: [['', '']] }))}
            {item('doc-insert-repeat', <Repeat size={13} />, 'Repeat over…', () => setMenu('repeat'), true)}
            {item('doc-insert-if', <GitBranch size={13} />, 'Condition…', () => setMenu('if'), true)}
            {item('doc-insert-input', <TextCursorInput size={13} />, 'Text to complete…', () => setMenu('input'), true)}
            {item('doc-insert-toc', <ListTree size={13} />, 'Contents', () => atom({ kind: 'toc' }))}
            {item('doc-insert-pagebreak', <SeparatorHorizontal size={13} />, 'Page break', () => atom({ kind: 'pageBreak' }))}
          </>}
          {menu !== 'root' && <button type="button" className="link small doc-menu-back" onMouseDown={(e) => e.preventDefault()} onClick={() => setMenu('root')}>‹ <span>Insert</span></button>}
          {menu === 'field' && <FieldPicker onPick={(key) => put({ type: 'field', attrs: { key } })} />}
          {menu === 'if' && <FieldPicker onPick={cond} />}
          {menu === 'table' && (
            <div className="doc-picker-list">
              {(info?.tables ?? []).length === 0 && <p className="muted small">The project has no tables yet.</p>}
              {(info?.tables ?? []).map((tb) => <button type="button" key={tb.source} className="doc-picker-item" data-field={`doc-table-${tb.source}`} onMouseDown={(e) => e.preventDefault()} onClick={() => atom({ kind: 'table', source: tb.source })}><span data-no-i18n="">{tb.label}</span></button>)}
            </div>
          )}
          {menu === 'image' && (
            <div className="doc-picker-list">
              {views.length === 0 && <p className="muted small">The project has no views yet.</p>}
              {views.map((v) => <button type="button" key={v.id} className="doc-picker-item" data-field={`doc-view-${v.id}`} onMouseDown={(e) => e.preventDefault()} onClick={() => atom({ kind: 'image', view: v.id })}><span data-no-i18n="">{v.name}</span><span className="doc-picker-value muted" data-no-i18n="">{v.kind}</span></button>)}
            </div>
          )}
          {menu === 'repeat' && (
            <div className="doc-picker-list">
              {(info?.collections ?? []).map((c) => <button type="button" key={c.key} className="doc-picker-item" data-field={`doc-collection-${c.key}`} onMouseDown={(e) => e.preventDefault()} onClick={() => repeat(c.key)}><span data-no-i18n="">{c.label}</span><span className="doc-picker-value muted">{c.count}</span></button>)}
            </div>
          )}
          {menu === 'input' && <InputForm onDone={input} />}
        </div>
      )}
    </div>
  );
}

function Toolbar({ editor }: { editor: Editor }) {
  useDocEditor((s) => s.sel);
  useDocEditor((s) => s.tick);
  const b = (id: string, title: string, icon: React.ReactNode, on: boolean, go: () => void, disabled = false) => (
    <button type="button" className={`icon${on ? ' on' : ''}`} data-field={id} title={title} disabled={disabled} onMouseDown={(e) => e.preventDefault()} onClick={go}>{icon}</button>
  );
  const c = () => editor.chain().focus();
  return (
    <div className="doc-toolbar" data-field="doc-toolbar">
      {b('doc-tb-paragraph', 'Paragraph', <Pilcrow size={14} />, editor.isActive('paragraph'), () => c().setParagraph().run())}
      {b('doc-tb-h1', 'Heading 1', <Heading1 size={14} />, editor.isActive('heading', { level: 1 }), () => c().toggleHeading({ level: 1 }).run())}
      {b('doc-tb-h2', 'Heading 2', <Heading2 size={14} />, editor.isActive('heading', { level: 2 }), () => c().toggleHeading({ level: 2 }).run())}
      {b('doc-tb-h3', 'Heading 3', <Heading3 size={14} />, editor.isActive('heading', { level: 3 }), () => c().toggleHeading({ level: 3 }).run())}
      <span className="sep" />
      {b('doc-tb-bold', 'Bold (Ctrl+B)', <Bold size={14} />, editor.isActive('bold'), () => c().toggleBold().run())}
      {b('doc-tb-italic', 'Italic (Ctrl+I)', <Italic size={14} />, editor.isActive('italic'), () => c().toggleItalic().run())}
      <span className="sep" />
      {b('doc-tb-bullets', 'Bulleted list', <List size={14} />, editor.isActive('bulletList'), () => c().toggleBulletList().run())}
      {b('doc-tb-numbers', 'Numbered list', <ListOrdered size={14} />, editor.isActive('orderedList'), () => c().toggleOrderedList().run())}
      <span className="sep" />
      {b('doc-tb-undo', 'Undo (Ctrl+Z)', <Undo2 size={14} />, false, () => c().undo().run(), !editor.can().undo())}
      {b('doc-tb-redo', 'Redo (Ctrl+Shift+Z)', <Redo2 size={14} />, false, () => c().redo().run(), !editor.can().redo())}
      <span className="sep" />
      <InsertMenu editor={editor} />
    </div>
  );
}

function EditorPage({ source, k, page }: { source: DocSource; k: number; page: { w: number; margins: [number, number, number, number] } }) {
  const latest = useRef<string>(JSON.stringify({ blocks: source.blocks, inputDefs: source.inputDefs }));
  const saved = useRef(latest.current);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const id = source.id;

  const flush = useCallback(() => {
    if (timer.current) { clearTimeout(timer.current); timer.current = null; }
    if (latest.current === saved.current) return;
    saved.current = latest.current;
    void execute('document.update', { id, ...JSON.parse(latest.current) });
  }, [id]);
  const changed = useCallback((blocks: Block[]) => {
    latest.current = JSON.stringify({ blocks, inputDefs: useDocEditor.getState().inputDefs });
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(flush, SAVE_MS);
  }, [flush]);

  const editor = useEditor({
    extensions: docExtensions(docNodeViews),
    content: blocksToDoc(source.blocks),
    editorProps: { attributes: { class: 'doc-editor-text', spellcheck: 'true' } },
    onUpdate: ({ editor: e }) => {
      useDocEditor.setState((s) => ({ tick: s.tick + 1 }));
      changed(docToBlocks(e.getJSON() as PmNode));
    },
    onTransaction: ({ editor: e }) => useDocEditor.setState({ sel: selectedPart(e) }),
  });
  useEffect(() => {
    useDocEditor.setState({ editor });
    return () => useDocEditor.setState({ editor: null, sel: null });
  }, [editor]);
  // An input declared or changed is saved with the blocks.
  useEffect(() => useDocEditor.subscribe((s, prev) => {
    if (s.inputDefs !== prev.inputDefs && editor) changed(docToBlocks(editor.getJSON() as PmNode));
  }), [editor, changed]);
  // What is still unsaved goes when the editor is left (Preview, another document, the dialog closed).
  useEffect(() => () => flush(), [flush]);

  const [top, right, , left] = page.margins;
  if (!editor) return null;
  return (
    <div className="doc-editor" data-field="doc-editor">
      <Toolbar editor={editor} />
      <div className="doc-pages">
        <div className="doc-sheet doc-edit-sheet" style={{ width: page.w * k, padding: `${top * k}px ${right * k}px ${top * k}px ${left * k}px`, fontSize: 10.5 * (25.4 / 72) * k, ['--doc-k' as string]: k }}>
          <EditorContent editor={editor} />
        </div>
      </div>
    </div>
  );
}

/** The editor of a document: its own blocks read once (`document.source`) with what it can read, then edited on its page. */
export function DocumentEditor({ id, k, page }: { id: string; k: number; page: { w: number; margins: [number, number, number, number] } }) {
  const [source, setSource] = useState<DocSource | null>(null);
  useEffect(() => {
    let live = true;
    setSource(null);
    useDocEditor.setState({ docId: id, filled: {}, info: null });
    void Promise.all([query<DocSource>('document.source', { id }), query<DocFieldsInfo>('document.fields', { id })]).then(([src, info]) => {
      if (!live || !src) return;
      useDocEditor.setState({ info, inputDefs: src.inputDefs ?? [] });
      setSource(src);
    });
    return () => { live = false; };
  }, [id]);
  useEffect(() => {
    const [, right, , left] = page.margins;
    useDocEditor.setState({ k, width: page.w - left - right });
  }, [k, page]);
  if (!source || source.id !== id) return <p className="muted doc-empty">Reading…</p>;
  return <EditorPage key={id} source={source} k={k} page={page} />;
}
