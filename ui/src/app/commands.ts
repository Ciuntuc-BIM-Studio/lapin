/**
 * Every command the keyboard can reach, for the shortcuts' registry
 * (shortcuts.ts): the app's own (undo, save, the panels, the zoom…), the
 * quick tool slots 1–9, and each tool of the ribbon under its tab and group.
 * The keymap in force (`keymap()`) is the registry's resolution of the
 * user's bindings over these; `hint(id)` is what tooltips show.
 */

import { useMemo } from 'react';
import { select, useUi } from './store';
import { toggleBothPanels, togglePanel, showOverlay, usePanels, applyPanelsPart } from './panels';
import { onInterfaceChange, useInterface } from './interface';
import { effectiveBindings, formatBinding, resolveKeymap, useKeymap, type Binding, type CommandDef, type Keymap } from './shortcuts';
import { activate, allTools, hotbarTools, ribbonTabs } from '../platform/modules';
import { execute, redo, undo } from '../engine/session';
import { newProject, openProject, saveProject } from '../engine/files';
import { COMMANDS, setCommandFallback, startCommand, useCad } from '../viewport/cad/session';
import { zoomHost } from '../viewport/zoom';

const dialog = (id: string) => () => useUi.setState({ dialog: id });
const zoom = (f: (h: NonNullable<ReturnType<typeof zoomHost>>) => void) => () => { const h = zoomHost(); if (h) f(h); };

/** The app's own commands, with Lapin's classic bindings. */
function appCommands(): CommandDef[] {
  const general = 'General', views = 'Views', panels = 'Panels', draw = 'Drawing aids';
  return [
    { id: 'app.undo', label: 'Undo', category: general, defaults: ['Ctrl+Z'], run: () => void undo() },
    { id: 'app.redo', label: 'Redo', category: general, defaults: ['Ctrl+Y', 'Ctrl+Shift+Z'], run: () => void redo() },
    { id: 'app.new', label: 'New project', category: general, defaults: ['Ctrl+N'], run: () => void newProject() },
    { id: 'app.open', label: 'Open…', category: general, defaults: ['Ctrl+O'], run: () => void openProject() },
    { id: 'app.save', label: 'Save', category: general, defaults: ['Ctrl+S'], run: () => void saveProject() },
    { id: 'app.saveAs', label: 'Save as…', category: general, defaults: ['Ctrl+Shift+S'], run: () => void saveProject(true) },
    { id: 'app.delete', label: 'Delete the selection', category: general, defaults: ['Delete', 'Backspace'], run: () => {
      const ids = useUi.getState().selected;
      if (ids.length) { void execute('element.delete', { ids }); select(null); }
    } },
    { id: 'app.palette', label: 'Commands…', category: general, defaults: ['Ctrl+K'], run: dialog('palette') },
    { id: 'app.shortcuts', label: 'Keyboard shortcuts…', category: general, defaults: [], run: dialog('shortcuts') },
    { id: 'app.interface', label: 'Interface…', category: general, defaults: ['Ctrl+.'], run: dialog('interface') },
    { id: 'app.osnap', label: 'Object snap', category: draw, defaults: ['F3'], run: () => useCad.setState((s) => ({ osnap: !s.osnap })) },
    { id: 'app.ortho', label: 'Ortho', category: draw, defaults: ['F8'], run: () => useCad.setState((s) => ({ ortho: !s.ortho })) },
    { id: 'app.polar', label: 'Polar tracking', category: draw, defaults: ['F10'], run: () => useCad.setState((s) => ({ polar: !s.polar })) },
    { id: 'app.zoomIn', label: 'Zoom in', category: views, defaults: ['Plus', '='], same: 'ZI', run: zoom((h) => h.zoomBy(1.25)) },
    { id: 'app.zoomOut', label: 'Zoom out', category: views, defaults: ['-'], same: 'ZO', run: zoom((h) => h.zoomBy(0.8)) },
    { id: 'app.zoomSelection', label: 'Zoom to the selection', category: views, defaults: ['Shift+Z'], same: 'ZS', run: zoom((h) => h.fitSelection()) },
    { id: 'app.zoomFit', label: 'Zoom to fit', category: views, defaults: [], same: 'ZE', run: zoom((h) => h.fitAll()) },
    { id: 'app.zoomRegion', label: 'Zoom region', category: views, defaults: [], same: 'ZOOM', run: () => void startCommand('ZOOM') },
    { id: 'app.zoomPrevious', label: 'Zoom before', category: views, defaults: [], same: 'ZP', run: zoom((h) => h.previous()) },
    { id: 'app.toggleBrowser', label: 'Project browser: fold or unfold', category: panels, defaults: ['Ctrl+B'], run: () => togglePanel('browser') },
    { id: 'app.toggleProps', label: 'Properties: fold or unfold', category: panels, defaults: ['Ctrl+Shift+B'], run: () => togglePanel('props') },
    { id: 'app.togglePanels', label: 'Both panels: fold or unfold', category: panels, defaults: ['Tab'], run: toggleBothPanels },
    { id: 'app.propsOverlay', label: 'All properties (over the view)', category: panels, defaults: [], run: () => { if (usePanels.getState().propsOpen) return; showOverlay('props'); } },
  ];
}

const hotbarCommands = (): CommandDef[] => Array.from({ length: 9 }, (_, i) => ({
  id: `hotbar.${i + 1}`, label: `Quick tool ${i + 1}`, category: 'Quick tools', defaults: [String(i + 1)],
  run: () => { const t = hotbarTools()[i]; if (t) activate(t); },
}));

/** The command-line command a tool starts, when it is one (the Modify and Draft tools). */
const SAME: Record<string, string> = {
  'modify.move': 'MOVE', 'modify.copy': 'COPY', 'modify.rotate': 'ROTATE', 'modify.mirror': 'MIRROR', 'modify.delete': 'ERASE', 'modify.match': 'MATCHTYPE',
  'cad.line': 'LINE', 'cad.pline': 'PLINE', 'cad.rect': 'RECTANGLE', 'cad.circle': 'CIRCLE', 'cad.arc': 'ARC', 'cad.move': 'MOVE', 'cad.copy': 'COPY',
  'cad.rotate': 'ROTATE', 'cad.mirror': 'MIRROR', 'cad.scale': 'SCALE', 'cad.erase': 'ERASE', 'cad.offset': 'OFFSET', 'cad.trim': 'TRIM', 'cad.extend': 'EXTEND',
  'cad.fillet': 'FILLET', 'cad.chamfer': 'CHAMFER', 'cad.hatch': 'HATCH', 'cad.tag': 'TAG', 'drafting.dimension': 'DIMALIGNED', 'architecture.stair': 'STAIR',
};

let cached: CommandDef[] | null = null;

/** Every command, once: the app's, the quick tool slots, then the ribbon's tools tab by tab (and the quick tools no tab has). */
export function commands(): CommandDef[] {
  if (cached) return cached;
  const out = [...appCommands(), ...hotbarCommands()];
  const seen = new Set(out.map((c) => c.id));
  const tool = (t: ReturnType<typeof allTools>[number], category: string, group?: string) => {
    if (seen.has(t.id)) return;
    seen.add(t.id);
    out.push({
      id: t.id, label: t.label, category, group,
      defaults: t.shortcut && /^[A-Z]{1,3}$/.test(t.shortcut) ? [t.shortcut] : [],
      fixed: t.shortcut === 'Esc' ? ['Escape'] : undefined,
      same: SAME[t.id], run: () => activate(t),
    });
  };
  for (const t of hotbarTools()) if (!ribbonTabs().some((tab) => tab.groups.some((g) => [...g.tools, ...(g.small ?? [])].some((x) => x.id === t.id)))) tool(t, 'Quick tools');
  for (const tab of ribbonTabs()) for (const g of tab.groups) for (const t of [...g.tools, ...(g.small ?? [])]) tool(t, tab.label, g.label);
  for (const t of allTools()) tool(t, 'Other');
  cached = out;
  return out;
}

export const findCommandDef = (id: string) => commands().find((c) => c.id === id) ?? null;

/** The command line's names, each with the command it is. */
const cliNames = () => COMMANDS.flatMap((c) => c.names.map((name) => ({ name, command: c.names[0] })));

let memo: { state: unknown; map: Keymap; bindings: Record<string, Binding[]> } | null = null;

/** The bindings in force and their keymap (recomputed when the user changes them). */
export function current(): { map: Keymap; bindings: Record<string, Binding[]> } {
  const state = useKeymap.getState();
  if (memo?.state === state) return memo;
  const cmds = commands();
  const bindings = effectiveBindings(cmds, state);
  const map = resolveKeymap(cmds, bindings, cliNames());
  memo = { state, map, bindings };
  return memo;
}

export const keymap = () => current().map;

export function runCommand(id: string): void {
  findCommandDef(id)?.run();
}

/** A command's bindings as the interface shows them ("WA", "Ctrl ⇧ S"), joined; '' when it has none. */
export function bindingText(id: string, bindings = current().bindings): string {
  const c = findCommandDef(id);
  return [...(c?.fixed ?? []), ...(bindings[id] ?? [])].map((b) => formatBinding(b)).join(', ');
}

/** A tooltip: "Name — keys · group" (the keys and group when there are). */
export function hint(id: string, label: string, group?: string): string {
  const keys = bindingText(id);
  return `${label}${keys ? ` — ${keys}` : ''}${group ? ` · ${group}` : ''}`;
}

/** For components: re-renders when the bindings change. */
export function useBindings(): Record<string, Binding[]> {
  const state = useKeymap();
  return useMemo(() => { void state; return current().bindings; }, [state]);
}

// A word typed on the command line and ended with Enter that no command-line command knows: a sequence's command (ZF ↵).
setCommandFallback((text) => {
  const id = current().map.sequences[text.trim().toUpperCase()];
  if (!id) return false;
  runCommand(id);
  return true;
});

// The panels follow the interface's choice of them; Compact narrows the properties.
onInterfaceChange((s, before) => {
  if (s.parts.panels !== before.parts.panels) applyPanelsPart(s.parts.panels, s.preset === 'compact' && before.preset !== 'compact' ? 260 : undefined);
  else if (s.preset === 'compact' && before.preset !== 'compact') applyPanelsPart(s.parts.panels, 260);
});
{
  const s = useInterface.getState();
  usePanels.setState({ railsHidden: s.parts.panels === 'hidden' });
  // The first start in Compact (the web build on a short window): its panels, until the user chooses.
  if (!s.chosen && s.preset === 'compact') applyPanelsPart(s.parts.panels, 260);
}
