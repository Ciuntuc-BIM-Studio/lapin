/**
 * The types kind of extension (docs/plan-extensions.md, §9.3): a type pack (.lapintypes) kept on this computer and its
 * types brought into the open project (types.importFrom: materials matched by name or made, an identical type reused,
 * one undo step). Installed, its types go into the project open now; Installed → Add its types to this project brings
 * them into any other. Uninstalled, the pack is forgotten; the types already in projects stay, as with every kind.
 * Pure: where the packs are kept and how the project takes them are given (tests give fakes).
 */

import type { ContentInstaller } from './install';
import { readTypePack } from './packs';

export interface TypePackRow { id: string; name: string; file: string; text: string; types: string[]; at: string }

export interface TypePackDeps {
  rows: { all: () => Promise<TypePackRow[]>; put: (row: TypePackRow) => Promise<void>; remove: (id: string) => Promise<void> };
  /** The pack's types brought into the open project; null when there is none (or it refused them, its message shown). */
  bring: (text: string) => Promise<{ created: number; reused: number } | null>;
  now?: () => string;
}

export function typesInstaller(deps: TypePackDeps): ContentInstaller {
  return {
    kind: 'types',
    install: async (file, m) => {
      const pack = readTypePack(file.bytes);
      const types = pack.types.map((t) => t.name);
      await deps.rows.put({ id: m.id, name: m.name, file: file.name, text: pack.text, types, at: deps.now?.() ?? new Date().toISOString() });
      const r = await deps.bring(pack.text).catch(() => null);
      return { pack: m.id, types, project: r ? { created: r.created, reused: r.reused } : null };
    },
    uninstall: async (r) => { if (typeof r.pack === 'string') await deps.rows.remove(r.pack); },
    open: {
      label: 'Add its types to this project',
      run: (x) => { void deps.rows.all().then((all) => all.find((p) => p.id === x.id)).then((p) => (p ? deps.bring(p.text) : null)).catch(() => null); },
    },
  };
}
