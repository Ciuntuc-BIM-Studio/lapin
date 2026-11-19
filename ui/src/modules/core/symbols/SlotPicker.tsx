/**
 * Marker symbols (docs/plan-user-extensibility.md §3.3): a picker of symbols with their previews, the slots the project names in
 * Drafting standards → Project, and the "Marker symbol" of a section, an elevation, a grid axis, a level, a callout or a view in
 * Properties, which names a symbol of its own (or the project's). A slot's category lists its kind of symbols first; any symbol may be chosen.
 */

import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { ChevronDown } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import { execute, query, useSession } from '../../../engine/session';
import { selectionWith } from '../../../shell/params';
import { t as tr } from '../../../i18n';
import { byCategory, type SymSummary } from './symbolModel';
import { listSymbols, type SymList } from './symbols';
import { Thumb } from './Thumb';
import { Cell } from '../../../shell/cells';
import { automaticMark } from './viewMark';

export interface SlotInfo { id: string; label: string; owner: string; category: string; project: string | null; own: string | null }
/** A view's mark: its name, its number on its sheet (empty: on none) and that sheet's number, and its mark of its own (null: none). */
export interface ViewMarkInfo { name: string; number: string; sheet: string; own: string | null }

export const useSymbolList = (): SymList => {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<SymList>({ items: [], categories: [] });
  useEffect(() => { let live = true; void listSymbols(true).then((l) => { if (live) setList(l); }); return () => { live = false; }; }, [version]);
  return list;
};

export const useSlots = (ownerId?: string): SlotInfo[] => useSlotsAndMark(ownerId).slots;

/** The slots, and for a view its mark: what its marks write without one of its own, and its own (report #167). */
export const useSlotsAndMark = (ownerId?: string): { slots: SlotInfo[]; mark: ViewMarkInfo | null } => {
  const version = useSession((s) => s.version);
  const [got, setGot] = useState<{ slots: SlotInfo[]; mark: ViewMarkInfo | null }>({ slots: [], mark: null });
  useEffect(() => {
    let live = true;
    void query<{ slots: SlotInfo[]; mark?: ViewMarkInfo }>('symbols.slots', ownerId ? { ownerId } : {}).then((r) => { if (live) setGot({ slots: r?.slots ?? [], mark: r?.mark ?? null }); });
    return () => { live = false; };
  }, [version, ownerId]);
  return got;
};

/** The symbols with the slot's own category first, then the rest by category. */
export const orderForSlot = (items: SymSummary[], order: string[], category: string): [string, SymSummary[]][] => {
  const groups = byCategory(items, order);
  return [...groups.filter(([c]) => c === category), ...groups.filter(([c]) => c !== category)];
};

/** Where the list opens, in window coordinates: under the button, or above it when there is more room there, never off the window. */
export const listPlace = (r: { left: number; top: number; bottom: number }, w: number, h: number): { position: 'fixed'; left: number; top?: number; bottom?: number; maxHeight: number } => {
  const below = h - r.bottom - 8, above = r.top - 8;
  const left = Math.max(4, Math.min(r.left, w - 264));
  return below >= 220 || below >= above
    ? { position: 'fixed', left, top: r.bottom, maxHeight: Math.max(120, Math.min(420, below)) }
    : { position: 'fixed', left, bottom: h - r.top, maxHeight: Math.max(120, Math.min(420, above)) };
};

/** A symbol chosen from a list drawn with previews; `noneLabel` is the choice that names no symbol. */
export function SymbolPicker({ value, onChange, list, category, noneLabel, field }: {
  value: string | null; onChange: (id: string | null) => void; list: SymList; category: string; noneLabel: string; field?: string;
}) {
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLSpanElement>(null);
  const [place, setPlace] = useState<CSSProperties>({});
  useEffect(() => {
    if (!open) return;
    const off = (e: MouseEvent) => { if (!box.current?.contains(e.target as Node)) setOpen(false); };
    document.addEventListener('mousedown', off);
    return () => document.removeEventListener('mousedown', off);
  }, [open]);
  // The list is fixed to the window, not absolute in the panel: a Properties table clips what overflows it (reports #168, #169).
  const toggle = () => {
    if (!open && box.current) setPlace(listPlace(box.current.getBoundingClientRect(), window.innerWidth, window.innerHeight));
    setOpen(!open);
  };
  const groups = useMemo(() => orderForSlot(list.items, list.categories, category), [list, category]);
  const current = value ? list.items.find((i) => i.id === value) : undefined;
  const pick = (id: string | null) => { setOpen(false); onChange(id); };
  return (
    <span className="slot-picker" ref={box}>
      <button type="button" className="btn slot-current" data-field={field} aria-haspopup="listbox" aria-expanded={open} onClick={toggle}>
        {current ? <Thumb p={current.preview} /> : null}
        <span className="grow" data-no-i18n={current ? true : undefined}>{current ? (current.builtin ? tr(current.name) : current.name) : value ? value : noneLabel}</span>
        <ChevronDown size={12} />
      </button>
      {open && (
        <div className="slot-list" role="listbox" style={place}>
          <button type="button" role="option" aria-selected={!value} className={`slot-row${!value ? ' on' : ''}`} data-symbol="" onClick={() => pick(null)}><span className="sy-thumb" /><span>{noneLabel}</span></button>
          {groups.map(([cat, items]) => (
            <div key={cat}>
              <p className="eyebrow">{cat}{cat === category ? ' ·' : ''}</p>
              {items.map((i) => (
                <button key={i.id} type="button" role="option" aria-selected={i.id === value} className={`slot-row${i.id === value ? ' on' : ''}`} data-symbol={i.id} onClick={() => pick(i.id)}>
                  <Thumb p={i.preview} /><span data-no-i18n>{i.builtin ? tr(i.name) : i.name}</span>
                </button>
              ))}
            </div>
          ))}
        </div>
      )}
    </span>
  );
}

/** Drafting standards → Project: one picker for each slot, on the project. */
export function MarkerSymbolsStandards() {
  const list = useSymbolList();
  const slots = useSlots();
  if (!slots.length) return null;
  return (
    <>
      <dt className="group-title" title="The symbol each mark of the views is drawn with; a mark's own choice, in its properties, goes before. Lapin's own drawing when none is chosen.">Marker symbols</dt>
      <dd className="muted small">Lapin draws each mark itself until a symbol is chosen.</dd>
      {slots.map((s) => [
        <dt key={`t${s.id}`}>{s.label}</dt>,
        <dd key={`d${s.id}`} data-slot={s.id}>
          <SymbolPicker field={`slot-${s.id}`} value={s.project} list={list} category={s.category} noneLabel="Lapin's own (default)"
            onChange={(id) => void execute('symbols.setSlot', { slot: s.id, symbolId: id })} />
        </dd>,
      ])}
    </>
  );
}

/** What a node's marker symbol is for: the slots its kind of mark has. */
export function slotsFor(node: NodeDto): string[] {
  if (node.type === 'IfcGrid') return ['gridBubble'];
  if (node.type === 'IfcBuildingStorey') return ['levelHead'];
  if (node.type === 'LapinInset' || node.type === 'LapinCallout') return ['calloutHead'];
  // A north arrow (issue #111): its own symbol over its view's and the project's.
  if (node.type === 'IfcAnnotation') return node.props.predefinedType === 'NORTHARROW' ? ['northArrow'] : [];
  if (node.type === 'LapinView') {
    const kind = node.props.kind;
    if (kind === 'section') return ['sectionHead', 'viewTitle'];
    if (kind === 'elevation') return ['elevationHead', 'viewTitle'];
    if (kind === 'plan') return ['northArrow', 'viewTitle'];
    return [];
  }
  return [];
}

/** Properties: the symbol this mark is drawn with, its own or the project's. */
export function MarkerSymbolSection({ node }: { node: NodeDto }) {
  const list = useSymbolList();
  const { slots, mark } = useSlotsAndMark(node.id);
  const wanted = slotsFor(node);
  const ids = selectionWith(node, (n) => n.type === node.type && (n.props.kind ?? null) === (node.props.kind ?? null) && (n.props.predefinedType ?? null) === (node.props.predefinedType ?? null));
  const mine = slots.filter((s) => wanted.includes(s.id) && (node.type !== 'LapinView' || s.owner === 'view' || s.owner === node.props.kind));
  if (!mine.length) return null;
  const own = typeof node.props.detailNumber === 'string' ? node.props.detailNumber : '';
  return (
    <dl className="fields" data-testid="marker-symbol-section">
      {/* The text its marks write (report #167): its own, or (empty) the automatic one shown greyed. One view at a time. */}
      {node.type === 'LapinView' && mark && (
        <>
          <dt title="What the view's marker writes: the section's or elevation's name with Lapin's own head, its number on its sheet with a head symbol, a callout or a view title. Empty: automatic.">Mark</dt>
          <dd><Cell testId="detailNumber" value={own} placeholder={automaticMark(node.props.kind, slots, mark) ?? 'automatic'} onCommit={(t) => void execute('element.update', { ids: [node.id], attrs: { detailNumber: t.trim() || null } })} /></dd>
        </>
      )}
      {mine.map((s) => {
        return [
          <dt key={`t${s.id}`} title={s.label}>{mine.length > 1 ? s.label : 'Marker symbol'}</dt>,
          <dd key={`d${s.id}`} data-slot={s.id}>
            <SymbolPicker field={`mark-${s.id}`} value={s.own} list={list} category={s.category}
              noneLabel="Project's choice"
              onChange={(id) => void execute('symbols.setSlot', { slot: s.id, symbolId: id, ownerIds: ids })} />
          </dd>,
        ];
      })}
    </dl>
  );
}
