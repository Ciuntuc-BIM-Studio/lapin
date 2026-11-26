import { describe, expect, it } from 'vitest';
import { cashChartOf, isCash, periodLabel, signedAxis, type CashFlowDto } from './cashFlow';

/** The worked example of the engine's CashFlowTests, its first four months. */
function example(): CashFlowDto {
  const p = (start: string, net: number, cumulative: number) => ({ start, cost: 0, certified: 0, retention: 0, recovered: 0, in: 0, out: 0, net, cumulative, retentionHeld: 0, advanceLeft: 0 });
  return {
    estimate: 'e', currency: 'RON', period: 'month',
    settings: {
      basis: 'net', contractValue: null, costBasis: 'direct', costSide: true, supplierDays: 30, advance: 10, advanceDate: null, advanceTask: null,
      retention: 5, retentionAtTakeover: 50, defectsMonths: 12, takeoverDate: null, takeoverTask: null, cycle: 'monthly', certificateDay: 0, paymentDays: 30, vat: false, indicative: false,
    },
    contractValue: 44000, costTotal: 40000, advance: 4400, advanceDate: '2027-01-01', takeover: '2027-03-31', defectsEnd: '2028-03-31', actual: false,
    certified: 44000, retention: 2200, cashIn: 44000, cashOut: 40000, peak: -5600, peakPeriod: '2027-02-01',
    periods: [p('2027-01-01', 4400, 4400), p('2027-02-01', -10000, -5600), p('2027-03-01', 8050, 2450), p('2027-04-01', 450, 2900)],
    certificates: [], releases: [],
  };
}

describe('the cash-flow chart', () => {
  it('takes a scale over nought and under it in steps of 1, 2 or 5 times a power of ten', () => {
    expect(signedAxis(-5600, 8050)).toEqual({ lo: -10000, hi: 10000, values: [-10000, -5000, 0, 5000, 10000] });
    expect(signedAxis(-500, 1200).values).toEqual([-500, 0, 500, 1000, 1500]);
    expect(signedAxis(0, 950).lo).toBe(0);
    expect(signedAxis(0, 950).hi).toBe(1000);
  });

  it('draws a bar a period up when in and down when out, the cumulative through the periods’ ends and the peak where it is lowest', () => {
    const box = { x0: 0, x1: 400, y0: 0, y1: 160 };
    const c = cashChartOf(example(), box);
    // 20 000 over 160 px: 0.008 px a leu; nought at 160 − 10 000 × 0.008 = 80.
    expect(c.zero).toBeCloseTo(80);
    expect(c.bars).toHaveLength(4);
    expect(c.bars[0].out).toBe(false);
    expect(c.bars[0].y).toBeCloseTo(44.8);
    expect(c.bars[0].h).toBeCloseTo(35.2);
    expect(c.bars[1].out).toBe(true);
    expect(c.bars[1].y).toBeCloseTo(80);
    expect(c.bars[1].h).toBeCloseTo(80);
    expect(c.line.startsWith('M100.0,')).toBe(true);
    expect(c.peak).toEqual({ x: 200, y: c.y(-5600) });
    expect(c.y(-5600)).toBeCloseTo(124.8);
  });

  it('has no peak when the cash flow never goes under nought', () => {
    const d = { ...example(), peak: 0, peakPeriod: null };
    expect(cashChartOf(d, { x0: 0, x1: 100, y0: 0, y1: 100 }).peak).toBeNull();
  });

  it('labels months and weeks, and tells a cash flow from a reason there is none', () => {
    expect(periodLabel('2027-02-01', 'month')).toBe('02.27');
    expect(periodLabel('2027-01-04', 'week')).toBe('04.01');
    expect(isCash(example())).toBe(true);
    expect(isCash({ why: 'No estimate yet' })).toBe(false);
  });
});
