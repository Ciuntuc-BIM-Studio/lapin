/**
 * The Speckle connector (docs/plan-speckle.md, decision 807): File → Speckle → Send…, the model sent to a Speckle server;
 * File → Speckle → Receive…, a version of a Speckle model into the project as references.
 * Desktop only (module.json): the browser's build leaves it out with its engine half (scripts/product.mjs).
 */

import type { LapinModule } from '../../platform/module';
import { lazyPart } from '../../platform/lazyPart';

// Fetched the first time it is opened (platform/lazyPart.tsx).
const SpeckleSendDialog = lazyPart(() => import('./SpeckleSendDialog').then((m) => m.SpeckleSendDialog), { title: 'Send to Speckle' });
const SpeckleReceiveDialog = lazyPart(() => import('./SpeckleReceiveDialog').then((m) => m.SpeckleReceiveDialog), { title: 'Receive from Speckle' });

const speckle: LapinModule = {
  id: 'speckle',
  name: 'Speckle',
  summary: 'The model sent to a Speckle server, for Revit, Rhino, Power BI, Blender and the web viewer: each element with its GlobalId, surfaces and properties; and models received from it as references.',
  dialogs: { speckleSend: SpeckleSendDialog, speckleReceive: SpeckleReceiveDialog },
};

export default speckle;
