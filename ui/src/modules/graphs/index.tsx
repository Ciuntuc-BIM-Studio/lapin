/**
 * Visual graphs (docs/plan-visual-graphs.md, decision 525): the open module `graphs`, in the free edition and on the web.
 * Its engine half (Lapin.Graphs) keeps the project's graphs, evaluates them and bakes them; this half is the Graphs panel,
 * a view of its own opened beside the model's (View → Graphs: split on the web; on the desktop its tab goes into a
 * window of its own, to float or to put on another screen), and the ghost of what the open graph would make in every 3D
 * view and plan.
 */

import { Workflow } from 'lucide-react';
import { useEffect } from 'react';
import type { LapinModule } from '../../platform/module';
import { GraphView } from './GraphView';
import { GraphGhost3D, GraphGhostPlan } from './Ghost';
import { listenForGhosts, openPanel } from './store';

function GraphsBackground() {
  useEffect(() => { listenForGhosts(); }, []);
  return null;
}

const graphs: LapinModule = {
  id: 'graphs',
  name: 'Graphs',
  summary: 'Visual graphs: nodes wired into a recipe (numbers, lists, points, the tools\' commands), evaluated by the engine, previewed as a ghost and baked in one undo step.',
  tabs: [{
    id: 'view', label: 'View', order: 90,
    groups: [{ label: 'Automation', tools: [{ id: 'graphs.panel', label: 'Graphs', icon: Workflow, run: openPanel, tooltip: 'Visual graphs: open the Graphs panel beside the view' }] }],
  }],
  views: { graphs: { label: 'Graphs', component: GraphView } },
  overlays3d: [GraphGhost3D],
  planOverlays: [GraphGhostPlan],
  background: GraphsBackground,
};

export default graphs;
