/** What the table tool and the table editor share: the next table's rows, columns and preset, and the table being edited. */

import { create } from 'zustand';
import type { NodeDto } from '../../../engine/protocol';
import { useUi } from '../../../app/store';
import type { Preset } from './tableModel';

export interface NewTable { rows: number; cols: number; preset: Preset }

/** What the next table is made of, kept while the tool is used again. */
export const useNewTable = create<NewTable>(() => ({ rows: 4, cols: 3, preset: 'header' }));

/** The table the editor shows. */
export const useTableEditor = create<{ id: string | null }>(() => ({ id: null }));

export function openTableEditor(id: string): void {
  useTableEditor.setState({ id });
  useUi.setState({ dialog: 'tableEditor' });
}

export const isTable = (node: NodeDto | undefined): boolean => node?.type === 'IfcAnnotation' && node.props.predefinedType === 'TABLE';
