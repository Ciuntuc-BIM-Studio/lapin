/**
 * Symbols on a view (docs/plan-user-extensibility.md §3): the place tool (SYMBOL: a ghost of the symbol follows the cursor, turned
 * with R or Space, flipped with X and Y, one click places it), Save as symbol… (SAVESYMBOL: the selected drafting, an insertion
 * point and a direction, then a small dialog), and the right-click entries for drafting and placed symbols.
 */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { FlipHorizontal2, FlipVertical2, RotateCw } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import type { Vec2 } from '../../../platform/module';
import type { MenuItem } from '../../../shell/ContextMenu';
import { notify, select, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { cadHost, cadLog, registerCommand, startCommand, useCad, withSelection, type Step } from '../../../viewport/cad/session';
import type { Shape } from '../../../viewport/cad/shapes';
import { explodeSymbol, flipSymbol, getSymbol, lastSymbol, listSymbols, placeSymbol, previewSymbol, rememberSymbol, type SymList } from './symbols';
import { byCategory, defaultValues, ghostFactor, ghostShapes, toggled, turned, type SymAttr, type SymScale } from './symbolModel';
import type { PenDto } from '../../../viewport/drawing';

// ------------------------------------------------------------ the place tool

interface PlaceState {
  symbolId: string | null; name: string; scale: SymScale; attributes: SymAttr[];
  flipX: boolean; flipY: boolean; angle: number;
  /** The values the user typed (the others keep the symbol's defaults). */
  values: Record<string, string>;
}
export const usePlace = create<PlaceState>(() => ({ symbolId: null, name: '', scale: 'annotation', attributes: [], flipX: false, flipY: false, angle: 0, values: {} }));

/** The ghost: the engine's pens for the symbol as turned, kept until the next change. */
const ghost: { pens: PenDto[]; key: string } = { pens: [], key: '' };

const nudge = () => useCad.setState((s) => ({ cursor: s.cursor ? { ...s.cursor } : s.cursor }));

async function refreshGhost(): Promise<void> {
  const s = usePlace.getState();
  if (!s.symbolId) { ghost.pens = []; return; }
  const key = JSON.stringify([s.symbolId, s.flipX, s.flipY, s.angle, s.values]);
  if (key === ghost.key) return;
  ghost.key = key;
  const r = await previewSymbol({ id: s.symbolId }, { values: { ...defaultValues(s.attributes), ...s.values }, flipX: s.flipX, flipY: s.flipY, angle: s.angle });
  if (ghost.key !== key) return;
  ghost.pens = r?.pens ?? [];
  nudge();
}

/** Chooses the symbol the tool places (its attributes' defaults read, the values asked for again). */
export async function chooseSymbol(id: string): Promise<boolean> {
  const sym = await getSymbol(id);
  if (!sym) return false;
  rememberSymbol(id);
  usePlace.setState({ symbolId: id, name: sym.name, scale: sym.def.scale, attributes: sym.def.attributes, values: {} });
  ghost.key = '';
  await refreshGhost();
  return true;
}

/** The view's scale (1:n) as a number. */
export const viewScaleOf = (viewId: string): number => {
  const n = useSession.getState().nodes.get(viewId);
  const v = Number(n?.props.scale);
  return v > 0 ? v : 100;
};

let unsubscribe: (() => void) | null = null;

function placeStep(): Step {
  const s = usePlace.getState();
  const host = cadHost();
  const k = host ? ghostFactor(s.scale, viewScaleOf(host.viewId)) : 0.001;
  const turn = () => { usePlace.setState((p) => ({ angle: turned(p.angle) })); return placeStep(); };
  const flip = (axis: 'x' | 'y') => () => { usePlace.setState((p) => toggled(p, axis)); return placeStep(); };
  return {
    prompt: `Insertion point of „${s.name}” [Rotate / X flip / Y flip] (Space turns it, Esc ends)`, want: 'point',
    preview: (c: Vec2): Shape[] => ghostShapes(ghost.pens, c, k),
    keywords: { R: turn, ROTATE: turn, X: flip('x'), Y: flip('y'), FLIPX: flip('x'), FLIPY: flip('y') },
    enter: turn,
    point: async (p) => {
      const h = cadHost();
      const st = usePlace.getState();
      if (!h || !st.symbolId) return null;
      const values: Record<string, string> = {};
      for (const a of st.attributes) if (st.values[a.key] != null && st.values[a.key] !== a.default) values[a.key] = st.values[a.key];
      const r = await placeSymbol({
        viewId: h.viewId, symbolId: st.symbolId, at: [p.x, p.y], ...(st.angle ? { angle: st.angle } : {}),
        ...(st.flipX ? { flipX: true } : {}), ...(st.flipY ? { flipY: true } : {}), ...(Object.keys(values).length ? { values } : {}),
      });
      if (r?.id) select(r.id);
      return placeStep();
    },
  };
}

async function startPlace(): Promise<Step | null> {
  if (!cadHost()) return null;
  let id = usePlace.getState().symbolId ?? lastSymbol();
  if (!id || !(await chooseSymbol(id))) {
    const list = await listSymbols(false);
    id = list.items[0]?.id ?? null;
    if (!id || !(await chooseSymbol(id))) { cadLog('There is no symbol to place: make one in Manage → Symbols….'); return null; }
  }
  usePlace.setState({ flipX: false, flipY: false, angle: 0 });
  ghost.key = '';
  await refreshGhost();
  unsubscribe?.();
  const stop = usePlace.subscribe(() => { if (useCad.getState().command !== 'SYMBOL') { stop(); return; } void refreshGhost(); });
  unsubscribe = stop;
  return placeStep();
}

registerCommand({ names: ['SYMBOL', 'SYM'], label: 'Symbol', start: startPlace, options: SymbolOptions });

/** Starts the place tool with a symbol (from the Symbols dialog, or the selected placed one's Properties). */
export async function placeTool(symbolId?: string): Promise<void> {
  if (symbolId) { usePlace.setState({ symbolId }); }
  await startCommand('SYMBOL');
}

/** The command line's bar while placing: the symbol, its turn and flips, the attributes' values. */
function SymbolOptions() {
  const s = usePlace();
  const version = useSession((x) => x.version);
  const [list, setList] = useState<SymList>({ items: [], categories: [] });
  useEffect(() => { void listSymbols(false).then(setList); }, [version]);
  const groups = byCategory(list.items, list.categories);
  return (
    <>
      <select className="cad-toggle" value={s.symbolId ?? ''} data-symbol-pick title="The symbol to place" onChange={(e) => { if (e.target.value) { usePlace.setState({ flipX: false, flipY: false, angle: 0 }); void chooseSymbol(e.target.value); } }}>
        {groups.map(([cat, items]) => <optgroup key={cat} label={cat}>{items.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}</optgroup>)}
      </select>
      <button type="button" className="cad-toggle" title="Turn a quarter (R or Space)" data-symbol-rotate onClick={() => usePlace.setState((p) => ({ angle: turned(p.angle) }))}><RotateCw size={12} /> {s.angle}°</button>
      <button type="button" className={`cad-toggle${s.flipX ? ' on' : ''}`} title="Flip left ↔ right (X)" data-symbol-flipx onClick={() => usePlace.setState((p) => toggled(p, 'x'))}><FlipHorizontal2 size={12} /></button>
      <button type="button" className={`cad-toggle${s.flipY ? ' on' : ''}`} title="Flip up ↔ down (Y)" data-symbol-flipy onClick={() => usePlace.setState((p) => toggled(p, 'y'))}><FlipVertical2 size={12} /></button>
      {s.attributes.map((a) => (
        <label key={a.key} className="cad-toggle" title={a.label}>
          <span data-no-i18n>{a.label}</span>{' '}
          <input value={s.values[a.key] ?? ''} placeholder={a.default} size={8} data-no-i18n data-symbol-attr={a.key}
            onKeyDown={(e) => e.stopPropagation()}
            onChange={(e) => { const v = e.target.value; usePlace.setState((p) => ({ values: { ...p.values, [a.key]: v } })); }} />
        </label>
      ))}
    </>
  );
}

// ------------------------------------------------------------ Save as symbol…

/** What Save as symbol… has so far: the drafting chosen, the insertion point, the direction. */
export interface SaveSymbolRequest { viewId: string; ids: string[]; base: Vec2; angle: number }
export const useSaveSymbol = create<{ request: SaveSymbolRequest | null }>(() => ({ request: null }));

/** Drafted lines, shapes, hatches and texts: what a symbol is made of. */
export const isSymbolSource = (n: NodeDto | undefined): boolean =>
  !!n && n.type === 'IfcAnnotation' && typeof n.props.viewId === 'string'
  && (n.props.predefinedType === 'TEXT' || (n.props.predefinedType === 'DRAFT' && (!!n.props.shape || !!n.props.hatch)) || (!!n.props.shape && n.props.predefinedType !== 'SYMBOL'));

function saveAsSymbol(): ReturnType<typeof withSelection> {
  return withSelection((ids) => {
    const nodes = useSession.getState().nodes;
    const sources = ids.filter((id) => isSymbolSource(nodes.get(id)));
    const host = cadHost();
    if (!host) return null;
    if (!sources.length) { cadLog('Select drafted lines, shapes, hatches or texts to save as a symbol.'); return null; }
    const open = (base: Vec2, angle: number) => { useSaveSymbol.setState({ request: { viewId: host.viewId, ids: sources, base, angle } }); useUi.setState({ dialog: 'saveSymbol' }); return null; };
    return {
      prompt: `Insertion point of the symbol (${sources.length} saved)`, want: 'point',
      point: (base) => ({
        prompt: 'Its direction: click a point along its +x, or Enter for 0°', want: 'point', base,
        preview: (c): Shape[] => [{ type: 'line', a: base, b: c }],
        point: (c) => open(base, Math.round((Math.atan2(c.y - base.y, c.x - base.x) * 180 / Math.PI) * 1e6) / 1e6),
        enter: () => open(base, 0),
      }),
    };
  });
}
registerCommand({ names: ['SAVESYMBOL', 'SYMBOLSAVE', 'SYMSAVE'], label: 'Save as symbol', start: saveAsSymbol });

// ------------------------------------------------------------ menus

/** The right-click entries for the selection: Save as symbol… for drafting, and for placed symbols Flip, Edit and Explode. */
export function symbolMenuItems(ids: string[], nodes: Map<string, NodeDto>): MenuItem[] {
  const out: MenuItem[] = [];
  if (ids.some((id) => isSymbolSource(nodes.get(id)))) out.push({ label: 'Save as symbol…', onSelect: () => void startCommand('SAVESYMBOL') });
  const placed = ids.filter((id) => nodes.get(id)?.props.predefinedType === 'SYMBOL');
  if (placed.length) {
    out.push(
      { label: 'Flip symbol left ↔ right', onSelect: () => void flipSymbol(placed, 'x') },
      { label: 'Flip symbol up ↔ down', onSelect: () => void flipSymbol(placed, 'y') },
      { label: 'Explode symbol', onSelect: () => void explodeSymbol(placed).then((r) => { if (r?.ids?.length) { useUi.setState({ selected: r.ids, selection: r.ids[0] }); notify('Symbol exploded into drafting.'); } }) },
    );
  }
  return out;
}
