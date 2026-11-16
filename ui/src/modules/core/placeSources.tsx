/**
 * The core's sources of the Place catalogue (S15): the project's types for the tools that place them (walls, doors, floors…, as
 * the browser's Catalog tab), the symbols (Lapin's, the project's with the blocks a DXF brought, this computer's) and the
 * components (a click copies an instance from a base point).
 */

import { useEffect, useState } from 'react';
import { select, useUi } from '../../app/store';
import { query, useSession } from '../../engine/session';
import { activate, allTools, typesOf } from '../../platform/modules';
import type { PlaceEntry, PlaceSourceDef } from '../../platform/module';
import { catalogKinds } from '../../shell/Catalog';
import { startCommand } from '../../viewport/cad/session';
import { freeName } from './titleBlocks/titleBlockModel';
import { Thumb } from './symbols/Thumb';
import { createSymbol, listSymbols, previewSymbol, type SymList } from './symbols/symbols';
import { librarySymbols, normaliseDef, type SymPreview } from './symbols/symbolModel';
import { placeTool } from './symbols/symbolTools';

function useTypeEntries(): PlaceEntry[] {
  const nodes = useSession((s) => s.nodes);
  return catalogKinds(allTools()).flatMap((tool) => {
    const typed = tool.typed!;
    const T = typed.thumb;
    return typesOf(tool).map((x): PlaceEntry => {
      const node = nodes.get(x.id)!;
      return {
        key: `type:${x.id}`, name: x.name, group: tool.label, about: typed.about?.(node) ?? undefined,
        thumb: T ? <T type={node} /> : <tool.icon size={26} strokeWidth={1.3} />,
        place: () => { typed.set(x.id); activate(tool); },
      };
    });
  });
}

function useSymbolEntries(): PlaceEntry[] {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<SymList>({ items: [], categories: [] });
  const [library, setLibrary] = useState<{ id: string; name: string; category: string; preview?: SymPreview }[]>([]);
  useEffect(() => { void listSymbols(true).then(setList); }, [version]);
  useEffect(() => {
    let live = true;
    const kept = librarySymbols();
    void Promise.all(kept.map(async (b) => ({ id: b.id, name: b.name, category: b.def.category || 'other', preview: (await previewSymbol({ def: normaliseDef(b.def) })) ?? undefined })))
      .then((all) => { if (live) setLibrary(all); });
    return () => { live = false; };
  }, []);
  const taken = list.items.filter((s) => !s.builtin).map((s) => s.name);
  return [
    ...list.items.map((s): PlaceEntry => ({
      key: `symbol:${s.id}`, name: s.name, group: s.builtin ? 'Built-in' : 'This project', about: s.uses ? `${s.category} · ${s.uses} placed` : s.category,
      thumb: <Thumb p={s.preview} />, place: () => placeTool(s.id),
    })),
    ...library.map((b): PlaceEntry => ({
      key: `symbolLib:${b.id}`, name: b.name, group: 'This computer', about: b.category, thumb: <Thumb p={b.preview} />,
      // A symbol of this computer's library comes into the project first, as Symbols… → Place does.
      place: async () => {
        const def = librarySymbols().find((x) => x.id === b.id)?.def;
        const made = def ? await createSymbol({ name: freeName(b.name, taken), def: normaliseDef(def) }) : null;
        if (made?.id) await placeTool(made.id);
      },
    })),
  ];
}

interface ComponentRow { id: string; name: string; kind?: string | null; instances: string[]; members: number }

function useComponentEntries(): PlaceEntry[] {
  const version = useSession((s) => s.nodes);
  const [rows, setRows] = useState<ComponentRow[]>([]);
  useEffect(() => { void query<ComponentRow[]>('component.list').then((r) => setRows(r ?? [])); }, [version]);
  return rows.filter((r) => r.instances.length).map((r): PlaceEntry => ({
    key: `component:${r.id}`, name: r.name, group: r.kind === 'unit' ? 'Apartment types' : 'Components',
    about: `${r.instances.length} × · ${r.members} elements`,
    // Another instance: one of them copied from a base point (Copy keeps it an instance of its component).
    place: () => { select(r.instances[0]); useUi.setState({ selected: [r.instances[0]] }); void startCommand('COPY'); },
  }));
}

export const corePlaceSources: PlaceSourceDef[] = [
  { id: 'types', label: 'Types', order: 10, useEntries: useTypeEntries },
  { id: 'symbols', label: 'Symbols', order: 20, useEntries: useSymbolEntries, manage: { label: 'Symbols…', run: () => useUi.setState({ dialog: 'symbols' }) } },
  { id: 'components', label: 'Components', order: 30, useEntries: useComponentEntries, manage: { label: 'Components…', run: () => useUi.setState({ dialog: 'components' }) } },
];
