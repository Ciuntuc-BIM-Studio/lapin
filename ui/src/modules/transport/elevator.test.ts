import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { elevatorGrips, groupStyles, itemPath, planBox, stopsBetween, styleAbout, toggleStop, type ElevatorStyle } from './elevator';

const node = (id: string, type: string, props: Record<string, unknown>, name = id): NodeDto => ({ id, type, name, props } as unknown as NodeDto);

describe('elevators', () => {
  it('groups the catalogue\'s styles and says each in a few words', () => {
    const styles = [
      { id: 'a', label: 'A', group: 'Passenger', description: '', values: {} },
      { id: 'h', label: 'H', group: 'Home', description: '', values: {} },
      { id: 'b', label: 'B', group: 'Passenger', description: '', values: {} },
    ] as ElevatorStyle[];
    expect(groupStyles(styles).map(([g, s]) => [g, s.map((x) => x.id)])).toEqual([['Passenger', ['a', 'b']], ['Home', ['h']]]);
    expect(styleAbout({ persons: 8, load: 630, cabinWidth: 1.1, cabinDepth: 1.4 })).toBe('8 p · 630 kg · 1100 × 1400');
  });

  it('lists the stops between its lowest and highest storey, and ticks them', () => {
    const storeys = [node('s2', 'IfcBuildingStorey', { elevation: 6 }), node('s0', 'IfcBuildingStorey', { elevation: 0 }), node('s1', 'IfcBuildingStorey', { elevation: 3 }), node('s3', 'IfcBuildingStorey', { elevation: 9 })];
    expect(stopsBetween(storeys, 's0', 's2').map((s) => s.id)).toEqual(['s0', 's1', 's2']);
    expect(stopsBetween(storeys, 's1', undefined).map((s) => s.id)).toEqual(['s1']);
    expect(toggleStop([], 's1', false)).toEqual(['s1']);
    expect(toggleStop(['s1', 's2'], 's1', true)).toEqual(['s2']);
  });

  it('moves it by its middle and turns its doors to the cursor', () => {
    const lift = node('e', 'IfcTransportElement', { at: [2, 3], rotation: 0 });
    const [move, turn] = elevatorGrips(lift, 1);
    expect(move.at).toEqual({ x: 2, y: 3 });
    expect(turn.at.y).toBeLessThan(3);
    expect(turn.step!({ x: 5, y: 3 })).toEqual({ command: 'element.transform', args: { ids: ['e'], op: 'rotate', center: [2, 3], angle: 90 } });
    expect(move.step!({ x: 3, y: 4 })).toEqual({ command: 'element.transform', args: { ids: ['e'], op: 'move', by: [1, 1] } });
  });

  it('draws the preview\'s plan with y up', () => {
    const i = { kind: 'wall', closed: true, points: [0, 0, 2, 0, 2, 1] };
    expect(itemPath(i)).toBe('M0.0000 0.0000 L2.0000 0.0000 L2.0000 -1.0000 Z');
    expect(planBox([i], 0)).toEqual([0, -1, 2, 1]);
  });
});
