/**
 * The symbol's and the title block's boards on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4): each a
 * paper view of its own at 1:1 in its millimetres (`drafting.editSymbol`, `drafting.editTitleBlock`), drawn on with every Draft
 * command, snap and grip; the fields the payload keeps beside the drawing (a symbol's name, scale, attributes and turned preview;
 * a title block's box, corner, frame and fields) in a panel beside it. Finish writes the symbol or the template in one undo step,
 * Cancel drops the board. The old editors stay for an edition without drafting or Studio's sketch bar.
 */

import { useEffect, useMemo, useState } from 'react';
import { Copy, FlipHorizontal2, FlipVertical2, Image as ImageIcon, Plus, RotateCw, Trash2 } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { hasModule } from '../../platform/brand';
import { ANNOTATE_TOOLS, registerBoard, SKETCH_TOOLS, type Board } from '../../viewport/sketch/board';
import { pensToSvg } from './documents/docModel';
import { previewSymbol, symbolFields, type SymFields } from './symbols/symbols';
import { keepSymbol, previewImage, updateLibrarySymbol, type SymAttr, type SymDef, type SymPreview } from './symbols/symbolModel';
import { titleBlockFields, type TbFields } from './titleBlocks/titleBlocks';
import { ANCHORS, type TbTemplate } from './titleBlocks/titleBlockModel';

const BOARD_TOOLS = [...SKETCH_TOOLS, ...ANNOTATE_TOOLS];

/** Whether the boards can be used in this edition: drafting draws on them, Studio's bar finishes them. */
export const boardsAvailable = (): boolean => hasModule('drafting') && hasModule('studio');

/** Where the user was before the board, to go back to; and which library symbol a board is of (this computer's, kept by the interface). */
const back = new Map<string, { view: ReturnType<typeof useUi.getState>['view']; drawingViewId: string | null; dialog: string | null }>();
const libraryOf = new Map<string, string>();

function open(viewId: string, dialog: string) {
  const s = useUi.getState();
  if (!back.has(viewId)) back.set(viewId, { view: s.view, drawingViewId: s.drawingViewId, dialog });
  useUi.setState({ dialog: null, view: 'drawing', drawingViewId: viewId, tool: null, selected: [], selection: null });
}

function leave(viewId: string) {
  const b = back.get(viewId);
  back.delete(viewId);
  libraryOf.delete(viewId);
  useUi.setState({ view: b && b.view !== 'drawing' ? b.view : b?.drawingViewId ? 'drawing' : 'plan', drawingViewId: b?.drawingViewId ?? null, dialog: b?.dialog ?? null, selected: [], selection: null });
}

/** A command whose refusal the user reads; the board stays when it refuses. */
async function run<T>(command: string, args: Record<string, unknown>): Promise<T | null> {
  const r = await execute(command, args) as T | null;
  const err = useSession.getState().error;
  if (r == null && err) notify(err);
  return r;
}

// ------------------------------------------------------------ symbols

/**
 * Opens a symbol on its board: a project symbol by its id, a new one or one of this computer's library by its name and definition
 * (the library's written back by the interface at Finish). False when the board could not open (the old editor is used then).
 */
export async function editSymbolOnBoard(target: { symbolId: string } | { name: string; def: SymDef; libraryId?: string }): Promise<boolean> {
  if (!boardsAvailable()) return false;
  const args = 'symbolId' in target ? { symbolId: target.symbolId } : { name: target.name, def: target.def, target: target.libraryId ? 'library' : 'new' };
  const r = await run<{ viewId?: string }>('drafting.editSymbol', args);
  if (!r?.viewId) return false;
  if (!('symbolId' in target) && target.libraryId) libraryOf.set(r.viewId, target.libraryId);
  open(r.viewId, 'symbols');
  notify('Draw the symbol with the Draft tools round its insertion point (the red cross), then Finish sketch.');
  return true;
}

interface SymbolOf { symbolId?: string; target: 'project' | 'new' | 'library'; name: string; def: SymDef; layer?: string }

async function finishSymbol(viewId: string) {
  const r = await run<{ symbolId?: string; name: string; def: SymDef }>('drafting.finishSymbol', { viewId });
  if (!r) return;
  const lib = libraryOf.get(viewId);
  if (lib) { if (!updateLibrarySymbol(lib, r.name, r.def)) notify("This computer's library is full: the symbol was not kept there."); }
  else if (!r.symbolId && !keepSymbol(r.name, r.def)) notify("This computer's library is full: the symbol was not kept there.");
  notify('Symbol saved.');
  leave(viewId);
}

async function cancelSymbol(viewId: string) {
  await run('drafting.cancelSymbol', { viewId });
  leave(viewId);
}

function useBoardSymbol(viewId: string) {
  const version = useSession((s) => s.version);
  const [read, setRead] = useState<{ name: string; def: SymDef } | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    void query<{ name: string; def: SymDef }>('drafting.symbolBoard', { viewId }).then((r) => {
      if (!live) return;
      if (r) { setRead(r); setProblem(null); } else setProblem(useSession.getState().error ?? null);
    });
    return () => { live = false; };
  }, [viewId, version]);
  return { read, problem };
}

function SymbolPanel({ board }: { board: Board }) {
  const viewId = board.viewId;
  const of = useSession((s) => s.nodes.get(viewId)?.props.symbolOf as SymbolOf | undefined);
  const { read, problem } = useBoardSymbol(viewId);
  const [fields, setFields] = useState<SymFields | null>(null);
  const [turn, setTurn] = useState({ flipX: false, flipY: false, angle: 0 });
  const [preview, setPreview] = useState<SymPreview | null>(null);
  const [name, setName] = useState(of?.name ?? '');
  useEffect(() => { setName(of?.name ?? ''); }, [of?.name]);
  const def = of?.def;
  useEffect(() => { if (def) void symbolFields({ def }).then(setFields); }, [def]);
  useEffect(() => {
    if (!read || read.def.items.length === 0) { setPreview(null); return; }
    void previewSymbol({ def: read.def }, turn).then((p) => setPreview(p ?? null));
  }, [read, turn]);
  const svg = useMemo(() => (preview ? pensToSvg(previewImage(preview, 2)) : ''), [preview]);
  if (!of || !def) return null;
  const set = (args: Record<string, unknown>) => void run('drafting.setSymbolBoard', { viewId, ...args });
  const attrs = read?.def.attributes ?? def.attributes;
  const setAttrs = (next: SymAttr[]) => set({ attributes: next });
  const copy = (token: string) => {
    void navigator.clipboard?.writeText(token).catch(() => undefined);
    notify('Field copied: paste it into a text.');
  };
  return (
    <aside className="board-panel panel" data-board-panel="symbol" aria-label="Symbol">
      <label className="field"><span>Name</span>
        <input value={name} data-field="sb-name" data-no-i18n onChange={(e) => setName(e.target.value)} onBlur={() => { if (name.trim() && name !== of.name) set({ name }); }} />
      </label>
      <label className="field"><span>Scale</span>
        <select value={def.scale} data-field="sb-scale" onChange={(e) => set({ scale: e.target.value })}>
          <option value="annotation">Paper size (the same at any scale)</option>
          <option value="model">Model size (real millimetres)</option>
        </select>
      </label>
      <label className="field"><span>Category</span>
        <input value={def.category} data-field="sb-category" data-no-i18n onChange={(e) => set({ category: e.target.value })} />
      </label>
      {def.layers && def.layers.length > 0 && (
        <label className="field"><span>New items on layer</span>
          <select value={of.layer ?? ''} data-field="sb-layer" onChange={(e) => set({ layer: e.target.value })}>
            <option value="">(none)</option>
            {def.layers.map((l) => <option key={l.id} value={l.id} data-no-i18n>{l.name}</option>)}
          </select>
        </label>
      )}
      <h4 className="eyebrow">Preview</h4>
      <div className="row">
        <button type="button" className={`icon${turn.flipX ? ' on' : ''}`} title="Flip left ↔ right" onClick={() => setTurn({ ...turn, flipX: !turn.flipX })}><FlipHorizontal2 size={14} /></button>
        <button type="button" className={`icon${turn.flipY ? ' on' : ''}`} title="Flip up ↔ down" onClick={() => setTurn({ ...turn, flipY: !turn.flipY })}><FlipVertical2 size={14} /></button>
        <button type="button" className="icon" title="Turn by 45°" onClick={() => setTurn({ ...turn, angle: (turn.angle + 45) % 360 })}><RotateCw size={14} /></button>
      </div>
      {problem ? <p className="small warn" data-no-i18n>{problem}</p> : <div className="tb-preview sy-detail-preview" data-field="sb-preview" data-no-i18n dangerouslySetInnerHTML={{ __html: svg }} />}
      <h4 className="eyebrow">Attributes</h4>
      <table className="mini-table" data-field="sb-attributes">
        <tbody>
          {attrs.map((a, i) => (
            <tr key={`${a.key}-${i}`}>
              <td><input defaultValue={a.key} data-no-i18n onBlur={(e) => { if (e.target.value !== a.key) setAttrs(attrs.map((x, j) => (j === i ? { ...x, key: e.target.value } : x))); }} /></td>
              <td><input defaultValue={a.label} data-no-i18n onBlur={(e) => { if (e.target.value !== a.label) setAttrs(attrs.map((x, j) => (j === i ? { ...x, label: e.target.value } : x))); }} /></td>
              <td><input defaultValue={a.default} data-no-i18n onBlur={(e) => { if (e.target.value !== a.default) setAttrs(attrs.map((x, j) => (j === i ? { ...x, default: e.target.value } : x))); }} /></td>
              <td><button type="button" className="icon" title="Remove the attribute" onClick={() => setAttrs(attrs.filter((_, j) => j !== i))}><Trash2 size={13} /></button></td>
            </tr>
          ))}
        </tbody>
      </table>
      <button type="button" className="btn small" data-field="sb-attr-add" onClick={() => { let k = attrs.length + 1; while (attrs.some((a) => a.key === `attr${k}`)) k++; setAttrs([...attrs, { key: `attr${k}`, label: `attr${k}`, default: '' }]); }}><Plus size={12} />Attribute</button>
      <h4 className="eyebrow">Fields</h4>
      <p className="muted small">Click a field to copy it, then paste it into a text (TEXT). It is filled where the symbol is placed.</p>
      <div className="board-fields" data-field="sb-fields">
        {[...attrs.map((a) => ({ key: `attr.${a.key}`, label: a.label })), ...(fields?.fields ?? []).filter((f) => !f.key.startsWith('attr.')).slice(0, 12)].map((f) => (
          <button key={f.key} type="button" className="chip" title={f.label} onClick={() => copy(`{${f.key}}`)}><Copy size={11} /><span data-no-i18n>{`{${f.key}}`}</span></button>
        ))}
      </div>
    </aside>
  );
}

function symbolBoard(viewId: string, nodes: ReadonlyMap<string, import('../../engine/protocol').NodeDto>): Board | null {
  const of = nodes.get(viewId)?.props.symbolOf as SymbolOf | undefined;
  if (!of || typeof of !== 'object') return null;
  return {
    kind: 'symbol', viewId, title: of.name, hint: 'Draw round the red cross (the insertion point) with Draft; texts take fields such as {attr.no}', tools: BOARD_TOOLS, constraints: false,
    finish: () => finishSymbol(viewId), cancel: () => cancelSymbol(viewId), panel: SymbolPanel,
  };
}

// ------------------------------------------------------------ title blocks

/** Opens a project title block on its board; false when it could not (the old designer is used then). */
export async function editTitleBlockOnBoard(id: string): Promise<boolean> {
  if (!boardsAvailable()) return false;
  const r = await run<{ viewId?: string }>('drafting.editTitleBlock', { id });
  if (!r?.viewId) return false;
  open(r.viewId, 'titleBlocks');
  notify('Draw the title block in its box with the Draft tools (lengths in paper mm), then Finish sketch.');
  return true;
}

interface TitleBlockOf { id: string; name: string; template: TbTemplate }

function TitleBlockPanel({ board }: { board: Board }) {
  const viewId = board.viewId;
  const of = useSession((s) => s.nodes.get(viewId)?.props.titleBlockOf as TitleBlockOf | undefined);
  const [fields, setFields] = useState<TbFields | null>(null);
  const [name, setName] = useState(of?.name ?? '');
  useEffect(() => { setName(of?.name ?? ''); }, [of?.name]);
  useEffect(() => { void titleBlockFields().then(setFields); }, []);
  if (!of) return null;
  const t = of.template;
  const set = (args: Record<string, unknown>) => void run('drafting.setTitleBlockBoard', { viewId, ...args });
  const num = (v: string) => { const n = Number(v.replace(',', '.')); return Number.isFinite(n) ? n : undefined; };
  const copy = (token: string) => {
    void navigator.clipboard?.writeText(token).catch(() => undefined);
    notify('Field copied: paste it into a text.');
  };
  const frame = t.frame ?? { show: true, left: 10, top: 10, right: 10, bottom: 10 };
  return (
    <aside className="board-panel panel" data-board-panel="titleBlock" aria-label="Title block">
      <label className="field"><span>Name</span>
        <input value={name} data-field="tbb-name" data-no-i18n onChange={(e) => setName(e.target.value)} onBlur={() => { if (name.trim() && name !== of.name) set({ name }); }} />
      </label>
      <div className="row">
        <label className="field"><span>Width (mm)</span><input key={`w${t.w}`} defaultValue={t.w} data-field="tbb-w" onBlur={(e) => { const n = num(e.target.value); if (n !== undefined && n !== t.w) set({ w: n }); }} /></label>
        <label className="field"><span>Height (mm)</span><input key={`h${t.h}`} defaultValue={t.h} data-field="tbb-h" onBlur={(e) => { const n = num(e.target.value); if (n !== undefined && n !== t.h) set({ h: n }); }} /></label>
      </div>
      <label className="field"><span>Corner of the sheet</span>
        <select value={t.anchor} data-field="tbb-anchor" onChange={(e) => set({ anchor: e.target.value })}>
          {ANCHORS.map((a) => <option key={a} value={a}>{({ bottomRight: 'Bottom right', bottomLeft: 'Bottom left', topRight: 'Top right', topLeft: 'Top left' } as const)[a]}</option>)}
        </select>
      </label>
      <label className="small"><input type="checkbox" checked={frame.show} data-field="tbb-frame" onChange={(e) => set({ frame: { show: e.target.checked } })} /> Frame round the sheet</label>
      <label className="field"><span>Revision table</span>
        <select value={t.revisions} data-field="tbb-revisions" onChange={(e) => set({ revisions: e.target.value })}>
          <option value="above">Above the block (when no item draws it)</option>
          <option value="none">None</option>
        </select>
      </label>
      <button type="button" className="btn small" data-field="tbb-image" onClick={() => useUi.setState({ dialog: 'importTrace' })}><ImageIcon size={12} />Logo or image…</button>
      <h4 className="eyebrow">Fields</h4>
      <p className="muted small">Click a field to copy it, then paste it into a text (TEXT). Each sheet fills it with its own value.</p>
      <div className="board-fields" data-field="tbb-fields">
        {(fields?.fields ?? []).map((f) => (
          <button key={f.key} type="button" className="chip" title={f.value ? `${f.label}: ${f.value}` : f.label} onClick={() => copy(`{${f.key}}`)}><Copy size={11} /><span data-no-i18n>{`{${f.key}}`}</span></button>
        ))}
      </div>
    </aside>
  );
}

function titleBlockBoard(viewId: string, nodes: ReadonlyMap<string, import('../../engine/protocol').NodeDto>): Board | null {
  const of = nodes.get(viewId)?.props.titleBlockOf as TitleBlockOf | undefined;
  if (!of || typeof of !== 'object') return null;
  return {
    kind: 'titleBlock', viewId, title: of.name, hint: 'Draw in the dashed box with Draft (paper mm); texts take fields such as {sheet.number}', tools: BOARD_TOOLS, constraints: false,
    finish: async () => { if (await run('drafting.finishTitleBlock', { viewId })) { notify('Title block saved.'); leave(viewId); } },
    cancel: async () => { await run('drafting.cancelTitleBlock', { viewId }); leave(viewId); },
    panel: TitleBlockPanel,
  };
}

registerBoard(symbolBoard);
registerBoard(titleBlockBoard);
