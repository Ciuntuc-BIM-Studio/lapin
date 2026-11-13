/**
 * A layer's material hatch, where its material is chosen (forum #30, decision 493): Wall types and Floor types show
 * each layer's cut hatch as a swatch, and a click on it opens the material's 2D graphics beside it, its cut (section
 * and plan) and elevation hatches, fills and pens, the same as Manage → Materials (MaterialGraphicsTable, the same
 * command, one undo step each change). It is the material's own: every type and element made of it changes too.
 */

import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { query, useSession } from '../../engine/session';
import { HatchSwatch } from './HatchSwatch';
import { MaterialGraphicsTable, type HatchPattern, type MaterialRow } from './MaterialGraphicsTable';
import { materialNamed, materialsOf, placeBeside } from './materialHatch';
import { openMaterialsFrom } from './dialogReturn';

/** `from`: the dialog this swatch is in and a snapshot of its editor, so that "Open in Materials…" can come back to it (forum #45). */
export interface HatchReturn { kind: string; snapshot: () => unknown }

export function MaterialHatchButton({ material, from }: { material: string; from?: HatchReturn }) {
  const nodes = useSession((s) => s.nodes);
  const [row, setRow] = useState<MaterialRow | null>(null);
  const [open, setOpen] = useState(false);
  const button = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    let live = true;
    void materialsOf(nodes).then((list) => { if (live) setRow(materialNamed(list, material)); });
    return () => { live = false; };
  }, [nodes, material]);
  const cut = row?.section;
  return (
    <>
      <button type="button" ref={button} className={`hatch-pick${open ? ' on' : ''}`} data-testid="layer-hatch" disabled={!row}
        title={row ? 'Its material\'s hatches: cut and elevation (the material\'s own, as in Manage → Materials)' : 'A new material: save the type first, then set its hatches'}
        onClick={() => setOpen((v) => !v)}>
        <HatchSwatch hatch={cut?.hatch ?? null} scale={cut?.hatchScale ?? 1} fill={cut?.fill ?? null} membrane={cut?.membrane ?? false} size={22} />
      </button>
      {open && row && <MaterialHatchEditor m={row} anchor={button.current} from={from} onClose={() => setOpen(false)} />}
    </>
  );
}

function MaterialHatchEditor({ m, anchor, from, onClose }: { m: MaterialRow; anchor: HTMLElement | null; from?: HatchReturn; onClose: () => void }) {
  const [hatches, setHatches] = useState<HatchPattern[]>([]);
  const [at, setAt] = useState<{ left: number; top: number } | null>(null);
  const panel = useRef<HTMLDivElement>(null);
  useEffect(() => { void query<HatchPattern[]>('hatch.list').then((r) => r && setHatches(r)); }, []);
  // Beside the button, inside the window; again when the table scrolls or the window changes.
  useLayoutEffect(() => {
    const place = () => {
      const r = anchor?.getBoundingClientRect(), p = panel.current;
      if (!r || !p) return;
      setAt(placeBeside(r, { w: p.offsetWidth, h: p.offsetHeight }, { w: window.innerWidth, h: window.innerHeight }));
    };
    place();
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);
    return () => { window.removeEventListener('resize', place); window.removeEventListener('scroll', place, true); };
  }, [anchor, hatches.length]);
  // Esc or a press outside it closes it (Esc not the dialog under it).
  useEffect(() => {
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); onClose(); } };
    const down = (e: PointerEvent) => {
      const t = e.target as Node;
      if (!panel.current?.contains(t) && !anchor?.contains(t)) onClose();
    };
    window.addEventListener('keydown', key, true);
    window.addEventListener('pointerdown', down, true);
    return () => { window.removeEventListener('keydown', key, true); window.removeEventListener('pointerdown', down, true); };
  }, [anchor, onClose]);
  const root = document.getElementById('root') ?? document.body;
  return createPortal(
    <div ref={panel} className="material-hatch-editor" data-testid="material-hatch-editor" role="dialog" aria-label="Material hatches"
      style={{ left: at?.left ?? -9999, top: at?.top ?? -9999 }}>
      <header className="row">
        <span className="panel-title">Hatches of <span data-no-i18n>{m.name}</span></span>
        <span className="grow" />
        <button type="button" className="icon mini" title="Close" onClick={onClose}><X size={13} /></button>
      </header>
      <MaterialGraphicsTable m={m} hatches={hatches} />
      {from && <button type="button" className="btn" data-testid="open-in-materials" onClick={() => openMaterialsFrom(from.kind, m.name, from.snapshot())}>Open in Materials…</button>}
      <p className="muted small">The material&apos;s own, as in Manage → Materials: every type and element made of it changes too.</p>
    </div>,
    root,
  );
}
