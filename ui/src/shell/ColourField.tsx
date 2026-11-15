/**
 * The one colour field of the interface (forum topic 97): the system's colour input, as it always was (`#rrggbb`; a value with
 * an opacity, `#rrggbbaa`, keeps its opacity when the colour changes), and beside it a button that opens the swatches: the
 * colours chosen lately, the project's palette (kept in the project) and the user's own palettes. Choosing a swatch is
 * choosing a colour. "Edit palettes…" opens the manager (PaletteManager), also in File → Preferences → Colours.
 */

import { useEffect, useLayoutEffect, useMemo, useRef, useState, type InputHTMLAttributes } from 'react';
import { colourDraft } from './colourDraft';
import { createPortal } from 'react-dom';
import { ArrowLeft, ArrowRight, ChevronDown, Plus, Trash2, X } from 'lucide-react';
import { addColour, addColours, coloursOfMaterials, createPalette, deletePalette, moveColour, movePalette, normalise, removeColour, renamePalette, rememberColour, setPalettes, splitAlpha, useColours, cleanColours } from '../app/colours';
import { execute, query, useSession } from '../engine/session';
import type { NodeDto } from '../engine/protocol';

/** The project's palette, as the engine keeps it on the project (a list of #rrggbb). */
export const projectPaletteOf = (raw: unknown): string[] => cleanColours(raw);
const projectNode = (nodes: ReadonlyMap<string, NodeDto>) => { for (const n of nodes.values()) if (n.type === 'IfcProject') return n; return undefined; };

/** The project's palette, and whether a project is open to keep one in. */
export function useProjectPalette(): { colours: string[]; open: boolean } {
  const raw = useSession((s) => projectNode(s.nodes)?.props.colourPalette);
  const open = useSession((s) => !!projectNode(s.nodes));
  return { colours: useMemo(() => projectPaletteOf(raw), [raw]), open };
}

const saveProjectPalette = (colours: string[]) => void execute('project.setColourPalette', { colours });

type FieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'value' | 'onChange'> & {
  value: string;
  onChange: (value: string) => void;
  /** A field that keeps the colour only when it is settled (a table's cell): called when a swatch is chosen and when the colour input is left. */
  onCommit?: (value: string) => void;
};

export function ColourField({ value, onChange, onCommit, disabled, ...rest }: FieldProps) {
  const { rgb, alpha } = splitAlpha(value);
  const [open, setOpen] = useState(false);
  const [, bump] = useState(0);
  const button = useRef<HTMLButtonElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const later = useRef<ReturnType<typeof setTimeout> | null>(null);
  // A field with no onCommit keeps the colour in itself while the picker is open and sends it once, when the pick is settled;
  // one with onCommit (a table's cell) is told of every move and of the settled colour.
  const live = useRef({ value, alpha, onChange, onCommit });
  live.current = { value, alpha, onChange, onCommit };
  const draft = useMemo(() => colourDraft(() => normalise(splitAlpha(live.current.value).rgb) ?? '#000000', (c) => live.current.onChange(`${c}${live.current.alpha}`)), []);
  const remember = (c: string, now = false) => {
    if (later.current) clearTimeout(later.current);
    if (now) rememberColour(c); else later.current = setTimeout(() => rememberColour(c), 700);
  };
  const settle = () => { const was = draft.pending(); draft.settle(); if (was) bump((n) => n + 1); };
  const move = (c: string) => {
    remember(c);
    if (live.current.onCommit) live.current.onChange(`${c}${live.current.alpha}`);
    else { draft.move(c); bump((n) => n + 1); }
  };
  const pick = (c: string) => { draft.move(c); settle(); remember(c, true); live.current.onCommit?.(`${c}${live.current.alpha}`); };
  useEffect(() => () => { if (later.current) clearTimeout(later.current); }, []);
  // The picker's own `change` event is the end of a pick (React has no event for it).
  useEffect(() => {
    const el = input.current;
    if (!el) return;
    const done = () => { if (live.current.onCommit) live.current.onCommit(`${el.value}${live.current.alpha}`); else settle(); };
    el.addEventListener('change', done);
    return () => el.removeEventListener('change', done);
  }, []);
  // Leaving the field while a pick is open settles it too.
  useEffect(() => () => draft.settle(), [draft]);
  return (
    <span className="colour-field">
      <input {...rest} ref={input} type="color" disabled={disabled} value={draft.shown(normalise(rgb) ?? '#000000')} onChange={(e) => move(e.target.value)}
        onBlur={(e) => { rest.onBlur?.(e); if (onCommit) onCommit(value); else settle(); }} />
      <button type="button" className="colour-more" ref={button} disabled={disabled} aria-haspopup="dialog" aria-expanded={open}
        title="Swatches: recent colours and palettes" onClick={() => setOpen((o) => !o)}><ChevronDown size={10} /></button>
      {open && <ColourPopover anchor={button} current={normalise(rgb) ?? '#000000'} onPick={pick} onClose={() => setOpen(false)} />}
    </span>
  );
}

function Swatches({ colours, current, onPick }: { colours: string[]; current: string; onPick: (c: string) => void }) {
  if (!colours.length) return <span className="muted small">None yet</span>;
  return <span className="cp-row">{colours.map((c) => (
    <button type="button" key={c} className={`cp-swatch${c === current ? ' on' : ''}`} style={{ background: c }} title={c} aria-label={c} onClick={() => onPick(c)} />
  ))}</span>;
}

function ColourPopover({ anchor, current, onPick, onClose }: { anchor: React.RefObject<HTMLButtonElement | null>; current: string; onPick: (c: string) => void; onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null);
  const [at, setAt] = useState<{ x: number; y: number } | null>(null);
  const [managing, setManaging] = useState(false);
  const [hex, setHex] = useState(current);
  const { recent, palettes } = useColours();
  const project = useProjectPalette();

  useLayoutEffect(() => {
    const a = anchor.current?.getBoundingClientRect(), el = ref.current;
    if (!a || !el) return;
    const w = el.offsetWidth, h = el.offsetHeight;
    const x = Math.max(4, Math.min(a.left, window.innerWidth - w - 4));
    const y = a.bottom + 4 + h <= window.innerHeight - 4 ? a.bottom + 4 : Math.max(4, a.top - 4 - h);
    setAt({ x, y });
  }, [anchor, managing, palettes.length, recent.length, project.colours.length]);

  useEffect(() => {
    const down = (e: PointerEvent) => { const t = e.target as Node; if (!ref.current?.contains(t) && !anchor.current?.contains(t)) onClose(); };
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.stopPropagation(); onClose(); } };
    window.addEventListener('pointerdown', down, true);
    window.addEventListener('keydown', key, true);
    return () => { window.removeEventListener('pointerdown', down, true); window.removeEventListener('keydown', key, true); };
  }, [anchor, onClose]);

  const addTo = (add: () => void, title: string) => <button type="button" className="icon mini" title={title} onClick={add}><Plus size={11} /></button>;
  return createPortal(
    <div ref={ref} className="colour-pop" role="dialog" aria-label="Colour swatches" onClick={(e) => e.stopPropagation()} onMouseDown={(e) => e.stopPropagation()} onPointerDown={(e) => e.stopPropagation()} style={at ? { left: at.x, top: at.y } : { left: -9999, top: 0 }}>
      {managing ? (
        <>
          <div className="cp-head"><button type="button" className="btn compact" onClick={() => setManaging(false)}><ArrowLeft size={12} />Back</button></div>
          <PaletteManager current={current} />
        </>
      ) : (
        <>
          <div className="cp-head">
            <span className="cp-now" style={{ background: current }} />
            <input type="text" className="cp-hex" aria-label="Colour" spellCheck={false} value={hex} onChange={(e) => { setHex(e.target.value); const n = normalise(e.target.value); if (n) onPick(n); }} onBlur={() => setHex(current)} />
          </div>
          <div className="cp-section"><span className="eyebrow">Recent</span><Swatches colours={recent} current={current} onPick={onPick} /></div>
          <div className="cp-section">
            <span className="eyebrow">Project palette {project.open && addTo(() => saveProjectPalette(addColours(project.colours, [current])), 'Add this colour')}</span>
            <Swatches colours={project.colours} current={current} onPick={onPick} />
          </div>
          {palettes.map((p) => (
            <div className="cp-section" key={p.id}>
              <span className="eyebrow"><span data-no-i18n>{p.name}</span> {addTo(() => setPalettes(addColour(palettes, p.id, current)), 'Add this colour')}</span>
              <Swatches colours={p.colours} current={current} onPick={onPick} />
            </div>
          ))}
          <div className="cp-foot"><button type="button" className="btn compact" onClick={() => setManaging(true)}>Edit palettes…</button></div>
        </>
      )}
    </div>,
    document.body,
  );
}

/** One list of colours edited: add (the colour chosen here, or the materials' in use), select one to move it earlier or later or take it out. */
function PaletteEditor({ colours, onChange, current, materials }: { colours: string[]; onChange: (next: string[]) => void; current?: string; materials?: boolean }) {
  const [sel, setSel] = useState<number | null>(null);
  const [adding, setAdding] = useState(current ?? '#b5532d');
  const fromMaterials = async () => {
    const rows = await query<{ color?: string; inUse?: boolean }[]>('material.list') ?? [];
    onChange(addColours(colours, coloursOfMaterials(rows)));
  };
  return (
    <div className="cp-editor">
      <span className="cp-row">
        {colours.map((c, i) => <button type="button" key={c} className={`cp-swatch${sel === i ? ' on' : ''}`} style={{ background: c }} title={c} aria-label={c} onClick={() => setSel(sel === i ? null : i)} />)}
        {!colours.length && <span className="muted small">None yet</span>}
      </span>
      {sel !== null && colours[sel] && (
        <span className="cp-tools">
          <button type="button" className="icon mini" title="Move left" disabled={sel === 0} onClick={() => { onChange(moveColour(colours, sel, sel - 1)); setSel(sel - 1); }}><ArrowLeft size={12} /></button>
          <button type="button" className="icon mini" title="Move right" disabled={sel === colours.length - 1} onClick={() => { onChange(moveColour(colours, sel, sel + 1)); setSel(sel + 1); }}><ArrowRight size={12} /></button>
          <button type="button" className="icon mini" title="Remove the colour" onClick={() => { onChange(removeColour(colours, sel)); setSel(null); }}><X size={12} /></button>
        </span>
      )}
      <span className="cp-add">
        <input type="color" aria-label="Colour to add" value={adding} onChange={(e) => setAdding(e.target.value)} />
        <button type="button" className="btn compact" onClick={() => onChange(addColours(colours, [adding]))}><Plus size={12} />Add</button>
        {materials && <button type="button" className="btn compact" onClick={() => void fromMaterials()}>Add the colours of the materials in use</button>}
      </span>
    </div>
  );
}

/** The palettes: the project's (kept in it) and the user's own (kept on this computer), created, renamed, moved and deleted. */
export function PaletteManager({ current }: { current?: string }) {
  const { palettes } = useColours();
  const project = useProjectPalette();
  const [name, setName] = useState('');
  return (
    <div className="palette-manager" data-testid="palette-manager">
      <h4>Project palette</h4>
      {project.open ? (
        <>
          <PaletteEditor colours={project.colours} onChange={saveProjectPalette} current={current} materials />
          <p className="muted small">Kept in the project: everyone who opens it sees these swatches.</p>
        </>
      ) : <p className="muted small">Open a project to keep a palette in it.</p>}
      <h4>My palettes</h4>
      {palettes.map((p, i) => (
        <div className="cp-palette" key={p.id} data-palette={p.name}>
          <span className="cp-palette-head">
            <input type="text" aria-label="Palette name" defaultValue={p.name} key={p.name} data-no-i18n onBlur={(e) => setPalettes(renamePalette(palettes, p.id, e.target.value))} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
            <button type="button" className="icon mini" title="Move the palette up" disabled={i === 0} onClick={() => setPalettes(movePalette(palettes, p.id, -1))}><ArrowLeft size={12} style={{ transform: 'rotate(90deg)' }} /></button>
            <button type="button" className="icon mini" title="Move the palette down" disabled={i === palettes.length - 1} onClick={() => setPalettes(movePalette(palettes, p.id, 1))}><ArrowRight size={12} style={{ transform: 'rotate(90deg)' }} /></button>
            <button type="button" className="icon mini" title="Delete the palette" onClick={() => setPalettes(deletePalette(palettes, p.id))}><Trash2 size={12} /></button>
          </span>
          <PaletteEditor colours={p.colours} current={current} materials onChange={(next) => setPalettes(palettes.map((x) => (x.id === p.id ? { ...x, colours: next } : x)))} />
        </div>
      ))}
      <span className="cp-add">
        <input type="text" placeholder="Palette name" aria-label="Palette name" value={name} onChange={(e) => setName(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter' && name.trim()) { setPalettes(createPalette(palettes, name)); setName(''); } }} />
        <button type="button" className="btn compact" disabled={!name.trim()} onClick={() => { setPalettes(createPalette(palettes, name)); setName(''); }}><Plus size={12} />New palette</button>
      </span>
      <p className="muted small">Kept on this computer, for every project.</p>
    </div>
  );
}
