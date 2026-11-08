/**
 * The File menu's actions, each with a stable id (file.save), in the menu's sections: what the menu draws and what a
 * module may replace (`replaces.fileActions`, docs/overrides.md). The keyboard's New, Open, Save and Save as run the
 * same actions, replaced or not.
 */

import type { FileAction } from '../platform/module';
import { modules } from '../platform/modules';
import { resolveFileActions } from '../platform/replaces';
import { useUi } from '../app/store';
import { importDxf, importIfc, linkPointCloud, newProject, openProject, saveProject, importFragments, importMesh, importTrace } from '../engine/files';
import { openOptimize } from '../engine/ifcOptimize';
import { engineInBrowser } from '../engine/session';
import { leave } from '../engine/share';

/** A section of the menu: its heading (none for the first), its actions by id. */
export interface FileSection { head?: string; ids: string[] }

const dialog = (id: string) => () => useUi.setState({ dialog: id });

/** Every File action as Lapin has it. */
export function baseFileActions(): FileAction[] {
  return [
    { id: 'file.new', label: 'New project', keys: 'app.new', run: () => newProject() },
    { id: 'file.open', label: 'Open…', keys: 'app.open', run: () => openProject() },
    { id: 'file.save', label: 'Save', keys: 'app.save', run: () => saveProject() },
    { id: 'file.saveAs', label: 'Save as…', keys: 'app.saveAs', run: () => saveProject(true) },
    { id: 'file.saveVersion', label: 'Save version…', run: dialog('versions') },
    { id: 'file.importIfc', label: 'Import IFC…', run: () => importIfc() },
    { id: 'file.importFragments', label: 'Import Fragments…', run: () => importFragments() },
    { id: 'file.linkIfc', label: 'Link IFC…', run: () => importIfc('link') },
    { id: 'file.exportExchange', label: 'Export IFC or Fragments…', run: dialog('exportExchange') },
    // The analytical model for JG Structures' structural analysis (docs/jg-structures.md).
    { id: 'file.exportJg', label: 'Export JG Structures (JSON)…', run: dialog('exportJg') },
    // A file cleaned and lightened into a copy, no project needed (docs/ifc-optimize.md).
    { id: 'file.optimizeIfc', label: 'Optimize IFC…', menu: 'optimize-ifc', run: () => openOptimize() },
    // A scan linked (LAS, LAZ, E57, PLY, XYZ, PTS, CSV; docs/point-clouds.md), kept outside the project.
    { id: 'file.linkPointCloud', label: 'Link point cloud…', run: () => linkPointCloud() },
    { id: 'file.importDxf', label: 'Import DXF…', run: () => importDxf() },
    { id: 'file.importTrace', label: 'Import PDF or image…', run: () => importTrace() },
    { id: 'file.importMesh', label: 'Import mesh (OBJ, STL, glTF)…', run: () => importMesh() },
    { id: 'file.exportMesh', label: 'Export 3D (glTF, USD, DAE)…', run: dialog('exportMesh') },
    { id: 'file.share', label: 'Share session…', shown: () => !engineInBrowser, run: dialog('share') },
    { id: 'file.join', label: 'Join session…', run: dialog('join') },
    { id: 'file.startScreen', label: 'Start screen', run: () => useUi.setState({ screen: 'start' }) },
    { id: 'file.leave', label: 'Leave session', run: () => leave() },
  ];
}

/** The menu of the computer's own user. The model exchanged (docs/fragments.md, Exchange): IFC and Fragments, chosen alike both ways. */
export const HOST_SECTIONS: FileSection[] = [
  { ids: ['file.new', 'file.open', 'file.save', 'file.saveAs', 'file.saveVersion'] },
  { head: 'IFC and Fragments', ids: ['file.importIfc', 'file.importFragments', 'file.linkIfc', 'file.exportExchange', 'file.exportJg', 'file.optimizeIfc'] },
  { head: 'Other formats', ids: ['file.linkPointCloud', 'file.importDxf', 'file.importTrace', 'file.importMesh', 'file.exportMesh'] },
  { ids: ['file.share', 'file.join', 'file.startScreen'] },
];

/** A guest works in the host's project: the host opens and saves it; a guest exports what they need, or leaves. */
export const GUEST_SECTIONS: FileSection[] = [{ ids: ['file.exportExchange', 'file.exportJg', 'file.exportMesh', 'file.leave'] }];

let actions: Map<string, FileAction> | null = null;

/** The File actions as this product has them, made once. */
export function fileActions(): Map<string, FileAction> {
  return actions ??= resolveFileActions(baseFileActions(), modules);
}

/** A File action run by its id (the keyboard's Save is file.save). */
export const runFileAction = (id: string): unknown => fileActions().get(id)?.run();
