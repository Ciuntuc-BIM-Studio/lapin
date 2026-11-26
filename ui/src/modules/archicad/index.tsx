/**
 * The Archicad connector (docs/plan-archicad.md, decision 882): File → Collaborate → Archicad: Receive…, the elements of a running
 * Archicad (through the free Tapir add-on) into the project, native where Lapin rebuilds their bodies, references otherwise;
 * Archicad: Send to Atrium…, Archicad's IFC put on the project's server as a reference model; Get latest from Archicad (A2), and the
 * status bar's "Archicad model changed (N)" while the project is linked to an Archicad model.
 * Desktop only (module.json): the browser's build leaves it out with its engine half (scripts/product.mjs).
 */

import { lazy, Suspense } from 'react';
import type { LapinModule } from '../../platform/module';
import { lazyPart } from '../../platform/lazyPart';

// Fetched the first time it is opened (platform/lazyPart.tsx).
const ArchicadReceiveDialog = lazyPart(() => import('./ArchicadReceiveDialog').then((m) => m.ArchicadReceiveDialog), { title: 'Receive from Archicad' });
// Get latest from Archicad (File → Collaborate): run as it is opened, no window of its own.
const ArchicadGetLatest = lazyPart(() => import('./getLatest').then((m) => m.GetLatestRun), { title: 'Get latest from Archicad' });
const ArchicadSendDialog = lazyPart(() => import('./ArchicadSendDialog').then((m) => m.ArchicadSendDialog), { title: 'Send to Atrium' });

// The status bar's item, fetched the first time the status bar draws it; nothing while it comes.
const ArchicadStatus = lazy(() => import('./ArchicadStatus').then((m) => ({ default: m.ArchicadStatus })));
const ArchicadStatusItem = () => <Suspense fallback={null}><ArchicadStatus /></Suspense>;

const archicad: LapinModule = {
  id: 'archicad',
  name: 'Archicad',
  summary: 'The elements of a running Archicad, through the free Tapir add-on, into the project: walls, slabs, columns, beams, openings and zones as native elements where they match Archicad\'s own bodies, the rest as references; and Archicad\'s IFC sent to the project\'s Atrium server.',
  dialogs: { archicadReceive: ArchicadReceiveDialog, archicadSend: ArchicadSendDialog, archicadUpdate: ArchicadGetLatest },
  statusItems: [ArchicadStatusItem],
};

export default archicad;
