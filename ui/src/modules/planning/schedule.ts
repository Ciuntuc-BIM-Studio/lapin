/**
 * The work schedule as the interface holds it: the engine's answer to planning.schedule (tasks with their dates, floats
 * and critical flags, the links, the calendar), asked again after every change to the model, once whoever shows it.
 * And what the Planning tab does: open the Gantt under the view, add, indent and link tasks, exchange files.
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { focusPane, notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { pickFile, safeFileName, saveFile, toBase64 } from '../../engine/files';
import { t } from '../../i18n';
import type { NodeDto } from '../../engine/protocol';
import { expansionChanged } from '../../platform/groups';
import { useCamera } from '../../viewport/camera';
import { zoomHost } from '../../viewport/zoom';
import { showDate, type ScheduleDto, type TaskDto } from './gantt';
import { elementIds, frameDistance, TASK } from './links';

export const usePlanning = create<{ data: ScheduleDto | null; version: number }>(() => ({ data: null, version: -2 }));

let asking: number | null = null;

async function refresh(version: number): Promise<void> {
  if (usePlanning.getState().version === version || asking === version) return;
  asking = version;
  const d = await query<ScheduleDto>('planning.schedule');
  // A newer change asked again meanwhile: its answer is the one kept.
  if (asking !== version) return;
  asking = null;
  if (d) { usePlanning.setState({ data: d, version }); expansionChanged(); }
}

/** The schedule, kept in step with the model. */
export function useSchedule(): ScheduleDto | null {
  const version = useSession((s) => s.version);
  const data = usePlanning((s) => s.data);
  useEffect(() => { void refresh(version); }, [version]);
  return data;
}

/** The tasks selected (the selection may hold elements too: they are left out). */
export function selectedTasks(): string[] {
  const nodes = useSession.getState().nodes;
  return useUi.getState().selected.filter((id) => nodes.get(id)?.type === 'IfcTask');
}

/** The Gantt in a pane of its own under the view (the second pane, the screen split in two rows), or brought in front. */
export function openGantt(): void {
  const s = useUi.getState();
  const at = s.panes.findIndex((p) => p.view === 'gantt');
  if (at >= 0) { focusPane(at); return; }
  if (s.panes.length < 2) {
    // The first pane keeps its view (the 3D, a plan); the Gantt goes under it.
    const first = s.panes[0];
    useUi.setState({ panes: [first, { ...first, view: 'gantt' }], split: 'column', pane: 1 });
    useUi.setState({ view: 'gantt' });
    return;
  }
  focusPane(1);
  useUi.setState({ view: 'gantt' });
}

/** A new task (or milestone) before the one selected, or at the end; it becomes the selection. */
export async function addTask(milestone = false, before?: string | null): Promise<string | null> {
  const sel = before === undefined ? selectedTasks()[0] ?? null : before;
  const r = await execute('planning.addTask', { milestone, ...(sel ? { before: sel } : {}) }) as { id: string } | null;
  if (r?.id) useUi.setState({ selection: r.id, selected: [r.id] });
  return r?.id ?? null;
}

function onTasks(command: string, none: string): () => void {
  return () => {
    const ids = selectedTasks();
    if (!ids.length) { notify(none); return; }
    void execute(command, { ids });
  };
}

export const indent = onTasks('planning.indent', 'Select the tasks to indent in the Gantt.');
export const outdent = onTasks('planning.outdent', 'Select the tasks to outdent in the Gantt.');
export const linkSelected = onTasks('planning.linkChain', 'Select two tasks or more in the Gantt to link them one after the other.');
export const unlinkSelected = onTasks('planning.unlink', 'Select the tasks to unlink in the Gantt.');
export const deleteSelected = onTasks('planning.deleteTasks', 'Select the tasks to delete in the Gantt.');

const title = () => safeFileName(usePlanning.getState().data?.name ?? 'Work schedule', 'Work schedule');
const bytes = (b64: string) => Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));

/** The schedule as MS Project XML (MS Project, ProjectLibre, GanttProject and Primavera P6 read it). */
export async function exportMsp(): Promise<void> {
  const b64 = await query<string>('planning.exportMsp');
  if (!b64) return;
  const saved = await saveFile(bytes(b64), `${title()}.xml`, 'application/xml');
  if (saved) notify(`Exported ${saved}.`);
}

/** The schedule as a workbook: the Gantt's table and the calendar's days off. */
export async function exportXlsx(): Promise<void> {
  const b64 = await query<string>('planning.exportXlsx');
  if (!b64) return;
  const saved = await saveFile(bytes(b64), `${title()}.xlsx`, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  if (saved) notify(`Exported ${saved}.`);
}

/** An MS Project XML file in place of the project's schedule (asked first when there is one). */
export async function importMsp(): Promise<void> {
  const picked = await pickFile('MS Project XML', ['xml']);
  if (!picked) return;
  const tasks = usePlanning.getState().data?.tasks.length ?? 0;
  if (tasks > 0 && !window.confirm(t("The project's schedule is replaced by the file's. Go on?"))) return;
  const r = await execute('planning.importMsp', { data: toBase64(picked.bytes), name: picked.name }) as { tasks: number; links: number; dropped: number; notes: string[] } | null;
  if (!r) return;
  notify(`Imported ${picked.name}: ${r.tasks} tasks, ${r.links} links.${r.notes.length ? ` ${r.notes.join(' ')}` : ''}`);
  openGantt();
}

/** The task in front of the properties panel, as the schedule has it now. */
export const taskOf = (data: ScheduleDto | null, id: string): TaskDto | undefined => data?.tasks.find((t) => t.id === id);

// ── The elements of the tasks (stage 2) ──────────────────────────────────────

/** What a task selected draws highlighted in the views: its elements (a summary's: its tasks' too). */
export function taskElementsOf(id: string, node: NodeDto | undefined): readonly string[] | null {
  if (node?.type !== TASK) return null;
  return usePlanning.getState().data?.tasks.find((x) => x.id === id)?.elements ?? null;
}

/** The elements of the selection (not its tasks). */
export const selectedElements = (): string[] => elementIds(useUi.getState().selected, useSession.getState().nodes);

/** Elements put in a task (an exception to its rule), or with `remove` taken out of it; what came of it said. */
export async function assignTo(task: string, ids: string[], remove = false): Promise<void> {
  if (!ids.length) { notify('Select elements of the model first: in 3D or in a plan.'); return; }
  const r = await execute('planning.assign', { task, ids, remove }) as { changed: number; noPart: number; count: number } | null;
  if (!r) return;
  const name = usePlanning.getState().data?.tasks.find((x) => x.id === task)?.name ?? '';
  if (r.noPart > 0) notify(`${r.noPart} element(s) have no such part and stay out of „${name}”.`);
  else notify(`„${name}” takes ${r.count} element(s).`);
}

/** The elements of a task selected in the views (a double-click on its bar), and framed: in 3D by the camera, in a plan by its zoom. */
export function showTaskElements(task: TaskDto): void {
  const ids = task.elements ?? [];
  if (!ids.length) { notify('This task takes no elements yet: drag the selection onto its row, or give it a rule in Properties.'); return; }
  const s = useUi.getState();
  const model = s.panes.findIndex((p) => p.view !== 'gantt');
  if (model >= 0) focusPane(model);
  useUi.setState({ selected: ids, selection: ids[ids.length - 1] });
  requestAnimationFrame(() => {
    if (zoomHost()?.fitSelection()) return;
    const cam = useCamera.getState();
    const box = cam.api?.boxOf?.(ids);
    if (!cam.api || !box) return;
    const c = { x: (box.min.x + box.max.x) / 2, y: (box.min.y + box.max.y) / 2, z: (box.min.z + box.max.z) / 2 };
    cam.api.orbitTo({ target: c, distance: frameDistance(box.min, box.max, cam.fov) });
  });
}

/** The elements no task takes (wholly or in part), selected, so they can be dragged onto a task. */
export async function selectUnlinked(): Promise<void> {
  const r = await query<{ none: string[]; partial: string[] }>('planning.unlinked');
  if (!r) return;
  const ids = [...r.none, ...r.partial];
  if (!ids.length) { notify('Every element has a task.'); return; }
  const s = useUi.getState();
  const model = s.panes.findIndex((p) => p.view !== 'gantt');
  if (model >= 0) focusPane(model);
  useUi.setState({ selected: ids, selection: ids[ids.length - 1] });
  notify(`${r.none.length} element(s) without a task, ${r.partial.length} with layers without one: selected.`);
}

/** TA: the elements selected put in a task chosen from a list. */
export function assignSelection(): void {
  if (!selectedElements().length) { notify('Select elements of the model first: in 3D or in a plan.'); return; }
  useUi.setState({ dialog: 'planning.assign' });
}

// ── Progress (stage 5) ───────────────────────────────────────────────────────

/**
 * The elements selected marked done, started, or back to not started, in every task that takes them (or `task` alone), on
 * the status date unless a day is given. A guest who joined to look may do it too (site marking through the shared session).
 */
export async function markSelection(state: 'done' | 'started' | 'none', ids = selectedElements(), task?: string, date?: string): Promise<void> {
  if (!ids.length) { notify('Select elements of the model first: in 3D or in a plan.'); return; }
  const command = state === 'done' ? 'planning.markDone' : state === 'started' ? 'planning.markStarted' : 'planning.unmark';
  const r = await execute(command, { ids, ...(task ? { task } : {}), ...(date ? { date } : {}) }) as { changed: number; tasks: number; none: number; date: string } | null;
  if (!r) return;
  if (state === 'none') notify(`${ids.length} element(s) not started, in ${r.tasks} task(s).`);
  else notify(`${ids.length} element(s) marked ${state === 'done' ? 'done' : 'started'} on ${showDate(r.date)}, in ${r.tasks} task(s).`);
}

/** The baseline saved from the dates as they are now (the one baseline: saved again, it is replaced, asked first). */
export async function saveBaseline(): Promise<void> {
  const data = usePlanning.getState().data;
  if (!data?.tasks.length) { notify('The schedule has no tasks to keep as a baseline.'); return; }
  if (data.baseline && !window.confirm(t('The baseline is replaced by the dates as they are now. Go on?'))) return;
  const r = await execute('planning.saveBaseline', {}) as { tasks: number } | null;
  if (r) { notify(`Baseline saved: ${r.tasks} tasks.`); openGantt(); }
}

export async function clearBaseline(): Promise<void> {
  if (!usePlanning.getState().data?.baseline) { notify('The schedule has no baseline.'); return; }
  if (!window.confirm(t('The baseline is taken off every task. Go on?'))) return;
  await execute('planning.clearBaseline', {});
}

/** The progress read back from a workbook (as Export XLSX writes it, filled in on site) or a CSV. */
export async function importProgress(): Promise<void> {
  const picked = await pickFile('Workbook', ['xlsx', 'csv']);
  if (!picked) return;
  const r = await execute('planning.importProgress', { data: toBase64(picked.bytes) }) as { rows: number; changed: number; unknown: number } | null;
  if (!r) return;
  notify(`Progress from ${picked.name}: ${r.changed} of ${r.rows} task(s) changed, ${r.unknown} row(s) not found.`);
  openGantt();
}
