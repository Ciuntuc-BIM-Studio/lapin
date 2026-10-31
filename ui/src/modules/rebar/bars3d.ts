/**
 * The Rebar 2D tab's button for the bars in 3D (docs/plan-rebar.md, decision
 * 221): the same setting as View → Bars in 3D; switched on from a 2D view, it
 * goes to the 3D view, where the tubes are.
 */

import { Cable } from 'lucide-react';
import type { ToolDef } from '../../platform/module';
import { useUi } from '../../app/store';

export const bars3d: ToolDef = {
  id: 'rebar.bars3d', label: 'Bars in 3D', icon: Cable,
  run: () => {
    const s = useUi.getState();
    if (!s.rebar3d && s.view !== '3d') useUi.setState({ view: '3d' });
    useUi.setState({ rebar3d: !s.rebar3d });
  },
};
