import { describe, expect, it } from 'vitest';
import { baselineBarOf, barOf, dayOf, doneBar, isLate, markOf, parsePercent, percentText, varianceText, type TaskDto } from './gantt';
import { comparedOf, counts, DEFAULT_LOOK, legend, lookKit, looksAt, prepare, shownOf, type TimelineDto } from './simulation';

describe('progress in the Gantt', () => {
  it('fills the part of a bar that is done, from its start', () => {
    const bar = barOf({ start: '2027-03-01', finish: '2027-03-10', milestone: false }, 0, dayOf('2027-03-01'), 10, 26);
    expect(bar.w).toBe(100);
    expect(doneBar(bar, 60)).toMatchObject({ x: 0, w: 60 });
    expect(doneBar(bar, 0)).toBeNull();
    expect(doneBar(bar, 150)!.w).toBe(100);
  });

  it('draws the baseline as a thin bar under the task, a milestone as a point', () => {
    const own = barOf({ start: '2027-03-03', finish: '2027-03-12', milestone: false }, 2, dayOf('2027-03-01'), 10, 26);
    const b = baselineBarOf({ baselineStart: '2027-03-01', baselineFinish: '2027-03-10', baselineDuration: 8, milestone: false }, 2, dayOf('2027-03-01'), 10, 26)!;
    expect(b).toMatchObject({ x: 0, w: 100, h: 3 });
    expect(b.y).toBe(own.y + own.h + 1);
    expect(baselineBarOf({ baselineStart: '2027-03-01', baselineFinish: '2027-03-01', baselineDuration: 0, milestone: true }, 0, dayOf('2027-03-01'), 10, 26)!.w).toBe(0);
    expect(baselineBarOf({ milestone: false }, 0, 0, 10, 26)).toBeNull();
  });

  it('writes variances and percentages, and reads a percentage typed', () => {
    expect(varianceText(2)).toBe('+2 d');
    expect(varianceText(-1)).toBe('−1 d');
    expect(varianceText(0)).toBe('0 d');
    expect(varianceText(undefined)).toBe('');
    expect(percentText(33.333)).toBe('33.3 %');
    expect(percentText(100)).toBe('100 %');
    expect(percentText(undefined)).toBe('0 %');
    expect(parsePercent('60')).toBe(60);
    expect(parsePercent(' 60,5 % ')).toBe(60.5);
    expect(parsePercent('120')).toBeNull();
    expect(parsePercent('a lot')).toBeNull();
    expect(parsePercent('')).toBeNull();
    expect(isLate({ finishVariance: 2 })).toBe(true);
    expect(isLate({ finishVariance: 0 })).toBe(false);
  });

  it("says an element's marks across its tasks: done in all, started, or none", () => {
    const tasks: Pick<TaskDto, 'summary' | 'elements' | 'marks'>[] = [
      { summary: true, elements: ['w1', 'w2'] },
      { summary: false, elements: ['w1', 'w2'], marks: { w1: { started: '2027-03-01', done: '2027-03-03' } } },
      { summary: false, elements: ['w1'], marks: { w1: { started: '2027-03-04' } } },
    ];
    expect(markOf(tasks, 'w1')).toBe('started');
    expect(markOf(tasks, 'w2')).toBe('none');
    tasks[2].marks = { w1: { started: '2027-03-04', done: '2027-03-05' } };
    expect(markOf(tasks, 'w1')).toBe('done');
    expect(markOf(tasks, 'w9')).toBe('none');
  });
});

describe('the simulation against the baseline', () => {
  const T: TimelineDto = {
    start: '2027-03-01', finish: '2027-03-19', dataDate: '2027-03-05', look: { ...DEFAULT_LOOK },
    elements: [
      // Built 1–16 March, its baseline 1–12: late after the 12th.
      { id: 'late', build: ['2027-03-01', '2027-03-16'], base: ['2027-03-01', '2027-03-12'] },
      // Built 8–12 March, its baseline 15–19: ahead.
      { id: 'early', build: ['2027-03-08', '2027-03-12'], base: ['2027-03-15', '2027-03-19'] },
      // Not started on the 17th, its baseline 15–19: late.
      { id: 'notyet', build: ['2027-03-18', '2027-03-19'], base: ['2027-03-15', '2027-03-19'] },
      { id: 'plain', build: ['2027-03-01', '2027-03-05'] },
    ],
    tasks: [], stages: { chapters: [], months: [] },
  };
  const t = prepare(T);
  const el = (id: string) => t.elements.find((e) => e.id === id)!;

  it('is late when the baseline had it built or started, ahead when under way before the baseline had it start', () => {
    expect(t.baseline).toBe(true);
    expect(comparedOf(el('late'), dayOf('2027-03-10'))).toBe('active');
    expect(comparedOf(el('late'), dayOf('2027-03-15'))).toBe('late');
    expect(comparedOf(el('late'), dayOf('2027-03-17'))).toBe('done');
    expect(comparedOf(el('early'), dayOf('2027-03-09'))).toBe('ahead');
    expect(comparedOf(el('early'), dayOf('2027-03-16'))).toBe('done');
    expect(comparedOf(el('notyet'), dayOf('2027-03-17'))).toBe('late');
    expect(comparedOf(el('plain'), dayOf('2027-03-03'))).toBe('active');
    expect(shownOf(el('early'), dayOf('2027-03-09'), { compare: false })).toBe('active');
  });

  it('colours late and ahead, counts them, and names them in the legend only with a baseline', () => {
    const kit = lookKit(t.look);
    const { looks } = looksAt(t, dayOf('2027-03-09'), kit);
    expect(looks.early).toEqual({ color: DEFAULT_LOOK.ahead });
    expect(looksAt(t, dayOf('2027-03-15'), kit).looks.late).toEqual({ color: DEFAULT_LOOK.late });
    expect(counts(t, dayOf('2027-03-17'))).toMatchObject({ late: 1, done: 3 });
    expect(legend(DEFAULT_LOOK, true).map((x) => x.state)).toEqual(['active', 'demolishing', 'temporary', 'late', 'ahead']);
    expect(legend(DEFAULT_LOOK).map((x) => x.state)).toEqual(['active', 'demolishing', 'temporary']);
    expect(legend({ ...DEFAULT_LOOK, compare: false }, true).map((x) => x.state)).toEqual(['active', 'demolishing', 'temporary']);
  });
});
