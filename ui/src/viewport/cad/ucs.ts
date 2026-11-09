/**
 * UCS on the command line, as AutoCAD's (decision 445): the plans' user coordinate system, kept in the project
 * (Manage → Location; each change one undo step). Click its origin, then a point on its X axis (Enter keeps the
 * angle); or World (the plan's own axes), Base point (the project base point, the plan's axes), Survey (the survey
 * point, its axes east and north), Rotate (turned about its origin by an angle typed). In a UCS typed x,y and d<deg,
 * ortho, polar tracking and the grid follow its axes, and the status line can read the cursor in it.
 */

import { registerCommand, cadLog, type Step } from './session';
import { basePoint, mapFrame, setUcs, surveyPoint, ucsOf, useLocation, type Frame } from '../../app/location';

const current = (): Frame => ucsOf(useLocation.getState().loc) ?? { origin: { x: 0, y: 0 }, angle: 0 };
const done = (f: Frame | null, said: string) => { void setUcs(f); cadLog(said); return null; };
const deg = (a: number) => Math.round(a * 1e6) / 1e6;

export function ucsStart(): Step {
  return {
    prompt: 'UCS: origin [World/Base point/Survey point/Rotate]', want: 'point',
    keywords: {
      W: () => done(null, 'UCS: the plan’s own axes (World).'),
      B: () => { const b = basePoint(useLocation.getState().loc); return done({ origin: { x: b.x, y: b.y }, angle: 0 }, 'UCS at the project base point.'); },
      S: () => {
        const loc = useLocation.getState().loc;
        if (!surveyPoint(loc)) { cadLog('The project has no survey point: add one in Manage → Location.'); return null; }
        return done(mapFrame(loc), 'UCS at the survey point, its axes east and north.');
      },
      R: () => ({
        prompt: 'Rotation about the UCS origin (degrees)', want: 'none',
        scalar: (a: number) => { const c = current(); return done({ origin: c.origin, angle: deg(c.angle + a) }, `UCS turned by ${a}°.`); },
      }),
    },
    point: (o) => ({
      prompt: 'Point on the X axis (Enter: keep the angle)', want: 'point', base: o,
      preview: (p) => [{ type: 'line', a: o, b: p }],
      point: (p) => {
        if (Math.hypot(p.x - o.x, p.y - o.y) < 1e-9) return done({ origin: o, angle: current().angle }, 'UCS origin moved.');
        return done({ origin: o, angle: deg((Math.atan2(p.y - o.y, p.x - o.x) * 180) / Math.PI) }, 'UCS set.');
      },
      enter: () => done({ origin: o, angle: current().angle }, 'UCS origin moved.'),
    }),
  };
}

registerCommand({ names: ['UCS'], label: 'UCS', start: ucsStart });
