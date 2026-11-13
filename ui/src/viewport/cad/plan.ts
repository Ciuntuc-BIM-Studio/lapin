/**
 * PLAN on the command line, as AutoCAD's (decision 526): turns the plan open so that a direction reads horizontal, on
 * screen and on its sheets, PDF and DXF (the view's orientation, one undo step). Current UCS (Enter): the view follows
 * the UCS's X axis, as it changes; World: north up again; Angle: the model direction typed (degrees) shown horizontal;
 * Edge: two points picked, their direction horizontal; True north: the project's true north up. ALIGNVIEW starts at Edge.
 * A section or an elevation is not turned: it looks where its line or side says.
 */

import { registerCommand, cadHost, cadLog, type Step } from './session';
import { basePoint, ucsOf, useLocation } from '../../app/location';
import { alignAngle, FOLLOW_UCS, setOrientation } from '../orientation';

/** The plan the command line works on, or null (said why). */
function planId(): string | null {
  const h = cadHost();
  if (!h) return null;
  if (!h.isPlan?.()) { cadLog('Only a plan is rotated: a section or an elevation looks where its line or side says.'); return null; }
  return h.viewId;
}

const done = async (id: string, value: number | typeof FOLLOW_UCS | null, said: string) => { await setOrientation(id, value); cadLog(said); return null; };

/** Two points: their direction becomes horizontal (the nearer way round, so the plan turns by at most 90°). */
function edgeStep(id: string): Step {
  return {
    prompt: 'First point of the edge to align with', want: 'point',
    point: (a) => ({
      prompt: 'Second point of the edge', want: 'point', base: a,
      preview: (p) => [{ type: 'line', a, b: p }],
      point: (b) => {
        if (Math.hypot(b.x - a.x, b.y - a.y) < 1e-9) { cadLog('The two points are the same: pick two different points.'); return edgeStep(id); }
        return done(id, alignAngle(a, b), 'View rotated to the edge.');
      },
    }),
  };
}

export function planStart(): Step | null {
  const id = planId();
  if (!id) return null;
  const toUcs = () => {
    if (!ucsOf(useLocation.getState().loc)) { cadLog('There is no UCS: the view stays as it is (World turns it back north up).'); return null; }
    return done(id, FOLLOW_UCS, 'View follows the UCS.');
  };
  return {
    prompt: 'PLAN: [Current UCS/World/Angle/Edge/True north] <Current UCS>', want: 'none',
    keywords: {
      C: toUcs,
      U: toUcs,
      W: () => done(id, null, 'View north up (World).'),
      A: () => ({
        prompt: 'Direction to show horizontal (degrees from the X axis)', want: 'none',
        scalar: (a: number) => done(id, a, 'View rotated.'),
      }),
      E: () => edgeStep(id),
      T: () => {
        const north = basePoint(useLocation.getState().loc).northAngle;
        if (Math.abs(north) < 1e-9) { cadLog('The project is not turned from true north: the view stays north up.'); return done(id, null, 'View north up (World).'); }
        return done(id, north, 'View rotated to true north.');
      },
    },
    enter: toUcs,
  };
}

/** Rotate view to align with an edge, straight away (the plan's Rotate view menu). */
export function alignViewStart(): Step | null {
  const id = planId();
  return id ? edgeStep(id) : null;
}

registerCommand({ names: ['PLAN'], label: 'Rotate view (PLAN)', start: planStart });
registerCommand({ names: ['ALIGNVIEW'], label: 'Rotate view to an edge', start: alignViewStart });
