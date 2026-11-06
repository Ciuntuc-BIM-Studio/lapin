import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../../engine/protocol';
import { applies, boxOf, choiceLabel, pathOf, type ParamDef } from '../../../shell/typeConfigurator';
import { chooseFanType, fanGrips, fanSummary, typeArgs, typesOfKind, type FanKind } from './fans';

const node = (id: string, type: string, props: Record<string, unknown>, name = id): NodeDto => ({ id, type, name, props } as unknown as NodeDto);

describe('exhaust fans', () => {
  const kinds = [{ key: 'wallAxial', label: 'Wall fan (axial)', predefined: 'PROPELLORAXIAL', mountings: ['wall', 'window'], defaults: { diameter: '100', grille: 'round', faceSize: 0.165, airflow: 95 } }] as FanKind[];

  it('lists a kind\'s types by name and says what they are', () => {
    const nodes = new Map([
      ['b', node('b', 'LapinExhaustFanType', { spec: { kind: 'wallAxial', diameter: '125', grille: 'square', faceSize: 0.2 } }, 'Fan B')],
      ['a', node('a', 'LapinExhaustFanType', { spec: { kind: 'wallAxial' } }, 'Fan A')],
      ['c', node('c', 'LapinExhaustFanType', { spec: { kind: 'inline' } }, 'Inline')],
    ]);
    expect(typesOfKind(nodes, 'wallAxial').map((t) => t.name)).toEqual(['Fan A', 'Fan B']);
    expect(fanSummary(nodes.get('a'), kinds)).toBe('Ø100 · Ø165 · 95 m³/h · wall');
    expect(fanSummary(nodes.get('b'), kinds)).toBe('Ø125 · 200 × 200 · 95 m³/h · wall');
  });

  it('puts down the chosen type, a built-in one, or the kind\'s first', () => {
    const nodes = new Map([['t', node('t', 'LapinExhaustFanType', { spec: { kind: 'wallAxial' } })]]);
    expect(typeArgs('ceilingDuct', nodes)).toEqual({ kind: 'ceilingDuct' });
    chooseFanType('wallAxial', 't');
    expect(typeArgs('wallAxial', nodes)).toEqual({ typeId: 't' });
    chooseFanType('wallAxial', 'preset:wall100h');
    expect(typeArgs('wallAxial', nodes)).toEqual({ preset: 'wall100h' });
  });

  it('turns a fan so its front looks at the cursor', () => {
    const [move, turn] = fanGrips(node('f', 'LapinExhaustFan', { at: [1, 1], rotation: 0 }));
    expect(move.at).toEqual({ x: 1, y: 1 });
    expect(turn.at.y).toBeLessThan(1);
    expect(turn.step!({ x: 3, y: 1 })).toEqual({ command: 'element.transform', args: { ids: ['f'], op: 'rotate', center: [1, 1], angle: 90 } });
  });

  it('shows a parameter only where it applies, and draws the preview with y up', () => {
    const light: ParamDef = { key: 'light', label: 'Light', kind: 'bool', showIf: { kind: ['ceilingLight'] } };
    expect(applies(light, { kind: 'ceilingLight' })).toBe(true);
    expect(applies(light, { kind: 'wallAxial' })).toBe(false);
    const spacing: ParamDef = { key: 'spacing', label: 'Spacing', kind: 'length', showIf: { infill: ['verticalBars', 'horizontalSlats'] } };
    expect(applies(spacing, {}, { infill: 'verticalBars' })).toBe(true);
    expect(applies(spacing, { infill: 'solid' }, { infill: 'verticalBars' })).toBe(false);
    expect(applies({ ...spacing, showIf: { motor: ['true'] } }, { motor: true })).toBe(true);
    expect(pathOf([0, 0, 1, 0.5], false)).toBe('M0.0000 0.0000 L1.0000 -0.5000');
    expect(boxOf([[0, 0, 2, 1]], 0)).toEqual([0, -1, 2, 1]);
    expect(choiceLabel('humidityTimer')).toBe('humidity timer');
    expect(choiceLabel('round')).toBe('round');
  });
});
