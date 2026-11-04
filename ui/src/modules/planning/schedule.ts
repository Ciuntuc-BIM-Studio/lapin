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
import type { ScheduleDto, TaskDto } from './gantt';

export const usePlanning = create<{ data: ScheduleDto | null; version: number }>(() => ({ data: null, version: -2 }));

let asking: number | null = null;

async function refresh(version: number): Promise<void> {
  if (usePlanning.getState().version === version || asking === version) return;
  asking = version;
  const d = await query<ScheduleDto>('planning.schedule');
  // A newer change asked again meanwhile: its answer is the one kept.
  if (asking !== version) return;
  asking = null;
  if (d) usePlanning.setState({ data: d, version });
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
