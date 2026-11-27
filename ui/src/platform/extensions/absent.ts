/**
 * Extensions a project holds data of but this Lapin does not run (docs/plan-extension-modules.md §4.4): `project.open` lists them
 * as `absentExtensions` ({module, id, version}). Their data is kept and written back unchanged; the interface says so once, with
 * a way to the Extensions dialog to install (or turn on) the extension. The notice stays until it is closed.
 */

import { create } from 'zustand';
import { phrase } from '../../shell/prompts';

/** One extension the project needs, as `project.open` returns it. */
export interface AbsentExtension { module: string; id: string; version: string }

/** The extensions a project opened with that are not here, kept for the notice; empty once it is closed. */
export const useAbsentExtensions = create<{ list: AbsentExtension[] }>(() => ({ list: [] }));

/** The Extensions dialog's tab asked for (Installed when the extension is installed but off), taken once by the dialog. */
let requestedTab: 'browse' | 'installed' | null = null;
export const requestExtensionsTab = (tab: 'browse' | 'installed' | null): void => { requestedTab = tab; };
export function takeExtensionsTab(): 'browse' | 'installed' {
  const t = requestedTab ?? 'browse';
  requestedTab = null;
  return t;
}

/** What `project.open` answered, as a list of well-formed entries (anything else is none). */
export function absentList(r: unknown): AbsentExtension[] {
  const list = (r as { absentExtensions?: unknown } | null)?.absentExtensions;
  if (!Array.isArray(list)) return [];
  return list.filter((x): x is AbsentExtension => !!x && typeof x === 'object' && typeof (x as AbsentExtension).id === 'string' && !!(x as AbsentExtension).id)
    .map((x) => ({ module: String(x.module ?? ''), id: x.id, version: String(x.version ?? '') }));
}

/** Says the project's absent extensions (replacing an earlier notice); none: closes it. */
export function tellAbsentExtensions(r: unknown): void {
  useAbsentExtensions.setState({ list: absentList(r) });
}

export const dismissAbsentExtensions = (): void => useAbsentExtensions.setState({ list: [] });

/** The notice's sentence in the language chosen, `names` being the extensions' names as the catalogue gives them (else their ids). */
export function absentNotice(names: readonly string[]): string {
  return names.length === 1
    ? phrase('This project uses {name} (extension), which is not installed. Its data is kept and untouched.', { name: names[0] })
    : phrase('This project uses {names} (extensions), which are not installed. Their data is kept and untouched.', { names: names.join(', ') });
}
