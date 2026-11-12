/** A view's partial structure display changed from the view (decision 500). */

import { describe, expect, it } from 'vitest';
import { STRUCTURE_MODES, structureLabel, styleStructure } from '../modules/core/viewStyles';
import { drawnInPlan } from './finishSnaps';
import { structureCommand } from './viewStructure';

describe('the partial structure display picker', () => {
  it('offers the four presets, the entire model first', () => {
    expect(STRUCTURE_MODES.map(([k]) => k)).toEqual(['all', 'noFinishes', 'core', 'coreLoadBearing']);
    expect(structureLabel('coreLoadBearing')).toBe('Core of load-bearing elements only');
    expect(structureLabel(undefined)).toBe('Entire model');
  });

  it('reads a style for a view of its kind: its plans\' own, its sections\' own, else its layers', () => {
    const style = { layers: 'all' as const, layersPlan: 'noFinishes' as const, layersSection: null };
    expect(styleStructure(style, 'plan')).toBe('noFinishes');
    expect(styleStructure(style, 'section')).toBe('all');
    expect(styleStructure(style, 'elevation')).toBe('all');
    expect(styleStructure(style, '3d')).toBe('all');
    expect(styleStructure({ layers: 'core' }, 'plan')).toBe('core');
  });

  it('sets the view\'s own in one step, takes the style\'s again when it is picked, and makes a level\'s default plan its plan view', () => {
    expect(structureCommand('v1', 'core', 'noFinishes', 'noFinishes')).toEqual({ command: 'view.setStyle', args: { id: 'v1', layers: 'core' } });
    expect(structureCommand('v1', 'noFinishes', 'core', 'noFinishes')).toEqual({ command: 'view.setStyle', args: { id: 'v1', layers: null } });
    expect(structureCommand('v1', 'core', 'core', 'noFinishes')).toBeNull();
    expect(structureCommand('plan:s1', 'all', 'noFinishes', 'noFinishes')).toEqual({ command: 'view.create', args: { kind: 'plan', storeyId: 's1', layers: 'all' } });
  });
});

describe('snapping to the layers the structure display draws', () => {
  const cut = (sub?: string) => ({ kind: 'cut', closed: true, points: [0, 0, 1, 0, 1, 1], sub });
  it('leaves out the cut pieces of the roles it leaves out, of every layered element', () => {
    expect(drawnInPlan('IfcWall', cut('insulation'), [], ['finishes', 'insulation', 'membranes'])).toBe(false);
    expect(drawnInPlan('IfcSlab', cut('finishes'), [], ['finishes'])).toBe(false);
    expect(drawnInPlan('IfcWall', cut('core'), [], ['finishes', 'insulation', 'membranes'])).toBe(true);
    expect(drawnInPlan('IfcWall', cut('insulation'), [], ['finishes'])).toBe(true);
    expect(drawnInPlan('IfcWall', cut(), [], ['finishes'])).toBe(true);
  });
});
