import { describe, expect, it } from 'vitest';
import { dayOf } from './gantt';
import {
  axisRows, clickPick, distanceTo, dragArgs, fitLbs, geomOf, handleAt, hit, labelOf, movedLabel, noteAt, pickFromSelection, picked, rowAt, toggleFold, turnedLabel, wheelZoom, xOf, yOf,
  type LbsLocation, type LbsSegment, type Scale,
} from './lbs';

const S: Scale = { origin: dayOf('2027-03-01'), ppd: 10, ppm: 20, height: 9 };

const seg = (id: string, task: string, group: string, start: string, finish: string, y0: number, y1: number, rep: LbsSegment['rep'] = 'line'): LbsSegment => ({
  id, task, name: task, group, colour: '#4e79a7', from: 'a', to: 'a', locations: ['a'], row0: 0, row1: 0, y0, y1, start, finish, rep,
  critical: false, status: 'planned', complete: 0, count: 1, totalFloat: 0, duration: 5,
});

const segs = [
  seg('s1@gf', 's1', 'Structure', '2027-03-01', '2027-03-05', 0, 3),
  seg('s2@ff', 's2', 'Structure', '2027-03-06', '2027-03-10', 3, 6),
  seg('m@gf', 'm', 'Masonry', '2027-03-11', '2027-03-20', 0, 9, 'block'),
];

describe('the location-based schedule', () => {
  it('lays time across and the locations up, the foot at the bottom', () => {
    expect(xOf(S, dayOf('2027-03-03'))).toBe(20);
    expect(yOf(S, 0)).toBe(180);
    expect(yOf(S, 9)).toBe(0);
    // A line from its start at its location's foot to the end of its finish day at its top.
    expect(geomOf(segs[0], S)).toEqual({ kind: 'line', x0: 0, y0: 180, x1: 50, y1: 120 });
    expect(geomOf(segs[2], S)).toEqual({ kind: 'block', x0: 100, y0: 180, x1: 200, y1: 0 });
    expect(geomOf({ ...segs[0], rep: 'milestone' }, S).x1).toBe(0);
  });

  it('hits the nearest segment, a line before the block it crosses', () => {
    expect(distanceTo(geomOf(segs[0], S), 25, 150)).toBeCloseTo(0, 6);
    expect(hit(segs, S, 25, 152)?.id).toBe('s1@gf');
    expect(hit(segs, S, 150, 60)?.id).toBe('m@gf');
    expect(hit(segs, S, 80, 10)).toBeNull();
    const crossing = [...segs, seg('p@all', 'p', 'Plaster', '2027-03-11', '2027-03-20', 0, 9)];
    expect(hit(crossing, S, 150, 90)?.id).toBe('p@all');
  });

  it('drags the bottom handle to a start no earlier than, the top one to a finish', () => {
    const g = geomOf(segs[0], S);
    expect(handleAt(g, 1, 179)).toBe('start');
    expect(handleAt(g, 49, 121)).toBe('finish');
    expect(handleAt(g, 25, 150)).toBeNull();
    expect(dragArgs(segs[0], 'start', 31, S.ppd)).toEqual({ id: 's1', start: '2027-03-04' });
    expect(dragArgs(segs[0], 'finish', -20, S.ppd)).toEqual({ id: 's1', finish: '2027-03-03' });
    expect(dragArgs(segs[0], 'finish', -200, S.ppd)).toEqual({ id: 's1', finish: '2027-03-01' });
    expect(dragArgs(segs[0], 'start', 3, S.ppd)).toBeNull();
  });

  it('selects a segment on the first click and its task across all its locations on the second', () => {
    const one = clickPick(null, segs[0], segs);
    expect(one).toEqual({ segment: 's1@gf', whole: false, tasks: ['s1'] });
    expect([...picked(one, segs)]).toEqual(['s1@gf']);
    const all = clickPick(one, segs[0], segs);
    expect(all).toEqual({ segment: 's1@gf', whole: true, tasks: ['s1', 's2'] });
    expect([...picked(all, segs)].sort()).toEqual(['s1@gf', 's2@ff']);
    // Again: back to the one segment; another segment, or none, starts again.
    expect(clickPick(all, segs[0], segs)?.whole).toBe(false);
    expect(clickPick(one, segs[1], segs)).toEqual({ segment: 's2@ff', whole: false, tasks: ['s2'] });
    expect(clickPick(one, null, segs)).toBeNull();
  });

  it('follows the selection made elsewhere (the Gantt, the 3D)', () => {
    const all = clickPick(clickPick(null, segs[0], segs), segs[0], segs);
    expect(pickFromSelection(all, ['s2', 's1'], segs)).toBe(all);
    expect(pickFromSelection(all, ['m'], segs)).toEqual({ segment: 'm@gf', whole: false, tasks: ['m'] });
    expect(pickFromSelection(all, ['wall-1'], segs)).toBeNull();
  });

  it('puts a label along its line, kept moved and turned in days and metres', () => {
    const g = geomOf(segs[0], S);
    const l = labelOf(g, S);
    expect(l.x).toBe(25);
    expect(l.y).toBe(147);
    expect(l.angle).toBeCloseTo((Math.atan2(60, 50) * 180) / Math.PI, 6);
    const kept = movedLabel(undefined, g, S, 20, -40);
    expect(kept.dx).toBe(2);
    expect(kept.dy).toBe(2);
    const moved = labelOf(g, S, kept);
    expect([moved.x, moved.y]).toEqual([45, 107]);
    // At another zoom it stays at the same day and height.
    const z = { ...S, ppd: 20 };
    expect(labelOf(geomOf(segs[0], z), z, kept).x).toBe(90);
    expect(turnedLabel(kept, 50, 40).angle).toBeCloseTo(kept.angle - 20, 6);
    expect(turnedLabel(kept, 50, -1000).angle).toBe(90);
  });

  it('zooms the time with Ctrl and the locations with Shift about the pointer; fits the whole chart', () => {
    const z = wheelZoom(S, { deltaY: -100, ctrl: true, shift: false }, 100, 0, { left: 50, top: 0 })!;
    expect(z.scale.ppd).toBeCloseTo(10 * Math.exp(0.2), 6);
    expect((z.scroll.left + 100) / z.scale.ppd).toBeCloseTo(15, 6);
    const v = wheelZoom(S, { deltaY: 100, ctrl: false, shift: true }, 0, 60, { left: 0, top: 30 })!;
    expect(v.scale.ppm).toBeCloseTo(20 * Math.exp(-0.2), 6);
    expect(v.scale.ppd).toBe(10);
    expect(wheelZoom(S, { deltaY: 100, ctrl: false, shift: false }, 0, 0, { left: 0, top: 0 })).toBeNull();
    const fit = fitLbs({ start: '2027-03-01', finish: '2027-03-31', height: 9.5 }, 450, 202);
    expect(fit.origin).toBe(dayOf('2027-02-22'));
    expect(fit.ppd).toBeCloseTo(450 / 44, 6);
    expect(fit.ppm).toBeCloseTo(20, 6);
  });

  it('lists the axis top first with its levels, folds, places notes and finds the row at a height', () => {
    const locs: LbsLocation[] = [
      { id: 'b', name: 'Building', level: 'building', parent: null, at: 0, extent: 9, row: -1, children: ['gf', 'ff'] },
      { id: 'gf', name: 'Ground floor', level: 'storey', parent: 'b', at: 0, extent: 3, row: 0, elevation: 0 },
      { id: 'ff', name: 'First floor', level: 'storey', parent: 'b', at: 3, extent: 6, row: 1, elevation: 3 },
    ];
    expect(axisRows(locs).map((r) => [r.loc.id, r.depth])).toEqual([['b', 0], ['ff', 1], ['gf', 1]]);
    expect(axisRows([{ ...locs[0], folded: true, row: 0 }, ...locs.slice(1)]).map((r) => r.loc.id)).toEqual(['b']);
    expect(toggleFold(['x'], 'b')).toEqual(['x', 'b']);
    expect(toggleFold(['x', 'b'], 'b')).toEqual(['x']);
    expect(noteAt({ id: 'n', date: '2027-03-03', location: 'ff', text: 'Crane' }, locs, S)).toEqual({ x: 20, y: 60 });
    expect(noteAt({ id: 'n', date: '2027-03-03', location: 'gone', text: 'Crane' }, locs, S)).toBeNull();
    expect(rowAt(locs, 4)?.id).toBe('ff');
    expect(rowAt(locs, 10)).toBeNull();
  });
});
