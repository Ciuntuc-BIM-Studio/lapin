/** Entries a module adds to the right-click menu of drafting (Studio's Make family from sketch…), beside the core's Save as symbol…. */

import type { NodeDto } from '../engine/protocol';
import type { MenuItem } from '../shell/ContextMenu';

type Provider = (ids: string[], nodes: Map<string, NodeDto>) => MenuItem[];
const providers: Provider[] = [];

export function registerDraftMenu(p: Provider): void { if (!providers.includes(p)) providers.push(p); }
export const draftMenuItems = (ids: string[], nodes: Map<string, NodeDto>): MenuItem[] => providers.flatMap((p) => p(ids, nodes));
