import { describe, expect, it } from 'vitest';
import { axisOf, chartOf, isCurve, niceStep, shortMoney, valueAt, type OverTimeDto } from './costTime';

/** The house of the engine's CostTimeTests: masonry 26.04–04.05.2027 (over Good Friday and Easter Monday), plaster to 18.05. */
function house(): OverTimeDto {
  const planned: number[] = [];
  const days = ['2027-04-26', '2027-04-27', '2027-04-28', '2027-04-29', '2027-05-04'];
  let run = 0;
  for (let d = 0; d < 23; d++) {
    const iso = new Date(Date.UTC(2027, 3, 26 + d)).toISOString().slice(0, 10);
    if (days.includes(iso)) run += 727.5;
    else if (iso >= '2027-05-05' && iso <= '2027-05-18' && ![0, 6].includes(new Date(iso).getUTCDay())) run += 252;
    planned.push(run);
  }
  return {
    currency: 'RON', budget: 6157.5, scheduled: 6157.5, unscheduled: 0, toDate: 2182.5, net: 7162.77, total: 8666.95,
    schedule: 'Casa', start: '2027-04-26', finish: '2027-05-18', dataDate: '2027-04-28', doneThrough: '2027-04-28', from: '2027-04-26', to: '2027-05-18',
    planned, baseline: null, done: [727.5, 1455, 2182.5],
    months: [
      { month: '2027-04', planned: 2910, done: 2182.5, plannedCum: 2910, doneCum: 2182.5 },
      { month: '2027-05', planned: 3247.5, done: 0, plannedCum: 6157.5, doneCum: 2182.5 },
    ],
    chapters: [], tasks: [],
  };
}

describe('the S-curve chart', () => {
  it('steps its scales by 1, 2 or 5 times a power of ten and tops them over the largest value', () => {
    expect(niceStep(6157.5)).toBe(2000);
    expect(niceStep(950)).toBe(200);
    expect(niceStep(0)).toBe(1);
    const a = axisOf(6157.5);
    expect(a.top).toBe(8000);
    expect(a.values).toEqual([0, 2000, 4000, 6000, 8000]);
    expect(axisOf(4000).top).toBe(4000);
  });

  it('writes sums on its axes in thousands and millions', () => {
    expect(shortMoney(2000)).toBe('2 k');
    expect(shortMoney(1_250_000)).toBe('1.25 M');
    expect(shortMoney(500)).toBe('500');
  });

  it('lays the days across the plot and the cumulative cost up it, from nothing at the first day', () => {
    const d = house();
    const c = chartOf(d, { x0: 0, x1: 230, y0: 0, y1: 100 });
    // 23 days on 230 units: 10 a day; the scale tops at 8000, so 6157.50 stands at 23 % from the top.
    expect(c.x(c.x(0) * 0 + Math.round(Date.UTC(2027, 3, 26) / 86_400_000))).toBe(0);
    expect(c.cum.top).toBe(8000);
    expect(c.planned.startsWith('M0.0,100.0L10.0,')).toBe(true);
    expect(c.planned.endsWith(`L230.0,${(100 - 6157.5 / 80).toFixed(1)}`)).toBe(true);
    // The work done stops at the status date; its line drawn there.
    expect(c.done.split('L')).toHaveLength(4);
    expect(c.status).toBe(30);
    // April's bar from its 26th to its end (5 days), May's over 18 days; planned left of the middle, done right of it.
    expect(c.bars.map((b) => b.month)).toEqual(['2027-04', '2027-05']);
    expect(c.bars[0].mid).toBe(25);
    expect(c.bars[0].planned.x + c.bars[0].planned.w).toBe(c.bars[0].mid);
    expect(c.bars[0].done.x).toBe(c.bars[0].mid);
    expect(c.bars[1].planned.h).toBeGreaterThan(c.bars[0].planned.h);
  });

  it('reads a curve at a day: holidays stand still, before it nothing, after it its last value', () => {
    const d = house();
    expect(valueAt(d, d.planned, '2027-04-29')).toBe(2910);
    expect(valueAt(d, d.planned, '2027-05-03')).toBe(2910);
    expect(valueAt(d, d.planned, '2027-05-04')).toBe(3637.5);
    expect(valueAt(d, d.planned, '2027-04-01')).toBe(0);
    expect(valueAt(d, d.done, '2027-06-01')).toBe(2182.5);
  });

  it('tells a curve from the engine saying why there is none', () => {
    expect(isCurve(house())).toBe(true);
    expect(isCurve({ why: 'No work schedule yet' })).toBe(false);
    expect(isCurve(null)).toBe(false);
  });
});
