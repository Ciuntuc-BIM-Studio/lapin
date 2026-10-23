/** Running a tool's completion: on the active storey, through the engine. */

import type { ToolDef, ToolInput } from '../platform/module';
import { ensureStorey, execute } from '../engine/session';

export async function completeTool(tool: ToolDef, input: ToolInput): Promise<void> {
  if (!tool.complete) return;
  const storeyId = await ensureStorey();
  if (!storeyId) return;
  await tool.complete(input, { storeyId, execute });
}
