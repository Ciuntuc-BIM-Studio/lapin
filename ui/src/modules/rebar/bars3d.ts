/**
 * The Rebar 2D tab's button for the bars in 3D (docs/plan-rebar.md, decisions
 * 221 and 401): the same as View → Bars in 3D, the 3D view's Rebar category
 * switched; switched on from a 2D view, it goes to the 3D view, where the tubes are.
 */

import { Cable } from 'lucide-react';
import type { ToolDef } from '../../platform/module';
import { useUi } from '../../app/store';
import { toggleRebar3d } from '../../viewport/rebar3d';

export const bars3d: ToolDef = {
  id: 'rebar.bars3d', label: 'Bars in 3D', icon: Cable,
  run: () => {
    void toggleRebar3d().then((shown) => {
      if (shown && useUi.getState().view !== '3d') useUi.setState({ view: '3d' });
    });
  },
};
