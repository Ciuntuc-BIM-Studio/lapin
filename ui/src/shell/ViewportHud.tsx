/**
 * Atrium's HUD over the view: the mode and level card, the hint, the
 * numbered tool bar (1–9). View → Interface puts the card in the status line
 * or leaves it out, the hint in the command line, and the tool bar floating
 * at the bottom, docked on the view's left edge, or off. The tool bar's slots
 * are the user's (decision 259): a ribbon tool dropped on one, two slots
 * swapped by a drag, a slot's right-click menu; floating, each shows its name.
 */

import { useUi } from '../app/store';
import { useInterface } from '../app/interface';
import { hint } from '../app/commands';
import { activateSlot, findTool, hotbarIds, putInSlot, quickSlot, resetSlots, swapSlots, typeSlot, typesOf } from '../platform/modules';
import { reach, SLOT_DRAG, TOOL_DRAG, useHotbars } from '../app/hotbar';
import { openContextMenu } from './ContextMenu';
import { useState, type DragEvent, type MouseEvent } from 'react';
import { activeStorey, useSession } from '../engine/session';
import { num } from '../engine/model';
import { ScopeBar } from './ScopeBar';
import { formatLevel, lengthText, useStandards } from '../engine/unitsStore';

const level = (e: number) => formatLevel(e);

/** What the active tool asks for, when it is a two-point or a pick-then-point tool; null otherwise. */
export function toolHint(tool: string | null): string | null {
  const active = findTool(tool);
  const g = active?.gesture;
  if (!active || !g) return null;
  if (g.kind === 'twoPoints') return `${active.label}: click its start, then its end · Shift squares it · Esc stops`;
  if (g.kind === 'pickThenPoint') return `${active.label}: ${g.hint ?? 'click the element, then a point · Enter or Esc takes the first click alone'}`;
  return null;
}

/** The HUD's facts: the mode, the level and its elevation, the view, the snap. */
export function useHudFacts() {
  const tool = useUi((s) => s.tool);
  const view = useUi((s) => s.view);
  useUi((s) => s.storeyId);
  useSession((s) => s.nodes);
  useStandards((s) => s.units);
  const storey = activeStorey();
  const active = findTool(tool);
  return {
    mode: active ? active.label.toUpperCase() : 'SELECT',
    level: (storey?.name ?? '—').toUpperCase(), elevation: level(num(storey ?? undefined, 'elevation')),
    view: view === 'plan' ? 'PLAN' : view === 'drawing' ? 'DRAWING' : view === 'sheet' ? 'SHEET' : view === 'schedule' ? 'SCHEDULE' : view === 'gantt' ? 'GANTT' : '3D · SHADED',
    snap: `POINTS · GRID ${lengthText(0.05)}`,
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
  useUi((s) => [s.wallTypeId, s.doorTypeId, s.windowTypeId, s.slabTypeId, s.stairTypeId, s.rampTypeId, s.ceilingTypeId, s.skirtingTypeId, s.curtainTypeId, s.memberTypeId].join());
  useSession((s) => s.nodes);
  const docked = parts.quickTools === 'docked';
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
  const hintText = toolHint(tool);
  return (
    <>
      {/* Not over a schedule: its toolbar runs across the pane's top, and it has no mode, level or snap to tell (report #65). */}
      {parts.hud === 'over' && facts.view !== 'SCHEDULE' && (
        <div className="hud-card">
          <span className="eyebrow">Mode</span><span>{facts.mode}</span>
          <span className="eyebrow">Level</span><span>{facts.level} <span className="accent">{facts.elevation}</span></span>
          <span className="eyebrow">View</span><span>{facts.view}</span>
          <span className="eyebrow">Snap</span><span>{facts.snap}</span>
        </div>
      )}
      <ScopeBar />
      {hintText && parts.prompt === 'banner' && <div className="hud-hint">{hintText}</div>}
      {parts.quickTools !== 'off' && (
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
                <t.icon size={docked ? 17 : 20} strokeWidth={1.5} />
                {!docked && (q!.typeName ? <span className="slot-name" data-no-i18n>{q!.typeName}</span> : <span className="slot-name">{t.label}</span>)}
              </button>
            );
          })}
        </div>
      )}
    </>
  );
}
