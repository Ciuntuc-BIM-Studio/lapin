/**
 * What a DXF holds, asked of the engine before the import dialog shows its
 * choices. Its answer or why it has none, never neither (forum topic 217: a
 * file the engine refused, or one it read for longer than the usual wait, left
 * the dialog on "Reading the file…" for good, the error dropped). A big drawing
 * may take minutes: it is waited for as long as an import is.
 */

import { LONG_TIMEOUT_MS } from '../../engine/client';
import { engine } from '../../engine/session';

/** The file as the dialog has it: by path (the desktop) or its bytes in base64 (the browser). */
export interface DxfSource { name: string; path?: string; data?: string }

/** The engine's answer, or the sentence the dialog shows instead. */
export type DxfInfoOutcome<T> = { info: T } | { problem: string; detail?: string };

export const NO_ANSWER = 'Lapin could not read this DXF file.';

export async function readDxfInfo<T>(file: DxfSource, viewId: string): Promise<DxfInfoOutcome<T>> {
  try {
    const info = await engine.call<T | null>('doc.query', { name: 'drafting.dxfInfo', args: { ...file, viewId } }, { timeoutMs: LONG_TIMEOUT_MS });
    return info ? { info } : { problem: NO_ANSWER };
  } catch (e) {
    // The engine's own sentence, shown under the general one (each translated on its own).
    const detail = e instanceof Error ? e.message : String(e);
    return detail ? { problem: NO_ANSWER, detail } : { problem: NO_ANSWER };
  }
}
