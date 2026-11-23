/**
 * The Speckle connector (docs/plan-speckle.md, decision 804): File → Speckle → Send…, the model sent to a Speckle server.
 * Desktop only (module.json): the browser's build leaves it out with its engine half (scripts/product.mjs).
 */

import type { LapinModule } from '../../platform/module';
import { lazyPart } from '../../platform/lazyPart';

// Fetched the first time it is opened (platform/lazyPart.tsx).
const SpeckleSendDialog = lazyPart(() => import('./SpeckleSendDialog').then((m) => m.SpeckleSendDialog), { title: 'Send to Speckle' });

const speckle: LapinModule = {
  id: 'speckle',
  name: 'Speckle',
  summary: 'The model sent to a Speckle server, for Revit, Rhino, Power BI, Blender and the web viewer: each element with its GlobalId, surfaces and properties.',
  dialogs: { speckleSend: SpeckleSendDialog },
};

export default speckle;
