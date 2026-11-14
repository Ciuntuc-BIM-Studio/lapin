/**
 * "Edit layers" (forum topic 89, stage 2): what keeps the edited element's layers' edges in step with the model (the watcher,
 * asking the engine again as the model changes) and their outlines drawn over plans and sections; the grips themselves are
 * the element's grips while the mode is on (layerEdit.ts).
 */

import { useEffect } from 'react';
import { useUi } from '../../app/store';
import { query, useSession } from '../../engine/session';
import type { DrawingOverlayProps, PlanOverlayProps, Vec2 } from '../../platform/module';
import { useScope } from '../../platform/scope';
import { bumpGrips } from '../../viewport/grips';
import { LAYER_HOSTS, LAYERS, outlineInView, useLayerEdit, type LayerEdges, type ViewOf } from './layerEdit';

/** Mounted while the workspace is open: the edges of the one wall or floor selected in the mode, told again as the model changes. */
export function LayerEditWatcher() {
  const on = useScope((s) => s.scope?.kind === LAYERS);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const id = on && selected.length === 1 && LAYER_HOSTS.has(nodes.get(selected[0])?.type ?? '') ? selected[0] : null;
  useEffect(() => {
    if (!id) { if (useLayerEdit.getState().id) { useLayerEdit.setState({ id: null, data: null }); bumpGrips(); } return; }
    let live = true;
    const t = setTimeout(() => {
      void query<LayerEdges>('architecture.layerEdges', { id }).then((data) => {
        if (!live) return;
        useLayerEdit.setState({ id, data: data ?? null });
        bumpGrips();
      });
    }, 60);
    return () => { live = false; clearTimeout(t); };
  }, [id, nodes]);
  return null;
}

function Outlines({ toScreen, view }: { toScreen: (p: Vec2) => { x: number; y: number }; view: ViewOf }) {
  const on = useScope((s) => s.scope?.kind === LAYERS);
  const { data } = useLayerEdit();
  if (!on || !data) return null;
  return (
    <g className="layer-edit" data-testid="layer-edit-outlines" pointerEvents="none">
      {data.layers.flatMap((l) => outlineInView(l, view).map((ring, i) => {
        const d = `M${ring.map((p) => { const q = toScreen(p); return `${q.x.toFixed(1)},${q.y.toFixed(1)}`; }).join('L')}Z`;
        return <path key={`${l.layer}:${i}`} d={d} className="ref-line outline" data-layer={l.layer} style={{ fill: 'none', strokeDasharray: '4 3' }} />;
      }))}
    </g>
  );
}

/** In a plan: the edited element's layers' footprints, on its storey's plans. */
export function LayerEditPlan({ toScreen, view }: PlanOverlayProps) {
  const id = useLayerEdit((s) => s.id);
  const storey = useSession((s) => (id ? s.nodes.get(id)?.storeyId : undefined));
  if (view.storeyId && storey && storey !== view.storeyId) return null;
  return <Outlines toScreen={toScreen} view={{}} />;
}

/** In a section or an elevation: the layers where the cut runs through them, or as seen. */
export function LayerEditDrawing({ toScreen, drawing }: DrawingOverlayProps) {
  if (!drawing?.frame) return null;
  return <Outlines toScreen={toScreen} view={{ frame: drawing.frame, kind: drawing.kind, developed: drawing.developed }} />;
}
