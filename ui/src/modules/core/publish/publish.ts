/**
 * Publish sets (forum topic 225, as Revit's print and export sets): named choices of sheets and views kept on the
 * project by the engine (`publish.setSet`, `publish.removeSet`, `publish.sets`), exported again in one step
 * (`export.publish`): each one as its own PDF, all in one PDF, each as a DXF. On the desktop the files go into a folder
 * picked; in a browser one file comes as itself and more as one zip. What the dialog lists, counts and names is
 * decided here.
 */

import type { NodeDto, RelationDto } from '../../../engine/protocol';
import { engine, execute, query } from '../../../engine/session';
import { exportFile, isDesktop } from '../../../engine/files';
import { storeys } from '../../../engine/model';
import { notify } from '../../../app/store';
import { LONG_TIMEOUT_MS } from '../../../engine/client';
import { implicitPlan, itemOf, THREE_D, type BrowserItem, type ItemKind } from '../../../shell/browserSets';
import { storeyPlanView } from '../../../viewport/storeyPlan';
import { pickFolder } from '../cde/cde';

export interface PublishFormats { pdf: boolean; combined: boolean; dxf: boolean }
export interface PublishSet { id: string; name: string; sheets: string[]; views: string[]; missing: number; formats: PublishFormats }
export interface PublishChoice { sheets: string[]; views: string[]; formats: PublishFormats }

export const DEFAULT_FORMATS: PublishFormats = { pdf: true, combined: false, dxf: false };

/** The kinds of view a set takes: those that print as a drawing (schedules and profiles go on sheets). */
export const PUBLISHABLE: ItemKind[] = ['plan', 'site', 'section', 'elevation', 'plane', '3d'];
export const KIND_LABEL: Record<string, string> = { plan: 'Floor plans', site: 'Site', section: 'Sections', elevation: 'Elevations', plane: 'Reference planes', '3d': '3D views' };

/** The views a set can take, as the project browser lists them (a level's default plan until it has a plan of its own, the 3D view), in its order by kind. */
export function publishableViews(nodes: Map<string, NodeDto>, relations: Map<string, RelationDto>): BrowserItem[] {
  const levels = storeys({ nodes, relations }).slice().reverse();
  const own = new Set(levels.filter((s) => storeyPlanView(nodes, s.id)).map((s) => s.id));
  const items = [...levels.filter((s) => !own.has(s.id)).map(implicitPlan), THREE_D, ...[...nodes.values()].map(itemOf).filter((i): i is BrowserItem => i !== null)];
  return PUBLISHABLE.flatMap((k) => items.filter((i) => i.kind === k));
}

/** The project's sheets in number order (as an issue lists them). */
export const sheetsOf = (nodes: Map<string, NodeDto>): NodeDto[] =>
  [...nodes.values()].filter((n) => n.type === 'LapinSheet').sort((a, b) => String(a.props.number ?? '').localeCompare(String(b.props.number ?? ''), undefined, { numeric: true }) || a.name.localeCompare(b.name));

/** How many files an export makes: a PDF and a DXF for each sheet and view as asked, and the one PDF for all. */
export function fileCount(c: PublishChoice): number {
  const n = c.sheets.length + c.views.length;
  if (n === 0) return 0;
  return (c.formats.pdf ? n : 0) + (c.formats.dxf ? n : 0) + (c.formats.combined ? 1 : 0);
}

/** Whether the choice is the set as saved (the same sheets and views in the same order, the same formats). */
export function sameAs(set: PublishSet | null | undefined, c: PublishChoice): boolean {
  if (!set) return false;
  const eq = (a: string[], b: string[]) => a.length === b.length && a.every((x, i) => x === b[i]);
  return eq(set.sheets, c.sheets) && eq(set.views, c.views) && set.formats.pdf === c.formats.pdf && set.formats.combined === c.formats.combined && set.formats.dxf === c.formats.dxf;
}

/** A name for a new set that no set has yet: "Set 1", "Set 2"… */
export function newSetName(sets: PublishSet[], base = 'Set'): string {
  const taken = new Set(sets.map((s) => s.name.trim().toLowerCase()));
  for (let i = sets.length + 1; ; i++) if (!taken.has(`${base} ${i}`.toLowerCase())) return `${base} ${i}`;
}

/** What a browser saves: the one file it would be (its extension), else a zip. */
export function deliveredAs(c: PublishChoice): { zip: boolean; ext: string } {
  if (fileCount(c) !== 1) return { zip: true, ext: 'zip' };
  return { zip: false, ext: c.formats.dxf ? 'dxf' : 'pdf' };
}

export const publishSets = () => query<PublishSet[]>('publish.sets').then((r) => r ?? []);
export const savePublishSet = (s: { id?: string; name?: string } & Partial<PublishChoice>) => execute('publish.setSet', { ...s }) as Promise<{ id: string } | null>;
export const removePublishSet = (id: string) => execute('publish.removeSet', { id });

/**
 * The export: on the desktop into a folder picked (each file by its name); in a browser saved as the one file, or as one zip named after the
 * set. False when stepped back or nothing went.
 */
export async function runPublish(c: PublishChoice, name: string, set?: string): Promise<boolean> {
  const args = set ? { set, formats: c.formats } : { sheets: c.sheets, views: c.views, formats: c.formats, name };
  if (isDesktop()) {
    const folder = await pickFolder(null);
    if (!folder) return false;
    try {
      const r = await engine.call<{ written: string[]; missing: number }>('export.publish', { ...args, folder }, { timeoutMs: LONG_TIMEOUT_MS });
      notify(`Exported ${r.written.length} files into ${folder.split(/[\\/]/).pop()}.`);
      if (r.missing) notify(`${r.missing} sheets or views of the set are no longer in the project.`);
      return true;
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); return false; }
  }
  const { zip, ext } = deliveredAs(c);
  const fileName = zip ? `${name.trim() || 'Sheets and views'}.zip` : `${name.trim() || 'drawing'}.${ext}`;
  return exportFile('export.publish', { ...args, zip }, fileName, ext);
}
