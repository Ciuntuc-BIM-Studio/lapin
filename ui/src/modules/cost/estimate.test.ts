/**
 * The estimate's logic in the interface (docs/plan-4d5d.md, stage 6): money and quantities as shown, the lines an
 * element selected marks, the unpriced count, rates typed with a decimal comma, the price columns matched in the
 * import dialog, this computer's library of price lists, and the article a Map articles row starts with.
 */

import { beforeEach, describe, expect, it, vi } from 'vitest';

const stored = new Map<string, string>();
vi.stubGlobal('localStorage', {
  getItem: (k: string) => stored.get(k) ?? null,
  setItem: (k: string, v: string) => void stored.set(k, v),
  removeItem: (k: string) => void stored.delete(k),
});

const { articleArg, choiceKey, durationText, forgetFromLibrary, keepInLibrary, library, linesHolding, money, parseRate, qty, remember, remembered, startingChoice, unpricedCount, FORMS, TIMES } = await import('./estimate');
const { FIELD_LABELS, fieldOf, setColumn, templateArg } = await import('./PriceListsDialog');
import type { EstimateDto, LineDto } from './estimate';

const line = (key: string, elements: string[]): LineDto => ({
  key, code: key, name: key, unit: 'm3', qty: 1, basis: 'volume', m: 1, l: 0, e: 0, t: 0, unitPrice: 1, value: 1, other: 0.2, priced: true, elements,
});

const estimate = (lines: LineDto[], unpriced: EstimateDto['unpriced'] = []): EstimateDto => ({
  id: 'e', name: 'E', predefined: 'COSTPLAN',
  settings: { currency: 'RON', other: 'EUR', eurRate: 5, labourTax: 2.25, overheads: 10, profit: 5, vat: 21, grouping: 'chapter', equivalent: false, derive: true, priceLists: [] },
  chapters: [{ name: 'Walls', m: 0, l: 0, e: 0, t: 0, direct: 0, lines }],
  totals: { m: 0, l: 0, e: 0, t: 0, direct: 0, labourTax: 0, directTotal: 0, overheads: 0, profit: 0, net: 0, vat: 0, total: 0, other: 0 },
  unpriced, missing: [], lists: [],
});

beforeEach(() => stored.clear());

describe('the estimate as shown', () => {
  it('writes money with two decimals and thin spaces between thousands, quantities with three', () => {
    expect(money(8666.95)).toBe('8 666.95');
    expect(money(1234567.5)).toBe('1 234 567.50');
    expect(money(0)).toBe('0.00');
    expect(money(-12.345)).toBe('−12.35');
    expect(qty(7.5)).toBe('7.500');
    expect(qty(1.87549)).toBe('1.875');
  });

  it('marks the lines holding an element selected, none when nothing is', () => {
    const d = estimate([line('ZID01', ['w1', 'w2']), line('TEN01', ['w1']), line('USA01', ['d1'])]);
    expect([...linesHolding(d, ['w2'])]).toEqual(['ZID01']);
    expect([...linesHolding(d, ['w1', 'd1'])].sort()).toEqual(['TEN01', 'USA01', 'ZID01']);
    expect(linesHolding(d, []).size).toBe(0);
    expect(linesHolding(null, ['w1']).size).toBe(0);
  });

  it('counts the types no article prices and their elements', () => {
    const d = estimate([], [{ type: 't1', class: 'IfcWall', name: 'Partition', count: 3, elements: [] }, { type: null, class: 'IfcColumn', name: 'IfcColumn', count: 2, elements: [] }]);
    expect(unpricedCount(d)).toEqual({ types: 2, elements: 5 });
    expect(unpricedCount(null)).toEqual({ types: 0, elements: 0 });
  });

  it('reads a rate typed with a decimal comma or a point, and nothing else', () => {
    expect(parseRate('2,25')).toBe(2.25);
    expect(parseRate(' 4.9765 ')).toBe(4.9765);
    expect(parseRate('')).toBeNull();
    expect(parseRate('ten')).toBeNull();
  });

  it('offers the estimate and the six Romanian forms, the estimate first, then the cost per month (stage 7)', () => {
    expect(FORMS.map((f) => f.id)).toEqual(['estimate', 'f3', 'object', 'c6', 'c7', 'c8', 'c9', 'monthly']);
  });
});

describe('the price list import', () => {
  it('names every column the engine reads, the sources too, and adds a template by its id (Lapin\'s own as true)', () => {
    for (const f of ['code', 'name', 'unit', 'material', 'price', 'source', 'url', 'checked']) expect(FIELD_LABELS[f], f).toBeTruthy();
    expect(templateArg({ id: 'lapin' })).toBe(true);
    expect(templateArg({ id: 'bilka-dedeman-2026-11-24' })).toBe('bilka-dedeman-2026-11-24');
  });

  it('gives each column one meaning, a meaning moved from the column that had it', () => {
    const map = { code: 0, name: 1, unit: null, material: 3, price: null };
    const a = setColumn(map, 2, 'unit');
    expect(a).toMatchObject({ code: 0, name: 1, unit: 2, material: 3 });
    // The code put on column 1: the name is no longer matched.
    const b = setColumn(a, 1, 'code');
    expect(b).toMatchObject({ code: 1, name: null });
    expect(fieldOf(b, 1)).toBe('code');
    expect(fieldOf(b, 0)).toBeNull();
    // Not used.
    expect(fieldOf(setColumn(b, 3, null), 3)).toBeNull();
  });

  it('keeps lists in this computer\'s library, one per name, and forgets them', () => {
    expect(library()).toEqual([]);
    expect(keepInLibrary({ name: 'Prețuri 2026', currency: 'RON', rows: [{ c: 'A', n: 'a', u: 'mc', m: 1 }], added: '2026-11-05' })).toBe(true);
    keepInLibrary({ name: 'Prețuri 2026', currency: 'RON', rows: [{ c: 'B', n: 'b', u: 'mp', m: 2 }], added: '2026-11-06' });
    keepInLibrary({ name: 'Supplier', currency: 'EUR', rows: [], added: '2026-11-06' });
    expect(library().map((l) => `${l.name}:${l.rows[0]?.c ?? ''}`)).toEqual(['Prețuri 2026:B', 'Supplier:']);
    forgetFromLibrary('Supplier');
    expect(library().map((l) => l.name)).toEqual(['Prețuri 2026']);
  });
});

describe('Map articles', () => {
  const has = (code: string) => ['ZID01', 'TEN01'].includes(code);

  it('starts a row from what it has, else the choice made before for that name, else the suggestion', () => {
    const now = { code: 'X1', part: 'core' };
    const past = { code: 'ZID01', part: 'core' };
    const sug = { code: 'TEN01' };
    expect(startingChoice(now, past, sug, has)).toEqual({ choice: now, from: 'now' });
    expect(startingChoice(null, past, sug, has)).toEqual({ choice: past, from: 'remembered' });
    // A remembered code the project's lists do not have: the suggestion instead.
    expect(startingChoice(null, { code: 'OLD' }, sug, has)).toEqual({ choice: sug, from: 'suggested' });
    expect(startingChoice(null, null, null, has)).toEqual({ choice: null, from: 'none' });
  });

  it('remembers choices by a type\'s class and name or a material\'s name, whatever their case and diacritics', () => {
    expect(choiceKey('type', 'Zidărie BCA 30', 'IfcWall')).toBe(choiceKey('type', ' zidarie bca 30', 'IfcWall'));
    expect(choiceKey('type', 'BCA', 'IfcWall')).not.toBe(choiceKey('type', 'BCA', 'IfcSlab'));
    remember({ [choiceKey('material', 'Tencuială var')]: { code: 'TEN01' } });
    remember({ [choiceKey('type', 'Brick', 'IfcWall')]: { code: 'ZID01', part: 'core' } });
    expect(remembered()[choiceKey('material', 'TENCUIALA VAR')]).toEqual({ code: 'TEN01' });
    expect(Object.keys(remembered())).toHaveLength(2);
  });
  it('offers "× duration" beside the measure: by the price\'s unit first, none, then months, working days, calendar days and working hours (R7)', () => {
    expect(TIMES.map((t) => t.id)).toEqual(['auto', 'none', 'months', 'workingDays', 'days', 'hours']);
    expect(TIMES[0].label).toBe("By the price's unit");
  });

  it('sends an article\'s time only when one is chosen, so an article written before stays as it was', () => {
    expect(articleArg({ code: ' GARD ', part: null, basis: 'length', time: 'auto' })).toEqual({ code: 'GARD', part: null, basis: 'length' });
    expect(articleArg({ code: 'GARD', basis: 'length' })).toEqual({ code: 'GARD', part: null, basis: 'length' });
    expect(articleArg({ code: 'SEMN', part: 'core', basis: 'count', time: 'workingDays' })).toEqual({ code: 'SEMN', part: 'core', basis: 'count', time: 'workingDays' });
    expect(articleArg({ code: 'X', time: 'none' })).toEqual({ code: 'X', part: null, basis: 'auto', time: 'none' });
  });

  it('remembers the time chosen with the rest of the article', () => {
    remember({ [choiceKey('type', 'Site fence', 'IfcWall')]: { code: 'GARD', basis: 'auto', time: 'months' } });
    const past = remembered()[choiceKey('type', 'Site fence', 'IfcWall')];
    expect(startingChoice(null, past, null, () => true).choice?.time).toBe('months');
  });

  it('shows the duration a line\'s quantity was multiplied by, nothing when it was not', () => {
    expect(durationText({ time: 'months', duration: '3 months' })).toBe('× 3 months');
    expect(durationText({ time: 'months', duration: null })).toBe('');
    expect(durationText({})).toBe('');
  });
});

describe('money, rates and dates (audit 22.11)', () => {
  it('drops the sign of a value that rounds to nothing', () => {
    expect(money(-0.001)).toBe('0.00');
    expect(money(-0.004)).toBe('0.00');
    expect(money(-0.005)).toBe('−0.01');
  });
  it('keeps a rate inside its bounds', async () => {
    const { EXCHANGE, PERCENT } = await import('./estimate');
    expect(parseRate('0', EXCHANGE)).toBeNull();
    expect(parseRate('-4', EXCHANGE)).toBeNull();
    expect(parseRate('4,9765', EXCHANGE)).toBe(4.9765);
    expect(parseRate('-50', PERCENT)).toBeNull();
    expect(parseRate('900', PERCENT)).toBeNull();
    expect(parseRate('0', PERCENT)).toBe(0);
    expect(parseRate('19', PERCENT)).toBe(19);
    expect(parseRate('0')).toBe(0);
  });
  it('writes today by the local calendar, not UTC', async () => {
    const { localDate, inOther } = await import('./estimate');
    expect(localDate(new Date(2026, 10, 5, 0, 30))).toBe('2026-11-05');
    expect(localDate(new Date(2026, 0, 1, 23, 59))).toBe('2026-01-01');
    expect(inOther(497.65, { currency: 'RON', eurRate: 4.9765 })).toBe(100);
    expect(inOther(100, { currency: 'EUR', eurRate: 4.9765 })).toBe(497.65);
  });
});
