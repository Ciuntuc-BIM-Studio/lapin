/**
 * A reference model opened from the CDE web's Documents tab (docs/plan-cde-references.md §8, stage R7): a consultant's IFC added as a
 * reference document, shown in the browser's 3D read-only, the IDS report the server made of it beside it.
 *
 * - **Open in viewer**: the IFC opened as a project of its own (its spatial structure and GlobalIds), every element a reference as the file
 *   has it (an import reproduces the file, docs/spec-ifc.md). Nothing goes back to the server; Save keeps a copy on this computer.
 * - **Open with the project**: the project's latest commit on the server opened read-only, as "Open latest, read-only" does, and the model
 *   linked into it (docs/links.md: read-only, drawn in the views and in 3D, from its bytes, which the browser cannot read again).
 *
 * The file comes from the server (`GET {r}/{id}/files/{name}`); an .ifczip is read from its .ifc entry by the engine.
 */

import { notify, openTab, useUi } from '../../../app/store';
import { commitPackage, listRefs, readRefs, useCde } from '../../../engine/cdeServer';
import { LONG_TIMEOUT_MS } from '../../../engine/client';
import { confirmDiscard, enter, openProjectBytes, safeFileName } from '../../../engine/files';
import { begin, end, outcome, type ImportResult } from '../../../engine/ifcImport';
import type { IdsReport } from '../../../engine/serverIds';
import { engine } from '../../../engine/session';
import { fitAfterImport, poseKey } from '../../../viewport/camera';
import type { IfcHeaderInfo } from './adapter';
import type { RefFileData } from './references';

export const isModelName = (name: string) => /\.(ifc|ifczip)$/i.test(name);

/** The reference model open in the workspace, with its IDS report for the band beside the 3D. */
export type { OpenedReference } from '../../../engine/cdeServer';

/** The header's facts worth a line: the schema, the program that wrote it, the IFC project's name. */
export function headerFacts(h: IfcHeaderInfo | null | undefined): { label: string; value: string }[] {
  if (!h) return [];
  const out: { label: string; value: string }[] = [];
  if (h.schema) out.push({ label: 'Schema', value: h.schema });
  if (h.application) out.push({ label: 'Written by', value: h.application });
  if (h.projectName) out.push({ label: 'IFC project', value: h.projectName });
  if (h.organization) out.push({ label: 'Organisation', value: h.organization });
  return out;
}

/** An IDS report's counts, or null when the server ran none on the model. */
export function idsCounts(ids: IdsReport | null | undefined): { passed: number; failed: number; skipped: number } | null {
  if (!ids?.summary) return null;
  return { passed: ids.summary.passed, failed: ids.summary.failed, skipped: ids.summary.skipped };
}

/** The engine's import of the model: everything a reference, as a project of its own; or a link into the project open. */
export function importArgs(file: Pick<RefFileData, 'name' | 'data'>, withProject: boolean): Record<string, unknown> {
  return withProject
    ? { name: file.name, data: file.data, mode: 'link' }
    : { name: file.name, data: file.data, mode: 'reference', asProject: true, group: false };
}

/** What the notice says once it is open. */
export const openedNotice = (withProject: boolean) => (withProject
  ? 'The reference model is linked into the project\'s latest commit, read-only: nothing goes back to the server. Save keeps a copy on this computer.'
  : 'The reference model is open read-only: nothing goes back to the server. Save keeps a copy on this computer.');

/** The project's latest commit on main, or null when nothing was saved to it yet. */
async function latest(project: string): Promise<string | null> {
  return readRefs(await listRefs(project)).main?.target ?? null;
}

/**
 * Opens the reference model in the browser's 3D (alone, or linked into the project's latest commit). False when the user kept the open
 * project or it did not open.
 */
export async function openReferenceModel(o: { project: { id: string; name: string }; document: { id: string; title: string }; file: RefFileData; ids: IdsReport | null; withProject: boolean }): Promise<boolean> {
  const { project, document, file, ids, withProject } = o;
  if (withProject) {
    const head = await latest(project.id);
    if (!head) { notify('The project has nothing on the server yet: open the model alone.'); return false; }
    // The commit opened as "Open latest, read-only" opens it (it asks first when the open project has unsaved changes).
    if (!await openProjectBytes(safeFileName(`${project.name} ${head.slice(0, 8)}.lapin`), await commitPackage(project.id, head))) return false;
    useCde.setState({ opened: { project: project.name, commit: head } });
  } else {
    if (!(await confirmDiscard('The project has unsaved changes. Open another anyway?'))) return false;
    // Into an empty project first (entering forgets the 3D's bodies, so it comes before the import, as the start screen's IFC does).
    await engine.call('project.new', { plain: true });
    enter();
  }

  // The 3D in front (a model is looked at there first), before the camera's pose is taken: the import frames the model unless it moved.
  const tab = useUi.getState().viewTabs.find((t) => t.view === '3d');
  if (tab) openTab(useUi.getState().pane, tab);
  begin(`${withProject ? 'Linking' : 'Importing'} ${file.name}`, 'Reading the file');
  const pose = poseKey();
  try {
    const r = await engine.call<ImportResult>('project.importIfc', importArgs(file, withProject), { timeoutMs: LONG_TIMEOUT_MS });
    if (!withProject) useUi.setState({ storeyId: null });
    useCde.setState({ reference: { project: project.id, document: document.id, title: document.title, file: file.name, withProject, ids } });
    if (withProject) notify(outcome(file.name, 'link', r));
    // Said once, not kept: the band below says it stays read-only.
    useUi.setState({ notice: [openedNotice(withProject), ...(withProject ? [] : r.notes ?? [])].join(' '), noticeStays: false });
    fitAfterImport(pose);
    return true;
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
    return false;
  } finally { end(); }
}
