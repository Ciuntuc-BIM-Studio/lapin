/**
 * The My tools tab's band (decision 479): the user's groups of tools, large or small, ordered by dragging right here, in the
 * ribbon: a tool moves within its group or to another, a group moves among the groups by its name, a tool from another tab
 * dropped in lands where a line shows it will go. The same by keys (Alt+←/→ moves the tool focused, Delete removes it), and by
 * right-click menus (rename, delete, size, move). Customize My tools… (MyToolsDialog) does it all with the whole catalogue beside.
 * Also here: the menu that puts any ribbon tool into the tab, and the tab's export and import.
 */

import { useLayoutEffect, useRef, useState, type DragEvent, type KeyboardEvent, type MouseEvent } from 'react';
import { ChevronDown, Settings2 } from 'lucide-react';
import { create } from 'zustand';
import { TOOL_DRAG, parseSlot } from '../app/hotbar';
import { bindingText, hint, useBindings } from '../app/commands';
import { notify, useUi } from '../app/store';
import { pickFile, safeFileName, saveFile } from '../engine/files';
import { allTools } from '../platform/modules';
import type { ToolDef } from '../platform/module';
import { useExtraTools } from '../platform/extraTools';
import {
  MY_DRAG, MY_GROUP_DRAG, MY_TAB, NEW_GROUP, addGroup, addTool, addToNewGroup, changeMyTab, deleteGroup, exportTab, findGroup, getDragged, has, importMyTab, isEmpty,
  locate, moveGroup, myTab, nudgeGroup, removeTool, renameGroup, resetMyTab, setDragged, setSize, stepTool, undoMyTools, useMyTools, visibleTab, type MyGroup, type MyItem,
} from '../app/myTools';
import { openContextMenu, type MenuItem } from './ContextMenu';
import { useFlip } from './flip';
import { pressTool } from './pulldown';

/** The group a tool added by a menu goes to: the one last added to, else the last. */
let lastGroup: string | null = null;

const dialogOpen = () => useUi.setState({ dialog: 'myTools' });

// ---- what puts a tool into the tab, from anywhere in the ribbon

/** The tool an incoming drag carries (a ribbon tool's id; a catalog card's `tool|type` reads as its tool). */
export function toolOfDrag(e: DragEvent): string | null {
  const d = getDragged();
  if (d?.kind === 'tool') return d.tool;
  const raw = e.dataTransfer.getData(TOOL_DRAG);
  return raw ? parseSlot(raw)?.tool ?? null : null;
}

/** Whether a drag over passes a tool (its id is readable only on drop). */
export const carriesTool = (e: DragEvent): boolean => getDragged()?.kind === 'tool' || e.dataTransfer.types.includes(TOOL_DRAG);

/** A tool put in My tools: in a group, or in a new one; the tab says so. */
export function addToMyTools(tool: string, group: string | 'new' | null = null): void {
  if (group === 'new') changeMyTab((t) => addToNewGroup(t, tool));
  else changeMyTab((t) => addTool(t, tool, { group: group ?? (findGroup(t, lastGroup) ? lastGroup : null) }));
  lastGroup = myTab().groups.find((g) => g.items.some((i) => i.tool === tool))?.id ?? lastGroup;
  notify('Added to My tools');
}

/** What the right-click on a ribbon tool offers. */
export function toolMenu(e: MouseEvent, tl: ToolDef): void {
  e.preventDefault();
  const tab = myTab();
  const { clientX: x, clientY: y } = e;
  const items: MenuItem[] = [];
  if (has(tab, tl.id)) items.push({ label: 'Remove from My tools', onSelect: () => changeMyTab((t) => removeTool(t, tl.id)) });
  else {
    items.push({ label: 'Add to My tools', onSelect: () => addToMyTools(tl.id) });
    if (tab.groups.length > 1) {
      items.push({ label: 'Add to My tools in…', onSelect: () => openContextMenu(x, y, tab.groups.map((g) => ({ label: g.label, raw: true, onSelect: () => addToMyTools(tl.id, g.id) }))) });
    }
    items.push({ label: 'Add to a new group in My tools', onSelect: () => addToMyTools(tl.id, 'new') });
  }
  items.push({ label: 'Customize My tools…', onSelect: dialogOpen });
  openContextMenu(x, y, items);
}

/** What a ribbon tool carries when dragged: the same as for a quick slot, so it can be dropped on the My tools tab too. */
export function toolDragProps(id: string, internal = false) {
  return {
    draggable: true,
    onDragStart: (e: DragEvent) => { e.dataTransfer.setData(TOOL_DRAG, id); e.dataTransfer.setData(MY_DRAG, id); e.dataTransfer.effectAllowed = 'copyMove'; if (internal) setDragged({ kind: 'tool', tool: id }); },
    onDragEnd: () => setDragged(null),
  };
}

// ---- export and import

export async function exportMyTools(): Promise<void> {
  const tab = myTab();
  await saveFile(new TextEncoder().encode(exportTab(tab)), `${safeFileName(tab.label, 'my-tools')}.json`, 'application/json');
}

export async function importMyTools(): Promise<void> {
  const file = await pickFile('My tools', 'json');
  if (!file) return;
  if (!importMyTab(new TextDecoder().decode(file.bytes))) notify('The file is not a My tools file.');
}

/** A tool's hover text: its name and keys, then a script tool's own tooltip. */
const titleOf = (tl: ToolDef, group?: string) => (tl.tooltip ? `${hint(tl.id, tl.label, group)}\n${tl.tooltip}` : hint(tl.id, tl.label, group));

/** The tools of this edition, by id. */
export const toolsById = (): Map<string, ToolDef> => new Map(allTools().map((t) => [t.id, t]));

// ---- the band

/** Where a drop would land: among a group's tools, or among the groups. */
type Over = { kind: 'tool'; group: string | null; index: number } | { kind: 'group'; index: number } | null;

/** The tool focused after a change (a move by keys keeps the focus on it). */
const refocus = create<{ tool: string | null }>(() => ({ tool: null }));

export function MyToolsBand({ icons, drop, onPicked }: { icons: boolean; drop?: boolean; onPicked?: () => void }) {
  const stored = useMyTools((s) => myTab(s.tabs));
  const tool = useUi((s) => s.tool);
  // A script's tools come and go while the app runs (decision 489, stage 4).
  useExtraTools((s) => s.rev);
  const byId = toolsById();
  const tab = visibleTab(stored, (id) => byId.has(id));
  const [over, setOver] = useState<Over>(null);
  const [renaming, setRenaming] = useState<string | null>(null);
  const host = useRef<HTMLDivElement>(null);
  useBindings();
  useFlip(host, tab.groups.map((g) => `${g.id}:${g.items.map((i) => `${i.tool}${i.large ? '' : '.s'}`).join()}`).join('|') + (icons ? 'i' : ''));
  const focus = refocus((s) => s.tool);
  useLayoutEffect(() => {
    if (!focus) return;
    host.current?.querySelector<HTMLElement>(`[data-my-tool="${CSS.escape(focus)}"]`)?.focus();
    refocus.setState({ tool: null });
  }, [focus, stored]);

  // A pulldown (a script's menu, decision 528) opens its list under its button; the band stays open while one is chosen.
  const press = (tl: ToolDef, el: HTMLElement) => { pressTool(tl, el); if (!tl.items?.length) onPicked?.(); };
  const clear = () => { setOver(null); setDragged(null); };

  // Dragging over: a tool lands before or after the one under the cursor; a group among the groups.
  const overItem = (e: DragEvent, group: MyGroup, i: number, small: boolean) => {
    if (!carriesTool(e)) return;
    e.preventDefault(); e.stopPropagation();
    e.dataTransfer.dropEffect = getDragged() ? 'move' : 'copy';
    const r = e.currentTarget.getBoundingClientRect();
    const after = small && !icons ? e.clientY > r.top + r.height / 2 : e.clientX > r.left + r.width / 2;
    const index = after ? i + 1 : i;
    setOver((o) => (o?.kind === 'tool' && o.group === group.id && o.index === index ? o : { kind: 'tool', group: group.id, index }));
  };
  const overGroup = (e: DragEvent, g: MyGroup, gi: number) => {
    const d = getDragged();
    if (d?.kind === 'group') {
      e.preventDefault(); e.stopPropagation();
      const r = e.currentTarget.getBoundingClientRect();
      const index = e.clientX > r.left + r.width / 2 ? gi + 1 : gi;
      setOver((o) => (o?.kind === 'group' && o.index === index ? o : { kind: 'group', index }));
    } else if (carriesTool(e)) {
      e.preventDefault(); e.stopPropagation();
      e.dataTransfer.dropEffect = d ? 'move' : 'copy';
      setOver((o) => (o?.kind === 'tool' && o.group === g.id && o.index === g.items.length ? o : { kind: 'tool', group: g.id, index: g.items.length }));
    }
  };
  const overBand = (e: DragEvent) => {
    if (!carriesTool(e) || getDragged()?.kind === 'group') return;
    e.preventDefault();
    const last = tab.groups[tab.groups.length - 1];
    setOver({ kind: 'tool', group: last?.id ?? null, index: last?.items.length ?? 0 });
  };
  const dropped = (e: DragEvent) => {
    e.preventDefault(); e.stopPropagation();
    const o = over, d = getDragged();
    clear();
    if (!o) return;
    if (o.kind === 'group' && d?.kind === 'group') { const id = d.id; changeMyTab((t) => moveGroup(t, id, o.index)); return; }
    const id = toolOfDrag(e);
    if (o.kind === 'tool' && id) { changeMyTab((t) => addTool(t, id, { group: o.group, index: o.index })); lastGroup = o.group; }
  };

  const key = (e: KeyboardEvent, id: string) => {
    if (e.altKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
      e.preventDefault();
      changeMyTab((t) => stepTool(t, id, e.key === 'ArrowLeft' ? -1 : 1));
      refocus.setState({ tool: id });
    } else if (e.key === 'Delete' || e.key === 'Backspace') {
      e.preventDefault();
      changeMyTab((t) => removeTool(t, id));
    }
  };

  const itemMenu = (e: MouseEvent, id: string, it: MyItem) => {
    e.preventDefault(); e.stopPropagation();
    const at = locate(tab, id);
    openContextMenu(e.clientX, e.clientY, [
      { label: 'Remove from My tools', onSelect: () => changeMyTab((t) => removeTool(t, id)) },
      { label: it.large ? 'Make small' : 'Make large', onSelect: () => changeMyTab((t) => setSize(t, id, !it.large)) },
      { label: 'Move earlier', disabled: !at || (at.g === 0 && at.i === 0), onSelect: () => changeMyTab((t) => stepTool(t, id, -1)) },
      { label: 'Move later', disabled: !at || (at.g === tab.groups.length - 1 && at.i === tab.groups[at.g].items.length - 1), onSelect: () => changeMyTab((t) => stepTool(t, id, 1)) },
      { label: 'Customize My tools…', onSelect: dialogOpen },
    ]);
  };
  const groupMenu = (e: MouseEvent, g: MyGroup, gi: number) => {
    e.preventDefault(); e.stopPropagation();
    openContextMenu(e.clientX, e.clientY, [
      { label: 'Rename group', onSelect: () => setRenaming(g.id) },
      { label: 'Move group earlier', disabled: gi === 0, onSelect: () => changeMyTab((t) => nudgeGroup(t, g.id, -1)) },
      { label: 'Move group later', disabled: gi === tab.groups.length - 1, onSelect: () => changeMyTab((t) => nudgeGroup(t, g.id, 1)) },
      { label: 'New group', onSelect: () => changeMyTab((t) => addGroup(t, NEW_GROUP, gi + 1).tab) },
      { label: 'Delete group', onSelect: () => changeMyTab((t) => deleteGroup(t, g.id)) },
      { label: 'Undo the last change', disabled: useMyTools.getState().past.length === 0, onSelect: () => { undoMyTools(); } },
    ]);
  };
  const bandMenu = (e: MouseEvent) => {
    e.preventDefault();
    openContextMenu(e.clientX, e.clientY, [
      { label: 'New group', onSelect: () => changeMyTab((t) => addGroup(t).tab) },
      { label: 'Customize My tools…', onSelect: dialogOpen },
      { label: 'Export My tools…', onSelect: () => void exportMyTools() },
      { label: 'Import My tools…', onSelect: () => void importMyTools() },
      { label: 'Undo the last change', disabled: useMyTools.getState().past.length === 0, onSelect: () => { undoMyTools(); } },
      { label: 'Empty My tools', disabled: tab.groups.length === 0, onSelect: () => resetMyTab() },
    ]);
  };

  const mark = (g: MyGroup, i: number) => {
    if (over?.kind !== 'tool' || over.group !== g.id) return '';
    if (over.index === i) return ' drop-before';
    if (over.index === g.items.length && i === g.items.length - 1) return ' drop-after';
    return '';
  };

  const button = (g: MyGroup, it: MyItem, i: number, tl: ToolDef) => {
    const keys = bindingText(tl.id);
    const small = !it.large;
    const common = {
      'data-flip': it.tool, 'data-my-tool': it.tool, type: 'button' as const,
      onClick: (e: MouseEvent<HTMLButtonElement>) => press(tl, e.currentTarget), onContextMenu: (e: MouseEvent) => itemMenu(e, it.tool, it), onKeyDown: (e: KeyboardEvent) => key(e, it.tool),
      onDragOver: (e: DragEvent) => overItem(e, g, i, small), onDrop: dropped,
      ...toolDragProps(it.tool, true),
    };
    if (icons) return (
      <button key={it.tool} {...common} className={`tool icon-only${tool === tl.id ? ' on' : ''}${mark(g, i)}`} title={titleOf(tl, g.label)} aria-label={tl.label}>
        <tl.icon size={18} strokeWidth={1.5} />{tl.items?.length ? <ChevronDown className="pulldown-mark" size={9} strokeWidth={2} aria-hidden /> : null}
      </button>
    );
    return small ? (
      <button key={it.tool} {...common} className={`tool small${tool === tl.id ? ' on' : ''}${mark(g, i)}`} title={titleOf(tl)}>
        <tl.icon size={14} strokeWidth={1.6} /><span>{tl.label}</span>{tl.items?.length ? <ChevronDown size={10} strokeWidth={2} aria-hidden /> : null}
      </button>
    ) : (
      <button key={it.tool} {...common} className={`tool${tool === tl.id ? ' on' : ''}${mark(g, i)}`} title={titleOf(tl)}>
        <tl.icon size={22} strokeWidth={1.5} /><span>{tl.label}{tl.items?.length ? <ChevronDown className="pulldown-mark" size={10} strokeWidth={2} aria-hidden /> : null}</span>
        {keys && <kbd className="tool-key" data-no-i18n>{keys.split(', ')[0]}</kbd>}
      </button>
    );
  };

  const empty = isEmpty(tab);
  return (
    <div ref={host} className={`band my-band${icons ? ' icons' : ''}${drop ? ' drop' : ''}${over && tab.groups.length === 0 ? ' over' : ''}`} role="toolbar" aria-label="My tools" data-testid="my-tools-band"
      onContextMenu={bandMenu} onDragOver={overBand} onDrop={dropped} onDragLeave={(e) => { if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setOver(null); }}>
      {tab.groups.map((g, gi) => {
        const groupMark = over?.kind === 'group' ? (over.index === gi ? ' drop-before' : over.index === tab.groups.length && gi === tab.groups.length - 1 ? ' drop-after' : '') : '';
        const emptyOver = over?.kind === 'tool' && over.group === g.id && g.items.length === 0 ? ' over' : '';
        // Consecutive small tools stand in a column of three.
        const cells: { items: { it: MyItem; i: number }[]; small: boolean }[] = [];
        g.items.forEach((it, i) => {
          const last = cells[cells.length - 1];
          if (!icons && !it.large && last?.small && last.items.length < 3) last.items.push({ it, i });
          else cells.push({ items: [{ it, i }], small: !icons && !it.large });
        });
        const label = renaming === g.id
          ? <RenameField value={g.label} onDone={(v) => { setRenaming(null); if (v !== null) changeMyTab((t) => renameGroup(t, g.id, v)); }} />
          : (
            <div className="group-label my-group-label" draggable tabIndex={0} data-my-group={g.id} title="Drag to move the group; double-click to rename"
              onDoubleClick={() => setRenaming(g.id)} onContextMenu={(e) => groupMenu(e, g, gi)}
              onDragStart={(e) => { e.dataTransfer.setData(MY_GROUP_DRAG, g.id); e.dataTransfer.effectAllowed = 'move'; setDragged({ kind: 'group', id: g.id }); }} onDragEnd={clear}
              onKeyDown={(e) => {
                if (e.altKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) { e.preventDefault(); changeMyTab((t) => nudgeGroup(t, g.id, e.key === 'ArrowLeft' ? -1 : 1)); requestAnimationFrame(() => host.current?.querySelector<HTMLElement>(`[data-my-group="${CSS.escape(g.id)}"]`)?.focus()); }
                else if (e.key === 'F2' || e.key === 'Enter') { e.preventDefault(); setRenaming(g.id); }
              }}>
              {g.label}
            </div>
          );
        return (
          <div className={`group my-group${groupMark}${emptyOver}`} key={g.id} data-flip={g.id} role="group" aria-label={g.label} onDragOver={(e) => overGroup(e, g, gi)} onDrop={dropped} onContextMenu={(e) => groupMenu(e, g, gi)}>
            <div className="group-tools">
              {g.items.length === 0 && <span className="my-group-empty" aria-hidden>Drop tools here</span>}
              {cells.map((c) => {
                const buttons = c.items.map(({ it, i }) => { const tl = byId.get(it.tool); return tl ? button(g, it, i, tl) : null; });
                return c.small ? <div className="small-tools" key={c.items[0].it.tool}>{buttons}</div> : buttons;
              })}
            </div>
            {label}
          </div>
        );
      })}
      {empty && <div className="my-empty">Right-click any tool → Add to My tools, or drag a tool here.</div>}
      <button type="button" className="my-customize" onClick={dialogOpen} title="Customize My tools…" aria-label="Customize My tools…" data-testid="my-tools-customize"><Settings2 size={16} strokeWidth={1.6} /></button>
    </div>
  );
}

/** A group's name being typed: Enter keeps it, Escape or an empty name leaves the old one. */
function RenameField({ value, onDone }: { value: string; onDone: (v: string | null) => void }) {
  const [v, setV] = useState(value);
  const done = useRef(false);
  const finish = (keep: boolean) => { if (done.current) return; done.current = true; onDone(keep && v.trim() ? v.trim() : null); };
  return (
    <input className="my-rename" autoFocus value={v} maxLength={60} aria-label="Group name" data-no-i18n
      onChange={(e) => setV(e.target.value)} onFocus={(e) => e.currentTarget.select()} onBlur={() => finish(true)}
      onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') finish(true); else if (e.key === 'Escape') finish(false); }} />
  );
}

export { MY_TAB };
