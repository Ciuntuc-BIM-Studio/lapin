/**
 * Modules that replace what others bring (`replaces` on LapinModule, docs/overrides.md): a tool, a dialog, a module's
 * view or a File menu action, by id. Each replacement gets what it replaces, the one before it in load order, so it may
 * call it. Without replacements every function here gives back what it was given.
 */

import type { FileAction, LapinModule, RibbonGroupDef, Replaces, ToolDef } from './module';

/** One thing a module replaces, as Help → About lists it. */
export interface Replacement { kind: 'tool' | 'dialog' | 'view' | 'fileAction'; id: string; module: string }

const KINDS: Record<keyof Replaces, Replacement['kind']> = { tools: 'tool', dialogs: 'dialog', views: 'view', fileActions: 'fileAction' };

/** Everything the modules replace, in load order. */
export const replacementsOf = (modules: readonly LapinModule[]): Replacement[] =>
  modules.flatMap((m) => (Object.keys(m.replaces ?? {}) as (keyof Replaces)[])
    .flatMap((k) => Object.keys(m.replaces?.[k] ?? {}).map((id) => ({ kind: KINDS[k], id, module: m.id }))));

/** Whether any module replaces `id` among `kind`. */
export const replacesAny = (modules: readonly LapinModule[], kind: keyof Replaces, id: string): boolean =>
  modules.some((m) => Boolean(m.replaces?.[kind]?.[id]));

/** `original` through each module's replacement of `id` among `kind`, in load order: the original itself when none. */
export function through<T>(modules: readonly LapinModule[], kind: keyof Replaces, id: string, original: T): T {
  let v = original;
  for (const m of modules) {
    const f = m.replaces?.[kind]?.[id] as ((o: T) => T) | undefined;
    if (f) v = f(v);
  }
  return v;
}

/**
 * The modules with every replaced tool in its place, wherever it is (tabs, shared groups, quick tools), one object per
 * tool id; the same list when nothing replaces a tool. A tool no module of the product has is refused, naming who asked.
 */
export function withReplacedTools(modules: readonly LapinModule[]): readonly LapinModule[] {
  const ids = new Set(modules.flatMap((m) => Object.keys(m.replaces?.tools ?? {})));
  if (ids.size === 0) return modules;
  const done = new Map<string, ToolDef>();
  const swap = (t: ToolDef): ToolDef => {
    if (!ids.has(t.id)) return t;
    let r = done.get(t.id);
    // The id stays: the keys, the quick slots and the workspaces name the tool by it.
    if (!r) done.set(t.id, r = { ...through(modules, 'tools', t.id, t), id: t.id });
    return r;
  };
  const group = (g: RibbonGroupDef): RibbonGroupDef => ({ ...g, tools: g.tools.map(swap), ...(g.small ? { small: g.small.map(swap) } : {}) });
  const out = modules.map((m): LapinModule => ({
    ...m,
    ...(m.tabs ? { tabs: m.tabs.map((tab) => ({ ...tab, groups: tab.groups.map(group) })) } : {}),
    ...(m.sharedGroups ? { sharedGroups: Object.fromEntries(Object.entries(m.sharedGroups).map(([k, g]) => [k, group(g)])) } : {}),
    ...(m.hotbar ? { hotbar: m.hotbar.map(swap) } : {}),
  }));
  for (const id of ids)
    if (!done.has(id)) {
      const by = modules.find((m) => m.replaces?.tools?.[id])?.id;
      throw new Error(`Module „${by}” replaces the tool „${id}”, which no module of this product has.`);
    }
  return out;
}

/**
 * The File menu's actions (shell/fileActions.ts) through the modules' replacements (each gets the one before it), by id; an id the menu does not have is
 * refused, naming who asked.
 */
export function resolveFileActions(base: FileAction[], modules: readonly LapinModule[]): Map<string, FileAction> {
  const o = new Map(base.map((a) => [a.id, a]));
  for (const m of modules)
    for (const [id, f] of Object.entries(m.replaces?.fileActions ?? {})) {
      const original = o.get(id);
      if (!original) throw new Error(`Module „${m.id}” replaces the File action „${id}”, which the File menu does not have.`);
      o.set(id, { ...f(original), id });
    }
  return o;
}
