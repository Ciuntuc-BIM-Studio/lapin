import { describe, expect, it } from 'vitest';
import {
  arrowPoints, barOf, dateOf, dayOf, dragDays, endAt, fitScale, linkLabel, linkType, movedFinish, movedStart, parseDate, rowWindow, showDate,
  span, ticks, tierOf, visibleRows, weekday, workingDaysBetween, workingTest, type CalendarDto, type TaskDto,
} from './gantt';

const task = (id: string, parent: string | null, start: string, finish: string, extra: Partial<TaskDto> = {}): TaskDto => ({
  id, row: 0, name: id, wbs: '', level: 0, parent, summary: false, milestone: false, duration: 1, start, finish, lateStart: start, lateFinish: finish,
  totalFloat: 0, freeFloat: 0, critical: false, predecessors: '', ...extra,
});

const cal: CalendarDto = {
  id: 'c', name: 'Standard', days: [1, 2, 3, 4, 5], dayStart: '08:00', hours: 8, holidays: 'ro',
  exceptions: [{ from: '2026-11-21', to: '2026-11-21', working: true, name: 'Saturday worked' }], winter: { on: true, from: '12-15', to: '03-01' },
};

describe('the Gantt\'s dates', () => {
  it('count days from 1970 and know their weekday', () => {
    expect(dayOf('1970-01-01')).toBe(0);
    expect(dateOf(dayOf('2026-11-09'))).toBe('2026-11-09');
    expect(weekday(dayOf('2026-11-09'))).toBe(1);   // a Monday
    expect(weekday(dayOf('2026-11-15'))).toBe(7);
  });

  it('are shown day first and read as typed', () => {
    expect(showDate('2026-11-09')).toBe('09.11.2026');
    expect(parseDate('9.11.2026')).toBe('2026-11-09');
    expect(parseDate('09/11/26')).toBe('2026-11-09');
    expect(parseDate('2026-11-09')).toBe('2026-11-09');
    expect(parseDate('31.02.2026')).toBeNull();
    expect(parseDate('soon')).toBeNull();
  });

  it('count working days as the engine does: holidays it listed, a Saturday worked, a wet task\'s winter break', () => {
    const works = workingTest(cal, [{ date: '2026-11-30' }, { date: '2026-12-01' }]);
    // Thursday 26 November to Friday 4 December: Thu, Fri, Wed, Thu, Fri.
    expect(workingDaysBetween(works, dayOf('2026-11-26'), dayOf('2026-12-04'))).toBe(5);
    expect(works(dayOf('2026-11-21'))).toBe(true);
    expect(works(dayOf('2027-01-12'))).toBe(true);
    expect(works(dayOf('2027-01-12'), true)).toBe(false);
    expect(works(dayOf('2027-03-01'), true)).toBe(true);
  });
});

describe('the time scale', () => {
  it('chooses its header by its pixels a day', () => {
    expect(tierOf(28)).toBe('day');
    expect(tierOf(10)).toBe('week');
    expect(tierOf(3)).toBe('month');
    expect(tierOf(1)).toBe('quarter');
  });

  it('writes days under weeks starting on Mondays, months under years', () => {
    const from = dayOf('2026-11-09'), to = dayOf('2026-11-23');
    const d = ticks(28, from, to);
    expect(d.bottom).toHaveLength(14);
    expect(d.bottom[0]).toEqual({ from, to: from + 1, label: 'M9' });
    expect(d.top[0].label).toBe('9 Nov');
    expect(d.top.map((t) => weekday(t.from))).toEqual([1, 1]);
    const m = ticks(3, dayOf('2026-11-15'), dayOf('2027-02-10'));
    expect(m.bottom.map((t) => t.label)).toEqual(['Nov', 'Dec', 'Jan', 'Feb']);
    expect(m.top.map((t) => t.label)).toEqual(['2026', '2027']);
    expect(m.bottom[1].to - m.bottom[1].from).toBe(31);
    const q = ticks(1, dayOf('2027-01-01'), dayOf('2027-12-31'));
    expect(q.bottom.map((t) => t.label)).toEqual(['Q1', 'Q2', 'Q3', 'Q4']);
    expect(dateOf(q.bottom[3].to)).toBe('2028-01-01');
  });

  it('spans the schedule with today, from a Monday, and fits it in a width', () => {
    const s = span({ start: '2026-11-11', finish: '2027-03-01' }, dayOf('2026-11-04'));
    expect(weekday(s.from)).toBe(1);
    expect(s.from).toBeLessThanOrEqual(dayOf('2026-11-04') - 7);
    expect(s.to).toBe(dayOf('2027-03-01') + 45);
    expect(fitScale({ start: '2026-11-09', finish: '2026-11-22' }, 2700)).toBe(60);
    expect(fitScale({ start: '2026-01-01', finish: '2026-12-31' }, 1000)).toBeCloseTo(1000 / 378, 6);
  });
});

describe('the rows and bars', () => {
  it('hide what is under a collapsed summary, at any depth', () => {
    const tasks = [task('a', null, '2026-11-09', '2026-11-09'), task('b', 'a', '2026-11-09', '2026-11-09'), task('c', 'b', '2026-11-09', '2026-11-09'), task('d', null, '2026-11-09', '2026-11-09')];
    expect(visibleRows(tasks, new Set(['a'])).map((t) => t.id)).toEqual(['a', 'd']);
    expect(visibleRows(tasks, new Set(['b'])).map((t) => t.id)).toEqual(['a', 'b', 'd']);
    expect(visibleRows(tasks, new Set()).length).toBe(4);
  });

  it('stand from the start of the first day to the end of the last, a milestone a point', () => {
    const origin = dayOf('2026-11-09');
    expect(barOf(task('a', null, '2026-11-10', '2026-11-12'), 2, origin, 10, 24)).toEqual({ x: 10, w: 30, y: 54, h: 12 });
    expect(barOf(task('m', null, '2026-11-12', '2026-11-12', { milestone: true }), 0, origin, 10, 24).w).toBe(0);
  });

  it('move by whole days when dragged, an end never before the start', () => {
    expect(dragDays(26, 10)).toBe(3);
    expect(dragDays(-14, 10)).toBe(-1);
    const t = task('a', null, '2026-11-10', '2026-11-12');
    expect(movedStart(t, 3)).toBe('2026-11-13');
    expect(movedFinish(t, 2)).toBe('2026-11-14');
    expect(movedFinish(t, -9)).toBe('2026-11-10');
  });

  it('link by the ends they are dragged between', () => {
    const bar = { x: 100, w: 60, y: 0, h: 12 };
    expect(endAt(bar, 110)).toBe('start');
    expect(endAt(bar, 150)).toBe('finish');
    expect(linkType('finish', 'start')).toBe('FS');
    expect(linkType('start', 'start')).toBe('SS');
    expect(linkType('finish', 'finish')).toBe('FF');
    expect(linkType('start', 'finish')).toBe('SF');
    expect(linkLabel({ type: 'SS', lag: 2 })).toBe('SS+2d');
    expect(linkLabel({ type: 'FS', lag: -1 })).toBe('FS−1d');
  });

  it('draw an arrow straight down when there is room, elbowed between the rows when there is not', () => {
    const a = { x: 0, w: 50, y: 6, h: 12 }, b = { x: 80, w: 40, y: 30, h: 12 };
    expect(arrowPoints(a, b, 'FS', 24)).toEqual([[50, 12], [58, 12], [58, 36], [80, 36]]);
    const c = { x: 20, w: 40, y: 30, h: 12 };
    expect(arrowPoints(a, c, 'FS', 24)).toEqual([[50, 12], [58, 12], [58, 24], [12, 24], [12, 36], [20, 36]]);
    // Start to start: out to the left of both.
    expect(arrowPoints(c, a, 'SS', 24)[1]).toEqual([12, 36]);
    // Finish to finish: into the successor's end from its right.
    expect(arrowPoints(a, b, 'FF', 24).at(-1)).toEqual([120, 36]);
  });

  it('render only the rows in view, with a few spare', () => {
    expect(rowWindow(0, 240, 24, 5000)).toEqual([0, 16]);
    expect(rowWindow(24 * 1000, 240, 24, 5000)).toEqual([994, 1016]);
    expect(rowWindow(24 * 4995, 240, 24, 5000)).toEqual([4989, 4999]);
  });
});
