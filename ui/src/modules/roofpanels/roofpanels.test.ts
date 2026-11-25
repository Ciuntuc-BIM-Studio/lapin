import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import {
  BILKA_TEMPLATE, COST_GROUPS, costRows, costState, money, quantityText, sheetRows, subtotalText, type CostDto, type CostLineDto,
  choiceOf, colourLabel, edgeRows, faceTotals, ITEM_GROUPS, itemRows, laidFlat, lengthColours, needText, purchaseRows, purchaseText, REUSED, reuseLinks, setArgs,
  takeoffOptions, type LayoutDto, type PanelDto, type PanelProfileDto, type TakeoffDto,
  combinationLabel, downpipesOf, drainageArgs, drainageCheck, drainageRows, pieceColour, rainOf, sheetsOf, sheetUse,
} from './roofpanels';
import { roofFor } from './index';

const profile = (over: Partial<PanelProfileDto>): PanelProfileDto => ({
  id: 'p', name: 'P', maker: '', kind: 'tile', grossWidth: 1200, usefulWidth: 1120, slopeLap: 150, module: 350, joints: 'module',
  onOrder: true, minLength: 500, maxLength: 7000, maxLengths: [], colors: [], minPitch: 8, minPitchHipValley: 14, reuseOffcuts: true,
  notes: '', source: '', ...over,
});
const clasic = profile({ id: 'bilka-clasic-35', colors: [{ code: '7016', name: 'RAL 7016' }, { code: '8017', name: 'RAL 8017' }] });
const retro = profile({ id: 'bilka-retro-panel', grossWidth: 542, maxLength: 7130, maxLengths: [4000, 7130], colors: [{ code: '8019', name: 'RAL 8019' }] });

const node = (props: Record<string, unknown>, type = 'IfcRoof', id = 'r1') => ({ id, type, props } as unknown as NodeDto);

const panel = (id: string, face: string, length: number, plan: [number, number][], over: Partial<PanelDto> = {}): PanelDto => ({
  id, face, column: 1, row: 1, length, buy: over.donor ? 0 : 1, x: 0, y: 0, trimmedLength: length, grossArea: 1, coverage: 1, edges: [],
  plan, net: plan, ...over,
});

const sq = (x: number, y: number): [number, number][] => [[x, y], [x + 1, y], [x + 1, y + 1], [x, y + 1]];

const layout: LayoutDto = {
  roofId: 'r1', profile: clasic, color: '7016', stock: [],
  faces: [
    { id: 'A01', plane: 0, pitch: 30, eave: 10, area: 20, coverage: 19, slopeLength: 2, outline: [[0, 0], [10, 0], [10, 2], [0, 2]], planOutline: [] },
    { id: 'A02', plane: 1, pitch: 30, eave: 4, area: 6, coverage: 5, slopeLength: 1.5, outline: [[0, 0], [4, 0], [2, 1.5]], planOutline: [] },
  ],
  edges: [
    { kind: 'hip', faces: ['A01', 'A02'], length: 2.5, lap: 0.1, pieces: 2, joints: 1, a: [0, 0, 0], b: [1, 1, 1] },
    { kind: 'eave', faces: ['A01'], length: 10, lap: 0.075, pieces: 6, joints: 5, a: [0, 0, 0], b: [10, 0, 0] },
    { kind: 'eave', faces: ['A02'], length: 4, lap: 0.075, pieces: 3, joints: 2, a: [0, 0, 0], b: [4, 0, 0] },
    { kind: 'valley', faces: ['A01', 'A02'], length: 1, lap: 0.15, pieces: 1, joints: 0, a: [0, 0, 0], b: [0, 1, 0] },
  ],
  panels: [
    panel('A01-01-01', 'A01', 2600, sq(0, 0), { donatesTo: 'A02-01-01' }),
    panel('A01-02-01', 'A01', 2600, sq(1, 0)),
    panel('A01-03-01', 'A01', 1200, sq(2, 0)),
    panel('A02-01-01', 'A02', 2600, sq(5, 0), { donor: 'A01-01-01', shift: 350 }),
  ],
  purchase: { 1200: 1, 2600: 2 }, installed: { 1200: 1, 2600: 3 },
  totals: { roofArea: 26, coverage: 24, sheetArea: 7.68, panels: 4, bought: 3, reused: 1, lengths: 2, boughtLength: 6.4, edgeLengths: {} },
  warnings: [],
};

describe('the roof panels the interface reads', () => {
  it('reads what a roof names, and nothing when it names no profile', () => {
    expect(choiceOf(node({}))).toBeNull();
    expect(choiceOf(node({ panels: { profile: '' } }))).toBeNull();
    expect(choiceOf(node({ panels: { profile: 'bilka-retro-panel', maxLength: 4000 } }))).toEqual({ profile: 'bilka-retro-panel', color: null, maxLength: 4000 });
  });

  it('sets a profile in one command, dropping a colour or a longest panel the profile does not have', () => {
    const all = [clasic, retro];
    expect(setArgs('r1', null, all)).toEqual({ id: 'r1', profile: null });
    expect(setArgs('r1', { profile: 'bilka-clasic-35', color: '8017' }, all)).toEqual({ id: 'r1', profile: 'bilka-clasic-35', color: '8017' });
    // Clasic to Retro: Retro is not made in 8017; Retro at 4000 mm keeps its longest panel.
    expect(setArgs('r1', { profile: 'bilka-retro-panel', color: '8017', maxLength: 4000 }, all)).toEqual({ id: 'r1', profile: 'bilka-retro-panel', maxLength: 4000 });
    expect(setArgs('r1', { profile: 'bilka-clasic-35', maxLength: 4000 }, all)).toEqual({ id: 'r1', profile: 'bilka-clasic-35' });
  });

  it('names a colour short in its field, the maker\'s full name kept for its tooltip', () => {
    expect(colourLabel({ code: '8017', name: 'RAL 8017 matt brown' })).toBe('RAL 8017');
    expect(colourLabel({ code: 'X1', name: 'Graphite' })).toBe('Graphite');
    expect(colourLabel({ code: 'X2', name: '' })).toBe('X2');
  });

  it('lists what to buy by length, the longest first', () => {
    expect(purchaseRows(layout)).toEqual([{ length: 2600, count: 2, metres: 5.2 }, { length: 1200, count: 1, metres: 1.2 }]);
    expect(purchaseText(layout)).toBe('2600 mm × 2, 1200 mm × 1');
  });

  it('totals each roof plane: its panels, those bought and reused, the sheet bought for it', () => {
    expect(faceTotals(layout)).toEqual([
      { id: 'A01', pitch: 30, area: 20, panels: 3, bought: 3, reused: 0, sheet: (2600 * 2 + 1200) * 1200 / 1e6 },
      { id: 'A02', pitch: 30, area: 6, panels: 1, bought: 0, reused: 1, sheet: 0 },
    ]);
  });

  it('groups the edges by kind in their order: eaves, then hips, then valleys', () => {
    expect(edgeRows(layout)).toEqual([
      { kind: 'eave', count: 2, length: 14, pieces: 9 },
      { kind: 'hip', count: 1, length: 2.5, pieces: 2 },
      { kind: 'valley', count: 1, length: 1, pieces: 1 },
    ]);
  });

  it('draws each reused offcut from the middle of its donor to where it lies, and never colours a length orange', () => {
    expect(reuseLinks(layout)).toEqual([{ donor: 'A01-01-01', recipient: 'A02-01-01', shift: 350, from: [0.5, 0.5], to: [5.5, 0.5] }]);
    const colours = lengthColours([4350, 1200, 2600, 1200]);
    expect([...colours.keys()]).toEqual([1200, 2600, 4350]);
    expect([...colours.values()]).not.toContain(REUSED);
    expect(new Set(colours.values()).size).toBe(3);
  });

  it('lays the planes flat side by side without overlapping, the next row under the first', () => {
    const faces = [
      { id: 'A01', outline: [[0, 0], [10, 0], [10, 4], [0, 4]] as [number, number][] },
      { id: 'A02', outline: [[0, 0], [6, 0], [3, 3]] as [number, number][] },
      { id: 'A03', outline: [[0, 0], [10, 0], [10, 4], [0, 4]] as [number, number][] },
      { id: 'A04', outline: [[0, 0], [6, 0], [3, 3]] as [number, number][] },
    ];
    const o = laidFlat(faces, 1);
    const box = (i: number) => {
      const pts = faces[i].outline.map(([x, y]) => [x + o[i].dx, y + o[i].dy]);
      return { x0: Math.min(...pts.map((p) => p[0])), x1: Math.max(...pts.map((p) => p[0])), y0: Math.min(...pts.map((p) => p[1])), y1: Math.max(...pts.map((p) => p[1])) };
    };
    const boxes = faces.map((_, i) => box(i));
    for (let i = 0; i < boxes.length; i++)
      for (let j = i + 1; j < boxes.length; j++) {
        const [a, b] = [boxes[i], boxes[j]];
        const apart = a.x1 <= b.x0 || b.x1 <= a.x0 || a.y1 <= b.y0 || b.y1 <= a.y0;
        expect(apart, `${faces[i].id} and ${faces[j].id} overlap`).toBe(true);
      }
    // Rows of about twice the widest (20 m): A01 and A02 on the first, A03 and A04 under them.
    expect(boxes[1].y1).toBe(boxes[0].y1);
    expect(boxes[2].y1).toBeLessThan(boxes[0].y0);
  });

  it('opens Panel layout on the roof selected, else on the project\'s only roof', () => {
    const roofA = node({}, 'IfcRoof', 'a');
    const roofB = node({}, 'IfcRoof', 'b');
    const wall = node({}, 'IfcWall', 'w');
    const nodes = new Map([['a', roofA], ['b', roofB], ['w', wall]]);
    expect(roofFor(['w', 'b'], nodes)?.id).toBe('b');
    expect(roofFor([], nodes)).toBeNull();
    expect(roofFor([], new Map([['a', roofA], ['w', wall]]))?.id).toBe('a');
  });
});

describe('the roof panels in every language', () => {
  it('names the edges, the tables and the engine\'s warnings in each', async () => {
    const { loadAllLanguages, translate, LANGUAGES } = await import('../../i18n');
    const { EDGE_LABEL } = await import('./roofpanels');
    await loadAllLanguages();
    for (const { id } of LANGUAGES.filter((l) => l.id !== 'en')) {
      for (const phrase of [...Object.values(EDGE_LABEL), 'To buy', 'Panel layout…', 'Reused offcut', 'Sheet bought (m²)', 'The roof names no panel profile.'])
        expect(translate(phrase, id), `${phrase} in ${id}`).not.toBeNull();
      expect(translate("Roof plane A01 (6.5°) is under Bilka Clasic 35's least pitch of 8°.", id), id).toContain('6.5');
    }
  });

  it('names the takeoff (S3) in each: its groups, options, the shipped profiles\' lines and units', async () => {
    const { loadAllLanguages, translate, LANGUAGES } = await import('../../i18n');
    const { ITEM_GROUPS } = await import('./roofpanels');
    await loadAllLanguages();
    // The shipped profiles' accessories, fixings and support (profiles.json), as the engine names them.
    const profiles = (await import('../../../../engine/src/Modules/Roofing/Lapin.RoofPanels/profiles.json')).default as {
      common: { membrane: { name: string; unit: string }; membraneTape: { name: string; unit: string }; counterBatten: { name: string }; woodFixings: { name: string; unit: string }[] };
      profiles: { accessories: { name: string; unit: string; note?: string }[]; fixings: { name: string; unit: string }[]; support: { name: string; note: string }[] }[];
    };
    const c = profiles.common;
    const shipped = [c.membrane.name, c.membrane.unit, c.membraneTape.name, c.membraneTape.unit, c.counterBatten.name, ...c.woodFixings.flatMap((w) => [w.name, w.unit]),
      ...profiles.profiles.flatMap((p) => [...p.accessories.flatMap((a) => [a.name, a.unit, ...(a.note ? [a.note] : [])]), ...p.fixings.flatMap((x) => [x.name, x.unit]), ...p.support.flatMap((x) => [x.name, x.note])])];
    for (const { id } of LANGUAGES.filter((l) => l.id !== 'en')) {
      for (const phrase of [...ITEM_GROUPS.map((g) => g.label), 'Accessories and support', 'Ridge caps', 'Support fixings', 'Battens and membrane per roof plane', 'Roof panel accessories', ...shipped])
        expect(translate(phrase, id), `${phrase} in ${id}`).not.toBeNull();
      expect(translate("Bilka Clasic 35's ridge caps are large or small.", id), id).toContain('Bilka Clasic 35');
    }
  });

  it('names the Cost tab (S4) in each: its groups and columns, the subtotal, the notes, the engine\'s line notes', async () => {
    const { loadAllLanguages, translate, LANGUAGES } = await import('../../i18n');
    await loadAllLanguages();
    for (const { id } of LANGUAGES.filter((l) => l.id !== 'en')) {
      for (const phrase of [...COST_GROUPS.map((g) => g.label), 'Cost', 'Quantities', 'Price', 'Amount', 'Source', 'To quote', 'Indicative subtotal', 'Subtotal of the priced lines',
        'Joints between ridge caps', 'Cut from flat sheet', 'Add the Bilka – Dedeman template', 'Price lists…', 'Templates', 'Source (supplier)', 'Price checked on',
        'Indicative — check prices when ordering. Labour and transport are not included.', 'There is no such price list template.'])
        expect(translate(phrase, id), `${phrase} in ${id}`).not.toBeNull();
      expect(translate('18 panel(s) of 4350 mm: priced per m² of sheet bought (the nominal length by the 1200 mm width).', id), id).toContain('4350');
      expect(translate('Price list added to the project: Bilka – Dedeman (indicative, 24.11.2026).', id), id).toContain('Bilka – Dedeman');
    }
  });
});

describe('the takeoff the interface reads (S3)', () => {
  const takeoff: TakeoffDto = {
    support: 'battens', ridge: 'large', fixing: 'nails',
    supports: [{ id: 'battens', name: 'Batten 50 × 30 mm', kind: 'battens' }], ridges: ['large', 'small'],
    fixings: [{ id: 'nails', name: 'Nails 4 × 100 mm' }, { id: 'screws', name: 'Wood screws 6 × 100 mm' }],
    raftersEstimated: false, faces: [], stats: {},
    items: [
      { code: 'membrane', name: 'Breathable roof membrane 150 g/m²', group: 'support', quantity: 3, unit: 'roll 75 m²', need: 175.6, needUnit: 'm²', note: '' },
      { code: 'wood-nails', name: 'Nails 4 × 100 mm', group: 'fixings', quantity: 3, unit: 'box 5 kg', need: 1000, needUnit: 'pcs', note: '' },
      { code: 'ridge-large', name: 'Ridge cap, large (290 mm)', group: 'flashings', quantity: 7, unit: 'piece 2 m', need: 11, needUnit: 'm', note: '' },
      { code: 'hip-end-large', name: 'Hip end cap, large', group: 'flashings', quantity: 5, unit: 'piece', need: null, needUnit: null, note: '' },
    ],
  };

  it('groups the lines as the tables show them: flashings, fixings, then battens and membrane', () => {
    expect(ITEM_GROUPS.map((g) => g.id)).toEqual(['flashings', 'fixings', 'support']);
    expect(itemRows(takeoff, 'flashings').map((i) => i.code)).toEqual(['ridge-large', 'hip-end-large']);
    expect(itemRows(takeoff, 'support').map((i) => i.code)).toEqual(['membrane']);
    expect(itemRows(undefined, 'support')).toEqual([]);
  });

  it('says what a line is counted from, nothing for a count', () => {
    expect(needText(takeoff.items[0])).toBe('175.6 m²');
    expect(needText(takeoff.items[1])).toBe('1000 pcs');
    expect(needText(takeoff.items[2])).toBe('11.0 m');
    expect(needText(takeoff.items[3])).toBe('');
  });

  it('offers only the options with a choice: a tile its ridge caps and the fixings, never one support', () => {
    expect(takeoffOptions(takeoff).map((o) => `${o.key}=${o.value}:${o.choices.map((c) => c.id).join('/')}`)).toEqual(['ridge=large:large/small', 'fixing=nails:nails/screws']);
    const retroTakeoff = { ...takeoff, ridges: [], ridge: null, support: 'boards', supports: [{ id: 'boards', name: 'Board', kind: 'boards' }, { id: 'attached', name: 'Batten', kind: 'battens' }] };
    expect(takeoffOptions(retroTakeoff).map((o) => o.key)).toEqual(['support', 'fixing']);
    expect(takeoffOptions(undefined)).toEqual([]);
  });

  it('keeps the roof\'s options when its choice changes, those the profile has', () => {
    const tile = { ...clasic, ridges: ['large', 'small'], support: [{ id: 'battens', name: 'Batten', kind: 'battens', section: '50x30' }] };
    const panel = { ...retro, ridges: [], support: [{ id: 'boards', name: 'Board', kind: 'boards', section: '22x150' }, { id: 'attached', name: 'Batten', kind: 'battens', section: '50x30' }] };
    expect(choiceOf(node({ panels: { profile: 'bilka-clasic-35', ridge: 'small', fixing: 'screws' } }))).toEqual({ profile: 'bilka-clasic-35', color: null, maxLength: null, ridge: 'small', fixing: 'screws' });
    expect(setArgs('r1', { profile: 'bilka-clasic-35', ridge: 'small', fixing: 'screws' }, [tile, panel])).toEqual({ id: 'r1', profile: 'bilka-clasic-35', ridge: 'small', fixing: 'screws' });
    // To Retro Panel: no ridge option there, its support kept when it has it.
    expect(setArgs('r1', { profile: 'bilka-retro-panel', ridge: 'small', support: 'attached', fixing: 'screws' }, [tile, panel])).toEqual({ id: 'r1', profile: 'bilka-retro-panel', support: 'attached', fixing: 'screws' });
    expect(setArgs('r1', { profile: 'bilka-clasic-35', support: 'attached' }, [tile, panel])).toEqual({ id: 'r1', profile: 'bilka-clasic-35' });
  });
});

describe('the Cost tab (S4)', () => {
  const line = (over: Partial<CostLineDto>): CostLineDto => ({ code: 'x', name: 'X', group: 'flashings', quantity: 1, unit: 'piece 2 m', note: '', ...over });
  const cost: CostDto = {
    available: true, joints: 15, priced: 3, toQuote: 1, fromSheet: 1, indicative: true, subtotal: { RON: 22517.86 },
    lines: [
      line({ code: 'bilka-clasic-35/7016', variant: '4350 mm', group: 'panels', quantity: 93.96, unit: 'm²', pieces: 18, price: 50.77, amount: 4770.35 }),
      line({ code: 'hip-large', quantity: 15, price: 79.73, amount: 1195.95, source: 'Dedeman 6037249' }),
      line({ code: 'window-kit', quantity: 1, unit: 'piece' }),
      line({ code: 'ridge-nodes', quantity: 4, unit: 'piece', madeFrom: 'flatSheet' }),
      line({ code: 'batten', group: 'support', quantity: 165, unit: 'bar 3 m', price: 17.53, amount: 2892.45 }),
    ],
  };

  it('groups the lines bought, the panels first, and keeps what is cut from flat sheet apart', () => {
    expect(COST_GROUPS.map((g) => g.id)).toEqual(['panels', 'flashings', 'fixings', 'support', 'rainwater']);
    expect(costRows(cost, 'panels').map((l) => l.code)).toEqual(['bilka-clasic-35/7016']);
    expect(costRows(cost, 'flashings').map((l) => l.code)).toEqual(['hip-large', 'window-kit']);
    expect(sheetRows(cost).map((l) => l.code)).toEqual(['ridge-nodes']);
  });

  it('writes money with two decimals and thousands grouped, the subtotal a currency each', () => {
    expect(money(22517.86)).toBe('22\u00a0517.86');
    expect(money(1195.9)).toBe('1\u00a0195.90');
    expect(money(0.17)).toBe('0.17');
    expect(money(1234567)).toBe('1\u00a0234\u00a0567.00');
    expect(subtotalText(cost)).toBe('22\u00a0517.86 RON');
    expect(subtotalText({ subtotal: { EUR: 10, RON: 5 } })).toBe('10.00 EUR + 5.00 RON');
    expect(subtotalText({ subtotal: {} })).toBe('');
  });

  it('says whether there is a cost module, a price for the lines or a subtotal', () => {
    expect(costState(undefined)).toBe('none');
    expect(costState({ ...cost, available: false })).toBe('none');
    expect(costState({ ...cost, priced: 0, subtotal: {} })).toBe('unpriced');
    expect(costState(cost)).toBe('priced');
  });

  it('writes the panels\' m² with two decimals and counts as they are', () => {
    expect(quantityText({ quantity: 46.37352, unit: 'm²' })).toBe('46.37');
    expect(quantityText({ quantity: 165, unit: 'bar 3 m' })).toBe('165');
    expect(BILKA_TEMPLATE).toBe('bilka-dedeman-2026-11-24');
  });
});

describe('the rainwater system and the flat sheet (S5)', () => {
  it('reads the drainage a roof names and sends it, Auto left out, with drainage always when it changes', () => {
    const c = choiceOf(node({ panels: { profile: 'bilka-clasic-35', drainage: '150/100', downpipes: 6, rain: 0.08 } }))!;
    expect([c.drainage, c.downpipes, c.rain]).toEqual(['150/100', 6, 0.08]);
    expect(choiceOf(node({ panels: { profile: 'bilka-clasic-35' } }))).toMatchObject({ drainage: undefined, downpipes: undefined, rain: undefined });
    expect(setArgs('r1', c, [clasic])).toMatchObject({ drainage: '150/100', downpipes: 6, rain: 0.08 });
    expect(setArgs('r1', { profile: 'bilka-clasic-35', drainage: 'auto' }, [clasic])).not.toHaveProperty('drainage');
    expect(drainageArgs('r1', c, { drainage: 'auto' }, [clasic])).toMatchObject({ drainage: 'auto', downpipes: 6 });
    expect(drainageArgs('r1', c, { downpipes: null }, [clasic])).not.toHaveProperty('downpipes');
    expect(drainageArgs('r1', { profile: 'bilka-clasic-35' }, {}, [clasic]).drainage).toBe('auto');
  });

  it('takes a typed number of downpipes or rain, else Auto and the default', () => {
    expect(downpipesOf('')).toBeNull();
    expect(downpipesOf('0')).toBeNull();
    expect(downpipesOf('abc')).toBeNull();
    expect(downpipesOf(' 6 ')).toBe(6);
    expect(downpipesOf('5.6')).toBe(6);
    expect(rainOf('0,08')).toBe(0.08);
    expect(rainOf('-1')).toBeNull();
    expect(rainOf('')).toBeNull();
  });

  it('says the check as the app does: under the least first, then another system', () => {
    expect(drainageCheck({ belowMinimum: true, differs: true })).toBe('below');
    expect(drainageCheck({ belowMinimum: false, differs: true })).toBe('differs');
    expect(drainageCheck({ belowMinimum: false, differs: false })).toBe('ok');
    expect(drainageRows({ items: [
      { code: 'gutter-125', name: 'Gutter', group: 'rainwater', quantity: 6, unit: 'piece 4 m', note: '' },
      { code: 'gutter-special-125', name: 'Odd corner', group: 'rainwater', quantity: 2, unit: 'piece', note: '', madeFrom: 'flatSheet' },
    ] }).map((i) => i.code)).toEqual(['gutter-125']);
    expect(COST_GROUPS.map((g) => g.id)).toContain('rainwater');
  });

  it('splits the pieces by sheet, says how much of the sheets they take and names each combination', () => {
    const piece = (id: string, sheet: number, w: number, h: number) => ({ id, name: id, sheet, x: 10, y: 10, width: w, height: h, blankWidth: w, blankHeight: h });
    const f = { sheets: 2, width: 2000, height: 1250, pieces: [piece('NC-01', 1, 850, 900), piece('NC-02', 1, 900, 850), piece('CD-01', 2, 750, 650)] };
    expect(sheetsOf(f).map((s) => [s.sheet, s.pieces.map((p) => p.id)])).toEqual([[1, ['NC-01', 'NC-02']], [2, ['CD-01']]]);
    expect(sheetUse(f)).toBeCloseTo((2 * 850 * 900 + 750 * 650) / (2 * 2000 * 1250), 9);
    expect(sheetUse({ ...f, sheets: 0, pieces: [] })).toBe(0);
    expect(combinationLabel({ ridge: 'small', system: '150/100' })).toBe('Small · 150/100');
    expect(combinationLabel({ ridge: 'large', system: '125/90' })).toBe('Large · 125/90');
    expect(combinationLabel({ ridge: null, system: '125/90' })).toBe('125/90');
    expect(pieceColour('NC-03')).toBe(pieceColour('NC-01'));
    expect(pieceColour('ZZ-01')).toBe('#7f8c8d');
  });
});

describe('the rainwater and flat sheet tabs in every language (S5)', () => {
  it('names the tabs, the columns, the checks and the engine\'s lines and warnings', async () => {
    const { loadAllLanguages, translate, LANGUAGES } = await import('../../i18n');
    await loadAllLanguages();
    for (const { id } of LANGUAGES.filter((l) => l.id !== 'en')) {
      for (const phrase of ['Rainwater', 'Flat sheet', 'Rainwater system', 'Rain (l/s·m²)', 'Least estimated', 'Downpipes counted', 'In the model', 'Under the least estimated',
        'Another system than the recommended one', 'Within the estimate', 'Size the gutters to the system', 'Small ridge cap', 'Large ridge cap', 'Downpipe size',
        'Ridge node, three ways', 'Ridge–valley head', 'Strip for valley clips', 'The roof has no gutters: no outlet drains it and no gutter is counted.'])
        expect(translate(phrase, id), `${phrase} in ${id}`).not.toBeNull();
      for (const [phrase, keep] of [['Auto (150/100)', '150/100'], ['Gutter Ø125, 4 m', '125'], ['Gutter outlet Ø150 to downpipe Ø100', '100'], ['Gutter corner Ø125 at 135°', '135'],
        ['3 downpipe(s) are fewer than the 8 the 125/90 system needs.', '125/90'], ['The model has 4 downpipe(s); the estimate counts 6.', '6'],
        ["The roof's gutters are 125 mm; the estimate counts the 150/100 system.", '150/100'], ['12.40 m² of roof (A03) drain to no gutter: size their outlets yourself.', 'A03']])
        expect(translate(phrase, id), `${phrase} in ${id}`).toContain(keep);
    }
  });
});
