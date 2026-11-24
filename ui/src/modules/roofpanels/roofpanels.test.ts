import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import {
  choiceOf, colourLabel, edgeRows, faceTotals, laidFlat, lengthColours, purchaseRows, purchaseText, REUSED, reuseLinks, setArgs,
  type LayoutDto, type PanelDto, type PanelProfileDto,
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
});
