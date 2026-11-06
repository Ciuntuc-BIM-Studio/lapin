import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../../engine/protocol';
import { mergeTabs } from '../../../platform/ribbon';
import type { LapinModule, ToolDef } from '../../../platform/module';
import { chooseGateType, gateGrips, gateSummary, gateTypeArgs, overallOf, useGatePrefs, type GateKind } from './gates';

const node = (id: string, type: string, props: Record<string, unknown>, name = id): NodeDto => ({ id, type, name, props } as unknown as NodeDto);

describe('gates', () => {
  const kinds = [
    { key: 'doubleSwing', label: 'Double swing gate', operation: 'DOUBLE_DOOR_SINGLE_SWING', defaults: { width: 3.6, height: 1.6, infill: 'verticalBars', piers: 'masonry', pierSize: 0.4, motor: false } },
    { key: 'swingWicket', label: 'Gate with side pedestrian door', operation: 'USERDEFINED', defaults: { width: 3.6, height: 1.6, infill: 'verticalBars', piers: 'masonry', pierSize: 0.4, wicketWidth: 1 } },
  ] as GateKind[];

  it('says a type in a few words and measures it between its piers\' outer faces', () => {
    const t = node('t', 'LapinGateType', { spec: { kind: 'doubleSwing', motor: true, piers: 'steel', pierSize: 0.1 } });
    expect(gateSummary(t, kinds)).toBe('3600 × 1600 · verticalBars · steel · motor');
    expect(overallOf(t, kinds)).toBeCloseTo(3.8);
    const w = node('w', 'LapinGateType', { spec: { kind: 'swingWicket' } });
    expect(gateSummary(w, kinds)).toBe('3600 + 1000 × 1600 · verticalBars · masonry');
    expect(overallOf(w, kinds)).toBeCloseTo(3.6 + 1 + 3 * 0.4);
  });

  it('puts down the chosen type, a built-in style, or the kind\'s first, on lines by default', () => {
    const nodes = new Map([['t', node('t', 'LapinGateType', { spec: { kind: 'sliding' } })]]);
    expect(useGatePrefs.getState().onLine).toBe(true);
    expect(gateTypeArgs('sliding', nodes)).toEqual({ kind: 'sliding' });
    chooseGateType('sliding', 'style:slatSliding');
    expect(gateTypeArgs('sliding', nodes)).toEqual({ style: 'slatSliding' });
    chooseGateType('sliding', 't');
    expect(gateTypeArgs('sliding', nodes)).toEqual({ typeId: 't' });
  });

  it('flips a swinging gate\'s leaves and hand, and a sliding one\'s side and hand', () => {
    const swing = gateGrips(node('g', 'LapinGate', { at: [0, 0], rotation: 0, gateKind: 'swing', swing: 'in', hand: 'right' }), 3);
    expect(swing.map((g) => g.key)).toEqual(['at', 'rotate', 'swing', 'hand']);
    expect(swing[2].at.y).toBeGreaterThan(0);
    expect(swing[2].click!()).toEqual({ command: 'architecture.flipGate', args: { ids: ['g'], axis: 'swing' } });
    expect(swing[3].at.x).toBeGreaterThan(0);
    const double = gateGrips(node('d', 'LapinGate', { at: [0, 0], rotation: 90, gateKind: 'doubleSwing', swing: 'out' }), 4);
    expect(double.map((g) => g.key)).toEqual(['at', 'rotate', 'swing']);
    // Turned 90°, the street side (out) is +x.
    expect(double[2].at.x).toBeCloseTo(0.6);
    expect(double[1].step!({ x: -1, y: 0 })).toEqual({ command: 'element.transform', args: { ids: ['d'], op: 'rotate', center: [0, 0], angle: 90 } });
  });

  it('goes after the Site tab\'s Landscape, whichever module comes first', () => {
    const tool = (id: string) => ({ id, label: id } as ToolDef);
    const mod = (m: Partial<LapinModule>) => ({ id: 'm', name: 'm', ...m } as LapinModule);
    const tabs = mergeTabs([
      mod({ tabs: [{ id: 'site', label: 'Site', order: 40, groups: [{ label: 'Gates', after: 'Landscape', tools: [tool('gate')] }] }] }),
      mod({ tabs: [{ id: 'site', label: 'Site', order: 40, groups: [{ label: 'Terrain', tools: [] }, { label: 'Landscape', tools: [] }, { label: 'Entourage', tools: [] }] }] }),
    ]);
    expect(tabs[0].groups.map((g) => g.label)).toEqual(['Terrain', 'Landscape', 'Gates', 'Entourage']);
  });
});
