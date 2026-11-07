/**
 * Components dropped on the 3D view (docs/plan-3d-modelling.md, stage C;
 * testers' report #105): a catalog card or a ribbon tool dragged onto the view
 * starts its tool with that type; a tool placed by one click (furniture, a
 * column, a pad, an elevator, a plant…) is placed at once where it was
 * dropped, snapped to the model, on the storey of the floor it was dropped on.
 */

import { useEffect, useRef } from 'react';
import { useUi } from '../app/store';
import { TOOL_DRAG } from '../app/hotbar';
import { activeStorey } from '../engine/session';
import { num } from '../engine/model';
import { activate, quickSlot } from '../platform/modules';
import { completeTool } from './tools';
import { snapAt, type ViewRefs } from './scene3d';
import { storeyOf } from './Place3D';

export function Drop3D({ camera, canvas, scene }: ViewRefs) {
  const refs = useRef<ViewRefs>({ camera, canvas, scene });
  refs.current = { camera, canvas, scene };
  useEffect(() => {
    const el = canvas.current;
    if (!el) return;
    const over = (e: DragEvent) => {
      if (!e.dataTransfer?.types.includes(TOOL_DRAG)) return;
      e.preventDefault();
      e.dataTransfer.dropEffect = 'copy';
    };
    const drop = (e: DragEvent) => {
      const slot = e.dataTransfer?.getData(TOOL_DRAG);
      if (!slot) return;
      e.preventDefault();
      const q = quickSlot(slot);
      if (!q) return;
      if (q.typeId) q.tool.typed?.set(q.typeId);
      if (q.tool.gesture) useUi.setState({ tool: q.tool.id }); else activate(q.tool);
      if (q.tool.gesture?.kind !== 'point') return;
      const at = snapAt(refs.current, e.clientX, e.clientY, { level: num(activeStorey() ?? undefined, 'elevation') });
      if (!at) return;
      const on = at.kind !== 'On level' ? storeyOf(at.point.z) : null;
      void completeTool(q.tool, { points: [{ x: at.point.x, y: at.point.y }] }, undefined, on);
    };
    el.addEventListener('dragover', over);
    el.addEventListener('drop', drop);
    return () => { el.removeEventListener('dragover', over); el.removeEventListener('drop', drop); };
  }, [canvas]);
  return null;
}
