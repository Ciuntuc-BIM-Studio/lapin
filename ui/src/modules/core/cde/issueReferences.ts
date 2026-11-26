/**
 * Manage → Issue… with reference documents (docs/plan-cde-references.md §6, stage R5): which of the project's received documents
 * go with an issue, picked one by one or by folder (a folder ticks its live documents and its subfolders'), and which of their
 * files the project does not hold (above its keep limit), to fetch from the project's server before the issue. The engine sends
 * each file as a copy under its own name, lists it in the transmittal and the manifest with its SHA-256, and leaves out (and
 * says) a file it finds nowhere.
 */

import { folderIds, type RefDoc, type RefFolder } from '../references/references';

/** Whether a document can go with an issue: it has a file (a requested one has none yet). */
export const canSend = (d: RefDoc) => d.files.length > 0;

/** Whether a folder's tick takes a document: a live one (not superseded or withdrawn) with a file. */
const takenByFolder = (d: RefDoc) => canSend(d) && d.status !== 'superseded' && d.status !== 'withdrawn';

/** The documents a folder holds with its subfolders that a folder's tick takes. */
export function inFolder(docs: RefDoc[], tree: RefFolder[], folder: string): RefDoc[] {
  const ids = folderIds(tree, folder);
  return docs.filter((d) => ids.has(d.folder ?? '') && takenByFolder(d));
}

/** The documents chosen after a folder is ticked (its documents added) or unticked (taken away). */
export function toggleFolder(chosen: Set<string>, docs: RefDoc[], tree: RefFolder[], folder: string, on: boolean): Set<string> {
  const next = new Set(chosen);
  for (const d of inFolder(docs, tree, folder)) if (on) next.add(d.id); else next.delete(d.id);
  return next;
}

/** A folder's tick as shown: on when every document it would take is chosen (and it takes some), mixed when only some are. */
export function folderState(chosen: Set<string>, docs: RefDoc[], tree: RefFolder[], folder: string): 'on' | 'off' | 'mixed' {
  const inside = inFolder(docs, tree, folder);
  const n = inside.filter((d) => chosen.has(d.id)).length;
  return n === 0 ? 'off' : n === inside.length ? 'on' : 'mixed';
}

/** The documents to send, in the list's order, those that can go. */
export const chosenRefs = (docs: RefDoc[], chosen: Set<string>) => docs.filter((d) => chosen.has(d.id) && canSend(d));

/** Their files the project's file does not hold (above its keep limit): to fetch before the issue, or left out by the engine. */
export function notHeld(docs: RefDoc[]): { id: string; name: string; sha256: string }[] {
  return docs.flatMap((d) => d.files.filter((f) => !f.held).map((f) => ({ id: d.id, name: f.name, sha256: f.sha256 })));
}
