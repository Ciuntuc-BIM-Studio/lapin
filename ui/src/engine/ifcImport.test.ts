/** The IFC import's choice (what the user keeps of a surveyed file) and its progress, as the engine reports it. */

import { describe, expect, it, vi } from 'vitest';

vi.mock('./session', () => ({ engine: { onEvent: () => () => undefined, call: vi.fn() } }));
vi.mock('./client', () => ({ LONG_TIMEOUT_MS: 1 }));

import { choiceOf, chosenCount, onProgress, outcome, useIfcImport, type Survey } from './ifcImport';

const survey: Survey = {
  name: 'plant.ifc', size: 1, schema: 'IFC2X3', products: 10, noStorey: 2,
  storeys: [{ id: 'low', name: 'Low', elevation: 0, count: 6 }, { id: 'high', name: 'High', elevation: 4, count: 2 }],
  groups: [
    { discipline: 'structure', label: 'Structure', count: 6, kinds: [{ key: 'IfcBeam', class: 'IfcBeam', label: 'Beam', count: 4 }, { key: 'IfcColumn', class: 'IfcColumn', label: 'Column', count: 2 }] },
    { discipline: 'mep', label: 'MEP', count: 4, kinds: [{ key: 'IfcBuildingElementProxy|Mechanical Equipment', class: 'IfcBuildingElementProxy', category: 'Mechanical Equipment', label: 'Proxy · Mechanical Equipment', count: 4 }] },
  ],
};

describe('the IFC import', () => {
  it('sends no filter where everything is kept, and the kinds and storeys kept where some are not', () => {
    expect(choiceOf(survey, new Set(), new Set())).toEqual({ kinds: null, storeys: null });
    expect(choiceOf(survey, new Set(['IfcBuildingElementProxy|Mechanical Equipment']), new Set(['high', '']))).toEqual({ kinds: ['IfcBeam', 'IfcColumn'], storeys: ['low'] });
  });

  it('counts about what the choice brings', () => {
    expect(chosenCount(survey, new Set(), new Set())).toBe(10);
    // The MEP left out: six of ten.
    expect(chosenCount(survey, new Set(['IfcBuildingElementProxy|Mechanical Equipment']), new Set())).toBe(6);
    // And the high storey and those on none: six of ten of the six on the low storey, about four.
    expect(chosenCount(survey, new Set(['IfcBuildingElementProxy|Mechanical Equipment']), new Set(['high', '']))).toBe(4);
  });

  it('shows the engine’s progress while a call runs, and ignores it otherwise', () => {
    const progress = onProgress;
    progress({ stage: 'Building the model', done: 5, total: 10 });
    expect(useIfcImport.getState().busy).toBeNull();
    useIfcImport.setState({ busy: { title: 'Importing plant.ifc', stage: 'Reading the file', done: 0, total: 0, started: 0 } });
    progress({ stage: 'Building the model', done: 5, total: 10 });
    expect(useIfcImport.getState().busy).toMatchObject({ title: 'Importing plant.ifc', stage: 'Building the model', done: 5, total: 10 });
  });

  it('says what came in', () => {
    expect(outcome('plant.ifc', 'auto', { elements: 0, updated: 0, references: 3, recognized: 2, recognizedByClass: { IfcBeam: 2 }, notes: [] }))
      .toBe('Imported plant.ifc: 2 recognised (2 beams), 3 as reference.');
  });
});
