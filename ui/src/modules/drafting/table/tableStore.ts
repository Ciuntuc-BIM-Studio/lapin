/** What the table tool and the table editor share: the next table's rows, columns and preset, and the table being edited. */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { query, useSession } from '../../../engine/session';
import type { NodeDto } from '../../../engine/protocol';
import { useUi } from '../../../app/store';
import type { Preset } from './tableModel';

/** A table style of the user's own (the drafting standards' tableStyles, stage 3). */
export interface TableStyleEntry { id: string; name: string; cells?: boolean }

/** The next table's rows, columns and preset: a built-in one or the id of a table style of the user's. */
export interface NewTable { rows: number; cols: number; preset: Preset | string }

/** What the next table is made of, kept while the tool is used again. */
export const useNewTable = create<NewTable>(() => ({ rows: 4, cols: 3, preset: 'header' }));

/** The table the editor shows. */
export const useTableEditor = create<{ id: string | null }>(() => ({ id: null }));

export function openTableEditor(id: string): void {
  useTableEditor.setState({ id });
  useUi.setState({ dialog: 'tableEditor' });
}

export const isTable = (node: NodeDto | undefined): boolean => node?.type === 'IfcAnnotation' && node.props.predefinedType === 'TABLE';

/** The project's table styles (the drafting standards'), read again when the project changes. */
export function useTableStyles(): TableStyleEntry[] {
  const nodes = useSession((s) => s.nodes);
  const [styles, setStyles] = useState<TableStyleEntry[]>([]);
  useEffect(() => {
    let live = true;
    void query<{ tableStyles?: TableStyleEntry[] }>('standards.get').then((b) => { if (live) setStyles(b?.tableStyles ?? []); });
    return () => { live = false; };
  }, [nodes]);
  return styles;
}
