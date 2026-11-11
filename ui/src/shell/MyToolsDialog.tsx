/**
 * View → Customize My tools… (decision 479): the whole application's tools on the left (searched, grouped by ribbon tab) and
 * the user's My tools tab on the right (its groups and their tools). A tool is put in by dragging it across, by its + button or
 * by Enter; ordered by dragging (a line shows where it will land), by the ↑ ↓ buttons, by Alt+arrows (↑ ↓ within a group and
 * across its ends, ← → to the group before or after) and by the move-to list; taken out by × , Delete or by dragging it back to
 * the left. Every change is applied at once and kept; Undo (Ctrl+Z) takes back the last, Reset empties the tab (and Undo brings
 * it back), so a mistaken drop loses nothing.
 */

import { useLayoutEffect, useMemo, useRef, useState, type DragEvent, type KeyboardEvent } from 'react';
import { ArrowDown, ArrowUp, Check, Download, FolderPlus, GripVertical, Maximize2, Minimize2, Plus, RotateCcw, Search, Trash2, Undo2, Upload, X } from 'lucide-react';
import { useUi } from '../app/store';
import { TOOL_DRAG } from '../app/hotbar';
import { t } from '../i18n';
import { useSession } from '../engine/session';
import {
  MY_DRAG, MY_GROUP_DRAG, addGroup, addTool, changeMyTab, deleteGroup, getDragged, has, locate, moveGroup, myTab, nudgeGroup, nudgeTool, removeTool, renameGroup,
  resetMyTab, setDragged, setSize, stepTool, undoMyTools, useMyTools, visibleTab, type MyGroup, type MyItem,
} from '../app/myTools';
import { choices } from './QuickToolsDialog';
import { exportMyTools, importMyTools, toolsById, toolOfDrag } from './MyToolsBand';
import { useFlip } from './flip';
import type { ToolDef } from '../platform/module';

type Over = { kind: 'tool'; group: string; index: number } | { kind: 'group'; index: number } | { kind: 'catalogue' } | null;

export function MyToolsDialog() {
  useSession((s) => s.nodes);
  const stored = useMyTools((s) => myTab(s.tabs));
  const canUndo = useMyTools((s) => s.past.length > 0);
  const byId = toolsById();
  const tab = visibleTab(stored, (id) => byId.has(id));
  const [query, setQuery] = useState('');
  const [target, setTarget] = useState<string | null>(null);
  const [over, setOver] = useState<Over>(null);
  const [focus, setFocus] = useState<string | null>(null);
  const right = useRef<HTMLDivElement>(null);
  const close = () => useUi.setState({ dialog: null });
  const groups = useMemo(() => choices(), [byId.size]);   // eslint-disable-line react-hooks/exhaustive-deps
  const q = query.trim().toLowerCase();
  const shown = groups.map((g) => ({ ...g, tools: g.tools.filter((x) => !q || x.label.toLowerCase().includes(q) || g.label.toLowerCase().includes(q)) })).filter((g) => g.tools.length > 0);
  const targetId = tab.groups.some((g) => g.id === target) ? target : tab.groups[tab.groups.length - 1]?.id ?? null;
  useFlip(right, tab.groups.map((g) => `${g.id}:${g.items.map((i) => i.tool).join()}`).join('|'));

  // After a change by keys the row keeps the focus (or the one that took its place).
  useLayoutEffect(() => {
    if (!focus) return;
    const el = document.querySelector<HTMLElement>(`.my-tools-dialog [data-row="${CSS.escape(focus)}"]`);
    if (el) el.focus();
    setFocus(null);
  }, [focus, stored]);

  const addFromCatalogue = (id: string) => {
    changeMyTab((tb) => addTool(tb, id, { group: targetId }));
    setFocus(id);
  };
  const clear = () => { setOver(null); setDragged(null); };

  const dragTool = (id: string, from: 'tab' | 'catalogue') => ({
    draggable: true,
    onDragStart: (e: DragEvent) => { e.dataTransfer.setData(TOOL_DRAG, id); e.dataTransfer.setData(MY_DRAG, id); e.dataTransfer.effectAllowed = 'copyMove'; setDragged({ kind: 'tool', tool: id, from }); },
    onDragEnd: clear,
  });

  const draggingTool = () => getDragged()?.kind === 'tool';
  const overRow = (e: DragEvent, g: MyGroup, i: number) => {
    if (!draggingTool()) return;
    e.preventDefault(); e.stopPropagation();
    const r = e.currentTarget.getBoundingClientRect();
    const index = e.clientY > r.top + r.height / 2 ? i + 1 : i;
    setOver((o) => (o?.kind === 'tool' && o.group === g.id && o.index === index ? o : { kind: 'tool', group: g.id, index }));
  };
  const overCard = (e: DragEvent, g: MyGroup, gi: number) => {
    const d = getDragged();
    if (d?.kind === 'group') {
      e.preventDefault(); e.stopPropagation();
      const r = e.currentTarget.getBoundingClientRect();
      const index = e.clientY > r.top + r.height / 2 ? gi + 1 : gi;
      setOver((o) => (o?.kind === 'group' && o.index === index ? o : { kind: 'group', index }));
    } else if (d?.kind === 'tool') {
      e.preventDefault(); e.stopPropagation();
      setOver((o) => (o?.kind === 'tool' && o.group === g.id && o.index === g.items.length ? o : { kind: 'tool', group: g.id, index: g.items.length }));
    }
  };
  const dropped = (e: DragEvent) => {
    e.preventDefault(); e.stopPropagation();
    const o = over, d = getDragged();
    clear();
    if (!o) return;
    if (o.kind === 'group' && d?.kind === 'group') { const id = d.id; changeMyTab((tb) => moveGroup(tb, id, o.index)); return; }
    if (o.kind === 'catalogue') { if (d?.kind === 'tool' && d.from === 'tab') { const id = d.tool; changeMyTab((tb) => removeTool(tb, id)); } return; }
    const id = toolOfDrag(e);
    if (o.kind === 'tool' && id) { changeMyTab((tb) => addTool(tb, id, { group: o.group, index: o.index })); setTarget(o.group); }
  };

  const rowKey = (e: KeyboardEvent, id: string) => {
    if (e.target !== e.currentTarget) return;
    const at = locate(tab, id);
    if (e.altKey && ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key)) {
      e.preventDefault(); e.stopPropagation();
      changeMyTab((tb) => (e.key === 'ArrowUp' ? stepTool(tb, id, -1) : e.key === 'ArrowDown' ? stepTool(tb, id, 1) : nudgeTool(tb, id, e.key === 'ArrowLeft' ? 'left' : 'right')));
      setFocus(id);
    } else if (e.key === 'Delete' || e.key === 'Backspace') {
      e.preventDefault();
      const g = at ? tab.groups[at.g] : null;
      const next = g ? (g.items[at!.i + 1] ?? g.items[at!.i - 1])?.tool ?? null : null;
      changeMyTab((tb) => removeTool(tb, id));
      if (next) setFocus(next);
    } else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      // Plain arrows walk the rows.
      e.preventDefault();
      const rows = [...document.querySelectorAll<HTMLElement>('.my-tools-dialog .my-tab [data-row]')];
      const i = rows.indexOf(e.currentTarget as HTMLElement);
      rows[i + (e.key === 'ArrowDown' ? 1 : -1)]?.focus();
    }
  };

  const onKey = (e: KeyboardEvent) => {
    if ((e.ctrlKey || e.metaKey) && !e.shiftKey && e.key.toLowerCase() === 'z' && !(e.target as HTMLElement).matches('input')) { e.preventDefault(); e.stopPropagation(); undoMyTools(); }
    else if (e.key === 'Escape') { e.stopPropagation(); close(); }
  };

  const small = (it: MyItem) => !it.large;
  const rowMark = (g: MyGroup, i: number) => {
    if (over?.kind !== 'tool' || over.group !== g.id) return '';
    if (over.index === i) return ' drop-before';
    if (over.index === g.items.length && i === g.items.length - 1) return ' drop-after';
    return '';
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Customize My tools" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }} onKeyDown={onKey}>
      <div className="dialog wide my-tools-dialog">
        <header className="dialog-head">
          <span className="panel-title">Customize My tools</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body my-tools-body">
          <p className="muted small my-tools-help">Drag tools from the left into your groups on the right, and drag them there to reorder; or select a tool and press Alt+arrows. Right-click any tool in the ribbon → Add to My tools works too.</p>
          <div className="my-tools-cols">
            <section className={`my-catalogue${over?.kind === 'catalogue' ? ' remove' : ''}`} aria-label="All tools"
              onDragOver={(e) => { const d = getDragged(); if (d?.kind === 'tool' && d.from === 'tab') { e.preventDefault(); setOver({ kind: 'catalogue' }); } }}
              onDragLeave={(e) => { if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setOver((o) => (o?.kind === 'catalogue' ? null : o)); }}
              onDrop={dropped}>
              <div className="my-col-head">
                <span className="my-col-title">All tools</span>
                <label className="my-search"><Search size={13} /><input type="search" placeholder="Search tools…" value={query} onChange={(e) => setQuery(e.target.value)} aria-label="Search tools" /></label>
              </div>
              <div className="my-col-scroll">
                {shown.map((g) => (
                  <div className="my-cat-group" key={g.label}>
                    <div className="my-cat-title">{g.label}</div>
                    {g.tools.map((x) => <CatalogueRow key={x.id} tool={x} inTab={has(tab, x.id)} onAdd={() => addFromCatalogue(x.id)} drag={dragTool(x.id, 'catalogue')} />)}
                  </div>
                ))}
                {shown.length === 0 && <p className="muted small">No tool matches.</p>}
              </div>
              {over?.kind === 'catalogue' && <div className="my-remove-hint">Drop here to take the tool out of My tools</div>}
            </section>

            <section className="my-tab" aria-label="My tools" ref={right}>
              <div className="my-col-head">
                <span className="my-col-title">My tools</span>
                <span className="grow" />
                <button type="button" className="btn small-btn" onClick={() => { changeMyTab((tb) => addGroup(tb).tab); setTarget(myTab().groups[myTab().groups.length - 1].id); }}><FolderPlus size={13} />New group</button>
              </div>
              <div className="my-col-scroll" onDragOver={(e) => { if (draggingTool() && tab.groups.length === 0) { e.preventDefault(); setOver({ kind: 'tool', group: '', index: 0 }); } }}
                onDrop={(e) => { if (tab.groups.length === 0) { e.preventDefault(); const id = toolOfDrag(e); clear(); if (id) changeMyTab((tb) => addTool(tb, id)); } }}>
                {tab.groups.length === 0 && <p className="my-tab-empty">Nothing yet. Drag a tool here, or press + beside it.</p>}
                {tab.groups.map((g, gi) => {
                  const gmark = over?.kind === 'group' ? (over.index === gi ? ' drop-before' : over.index === tab.groups.length && gi === tab.groups.length - 1 ? ' drop-after' : '') : '';
                  const hot = over?.kind === 'tool' && over.group === g.id && g.items.length === 0 ? ' over' : '';
                  return (
                    <div className={`my-card${gmark}${hot}${targetId === g.id ? ' target' : ''}`} key={g.id} data-flip={g.id} onDragOver={(e) => overCard(e, g, gi)} onDrop={dropped} onFocusCapture={() => setTarget(g.id)} onMouseDown={() => setTarget(g.id)}>
                      <div className="my-card-head">
                        <span className="my-grip" title="Drag to move the group" draggable onDragStart={(e) => { e.dataTransfer.setData(MY_GROUP_DRAG, g.id); e.dataTransfer.effectAllowed = 'move'; setDragged({ kind: 'group', id: g.id }); }} onDragEnd={clear}><GripVertical size={14} /></span>
                        <GroupName value={g.label} onCommit={(v) => changeMyTab((tb) => renameGroup(tb, g.id, v))} />
                        <button type="button" className="icon" title="Move group up" aria-label="Move group up" disabled={gi === 0} onClick={() => changeMyTab((tb) => nudgeGroup(tb, g.id, -1))}><ArrowUp size={13} /></button>
                        <button type="button" className="icon" title="Move group down" aria-label="Move group down" disabled={gi === tab.groups.length - 1} onClick={() => changeMyTab((tb) => nudgeGroup(tb, g.id, 1))}><ArrowDown size={13} /></button>
                        <button type="button" className="icon" title="Delete group" aria-label="Delete group" onClick={() => changeMyTab((tb) => deleteGroup(tb, g.id))}><Trash2 size={13} /></button>
                      </div>
                      <div className="my-rows">
                        {g.items.length === 0 && <div className="my-card-empty">Drop tools here</div>}
                        {g.items.map((it, i) => {
                          const tl = byId.get(it.tool)!;
                          return (
                            <div className={`my-row${rowMark(g, i)}`} key={it.tool} data-flip={it.tool} data-row={it.tool} tabIndex={0} role="listitem" aria-label={tl.label}
                              onKeyDown={(e) => rowKey(e, it.tool)} onDragOver={(e) => overRow(e, g, i)} onDrop={dropped} {...dragTool(it.tool, 'tab')}>
                              <span className="my-grip" aria-hidden><GripVertical size={13} /></span>
                              <tl.icon size={16} strokeWidth={1.5} />
                              <span className="my-row-label">{tl.label}</span>
                              <button type="button" className="icon" tabIndex={-1} title={small(it) ? 'Show as a large tool' : 'Show as a small tool'} aria-label={small(it) ? 'Show as a large tool' : 'Show as a small tool'} onClick={() => changeMyTab((tb) => setSize(tb, it.tool, small(it)))}>{small(it) ? <Maximize2 size={13} /> : <Minimize2 size={13} />}</button>
                              <button type="button" className="icon" tabIndex={-1} title="Move up" aria-label="Move up" disabled={gi === 0 && i === 0} onClick={() => { changeMyTab((tb) => stepTool(tb, it.tool, -1)); setFocus(it.tool); }}><ArrowUp size={13} /></button>
                              <button type="button" className="icon" tabIndex={-1} title="Move down" aria-label="Move down" disabled={gi === tab.groups.length - 1 && i === g.items.length - 1} onClick={() => { changeMyTab((tb) => stepTool(tb, it.tool, 1)); setFocus(it.tool); }}><ArrowDown size={13} /></button>
                              {tab.groups.length > 1 && (
                                <select className="my-move" value={g.id} data-no-i18n title={t('Move to group')} aria-label={t('Move to group')} tabIndex={-1}
                                  onChange={(e) => { const to = e.target.value; changeMyTab((tb) => addTool(tb, it.tool, { group: to })); setFocus(it.tool); }}>
                                  {tab.groups.map((x) => <option key={x.id} value={x.id}>{x.label}</option>)}
                                </select>
                              )}
                              <button type="button" className="icon" tabIndex={-1} title="Remove from My tools" aria-label="Remove from My tools" onClick={() => changeMyTab((tb) => removeTool(tb, it.tool))}><X size={13} /></button>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  );
                })}
              </div>
            </section>
          </div>
          <p className="muted small my-keys">Alt+↑ ↓ moves a tool, Alt+← → moves it to the group before or after, Delete removes it, Ctrl+Z undoes the last change.</p>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" onClick={() => { undoMyTools(); }} disabled={!canUndo}><Undo2 size={13} />Undo</button>
          <button type="button" className="btn" onClick={() => resetMyTab()} disabled={tab.groups.length === 0}><RotateCcw size={13} />Reset</button>
          <button type="button" className="btn" onClick={() => void exportMyTools()}><Download size={13} />Export…</button>
          <button type="button" className="btn" onClick={() => void importMyTools()}><Upload size={13} />Import…</button>
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}

function CatalogueRow({ tool, inTab, onAdd, drag }: { tool: ToolDef; inTab: boolean; onAdd: () => void; drag: object }) {
  return (
    <div className={`my-cat-row${inTab ? ' in' : ''}`} tabIndex={0} data-cat={tool.id} {...drag}
      onKeyDown={(e) => { if (e.target === e.currentTarget && (e.key === 'Enter' || e.key === ' ') && !inTab) { e.preventDefault(); onAdd(); } }} onDoubleClick={() => { if (!inTab) onAdd(); }}>
      <tool.icon size={16} strokeWidth={1.5} />
      <span className="my-row-label">{tool.label}</span>
      {inTab
        ? <span className="my-in" title="Already in My tools" aria-label="Already in My tools"><Check size={13} /></span>
        : <button type="button" className="icon" tabIndex={-1} title="Add to My tools" aria-label="Add to My tools" onClick={onAdd}><Plus size={14} /></button>}
    </div>
  );
}

/** A group's name, edited in place: kept on Enter or on leaving the field, an empty one put back. */
function GroupName({ value, onCommit }: { value: string; onCommit: (v: string) => void }) {
  const [v, setV] = useState(value);
  useLayoutEffect(() => setV(value), [value]);
  const done = () => { if (v.trim() && v.trim() !== value) onCommit(v.trim()); else setV(value); };
  return (
    <input className="my-group-name" value={v} maxLength={60} data-no-i18n aria-label={t('Group name')} onChange={(e) => setV(e.target.value)} onBlur={done}
      onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') e.currentTarget.blur(); else if (e.key === 'Escape') { setV(value); e.currentTarget.blur(); } }} />
  );
}
