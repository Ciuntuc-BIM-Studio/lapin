/**
 * Getting quantities out of the model (decision 287, the tester's report #75): a schedule made and opened, the
 * quantity takeoff's groupings, a schedule placed on a sheet.
 */

import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { saveFile, safeFileName } from '../../engine/files';

export interface ScheduleKind { kind: string; label: string }

/** The takeoff's groupings, as the schedule keeps them (groupBy) and as the interface names them. */
export const TAKEOFF_GROUPINGS: { id: string; label: string; groupBy: string[] }[] = [
  { id: 'kind,type', label: 'By kind and type', groupBy: ['kind', 'type'] },
  { id: 'kind', label: 'By kind', groupBy: ['kind'] },
  { id: 'kind,material', label: 'By kind and material', groupBy: ['kind', 'material'] },
  { id: 'material', label: 'By material', groupBy: ['material'] },
  { id: 'storey,kind', label: 'By storey and kind', groupBy: ['storey', 'kind'] },
  { id: 'storey,kind,type', label: 'By storey, kind and type', groupBy: ['storey', 'kind', 'type'] },
  { id: 'storey,material', label: 'By storey and material', groupBy: ['storey', 'material'] },
  { id: 'type', label: 'By type', groupBy: ['type'] },
];

/** The grouping a takeoff's groupBy is (a list it does not know: shown as kind and type, the default). */
export function groupingOf(groupBy: unknown): string {
  const g = Array.isArray(groupBy) && groupBy.length ? groupBy.join(',') : 'kind,type';
  return TAKEOFF_GROUPINGS.some((x) => x.id === g) ? g : 'kind,type';
}

/** The schedule in front. */
export const openSchedule = (id: string) => useUi.setState({ view: 'schedule', scheduleId: id, selection: id, selected: [id], dialog: null });

/** A new schedule of this kind, opened. */
export async function newSchedule(kind: string, extra: Record<string, unknown> = {}): Promise<string | null> {
  const r = await execute('architecture.createSchedule', { kind }) as { id?: string } | null;
  if (!r?.id) return null;
  if (Object.keys(extra).length) await execute('architecture.updateSchedule', { id: r.id, ...extra });
  openSchedule(r.id);
  return r.id;
}

/** The project's quantity takeoff: the first there is, else a new one. */
export async function takeoff(): Promise<string | null> {
  const there = [...useSession.getState().nodes.values()].find((n) => n.type === 'LapinSchedule' && n.props.kind === 'quantities');
  if (there) { openSchedule(there.id); return there.id; }
  const id = await newSchedule('quantities');
  if (id) notify('Quantity takeoff made: it is in the project browser, under Schedules, and goes on a sheet like a view.');
  return id;
}

/** The quantity takeoff as a workbook, saved (downloaded in a browser). */
export async function exportTakeoff(): Promise<void> {
  const id = await takeoff();
  if (!id) return;
  const b64 = await query<string>('schedule.xlsx', { id });
  if (!b64) return;
  const name = useSession.getState().nodes.get(id)?.name ?? 'Quantity takeoff';
  const saved = await saveFile(Uint8Array.from(atob(b64), (c) => c.charCodeAt(0)), `${safeFileName(name, 'Quantities')}.xlsx`, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  if (saved) notify(`Exported ${saved}.`);
}

/** The sheets a schedule can go on: those it is not on yet. */
export function sheetsFor(scheduleId: string) {
  return [...useSession.getState().nodes.values()]
    .filter((n) => n.type === 'LapinSheet' && !((n.props.viewports as { viewId?: string }[] | undefined) ?? []).some((v) => v.viewId === scheduleId))
    .map((n) => ({ id: n.id, name: `${String(n.props.number ?? '')} · ${n.name}` }));
}

/** A schedule placed on a sheet (a new A3 one when none is given), and the sheet opened. */
export async function placeOnSheet(scheduleId: string, sheetId: string | null): Promise<void> {
  let id = sheetId;
  if (!id) {
    const name = useSession.getState().nodes.get(scheduleId)?.name ?? 'Schedule';
    const r = await execute('sheet.create', { name, paper: 'A3' }) as { id?: string } | null;
    id = r?.id ?? null;
  }
  if (!id) return;
  // In the middle of the paper's free area; the user moves it on the sheet.
  const r = await execute('sheet.addViewport', { id, viewId: scheduleId });
  if (r) useUi.setState({ view: 'sheet', sheetId: id });
}
