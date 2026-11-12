/** A placed symbol in the inspector (docs/plan-user-extensibility.md §3): its symbol, angle, scale, flips, each attribute's value, layer and colour; Edit symbol and Explode. */

import { useEffect, useState } from 'react';
import { FlipHorizontal2, FlipVertical2, Pencil, Unlink2 } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import { useSession } from '../../../engine/session';
import { notify, useUi } from '../../../app/store';
import { NumberField } from '../../../shell/cells';
import { selectionWith } from '../../../shell/params';
import { useLayers } from '../../../viewport/cad/layers';
import { byCategory, symbolsDialog, type SymAttr } from './symbolModel';
import { explodeSymbol, flipSymbol, getSymbol, listSymbols, setSymbol, type SymList } from './symbols';

export function SymbolSection({ node }: { node: NodeDto }) {
  const version = useSession((s) => s.version);
  const layers = useLayers();
  const [list, setList] = useState<SymList>({ items: [], categories: [] });
  const [attrs, setAttrs] = useState<SymAttr[]>([]);
  const symbolId = typeof node.props.symbolId === 'string' ? node.props.symbolId : '';
  const isSymbol = node.props.predefinedType === 'SYMBOL';
  useEffect(() => { if (isSymbol) void listSymbols(false).then(setList); }, [version, isSymbol]);
  useEffect(() => { if (isSymbol && symbolId) void getSymbol(symbolId).then((r) => setAttrs(r?.def.attributes ?? [])); }, [symbolId, version, isSymbol]);
  if (!isSymbol) return null;

  const ids = selectionWith(node, (n) => n.props.predefinedType === 'SYMBOL');
  const set = (a: Record<string, unknown>) => void setSymbol(ids, a);
  const values = (node.props.values && typeof node.props.values === 'object' ? node.props.values : {}) as Record<string, string>;
  const layerName = typeof node.props.layer === 'string' ? node.props.layer : '0';
  const own = typeof node.props.color === 'string' ? node.props.color : null;
  const layer = layers.find((l) => l.name === layerName);
  const current = list.items.find((i) => i.id === symbolId);
  const flipX = node.props.flipX === true, flipY = node.props.flipY === true;
  const edit = () => { symbolsDialog.open = symbolId; symbolsDialog.edit = true; useUi.setState({ dialog: 'symbols' }); };

  return (
    <dl className="fields" data-testid="symbol-section">
      <dt>Symbol</dt>
      <dd><select value={symbolId} data-field="sym-symbol" onChange={(e) => set({ symbolId: e.target.value })}>
        {!current && <option value={symbolId}>{symbolId}</option>}
        {byCategory(list.items, list.categories).map(([cat, items]) => <optgroup key={cat} label={cat}>{items.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}</optgroup>)}
      </select></dd>
      <dt>Angle</dt><dd><NumberField value={typeof node.props.angle === 'number' ? node.props.angle : 0} suffix="°" onCommit={(a) => { if (a != null) set({ angle: a }); }} /></dd>
      <dt>Size factor</dt><dd><NumberField value={typeof node.props.scale === 'number' ? node.props.scale : 1} min={0.05} max={100} onCommit={(a) => { if (a != null && a > 0) set({ scale: a }); }} /></dd>
      <dt>Flip</dt>
      <dd className="pair">
        <button type="button" className={`btn mini${flipX ? ' primary' : ''}`} data-field="sym-flip-x" title="Left ↔ right across the symbol's own axis" onClick={() => void flipSymbol(ids, 'x')}><FlipHorizontal2 size={12} />Flip X</button>
        <button type="button" className={`btn mini${flipY ? ' primary' : ''}`} data-field="sym-flip-y" title="Up ↔ down across the symbol's own axis" onClick={() => void flipSymbol(ids, 'y')}><FlipVertical2 size={12} />Flip Y</button>
      </dd>
      {attrs.map((a) => (
        <Attribute key={`${node.id}:${a.key}`} attr={a} value={values[a.key]} onCommit={(v) => set({ values: { [a.key]: v } })} />
      ))}
      <dt data-i18n-ctx="cad">Layer</dt>
      <dd><select value={layerName} onChange={(e) => set({ layer: e.target.value })}>
        {(layers.length ? layers : [{ name: layerName }]).map((l) => <option key={l.name} value={l.name}>{l.name}</option>)}
      </select></dd>
      <dt>Colour</dt>
      <dd className="pair">
        <input type="color" value={own ?? layer?.color ?? '#18181b'} onChange={(e) => set({ color: e.target.value })} />
        {own ? <button type="button" className="link" onClick={() => set({ color: null })}>by layer</button> : <span className="muted small">by layer</span>}
      </dd>
      <dt />
      <dd className="pair">
        <button type="button" className="btn mini" data-field="sym-edit" disabled={!current || current.builtin} title={current?.builtin ? "Lapin's own symbols are read only: duplicate one in Manage → Symbols…" : 'Change the symbol: every placed copy follows'} onClick={edit}><Pencil size={12} />Edit symbol</button>
        <button type="button" className="btn mini" data-field="sym-explode" title="Replace it with drafted lines, shapes and texts" onClick={() => void explodeSymbol(ids).then((r) => { if (r?.ids?.length) { useUi.setState({ selected: r.ids, selection: r.ids[0] }); notify('Symbol exploded into drafting.'); } })}><Unlink2 size={12} />Explode</button>
      </dd>
    </dl>
  );
}

/** An attribute's value: typed over the symbol's default (shown as the placeholder), committed on Enter or when left; emptied, it goes back to the default. */
function Attribute({ attr, value, onCommit }: { attr: SymAttr; value: string | undefined; onCommit: (v: string | null) => void }) {
  const [v, setV] = useState(value ?? '');
  useEffect(() => setV(value ?? ''), [value]);
  const commit = () => { if (v !== (value ?? '')) onCommit(v === '' ? null : v); };
  return (
    <>
      <dt data-no-i18n title={attr.key}>{attr.label}</dt>
      <dd><input value={v} placeholder={attr.default} data-no-i18n data-field={`sym-attr-${attr.key}`} onChange={(e) => setV(e.target.value)} onBlur={commit}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} /></dd>
    </>
  );
}
