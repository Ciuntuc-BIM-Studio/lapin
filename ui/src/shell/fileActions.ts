/**
 * The File menu's actions, each with a stable id (file.save), in the menu's sections: what the menu draws and what a
 * module may replace (`replaces.fileActions`, docs/overrides.md). The keyboard's New, Open, Save and Save as run the
 * same actions, replaced or not.
 */

import type { FileAction } from '../platform/module';
import { modules } from '../platform/modules';
import { resolveFileActions } from '../platform/replaces';
import { useUi } from '../app/store';
import { importDxf, importIfc, linkPointCloud, newProject, openProject, saveProject, importFragments, importBos, importSdnf, linkLapin, importMesh, importTrace, importProjectTypes, isDesktop } from '../engine/files';
import { openOptimize } from '../engine/ifcOptimize';
import { engineInBrowser } from '../engine/session';
import { hasModule } from '../platform/brand';
import { leave } from '../engine/share';
import { getLatest } from '../engine/remote';
import { cdeActive, useCde } from '../engine/cdeServer';

/** A submenu of the File menu (Import ▸, Export ▸): its name and its actions by id; `recent` lists the recent projects instead. */
export interface FileSub { sub: string; ids?: string[]; recent?: boolean }

/** A section of the menu: its heading (none for the first), its entries: an action's id, or a submenu. */
export interface FileSection { head?: string; ids: (string | FileSub)[] }

const dialog = (id: string) => () => useUi.setState({ dialog: id });

/** Every File action as Lapin has it. */
export function baseFileActions(): FileAction[] {
  return [
    { id: 'file.new', label: 'New project', keys: 'app.new', run: () => newProject() },
    { id: 'file.open', label: 'Open…', keys: 'app.open', run: () => openProject() },
    { id: 'file.save', label: 'Save', keys: 'app.save', run: () => saveProject() },
    { id: 'file.saveAs', label: 'Save as…', keys: 'app.saveAs', run: () => saveProject(true) },
    { id: 'file.saveVersion', label: 'Save version…', run: dialog('versions') },
    // Project templates (issues #9 and #20, decision 470): the project's set-up kept on this computer, new projects started from it.
    { id: 'file.newFromTemplate', label: 'New from template…', run: dialog('projectTemplates') },
    { id: 'file.saveTemplate', label: 'Save as template…', run: dialog('saveTemplate') },
    // Another project's types, chosen, with their materials (issue #74): the source only read.
    { id: 'file.importProjectTypes', label: 'Import types from a project…', run: () => importProjectTypes() },
    { id: 'file.importIfc', label: 'Import IFC…', run: () => importIfc() },
    { id: 'file.importFragments', label: 'Import Fragments…', run: () => importFragments() },
    { id: 'file.importBos', label: 'Import BIM Open Schema…', run: () => importBos() },
    // SDNF, the steel detailing neutral file (docs/sdnf.md): where the product has the steel module.
    { id: 'file.importSdnf', label: 'Import SDNF…', menu: 'import-sdnf', shown: () => hasModule('steel'), run: () => importSdnf() },
    { id: 'file.linkIfc', label: 'Link IFC…', run: () => importIfc('link') },
    { id: 'file.linkLapin', label: 'Link Lapin project…', run: () => linkLapin() },
    { id: 'file.exportExchange', label: 'Export IFC, Fragments or BOS…', run: dialog('exportExchange') },
    // The analytical model for JG Structures' structural analysis (docs/jg-structures.md).
    { id: 'file.exportJg', label: 'Export JG Structures (JSON)…', run: dialog('exportJg') },
    // A file cleaned and lightened into a copy, no project needed (docs/ifc-optimize.md).
    { id: 'file.optimizeIfc', label: 'Optimize IFC…', menu: 'optimize-ifc', run: () => openOptimize() },
    // A scan linked (LAS, LAZ, E57, PLY, XYZ, PTS, CSV; docs/point-clouds.md), kept outside the project.
    { id: 'file.linkPointCloud', label: 'Link point cloud…', run: () => linkPointCloud() },
    { id: 'file.importDxf', label: 'Import DXF…', run: () => importDxf() },
    { id: 'file.importTrace', label: 'Import PDF or image…', run: () => importTrace() },
    { id: 'file.importMesh', label: 'Import mesh (OBJ, STL, glTF, PLY)…', run: () => importMesh() },
    // Manage → Publish…: the sheets and views printed or sent out (also on the top bar's quick-access group).
    { id: 'file.publish', label: 'Publish…', run: dialog('publish') },
    { id: 'file.exportMesh', label: 'Export 3D (glTF, USD, DAE)…', run: dialog('exportMesh') },
    // The project as one .html that any browser opens, and that Lapin opens again (decision 482, docs/html-export.md).
    { id: 'file.exportHtml', label: 'Lapin for the browser (.html)…', menu: 'export-html', run: dialog('exportHtml') },
    // Speckle (docs/plan-speckle.md, S1): the model sent to a Speckle server; desktop only, where the product has the module.
    { id: 'file.speckleSend', label: 'Speckle: Send…', menu: 'speckle-send', shown: () => hasModule('speckle') && !engineInBrowser, run: dialog('speckleSend') },
    // S2: a version of a Speckle model received as references.
    { id: 'file.speckleReceive', label: 'Speckle: Receive…', menu: 'speckle-receive', shown: () => hasModule('speckle') && !engineInBrowser, run: dialog('speckleReceive') },
    // Archicad (docs/plan-archicad.md, A1): the model of a running Archicad (Tapir) received, and its IFC sent to the project's Atrium server.
    { id: 'file.archicadReceive', label: 'Archicad: Receive…', menu: 'archicad-receive', shown: () => hasModule('archicad') && !engineInBrowser, run: dialog('archicadReceive') },
    // A2: only what changed in Archicad since the last receive, in place (the module's "dialog" runs it and closes: no window of its own).
    { id: 'file.archicadUpdate', label: 'Get latest from Archicad', menu: 'archicad-update', shown: () => hasModule('archicad') && !engineInBrowser, run: dialog('archicadUpdate') },
    { id: 'file.archicadSend', label: 'Archicad: Send to Atrium…', menu: 'archicad-send', shown: () => hasModule('archicad') && !engineInBrowser, run: dialog('archicadSend') },
    // Lapin Extensions (docs/plan-extensions.md): content packs from the catalogue, into Lapin's own libraries.
    { id: 'file.extensions', label: 'Extensions…', menu: 'extensions', run: dialog('extensions') },
    // Server and collaboration (docs/remote.md): the project's history and its remote, a server or a folder.
    // In the browser they reach the CDE server the page is served by (its engine's remote is asynchronous, the same client as the
    // desktop's: Atrium docs/plan-cde-web.md, W2; same origin, so no other server); without one they stay in the desktop app.
    { id: 'file.collaborate', label: 'Collaborate…', shown: () => !engineInBrowser || cdeActive(), run: dialog('remote') },
    { id: 'file.saveServer', label: 'Save to server…', shown: () => !engineInBrowser || cdeActive(), run: dialog('remoteSave') },
    { id: 'file.getLatest', label: 'Get latest from server', shown: () => !engineInBrowser || cdeActive(), run: () => getLatest() },
    { id: 'file.cdeProjects', label: 'Server projects…', menu: 'cde-projects', shown: () => cdeActive(), run: () => { useCde.setState({ offline: false }); useUi.setState({ screen: 'start' }); } },
    { id: 'file.openServer', label: 'Open from server…', shown: () => isDesktop(), run: dialog('remoteClone') },
    { id: 'file.share', label: 'Share session…', shown: () => !engineInBrowser, run: dialog('share') },
    { id: 'file.join', label: 'Join session…', run: dialog('join') },
    { id: 'file.preferences', label: 'Preferences…', keys: 'app.preferences', run: dialog('preferences') },
    { id: 'file.startScreen', label: 'Start screen', run: () => useUi.setState({ screen: 'start' }) },
    { id: 'file.leave', label: 'Leave session', run: () => leave() },
  ];
}

/**
 * The menu of the computer's own user: the project's files at the top, then Import, Export and Collaborate as submenus
 * (forum topic 257); Undo and Redo are on the top bar and on their keys. The model exchanged (docs/fragments.md,
 * Exchange): IFC and Fragments, chosen alike both ways.
 */
export const HOST_SECTIONS: FileSection[] = [
  { ids: ['file.new', 'file.newFromTemplate', 'file.open', { sub: 'Open recent', recent: true }, 'file.save', 'file.saveAs', 'file.saveVersion', 'file.saveTemplate'] },
  {
    ids: [
      { sub: 'Import', ids: ['file.importProjectTypes', 'file.importIfc', 'file.importFragments', 'file.importBos', 'file.importSdnf', 'file.linkIfc', 'file.linkLapin', 'file.linkPointCloud', 'file.importDxf', 'file.importTrace', 'file.importMesh'] },
      { sub: 'Export', ids: ['file.exportExchange', 'file.exportJg', 'file.exportMesh', 'file.exportHtml', 'file.publish'] },
      { sub: 'Collaborate', ids: ['file.speckleSend', 'file.speckleReceive', 'file.archicadReceive', 'file.archicadUpdate', 'file.archicadSend', 'file.cdeProjects', 'file.collaborate', 'file.saveServer', 'file.getLatest', 'file.openServer', 'file.share', 'file.join'] },
    ],
  },
  { ids: ['file.optimizeIfc', 'file.extensions'] },
  // Preferences (Developer extras among them) where the guide and the Script panel's hint send people: File → Preferences….
  { ids: ['file.preferences', 'file.startScreen'] },
];

/** A guest works in the host's project: the host opens and saves it; a guest exports what they need, or leaves. */
export const GUEST_SECTIONS: FileSection[] = [{ ids: ['file.exportExchange', 'file.exportJg', 'file.exportMesh', 'file.exportHtml', 'file.leave'] }];

let actions: Map<string, FileAction> | null = null;

/** The File actions as this product has them, made once. */
export function fileActions(): Map<string, FileAction> {
  return actions ??= resolveFileActions(baseFileActions(), modules);
}

/** A File action run by its id (the keyboard's Save is file.save). */
export const runFileAction = (id: string): unknown => fileActions().get(id)?.run();
