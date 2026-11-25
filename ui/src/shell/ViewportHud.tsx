/**
 * Atrium's HUD over the view: the mode and level card, the hint, the
 * numbered tool bar (1–9). View → Interface puts the card in the status line
 * or leaves it out, the hint in the command line, and the tool bar floating
 * at the bottom, docked on the view's left edge, or off. The tool bar's slots
 * are the user's (decision 259): a ribbon tool dropped on one, two slots
 * swapped by a drag, a slot's right-click menu; floating, each shows its name.
 */

import { useShallow } from 'zustand/react/shallow';
import { gridSnapStep, useGridScale, usePrefs } from '../app/prefs';
import { useUi } from '../app/store';
import { useCad } from '../viewport/cad/session';
import { useInterface } from '../app/interface';
import { hint } from '../app/commands';
import { t } from '../i18n';
import { activateSlot, findTool, hotbarIds, putInSlot, quickSlot, resetSlots, swapSlots, typeSlot, typesOf, useToolTick } from '../platform/modules';
import { reach, SLOT_DRAG, TOOL_DRAG, useHotbars } from '../app/hotbar';
import { openContextMenu } from './ContextMenu';
import { useEffect, useState, type DragEvent, type MouseEvent } from 'react';
import { activeStorey, useSession } from '../engine/session';
import { num } from '../engine/model';
import { ScopeBar } from './ScopeBar';
import { DrillCrumbs } from './DrillCrumbs';
import { formatLevel, lengthText, useStandards } from '../engine/unitsStore';
import { ChevronDown, ChevronLeft, ChevronRight, ChevronUp } from 'lucide-react';
import { scopeOf, setQuickToolsHidden, useBars } from '../app/bars';
import { useXrayPeek } from '../app/xrayPeek';

const level = (e: number) => formatLevel(e);

/** What the active tool asks for, when it is a two-point or a pick-then-point tool; null otherwise. */
/** The views whose own toolbar fills the pane's top, where the mode / level card would lie over it (reports #65, #170). */
export const hudHidesOver = (view: string): boolean => view === 'SCHEDULE' || view === 'SHEET';

export function toolHint(tool: string | null): string | null {
  const active = findTool(tool);
  const g = active?.gesture;
  if (!active || !g) return null;
  if (g.kind === 'twoPoints' && g.measure) return `${active.label}: click two points a known distance apart · type their true distance (2400, 50m) · Esc cancels`;
  const space = active.spaceHint?.();
  // A tool of its own words (a contour dragged): its hint, translated as a phrase.
  if (g.kind === 'twoPoints' && g.hint) return `${t(active.label)}: ${t(g.hint)}`;
  if (g.kind === 'twoPoints') return `${active.label}: click its start, then its end · Shift squares it${space ? ` · Space: ${space}` : ''} · ${g.repeat ? 'Enter ends the chain, Esc cancels' : 'Esc cancels'}`;
  if (g.kind === 'threePoints' && g.hint) return `${t(active.label)}: ${t(g.hint)}`;
  if (g.kind === 'threePoints' && g.arc && space) return `${active.label}: click its start, its end, then a point it passes through · Space: ${space} · Esc cancels`;
  if (g.kind === 'pickThenPoint') return `${active.label}: ${g.hint ?? 'click the element, then a point · Enter takes the first click alone, Esc cancels'}`;
  return null;
}

const KEY_RE = /\b(Shift|Space|Enter|Esc|Tab|Ctrl|Alt|Backspace)\b/g;

/** Splits a tool hint into plain text and keycap tokens while preserving exact textContent. */
export function splitHintTokens(text: string): { text: string; key?: string }[] {
  const out: { text: string; key?: string }[] = [];
  let last = 0;
  for (const m of text.matchAll(KEY_RE)) {
    const idx = m.index ?? 0;
    if (idx > last) out.push({ text: text.slice(last, idx) });
    out.push({ text: m[0], key: m[0].toLowerCase() });
    last = idx + m[0].length;
  }
  if (last < text.length) out.push({ text: text.slice(last) });
  return out;
}

/** Reactive keycap hint bar: highlights Shift/Ctrl/Alt/Space live while held. */
function KeycapHint({ text }: { text: string }) {
  const [held, setHeld] = useState<Set<string>>(() => new Set());
  useEffect(() => {
    const name = (e: KeyboardEvent): string | null =>
      e.key === 'Shift' ? 'shift' : e.key === 'Control' || e.key === 'Meta' ? 'ctrl' : e.key === 'Alt' ? 'alt' : e.key === ' ' ? 'space' : null;
    const down = (e: KeyboardEvent) => { const k = name(e); if (k) setHeld((s) => (s.has(k) ? s : new Set([...s, k]))); };
    const up = (e: KeyboardEvent) => { const k = name(e); if (k) setHeld((s) => { if (!s.has(k)) return s; const n = new Set(s); n.delete(k); return n; }); };
    const clear = () => setHeld((s) => (s.size ? new Set() : s));
    window.addEventListener('keydown', down);
    window.addEventListener('keyup', up);
    window.addEventListener('blur', clear);
    return () => { window.removeEventListener('keydown', down); window.removeEventListener('keyup', up); window.removeEventListener('blur', clear); };
  }, []);
  return (
    <div className="hud-hint">
      {splitHintTokens(text).map((tok, i) => tok.key
        ? <kbd key={i} className={`hint-key${held.has(tok.key) ? ' on' : ''}`} data-no-i18n>{tok.text}</kbd>
        : <span key={i}>{tok.text}</span>)}
    </div>
  );
}

/** The HUD's facts: the mode, the level and its elevation, the view, the snap. */
export function useHudFacts() {
  const tool = useUi((s) => s.tool);
  const view = useUi((s) => s.view);
  const style = useUi((s) => s.visualStyle);
  useUi((s) => s.storeyId);
  useSession((s) => s.nodes);
  useStandards((s) => s.units);
  const prefs = usePrefs(useShallow((p) => ({ gridSnap: p.gridSnap, gridMode: p.gridMode, gridStep: p.gridStep })));
  const scale = useGridScale((s) => s.scale);
  // The step the cursor really snaps to at this zoom (forum topic 226: the status line once said 50 mm whatever the zoom did).
  const snapStep = gridSnapStep(scale, prefs);
  const storey = activeStorey();
  const active = findTool(tool);
  return {
    mode: active ? active.label.toUpperCase() : 'SELECT',
    level: (storey?.name ?? '—').toUpperCase(), elevation: level(num(storey ?? undefined, 'elevation')),
    view: view === 'plan' ? 'PLAN' : view === 'drawing' ? 'DRAWING' : view === 'sheet' ? 'SHEET' : view === 'schedule' ? 'SCHEDULE' : view === 'gantt' ? 'GANTT' : view === 'estimate' ? 'ESTIMATE' : view === 'costTime' ? 'S-CURVE' : `3D · ${style === 'edges' ? 'EDGES' : style === 'xray' ? 'X-RAY' : style === 'wireframe' ? 'WIREFRAME' : 'SHADED'}`,
    snap: snapStep > 0 ? `POINTS · GRID ${lengthText(snapStep)}` : 'POINTS',
  };
}

export function ViewportHud() {
  const tool = useUi((s) => s.tool);
  const parts = useInterface((s) => s.parts);
  const facts = useHudFacts();
  const hotbars = useHotbars();
  const workspace = useInterface((s) => s.workspace);
  const ids = hotbarIds(hotbars, workspace);
  // The slots up to the last tool, and one empty after it to drop a tool on.
  const shown = ids.slice(0, Math.min(ids.length, reach(ids) + 1));
  const [over, setOver] = useState<number | null>(null);
  // The tools' current types and the project's types: a typed slot is lit only while its type is the tool's.
  useUi((s) => [s.wallTypeId, s.doorTypeId, s.windowTypeId, s.slabTypeId, s.stairTypeId, s.rampTypeId, s.ceilingTypeId, s.skirtingTypeId, s.curtainTypeId, s.memberTypeId, s.joistTypeId].join());
  useSession((s) => s.nodes);
  const docked = parts.quickTools === 'docked';
  // Hidden by its own button, apart in the model's views and in the documents (app/bars.ts).
  const scope = scopeOf(useUi((s) => s.view));
  const hidden = useBars((s) => s.quickToolsHidden[scope]);
  const drop = (i: number, e: DragEvent) => {
    e.preventDefault();
    setOver(null);
    const tool = e.dataTransfer.getData(TOOL_DRAG);
    const from = e.dataTransfer.getData(SLOT_DRAG);
    if (tool) putInSlot(i, tool);
    else if (from !== '') swapSlots(Number(from), i);
  };
  const menu = (i: number, e: MouseEvent) => {
    e.preventDefault();
    const q = quickSlot(ids[i]);
    // A typed tool's slot: the tool's current type, or one of the project's (decision 260).
    const types = typesOf(q?.tool ?? null).slice(0, 14);
    openContextMenu(e.clientX, e.clientY, [
      { label: 'Choose a tool…', onSelect: () => useUi.setState({ dialog: 'quickTools' }) },
      ...(q?.tool.typed ? [
        { label: 'The tool\'s current type', checked: !q.typeName, onSelect: () => typeSlot(i, null) },
        ...types.map((t) => ({ label: t.name, checked: q.typeName === t.name, onSelect: () => typeSlot(i, t.name) })),
      ] : []),
      { label: 'Empty this slot', disabled: !ids[i], onSelect: () => putInSlot(i, null) },
      { label: 'Reset quick tools', onSelect: resetSlots },
    ]);
  };
  // A repeating CAD command (Symbol) says how to go on and how to finish.
  const stepHint = useCad((s) => s.step?.hint ?? null);
  useToolTick((s) => s.n); // Space while a tool is on changes what its hint says (a wall's line)
  const hintText = stepHint ?? toolHint(tool);
  const xrayPeeking = useXrayPeek((s) => s.active);
  return (
    <>
      {xrayPeeking && <div className="xray-peek-badge" role="status">X-Ray peek · release <kbd data-no-i18n>X</kbd></div>}
      {/* Not over a schedule or a sheet: their toolbars run across the pane's top (reports #65, #170), and a schedule has no mode, level or snap to tell. */}
      {parts.hud === 'over' && !hudHidesOver(facts.view) && (
        <div className="hud-card">
          <span className="eyebrow">Mode</span><span>{facts.mode}</span>
          <span className="eyebrow">Level</span><span>{facts.level} <span className="accent">{facts.elevation}</span></span>
          <span className="eyebrow">View</span><span>{facts.view}</span>
          <span className="eyebrow">Snap</span><span>{facts.snap}</span>
        </div>
      )}
      <ScopeBar />
      {hintText && parts.prompt === 'banner' && <KeycapHint text={hintText} />}
      <DrillCrumbs />
      {parts.quickTools !== 'off' && hidden && (
        <button type="button" className={`hotbar-handle${docked ? ' docked' : ''}`} data-testid="quick-tools-show" title="Show the quick tools" onClick={() => setQuickToolsHidden(scope, false)}>
          {docked ? <ChevronRight size={14} /> : <><ChevronUp size={14} /><span className="small">Quick tools</span></>}
        </button>
      )}
      {parts.quickTools !== 'off' && !hidden && (
        <div className={`hotbar${docked ? ' docked' : ' named'}`} role="toolbar" aria-label="Quick tools">
          {shown.map((id, i) => {
            const q = quickSlot(id);
            const t = q?.tool ?? null;
            const target = { onDragOver: (e: DragEvent) => { e.preventDefault(); setOver(i); }, onDragLeave: () => setOver(null), onDrop: (e: DragEvent) => drop(i, e), onContextMenu: (e: MouseEvent) => menu(i, e) };
            if (!t) return (
              <button type="button" key={i} className={`slot empty${over === i ? ' over' : ''}`} title="Drag a tool here from the ribbon, or click to choose one" onClick={() => useUi.setState({ dialog: 'quickTools' })} {...target}>
                <span className="slot-n">{i + 1}</span>
                <span className="slot-plus" aria-hidden>+</span>
              </button>
            );
            const on = tool === t.id && (!q!.typeId || t.typed?.get() === q!.typeId);
            const title = q!.typeName ? `${hint(t.id, t.label)} · ${q!.typeName}${q!.typeId ? '' : ' (not in this project)'}` : hint(t.id, t.label);
            return (
              <button type="button" key={i} data-tool={t.id} data-type={q!.typeName ?? undefined} className={`slot${on ? ' on' : ''}${over === i ? ' over' : ''}${q!.typeName ? ' typed' : ''}`} onClick={() => activateSlot(q!)} title={title}
                draggable onDragStart={(e) => { e.dataTransfer.setData(SLOT_DRAG, String(i)); e.dataTransfer.effectAllowed = 'move'; }} {...target}>
                <span className="slot-n">{i + 1}</span>
                <t.icon size={docked ? 15 : 20} strokeWidth={1.5} />
                {!docked && (q!.typeName ? <span className="slot-name" data-no-i18n>{q!.typeName}</span> : <span className="slot-name">{t.label}</span>)}
              </button>
            );
          })}
          <button type="button" className="hotbar-hide" data-testid="quick-tools-hide" title={scope === 'docs' ? 'Hide the quick tools in sheets and schedules' : 'Hide the quick tools'} onClick={() => setQuickToolsHidden(scope, true)}>
            {docked ? <ChevronLeft size={13} /> : <ChevronDown size={13} />}
          </button>
        </div>
      )}
    </>
  );
}
