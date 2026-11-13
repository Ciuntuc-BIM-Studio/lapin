/** A classification's items as a tree (the top ones, their children opened on a click) or, with a search, a flat list of the matches. Shared by Manage → Classifications… and the picker in Properties. */

import { useEffect, useState } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import type { ClassItem } from './classModel';
import { systemItems } from './classifications';

function Level({ system, parent, depth, onPick, picked }: { system: string; parent: string | null; depth: number; onPick?: (i: ClassItem) => void; picked?: string | null }) {
  const [items, setItems] = useState<ClassItem[] | null>(null);
  const [open, setOpen] = useState<Record<string, boolean>>({});
  useEffect(() => { let live = true; void systemItems(system, { parent }).then((r) => { if (live) setItems(r?.items ?? []); }); return () => { live = false; }; }, [system, parent]);
  if (!items) return <p className="muted small" style={{ paddingLeft: depth * 14 }}>Reading…</p>;
  return (
    <ul className="class-tree">
      {items.map((i) => (
        <li key={i.code}>
          <div className={`class-row${picked === i.code ? ' on' : ''}`} style={{ paddingLeft: depth * 14 }}>
            {i.children ? <button type="button" className="icon mini" aria-label="Open" onClick={() => setOpen({ ...open, [i.code]: !open[i.code] })}>{open[i.code] ? <ChevronDown size={12} /> : <ChevronRight size={12} />}</button> : <span className="class-spacer" />}
            <button type="button" className="link-button class-item" data-class-item={i.code} disabled={!onPick} onClick={() => onPick?.(i)}>
              <span className="mono" data-no-i18n>{i.code}</span> <span data-no-i18n>{i.title}</span>
            </button>
          </div>
          {i.children && open[i.code] && <Level system={system} parent={i.code} depth={depth + 1} onPick={onPick} picked={picked} />}
        </li>
      ))}
      {items.length === 0 && depth === 0 && <li className="muted small">No items yet.</li>}
    </ul>
  );
}

export function ItemTree({ system, search, onPick, picked }: { system: string; search: string; onPick?: (i: ClassItem) => void; picked?: string | null }) {
  const [found, setFound] = useState<{ items: ClassItem[]; count: number } | null>(null);
  const q = search.trim();
  useEffect(() => {
    if (!q) { setFound(null); return; }
    let live = true;
    const t = setTimeout(() => { void systemItems(system, { search: q }).then((r) => { if (live) setFound(r); }); }, 150);
    return () => { live = false; clearTimeout(t); };
  }, [system, q]);
  if (!q) return <Level key={system} system={system} parent={null} depth={0} onPick={onPick} picked={picked} />;
  return (
    <ul className="class-tree" data-testid="class-found">
      {(found?.items ?? []).map((i) => (
        <li key={i.code}><div className={`class-row${picked === i.code ? ' on' : ''}`}>
          <button type="button" className="link-button class-item" data-class-item={i.code} disabled={!onPick} onClick={() => onPick?.(i)}><span className="mono" data-no-i18n>{i.code}</span> <span data-no-i18n>{i.title}</span></button>
        </div></li>
      ))}
      {found && found.items.length === 0 && <li className="muted small">Nothing matches.</li>}
      {found && found.items.length >= 500 && <li className="muted small">The first 500 matches: narrow the search.</li>}
    </ul>
  );
}
