/** Deleting a type from its dialog: the engine refuses while elements use it (its reason lands in the session's error). */

import { execute, useSession } from '../../engine/session';

/** Delete a type (or any node); true when the engine took it. `element.delete` answers null either way, so success is no error. */
export async function deleteType(id: string): Promise<boolean> {
  await execute('element.delete', { id });
  return useSession.getState().error == null;
}
