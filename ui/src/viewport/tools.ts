/** Running a tool's completion: on the active storey, through the engine. */

import type { ToolDef, ToolInput } from '../platform/module';
import { ensureStorey, execute, useSession } from '../engine/session';
import { useUi } from '../app/store';

/**
 * <paramref name="onView"/>: the view the tool draws on instead of the one shown (a view inset in another: what is drawn on the
 * inset goes on its source). <paramref name="onStorey"/>: the storey it puts the element on instead of the active one (in 3D, a
 * piece of furniture set on the floor of the storey above: docs/plan-3d-modelling.md, stage C).
 */
export async function completeTool(tool: ToolDef, input: ToolInput, onView?: string, onStorey?: string | null): Promise<void> {
  if (!tool.complete) return;
  const storeyId = onStorey && useSession.getState().nodes.get(onStorey) ? onStorey : await ensureStorey();
  if (!storeyId) return;
  const ui = useUi.getState();
  const own = ui.planViewId && useSession.getState().nodes.get(ui.planViewId)?.props.storeyId === storeyId ? ui.planViewId : null;
  const viewId = onView ?? (ui.view === 'drawing' && ui.drawingViewId ? ui.drawingViewId : own ?? `plan:${storeyId}`);
  await tool.complete(input, { storeyId, viewId, execute });
}
