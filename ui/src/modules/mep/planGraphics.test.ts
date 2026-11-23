import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { coverageMode, coverageRadius, pale } from './planGraphics';

const node = (props: Record<string, unknown>): NodeDto => ({ id: 'n', type: 'IfcSensor', name: 'n', storeyId: 's', props } as unknown as NodeDto);

describe('MEP coverage circles', () => {
  it('draw as the view says, else the system, else on the selected devices', () => {
    expect(coverageMode(undefined, undefined)).toBe('selected');
    expect(coverageMode(node({}), node({ coverage: 'all' }))).toBe('all');
    expect(coverageMode(node({ mepCoverage: 'none' }), node({ coverage: 'all' }))).toBe('none');
    expect(coverageMode(node({ mepCoverage: 'odd' }), node({}))).toBe('selected');
  });

  it('reach the node’s own radius, a hydrant’s hose and jet, else the role’s, as the engine’s', () => {
    expect(coverageRadius(node({}), { coverage: 5.8 })).toBe(5.8);
    expect(coverageRadius(node({ coverage: 4 }), { coverage: 5.8 })).toBe(4);
    expect(coverageRadius(node({}), { hoseLength: 20, jetLength: 3 })).toBe(23);
    expect(coverageRadius(node({ hoseLength: 30 }), { hoseLength: 20, jetLength: 3 })).toBe(33);
    expect(coverageRadius(node({}), undefined)).toBe(0);
  });

  it('pale their system’s colour as the engine does', () => {
    expect(pale('#e53935')).toBe('#f6bab8');
    expect(pale('nope')).toBe('#c8c8c8');
  });
});
