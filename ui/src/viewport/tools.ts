/** Running a tool's completion: on the active storey, through the engine. */

import type { ToolDef, ToolInput } from '../platform/module';
import { ensureStorey, execute, useSession } from '../engine/session';
import { useUi } from '../app/store';

export async function completeTool(tool: ToolDef, input: ToolInput): Promise<void> {
  if (!tool.complete) return;
  const storeyId = await ensureStorey();
  if (!storeyId) return;
  const ui = useUi.getState();
  const own = ui.planViewId && useSession.getState().nodes.get(ui.planViewId)?.props.storeyId === storeyId ? ui.planViewId : null;
  const viewId = ui.view === 'drawing' && ui.drawingViewId ? ui.drawingViewId : own ?? `plan:${storeyId}`;
  await tool.complete(input, { storeyId, viewId, execute });
}
