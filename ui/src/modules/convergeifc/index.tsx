/**
 * The Converge IFC connector (docs/plan-converge-ifc.md): File → Collaborate → Converge IFC: Send…, the project sent as IFC to a
 * Converge IFC server (an ISO 19650 common data environment) as a new file or a new version of one; Converge IFC: Get…, a version
 * of a file there saved on this computer and handed to the IFC import. Desktop only (module.json): the browser's build leaves it
 * out with its engine half (scripts/product.mjs).
 */

import type { LapinModule } from '../../platform/module';
import { lazyPart } from '../../platform/lazyPart';

// Fetched the first time it is opened (platform/lazyPart.tsx).
const ConvergeSendDialog = lazyPart(() => import('./ConvergeSendDialog').then((m) => m.ConvergeSendDialog), { title: 'Send to Converge IFC' });
const ConvergeGetDialog = lazyPart(() => import('./ConvergeGetDialog').then((m) => m.ConvergeGetDialog), { title: 'Get from Converge IFC' });

const convergeifc: LapinModule = {
  id: 'convergeifc',
  name: 'Converge IFC',
  summary: 'The project sent as IFC to a Converge IFC server, a common data environment (ISO 19650) for IFC files, as a new file or a new version of one; and versions of the files there fetched into the IFC import.',
  dialogs: { convergeifcSend: ConvergeSendDialog, convergeifcGet: ConvergeGetDialog },
};

export default convergeifc;
