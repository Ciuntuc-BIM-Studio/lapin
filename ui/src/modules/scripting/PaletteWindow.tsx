/**
 * A script's palettes (`lapin.ui.palette`, decision 526): small floating panels of the window's own, drawn from the rows
 * the script registered (palette.ts reads them) — headings, texts, separators, the fields `lapin.ui.ask` has (the same
 * Field as AskDialog.tsx) and buttons — never anything of the script's to run. Non-modal, without a backdrop, moved by
 * their title bar; each opened and closed by its button in My tools. Where it floats and what its fields hold are kept on
 * this computer per palette. A button pressed runs like a tool (scriptTools.ts: one Run, one undo step named after the
 * button) with the palette's values, checked first; the buttons wait while a script runs.
 */

import { useEffect, useMemo, useRef, useState, type PointerEvent } from 'react';
import { PanelsTopLeft, X } from 'lucide-react';
import { collect, initialState, type FieldState } from './ask';
import { Field, useFacts } from './AskDialog';
import { scriptIcon } from './icons';
import { clampAt, fieldsOf, readMemory, type PaletteButton, type PaletteMemory } from './palette';
import { runTool, togglePalette, useScriptTools } from './scriptTools';
import { useScripting } from './store';
import type { ScriptTool } from './tools';

const memoryKey = (id: string) => `lapin.scriptPalette.${id}`;

function readKept(id: string): unknown {
  try { const raw = localStorage.getItem(memoryKey(id)); return raw ? JSON.parse(raw) : null; } catch { return null; }
}
function keep(id: string, m: PaletteMemory): void {
  try { localStorage.setItem(memoryKey(id), JSON.stringify(m)); } catch { /* this session only */ }
}

const win = () => ({ w: window.innerWidth, h: window.innerHeight });

function PaletteWindow({ p, place }: { p: ScriptTool; place: number }) {
  const rows = p.rows ?? [];
  const fields = useMemo(() => fieldsOf(rows), [rows]);
  const facts = useFacts();
  const kept = useMemo(() => readMemory(readKept(p.id), fields), [p.id, fields]);
  const [state, setState] = useState<Record<string, FieldState>>(() => ({ ...initialState(fields, facts), ...kept.state }));
  const [at, setAt] = useState(() => clampAt(kept.x ?? window.innerWidth - 300 - place * 24, kept.y ?? 140 + place * 28, win()));
  const [wrong, setWrong] = useState<{ key: string; error: string } | null>(null);
  const running = useScripting((s) => s.running != null);
  const drag = useRef<{ dx: number; dy: number } | null>(null);
  const box = useRef<HTMLElement>(null);

  // What the fields hold and where the palette is, kept as they change.
  useEffect(() => { keep(p.id, { x: at.x, y: at.y, state }); }, [p.id, at, state]);

  const down = (e: PointerEvent<HTMLElement>) => {
    if ((e.target as HTMLElement).closest('button')) return;
    drag.current = { dx: e.clientX - at.x, dy: e.clientY - at.y };
    e.currentTarget.setPointerCapture(e.pointerId);
  };
  const move = (e: PointerEvent<HTMLElement>) => {
    if (!drag.current) return;
    const r = box.current?.getBoundingClientRect();
    setAt(clampAt(e.clientX - drag.current.dx, e.clientY - drag.current.dy, win(), r ? { w: r.width, h: 40 } : undefined));
  };
  const up = () => { drag.current = null; };

  const press = (b: PaletteButton) => {
    const r = collect(fields, state);
    if ('error' in r) { setWrong(r); return; }
    setWrong(null);
    void runTool(p.id, { name: b.name, call: b.call, values: r.values });
  };
  const button = (b: PaletteButton) => {
    const Icon = b.icon ? scriptIcon(b.icon, b.name) : null;
    return (
      <button key={b.name} type="button" className="btn" disabled={running} title={b.tooltip} data-palette-button={b.name} onClick={() => press(b)}>
        {Icon && <Icon size={14} strokeWidth={1.6} />}<span data-no-i18n>{b.name}</span>
      </button>
    );
  };
  const Icon = p.icon ? scriptIcon(p.icon, p.name) : PanelsTopLeft;
  return (
    <section ref={box} className="script-palette" role="dialog" aria-label={p.name} data-palette={p.name} style={{ left: at.x, top: at.y }}
      onKeyDown={(e) => e.stopPropagation()}>
      <header className="script-palette-head" onPointerDown={down} onPointerMove={move} onPointerUp={up} onPointerCancel={up} title={p.tooltip}>
        <span className="panel-title"><Icon size={14} /> <span data-no-i18n>{p.name}</span></span>
        <button type="button" className="icon" onClick={() => togglePalette(p.id)} title="Close" aria-label="Close" data-action="palette-close"><X size={14} /></button>
      </header>
      <div className="script-palette-body">
        {rows.map((r, i) => {
          switch (r.row) {
            case 'heading': return <h4 key={i} className="script-palette-heading" data-no-i18n>{r.text}</h4>;
            case 'text': return <p key={i} className="small" data-no-i18n>{r.text}</p>;
            case 'separator': return <hr key={i} />;
            case 'field': return (
              <Field key={i} f={r.field} value={state[r.field.key]} facts={facts} wrong={wrong?.key === r.field.key} prefix={`palette-${p.id}`}
                set={(v) => { setState((s) => ({ ...s, [r.field.key]: v })); if (wrong?.key === r.field.key) setWrong(null); }} />
            );
            case 'buttons': return <div key={i} className="script-palette-buttons">{r.buttons.map(button)}</div>;
          }
        })}
        {wrong && <p className="error-text small" data-field="palette-error">{wrong.error}</p>}
      </div>
    </section>
  );
}

/** The palettes open now, each keyed by its fields (a script that changes them starts it afresh, keeping what still fits). */
export function ScriptPalettes() {
  const open = useScriptTools((s) => s.open);
  const palettes = useScriptTools((s) => s.palettes);
  return (
    <>
      {open.map((id, i) => {
        const p = palettes.find((x) => x.id === id);
        if (!p) return null;
        const sig = fieldsOf(p.rows ?? []).map((f) => `${f.key}:${f.kind}`).join(',');
        return <PaletteWindow key={`${id}|${sig}`} p={p} place={i} />;
      })}
    </>
  );
}
