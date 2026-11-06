/** The engine's title blocks (decision 340): the list, one template, its file, the fields to insert, a preview, the commands. */

import { execute, query } from '../../../engine/session';
import type { PenDto } from '../../../viewport/drawing';
import { normalise, type TbField, type TbFile, type TbTemplate } from './titleBlockModel';

export interface TbList { items: { id: string; name: string; builtin: boolean; default: boolean; uses: number; description?: string }[]; default: string }
export interface TbFields { sheetId: string | null; fields: TbField[]; textStyles: { id: string; name: string; height: number; bold: boolean; italic: boolean; font: string }[]; patterns: string[] }

export const STANDARD = 'builtin:standard';

export const listTitleBlocks = () => query<TbList>('titleBlock.list').then((r) => r ?? { items: [], default: STANDARD });
export const getTitleBlock = (id: string) => query<{ id: string; name: string; builtin: boolean; template: TbTemplate }>('titleBlock.get', { id })
  .then((r) => (r ? { ...r, template: normalise(r.template) } : null));
export const exportTitleBlock = (id: string) => query<TbFile>('titleBlock.export', { id });
export const titleBlockFields = (sheetId?: string | null) => query<TbFields>('titleBlock.fields', sheetId ? { sheetId } : {});
export const previewTitleBlock = (args: { id: string } | { template: TbTemplate }, sheetId?: string | null) =>
  query<{ w: number; h: number; pens: PenDto[] }>('titleBlock.preview', { ...args, ...(sheetId ? { sheetId } : {}) });

export const createTitleBlock = (args: { name?: string; from?: string; template?: TbTemplate }) =>
  execute('titleBlock.create', args).then((r) => (r as { id?: string } | null)?.id ?? null);
export const importTitleBlock = (json: TbFile, name?: string) =>
  execute('titleBlock.import', { json, ...(name ? { name } : {}) }).then((r) => (r as { id?: string } | null)?.id ?? null);
export const updateTitleBlock = (id: string, args: { name?: string; template?: TbTemplate }) => execute('titleBlock.update', { id, ...args });
export const deleteTitleBlock = (id: string) => execute('titleBlock.delete', { id });
export const setDefaultTitleBlock = (id: string) => execute('titleBlock.setDefault', { id });
export const setSheetTitleBlock = (id: string, titleBlockId: string | null) => execute('sheet.setTitleBlock', { id, titleBlockId });
