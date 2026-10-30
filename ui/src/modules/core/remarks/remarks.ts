/**
 * Remarks on the model as BCF topics (docs/plan-cde.md, stage 2): what the
 * interface knows on its own — the filters over the list, who is writing, a
 * viewpoint taken from the 3D view and one opened in it — and the calls to
 * the engine. A BCF file comes in and goes out through the same pickers the
 * IFC import and export use, on the desktop and in a browser.
 */

import { notify, useUi } from '../../../app/store';
import { engine, execute, query, useSession } from '../../../engine/session';
import { exportFile, isDesktop, pickFile, pickPath, toBase64 } from '../../../engine/files';
import { savedName } from '../../../engine/share';
import { LONG_TIMEOUT_MS } from '../../../engine/client';
import { useCamera } from '../../../viewport/camera';
import { modelBounds, useSectionBox } from '../../../viewport/sectionBox';
import { boxOfPlanes, distanceTo, fromBcfCamera, planesOfBox, toBcfCamera, type BcfCamera, type BcfPlane, type Triplet } from './bcfCamera';

export interface Viewpoint {
  guid: string; index?: number; camera?: BcfCamera; selection: string[]; defaultVisibility: boolean; exceptions: string[];
  clippingPlanes: BcfPlane[]; lines: { start: Triplet; end: Triplet }[]; hasSnapshot: boolean; snapshot?: string;
}
export interface Comment { guid: string; date: string; author: string; text: string; viewpoint?: string; modifiedDate?: string; modifiedAuthor?: string }
export interface Remark {
  id: string; guid: string; title: string; description?: string; type?: string; status?: string; priority?: string; index?: number;
  creationDate: string; creationAuthor: string; modifiedDate?: string; modifiedAuthor?: string; assignedTo?: string; dueDate?: string; stage?: string;
  labels: string[]; comments: Comment[]; viewpoints: Viewpoint[]; open: boolean;
}
export interface RemarkList { remarks: Remark[]; statuses: string[]; types: string[]; priorities: string[] }

export const ANY = '';

export interface Filter { status: string; priority: string; assignee: string; text: string; open: boolean }
export const noFilter: Filter = { status: ANY, priority: ANY, assignee: ANY, text: '', open: false };

/** The remarks the filter keeps: by status, priority and assignee (exact, case blind), open only, and a text in the title, description, labels or comments. */
export function filterRemarks(list: Remark[], f: Filter): Remark[] {
  const same = (a: string | undefined, b: string) => !b || (a ?? '').toLowerCase() === b.toLowerCase();
  const text = f.text.trim().toLowerCase();
  return list.filter((r) =>
    same(r.status, f.status) && same(r.priority, f.priority) && same(r.assignedTo, f.assignee) && (!f.open || r.open)
    && (!text || [r.title, r.description ?? '', ...r.labels, ...r.comments.map((c) => c.text)].some((s) => s.toLowerCase().includes(text))));
}

/** The assignees the list names, for the filter's choices. */
export const assigneesOf = (list: Remark[]): string[] => [...new Set(list.map((r) => r.assignedTo).filter((s): s is string => Boolean(s)))].sort();

/** A remark's time, kept as the file gave it (ISO 8601), shown in the user's own. */
export function when(iso: string | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
}

const AUTHOR_KEY = 'lapin.remarks.author';
/** Who writes the remarks here: the name kept for them, else the one used in shared sessions. */
export function author(): string {
  try { return localStorage.getItem(AUTHOR_KEY) || savedName() || ''; } catch { return savedName() || ''; }
}
export function setAuthor(name: string): void {
  try { localStorage.setItem(AUTHOR_KEY, name.trim()); } catch { /* the name lasts this session */ }
}

export const listRemarks = () => query<RemarkList>('remarks.list');
export const snapshotOf = (id: string, guid?: string) => query<{ data: string | null }>('remarks.snapshot', { id, ...(guid ? { guid } : {}) }).then((r) => r?.data ?? null);

/** The 3D view as it is now, as a BCF viewpoint: the camera, the selection, the section box as clipping planes, the snapshot. */
export function viewpointNow(): Omit<Viewpoint, 'guid' | 'hasSnapshot'> & { snapshot?: string } {
  const cam = useCamera.getState();
  const nodes = useSession.getState().nodes;
  const selection = useUi.getState().selected.filter((id) => nodes.has(id));
  const box = useSectionBox.getState().box;
  const png = cam.api?.snapshot(1280) ?? null;
  return {
    camera: toBcfCamera(cam), selection, defaultVisibility: true, exceptions: [],
    clippingPlanes: box ? planesOfBox(box) : [], lines: [],
    ...(png ? { snapshot: png.replace(/^data:image\/png;base64,/, '') } : {}),
  };
}

/** The elements of a viewpoint's selection that the project has, by GlobalId (a remark from another tool names elements this model may lack). */
export const found = (vp: Viewpoint | undefined): string[] => { const nodes = useSession.getState().nodes; return (vp?.selection ?? []).filter((id) => nodes.has(id)); };

/** The centre of what the render draws for these elements; null when none is drawn. */
function centreOf(ids: string[]): Triplet | null {
  const render = useSession.getState().render;
  let n = 0; const sum = [0, 0, 0];
  for (const id of ids) {
    const item = render.get(id);
    if (!item || item.positions.length < 3) continue;
    const p = item.positions;
    let x = 0, y = 0, z = 0; const count = p.length / 3;
    for (let i = 0; i < p.length; i += 3) { x += p[i]; y += p[i + 1]; z += p[i + 2]; }
    sum[0] += x / count; sum[1] += y / count; sum[2] += z / count; n++;
  }
  return n ? [sum[0] / n, sum[1] / n, sum[2] / n] : null;
}

/** Waits for the 3D view's camera api (the view may be opening now). */
async function cameraApi(): Promise<NonNullable<ReturnType<typeof useCamera.getState>['api']>> {
  for (let i = 0; i < 40; i++) {
    const api = useCamera.getState().api;
    if (api) return api;
    await new Promise((r) => setTimeout(r, 50));
  }
  throw new Error('The 3D view did not open.');
}

/**
 * A viewpoint opened in the 3D view: the view in front, the camera at the
 * remark's (a perspective camera aimed at what it selected), the elements it
 * named selected, its clipping planes as the section box (axis-aligned ones;
 * none: the box goes). Hidden elements are not applied: Lapin's 3D has no
 * per-element hiding yet.
 */
export async function openViewpoint(vp: Viewpoint): Promise<void> {
  useUi.setState({ view: '3d', dialog: null });
  const api = await cameraApi();
  const ids = found(vp);
  if (vp.camera) {
    const centre = centreOf(ids);
    const distance = (centre && distanceTo(vp.camera, centre)) ?? 10;
    const { pose, ortho } = fromBcfCamera(vp.camera, useCamera.getState().fov, distance);
    api.setOrtho(ortho);
    api.orbitTo(pose, false);
  }
  useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
  const s = useSession.getState();
  const bounds = modelBounds(s.render.values(), 0.5, 0, s.meshes);
  useSectionBox.setState({ box: vp.clippingPlanes.length ? boxOfPlanes(vp.clippingPlanes, bounds) : null });
}

export interface NewRemark { title: string; description: string; type: string; status: string; priority: string; assignedTo: string; dueDate: string; labels: string[] }

export const createRemark = (r: NewRemark, viewpoint: ReturnType<typeof viewpointNow> | null) =>
  execute('remarks.create', { ...r, author: author(), ...(viewpoint ? { viewpoint } : {}) }) as Promise<{ id: string; guid: string }>;
export const updateRemark = (id: string, fields: Partial<NewRemark>) => execute('remarks.update', { id, ...fields, author: author() });
export const commentRemark = (id: string, text: string, withView: boolean) => execute('remarks.comment', { id, text, author: author(), ...(withView ? { viewpoint: viewpointNow() } : {}) });
export const retakeViewpoint = (id: string) => execute('remarks.addViewpoint', { id, replace: true, viewpoint: viewpointNow(), author: author() });
export const deleteRemarks = (ids: string[]) => execute('remarks.delete', { ids });

export interface ImportResult { name: string; topics: number; added: number; updated: number; comments: number; viewpoints: number; version: string }

/** A BCF file (2.1 .bcfzip or 3.0 .bcf) picked and read into the project's remarks: by path on the desktop, by its bytes in a browser. */
export async function importBcf(): Promise<ImportResult | null> {
  const picked = isDesktop() ? null : await pickFile('BCF', ['bcfzip', 'bcf']);
  const path = isDesktop() ? await pickPath('open', undefined, [{ name: 'BCF', extensions: ['bcfzip', 'bcf'] }]) : picked?.name ?? null;
  if (!path) return null;
  const source = picked ? { name: picked.name, data: toBase64(picked.bytes) } : { name: path.split(/[\\/]/).pop() ?? path, path };
  try {
    const r = await engine.call<ImportResult>('project.importBcf', source, { timeoutMs: LONG_TIMEOUT_MS });
    notify(`Imported ${r.name}: ${r.topics} remarks (${r.added} new, ${r.updated} updated).`);
    return r;
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
    return null;
  }
}

/** The remarks as a BCF file: all, the open ones, or those given; 2.1 (.bcfzip) unless 3.0 (.bcf) is asked. */
export async function exportBcf(what: { ids?: string[]; open?: boolean }, version: '2.1' | '3.0' = '2.1'): Promise<void> {
  const title = useSession.getState().doc?.path?.split(/[\\/]/).pop()?.replace(/\.[^.]+$/, '') || 'remarks';
  const ext = version === '3.0' ? 'bcf' : 'bcfzip';
  await exportFile('project.exportBcf', { ...what, version }, `${title}.${ext}`, ext);
}
