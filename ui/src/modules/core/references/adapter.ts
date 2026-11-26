/**
 * Where the reference documents' interface gets its documents from (docs/plan-cde-references.md §5, stage R4): the open project's
 * engine (Manage → Reference documents…) or an Atrium Server directly (the CDE web's Documents tab, without opening the project).
 * The list, the detail, the checklist, the preview and the settings are the same components over either; they ask the adapter and
 * know nothing of where the answer comes from.
 */

import { createContext, useContext } from 'react';
import {
  addRef, attachFiles, checklistOf, setChecklist, deleteRef, detachFile, fileOf, folderTree, listRefs, moveRefs, scanFolder, setFolders, setTypes, suggestFor, supersedeRef, textOf, typeList, updateRef, withdrawRef,
  type AddResult, type Checklist, type ChecklistOption, type FolderTree, type RefDoc, type RefFile, type RefFileData, type RefType, type ScanResult, type Suggestion, type TypeList,
} from './references';

export type FilePart = { name: string; data: string };

/** A reference model's IFC header as the server read it on upload (Atrium docs/atrium-server.md, "Reading the files"). */
export interface IfcHeaderInfo { schema?: string | null; application?: string | null; projectName?: string | null; author?: string | null; organization?: string | null; timeStamp?: string | null; readable?: boolean }
/** What the server found in a reference model: its header, and the IDS report when the project's IDS rule ran on it. */
export interface ModelInfo { kind: string | null; ifc: IfcHeaderInfo | null; ids: import('../../../engine/serverIds').IdsReport | null }

export interface RefAdapter {
  /** Where the documents are: the project open here, or a server (some things then do not exist: Scan folder, the keep limit). */
  kind: 'engine' | 'server';
  list(): Promise<RefDoc[]>;
  folders(): Promise<FolderTree | null>;
  types(): Promise<TypeList | null>;
  checklist(id: string): Promise<Checklist | null>;
  file(id: string, name?: string): Promise<RefFileData | null>;
  text(id: string, name?: string, pages?: string): Promise<{ pageCount: number; pages: { page: number; text: string }[]; warning?: string; textStatus?: string } | null>;
  add(args: Record<string, unknown>): Promise<AddResult | null>;
  update(id: string, patch: Record<string, unknown>): Promise<unknown>;
  move(ids: string[], folder: string): Promise<unknown>;
  attach(id: string, files: FilePart[]): Promise<{ files: RefFile[]; warnings: string[] } | null>;
  detach(id: string, name: string): Promise<unknown>;
  supersede(id: string, by: string): Promise<unknown>;
  withdraw(id: string, reason: string): Promise<unknown>;
  remove(id: string): Promise<unknown>;
  setFolders(args: { folders?: unknown; typeFolders?: Record<string, string>; roleFolders?: Record<string, string>; language?: string }): Promise<unknown>;
  setTypes(types: RefType[]): Promise<unknown>;
  /** The certificate's checklist items offered; null (the engine only) the indicative defaults again. */
  setChecklist(items: ChecklistOption[] | null): Promise<unknown>;
  /** Server only (R7): the server project the documents are in, and what reading a model found (its IFC header, its IDS report). */
  project?: { id: string; name: string };
  model?(id: string, name: string): Promise<ModelInfo | null>;
  /** What a file's text suggests (the engine's patterns, or the server's copy of them); the project folder's scan (engine only). */
  suggest?(id: string, name?: string): Promise<Suggestion | null>;
  scan?(): Promise<ScanResult | null>;
}

/** What the person may do: add documents, change this one, delete, and change the folders and types. */
export interface RefPerms { add: boolean; canChange(doc: RefDoc): boolean; remove: boolean; admin: boolean }

export const engineAdapter: RefAdapter = {
  kind: 'engine',
  list: listRefs, folders: folderTree, types: typeList, checklist: checklistOf, file: fileOf, text: textOf,
  add: addRef, update: updateRef, move: moveRefs, attach: attachFiles, detach: detachFile, supersede: supersedeRef, withdraw: withdrawRef, remove: deleteRef,
  setFolders, setTypes, setChecklist, suggest: suggestFor, scan: scanFolder,
};

/** The permissions of the open project: everything, except for a guest of a shared session. */
export const enginePerms = (guest: boolean): RefPerms => ({ add: !guest, canChange: () => !guest, remove: !guest, admin: !guest });

export interface RefContext { adapter: RefAdapter; perms: RefPerms }
const Ctx = createContext<RefContext>({ adapter: engineAdapter, perms: enginePerms(false) });
export const RefProvider = Ctx.Provider;
export const useRefAdapter = (): RefAdapter => useContext(Ctx).adapter;
export const useRefPerms = (): RefPerms => useContext(Ctx).perms;
