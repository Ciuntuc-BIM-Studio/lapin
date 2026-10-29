/** The assistant's gate (gate.ts) with this app's own facts: the desktop check the app uses everywhere, and the session's guest state. */

import { isDesktop } from '../engine/files';
import { isGuest } from '../engine/share';
import { ASSISTANT_BUILD, devOverride, gateOpen } from './gate';

export function assistantAvailable(): boolean {
  if (!ASSISTANT_BUILD || typeof window === 'undefined') return false;
  return gateOpen({ build: ASSISTANT_BUILD, desktop: isDesktop(), guest: isGuest(), devOverride: devOverride() });
}
