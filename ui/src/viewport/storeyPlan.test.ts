import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { ownPlanOf, planViewIdOf, storeyPlanView } from './storeyPlan';

const node = (id: string, props: Record<string, unknown>, type = 'LapinView'): NodeDto => ({ id, type, name: id, props } as unknown as NodeDto);

describe('a level’s plan (report #113)', () => {
  it('is the default plan until the level has a floor plan view of its own, then that view', () => {
    const nodes = new Map<string, NodeDto>([['st', node('st', {}, 'IfcBuildingStorey')]]);
    expect(planViewIdOf(nodes, 'st', null)).toBe('plan:st');
    expect(ownPlanOf(nodes, 'st', null)).toBeNull();
    // Site and foundation plans stand on the level too, but are not its plan.
    nodes.set('site', node('site', { kind: 'plan', storeyId: 'st', site: 'situation' }));
    nodes.set('fnd', node('fnd', { kind: 'plan', storeyId: 'st', foundation: true }));
    expect(storeyPlanView(nodes, 'st')).toBeUndefined();
    // The view the DXF went into: shown when the project opens again, with no plan view chosen.
    nodes.set('v1', node('v1', { kind: 'plan', storeyId: 'st' }));
    nodes.set('v2', node('v2', { kind: 'plan', storeyId: 'st' }));
    expect(planViewIdOf(nodes, 'st', null)).toBe('v1');
    expect(ownPlanOf(nodes, 'st', null)).toBe('v1');
    // A plan view chosen stays chosen; one of another level does not count.
    expect(planViewIdOf(nodes, 'st', 'v2')).toBe('v2');
    nodes.set('v3', node('v3', { kind: 'plan', storeyId: 'other' }));
    expect(planViewIdOf(nodes, 'st', 'v3')).toBe('v1');
  });
});
