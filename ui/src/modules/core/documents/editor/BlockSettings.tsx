/**
 * The settings of what is selected in the document editor (docs/plan-documents.md, Etapa 2), in the dialog's right panel: a
 * table's source, columns (from its header), title and total; a picture's view, scale or width and caption; key–value rows; the
 * contents' title; a text to complete's label, group, default and lines (the document's input definitions); a repeat's
 * collection and where (a field of its items = a value, often an outer item's field); a condition's field and whether it tests
 * empty; the other branch of either. Each change is a step of the editor's (Ctrl+Z), saved with the document.
 */

import { useEffect, useRef, useState } from 'react';
import type { Editor } from '@tiptap/core';
import { NodeSelection } from '@tiptap/pm/state';
import { Plus, Trash2, X } from 'lucide-react';
import { useDocEditor, useFilled, type SelectedPart } from './editorStore';
import { searchFields } from './fields';
import { chainAt } from './nodeViews';
import { useViews } from './DocumentEditor';
import type { Block } from './convert';

/** A text kept as typed and given on leaving it, on Enter, or a moment after the typing stops. */
function Text({ value, onCommit, field, multiline = false, placeholder }: { value: string; onCommit: (v: string) => void; field: string; multiline?: boolean; placeholder?: string }) {
  const [draft, setDraft] = useState(value);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const last = useRef(value);
  useEffect(() => { setDraft(value); last.current = value; }, [value]);
  const commit = (v: string) => {
    if (timer.current) { clearTimeout(timer.current); timer.current = null; }
    if (v !== last.current) { last.current = v; onCommit(v); }
  };
  const type = (v: string) => {
    setDraft(v);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => commit(v), 600);
  };
  return multiline
    ? <textarea rows={3} value={draft} data-field={field} placeholder={placeholder} onChange={(e) => type(e.target.value)} onBlur={() => commit(draft)} />
    : <input value={draft} data-field={field} placeholder={placeholder} onChange={(e) => type(e.target.value)} onBlur={() => commit(draft)} onKeyDown={(e) => { if (e.key === 'Enter') commit(draft); }} />;
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return <label className="doc-input"><span className="small">{label}</span>{children}</label>;
}

/** Attributes of the node at a position replaced; an atom stays selected. */
function setAttrs(editor: Editor, sel: SelectedPart, attrs: Record<string, unknown>) {
  editor.commands.command(({ tr }) => {
    tr.setNodeMarkup(sel.pos, undefined, attrs);
    if (sel.type === 'docAtom') tr.setSelection(NodeSelection.create(tr.doc, sel.pos));
    return true;
  });
}

/** A block's key set, or taken away when the value is empty (the block's own default then). */
function withKey(b: Record<string, unknown>, key: string, v: unknown): Record<string, unknown> {
  const o = { ...b };
  if (v === '' || v == null || (Array.isArray(v) && v.length === 0)) delete o[key]; else o[key] = v;
  return o;
}

function TableSettings({ b, set }: { b: Block; set: (b: Block) => void }) {
  const info = useDocEditor((s) => s.info);
  const source = String(b.source ?? '');
  // The table's columns: those of its source, every one.
  const all = useFilled({ kind: 'table', source }, []);
  const header = (all?.blocks[0] as { header?: string[] } | undefined)?.header ?? [];
  const chosen = Array.isArray(b.columns) ? (b.columns as string[]) : null;
  const on = (c: string) => !chosen || chosen.some((x) => x.trim().toLowerCase() === c.trim().toLowerCase());
  const toggle = (c: string) => {
    const now = header.filter((h) => (h === c ? !on(h) : on(h)));
    set(withKey(b, 'columns', now.length === header.length ? null : now) as Block);
  };
  return (
    <>
      <Row label="Source">
        <select value={source} data-field="doc-set-source" onChange={(e) => set({ ...b, source: e.target.value })}>
          {!info?.tables.some((x) => x.source === source) && <option value={source}>{source}</option>}
          {info?.tables.map((x) => <option key={x.source} value={x.source}>{x.label}</option>)}
        </select>
      </Row>
      <Row label="Title"><Text value={typeof b.title === 'string' ? b.title : ''} field="doc-set-title" placeholder="The table's own" onCommit={(v) => set(withKey(b, 'title', v) as Block)} /></Row>
      <div className="doc-input">
        <span className="small" data-i18n-ctx="table">Columns</span>
        {header.length === 0 && <p className="muted small">Reading…</p>}
        <div className="doc-columns" data-field="doc-set-columns">
          {header.map((c) => <label key={c} className="doc-check"><input type="checkbox" checked={on(c)} onChange={() => toggle(c)} /> <span data-no-i18n="">{c}</span></label>)}
        </div>
      </div>
      <label className="doc-check"><input type="checkbox" checked={b.total !== false} data-field="doc-set-total" onChange={(e) => set(withKey(b, 'total', e.target.checked ? null : false) as Block)} /> <span>Total row, when the table has one</span></label>
    </>
  );
}

function ImageSettings({ b, set }: { b: Block; set: (b: Block) => void }) {
  const views = useViews();
  const num = (v: string) => (v.trim() === '' ? null : Number(v.replace(',', '.')) > 0 ? Number(v.replace(',', '.')) : null);
  return (
    <>
      <Row label="View">
        <select value={String(b.view ?? '')} data-field="doc-set-view" onChange={(e) => set({ ...b, view: e.target.value })}>
          {!views.some((v) => v.id === b.view) && <option value={String(b.view ?? '')}>{String(b.view ?? '')}</option>}
          {views.map((v) => <option key={v.id} value={v.id}>{v.name}</option>)}
        </select>
      </Row>
      <Row label="Scale 1:"><Text value={b.scale != null ? String(b.scale) : ''} field="doc-set-scale" placeholder="The view's own" onCommit={(v) => set(withKey(withKey(b, 'scale', num(v)), 'width', num(v) ? null : b.width) as Block)} /></Row>
      <Row label="Width (mm)"><Text value={b.width != null ? String(b.width) : ''} field="doc-set-width" placeholder="As the scale gives" onCommit={(v) => set(withKey(withKey(b, 'width', num(v)), 'scale', num(v) ? null : b.scale) as Block)} /></Row>
      <Row label="Caption"><Text value={typeof b.caption === 'string' ? b.caption : ''} field="doc-set-caption" onCommit={(v) => set(withKey(b, 'caption', v) as Block)} /></Row>
      <p className="muted small">A picture is never wider than the text: a scale that would be is stepped down.</p>
    </>
  );
}

function FieldsSettings({ b, set }: { b: Block; set: (b: Block) => void }) {
  const rows = (Array.isArray(b.rows) ? b.rows : []) as string[][];
  const setRows = (r: string[][]) => set({ ...b, rows: r });
  return (
    <>
      <Row label="Title"><Text value={typeof b.title === 'string' ? b.title : ''} field="doc-set-title" onCommit={(v) => set(withKey(b, 'title', v) as Block)} /></Row>
      <div className="doc-input">
        <span className="small">Rows: a label and its value (text with {'{{fields}}'})</span>
        {rows.map((r, i) => (
          <div key={i} className="doc-kv-row">
            <Text value={r[0] ?? ''} field={`doc-set-row-${i}-label`} placeholder="Label" onCommit={(v) => setRows(rows.map((x, j) => (j === i ? [v, x[1] ?? ''] : x)))} />
            <Text value={r[1] ?? ''} field={`doc-set-row-${i}-value`} placeholder="{{project.name}}" onCommit={(v) => setRows(rows.map((x, j) => (j === i ? [x[0] ?? '', v] : x)))} />
            <button type="button" className="icon" title="Remove the row" onClick={() => setRows(rows.filter((_, j) => j !== i))}><X size={12} /></button>
          </div>
        ))}
        <button type="button" className="link small" data-field="doc-set-row-add" onClick={() => setRows([...rows, ['', '']])}><Plus size={11} /> Add a row</button>
      </div>
      <label className="doc-check"><input type="checkbox" checked={b.skipEmpty === true} onChange={(e) => set(withKey(b, 'skipEmpty', e.target.checked || null) as Block)} /> <span>Leave out rows whose value is empty</span></label>
    </>
  );
}

function InputSettings({ b }: { b: Block }) {
  const defs = useDocEditor((s) => s.inputDefs);
  const key = String(b.key ?? '');
  const def = defs.find((d) => d.key === key);
  const change = (patch: Record<string, unknown>) => useDocEditor.setState((s) => ({
    inputDefs: s.inputDefs.some((d) => d.key === key) ? s.inputDefs.map((d) => (d.key === key ? { ...d, ...patch } : d)) : [...s.inputDefs, { key, label: key, multiline: true, ...patch }],
  }));
  return (
    <>
      <p className="muted small"><span>Key</span> <code data-no-i18n="">{key}</code>. <span>Its text is written in Inputs, in preview.</span></p>
      <Row label="Label"><Text value={def?.label ?? key} field="doc-set-input-label" onCommit={(v) => change({ label: v.trim() || key })} /></Row>
      <Row label="Group"><Text value={def?.group ?? ''} field="doc-set-input-group" onCommit={(v) => change({ group: v.trim() || null })} /></Row>
      <Row label="Default text"><Text multiline value={def?.default ?? ''} field="doc-set-input-default" onCommit={(v) => change({ default: v || null })} /></Row>
      <label className="doc-check"><input type="checkbox" checked={def?.multiline ?? true} onChange={(e) => change({ multiline: e.target.checked })} /> <span>Several lines</span></label>
    </>
  );
}

function RepeatSettings({ src, set, editor, sel }: { src: Record<string, unknown>; set: (s: Record<string, unknown>) => void; editor: Editor; sel: SelectedPart }) {
  const info = useDocEditor((s) => s.info);
  const over = String(src.over ?? '');
  const c = info?.collections.find((x) => x.key === over);
  const where = Object.entries((src.where ?? {}) as Record<string, string>);
  const setWhere = (w: [string, string][]) => set(withKey(src, 'where', w.length ? Object.fromEntries(w) : null));
  // The fields of the items around this repeat, for a where that follows them ({{storey.id}}).
  const { scopes } = chainAt(editor.state.doc, sel.pos, info);
  const outer = searchFields(info, scopes, '').filter((g) => g.item).flatMap((g) => g.fields.map((f) => f.key));
  return (
    <>
      <Row label="For each">
        <select value={over} data-field="doc-set-over" onChange={(e) => set({ ...src, over: e.target.value })}>
          {!c && <option value={over}>{over}</option>}
          {info?.collections.map((x) => <option key={x.key} value={x.key}>{x.label}</option>)}
        </select>
      </Row>
      <p className="muted small"><span>Its items' fields</span> <code data-no-i18n="">{`{{${String(src.as || c?.itemPrefix || over)}.…}}`}</code></p>
      <div className="doc-input">
        <span className="small">Only the items where</span>
        {where.map(([f, v], i) => (
          <div key={i} className="doc-kv-row">
            <select value={f} onChange={(e) => setWhere(where.map((x, j) => (j === i ? [e.target.value, x[1]] : x)))}>
              {!c?.fields.includes(f) && <option value={f}>{f}</option>}
              {c?.fields.map((k) => <option key={k} value={k}>{k}</option>)}
            </select>
            <Text value={v} field={`doc-set-where-${i}`} onCommit={(nv) => setWhere(where.map((x, j) => (j === i ? [x[0], nv] : x)))} />
            {outer.length > 0 && (
              <select value="" title="An outer item's field" onChange={(e) => e.target.value && setWhere(where.map((x, j) => (j === i ? [x[0], `{{${e.target.value}}}`] : x)))}>
                <option value="">{'{{…}}'}</option>
                {outer.map((k) => <option key={k} value={k}>{k}</option>)}
              </select>
            )}
            <button type="button" className="icon" title="Remove" onClick={() => setWhere(where.filter((_, j) => j !== i))}><X size={12} /></button>
          </div>
        ))}
        <button type="button" className="link small" data-field="doc-set-where-add" onClick={() => setWhere([...where, [c?.fields[0] ?? 'id', '']])}><Plus size={11} /> Add a condition</button>
      </div>
    </>
  );
}

function IfSettings({ src, set, editor, sel }: { src: Record<string, unknown>; set: (s: Record<string, unknown>) => void; editor: Editor; sel: SelectedPart }) {
  const info = useDocEditor((s) => s.info);
  const field = String(src.field ?? '');
  const { scopes } = chainAt(editor.state.doc, sel.pos + 1, info);
  const groups = searchFields(info, scopes, '');
  return (
    <>
      <Row label="Field">
        <select value={field} data-field="doc-set-field" onChange={(e) => set({ ...src, field: e.target.value })}>
          {!groups.some((g) => g.fields.some((f) => f.key === field)) && <option value={field}>{field}</option>}
          {groups.map((g) => <optgroup key={g.id} label={g.title}>{g.fields.map((f) => <option key={f.key} value={f.key}>{f.label}{f.value ? ` (${f.value})` : ''}</option>)}</optgroup>)}
        </select>
      </Row>
      <label className="doc-check"><input type="checkbox" checked={src.not === true} data-field="doc-set-not" onChange={(e) => set(withKey(src, 'not', e.target.checked || null))} /> <span>Only when it is empty (or 0)</span></label>
    </>
  );
}

const TITLES: Record<string, string> = { table: 'Table', image: 'Picture', fields: 'Key–value fields', input: 'Text to complete', toc: 'Contents', pageBreak: 'Page break', docRepeat: 'Repeat', docIf: 'Condition' };

export function BlockSettings() {
  const editor = useDocEditor((s) => s.editor);
  const sel = useDocEditor((s) => s.sel);
  if (!editor || !sel) {
    return <div className="doc-block-settings" data-field="doc-block-settings"><p className="muted small">Select a table, a picture or another block of the project, or put the cursor in a repeat or a condition, to see its settings here.</p></div>;
  }
  const node = editor.state.doc.nodeAt(sel.pos);
  if (!node) return null;
  const b = (sel.attrs.block ?? {}) as Block;
  const src = (sel.attrs.src ?? {}) as Record<string, unknown>;
  const setBlock = (nb: Block) => setAttrs(editor, sel, { ...sel.attrs, block: nb });
  const setSrc = (ns: Record<string, unknown>) => setAttrs(editor, sel, { ...sel.attrs, src: ns });
  const container = sel.type !== 'docAtom';
  const toggleAlt = () => editor.commands.command(({ tr, state }) => {
    const n = tr.doc.nodeAt(sel.pos);
    if (!n) return false;
    if (n.childCount > 1) {
      const at = sel.pos + 1 + n.child(0).nodeSize;
      tr.delete(at, at + n.child(1).nodeSize);
    } else tr.insert(sel.pos + n.nodeSize - 1, state.schema.nodes.docZone.create({ role: 'alt' }, state.schema.nodes.paragraph.create()));
    return true;
  });
  const unwrap = () => editor.commands.command(({ tr }) => { tr.replaceWith(sel.pos, sel.pos + node.nodeSize, node.child(0).content); return true; });
  const remove = () => editor.chain().focus().command(({ tr }) => { tr.delete(sel.pos, sel.pos + node.nodeSize); return true; }).run();
  const kind = sel.type === 'docAtom' ? b.kind : sel.type;
  return (
    <div className="doc-block-settings" data-field="doc-block-settings">
      <h4>{TITLES[kind] ?? kind}</h4>
      {kind === 'table' && <TableSettings b={b} set={setBlock} />}
      {kind === 'image' && <ImageSettings b={b} set={setBlock} />}
      {kind === 'fields' && <FieldsSettings b={b} set={setBlock} />}
      {kind === 'input' && <InputSettings b={b} />}
      {kind === 'toc' && <Row label="Title"><Text value={typeof b.title === 'string' ? b.title : ''} field="doc-set-title" placeholder="Contents" onCommit={(v) => setBlock(withKey(b, 'title', v) as Block)} /></Row>}
      {kind === 'pageBreak' && <p className="muted small">What follows starts on a new page.</p>}
      {kind === 'docRepeat' && <RepeatSettings src={src} set={setSrc} editor={editor} sel={sel} />}
      {kind === 'docIf' && <IfSettings src={src} set={setSrc} editor={editor} sel={sel} />}
      {container && (
        <label className="doc-check"><input type="checkbox" checked={sel.alt} data-field="doc-set-alt" onChange={toggleAlt} /> <span>{kind === 'docRepeat' ? 'Text when there are none' : 'Otherwise: text when it does not hold'}</span></label>
      )}
      <div className="doc-settings-actions">
        {container && <button type="button" className="btn" data-field="doc-set-unwrap" title="The blocks inside kept, the band taken away" onClick={unwrap}>Keep the content only</button>}
        <button type="button" className="btn" data-field="doc-set-remove" onClick={remove}><Trash2 size={12} /> Remove</button>
      </div>
    </div>
  );
}
