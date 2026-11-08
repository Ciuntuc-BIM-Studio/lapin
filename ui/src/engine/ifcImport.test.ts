/** The IFC import's choice (what the user keeps of a surveyed file) and its progress, as the engine reports it. */

import { describe, expect, it, vi } from 'vitest';

vi.mock('./session', () => ({ engine: { onEvent: () => () => undefined, call: vi.fn() } }));
vi.mock('./client', () => ({ LONG_TIMEOUT_MS: 1 }));

import { engine } from './session';
import { choiceOf, chosenCount, fragOutcome, onProgress, outcome, runIfcImport, storeysText, useIfcImport, type Survey } from './ifcImport';
import { useCamera, type CameraApi } from '../viewport/camera';

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
  it('opens an IFC as a project only when asked, never for a link', async () => {
    const call = vi.mocked(engine.call);
    call.mockResolvedValue({ elements: 1, updated: 0, references: 0, notes: [] });
    const choice = { kinds: null, storeys: null, mode: 'auto' as const };
    useIfcImport.setState({ source: { name: 'house.ifc', path: '/x/house.ifc' }, format: 'ifc', asProject: true });
    await runIfcImport(choice);
    expect(call.mock.calls.at(-1)?.[1]).toMatchObject({ path: '/x/house.ifc', asProject: true });
    // Once done, the next import goes into the project.
    expect(useIfcImport.getState().asProject).toBe(false);
    useIfcImport.setState({ source: { name: 'house.ifc', path: '/x/house.ifc' }, format: 'ifc' });
    await runIfcImport(choice);
    expect(call.mock.calls.at(-1)?.[1]).not.toHaveProperty('asProject');
    useIfcImport.setState({ source: { name: 'house.ifc', path: '/x/house.ifc' }, format: 'ifc', asProject: true });
    await runIfcImport({ ...choice, mode: 'link' });
    expect(call.mock.calls.at(-1)?.[1]).not.toHaveProperty('asProject');
  });

  it('says what the file\'s storeys did to the project\'s', () => {
    expect(storeysText({ merged: [{ storey: 'Ground floor', into: 'E00' }], removed: ['Foundations', 'First floor'], kept: ['Mezzanine'] }))
      .toBe(" The file's storeys replace the project's: „Ground floor” merged into „E00”. Empty storeys removed: „Foundations”, „First floor”. Kept, since they hold elements: „Mezzanine”.");
    expect(storeysText({ merged: [], removed: [], kept: [] })).toBe('');
    expect(fragOutcome('hub.bos', { elements: 2, shared: 0, storeys: 3, skipped: 0, notes: [], replaced: { merged: [], removed: ['Ground floor'], kept: [] } }))
      .toBe("Imported hub.bos: 2 elements, 3 storeys made. The file's storeys replace the project's. Empty storeys removed: „Ground floor”.");
  });

  it('frames the imported model when it is in, unless the camera was moved meanwhile', async () => {
    const fit = vi.fn();
    useCamera.setState({ api: { fit } as unknown as CameraApi, azimuth: 0, pitch: -0.5, distance: 30, target: { x: 0, y: 0, z: 0 } });
    const call = vi.mocked(engine.call);
    call.mockResolvedValue({ elements: 2, shared: 0, storeys: 0, skipped: 0, notes: [] });
    useIfcImport.setState({ source: { name: 'hub.bos', path: '/x/hub.bos' }, format: 'bos' });
    await runIfcImport({ kinds: null, storeys: null, mode: 'auto' });
    await new Promise((r) => setTimeout(r, 80));
    expect(fit).toHaveBeenCalledWith(false);
    // Moved while it came in: left where the user put it.
    fit.mockClear();
    call.mockImplementation(async () => { useCamera.setState({ distance: 12 }); return { elements: 2, shared: 0, storeys: 0, skipped: 0, notes: [] }; });
    useIfcImport.setState({ source: { name: 'hub.bos', path: '/x/hub.bos' }, format: 'bos' });
    await runIfcImport({ kinds: null, storeys: null, mode: 'auto' });
    await new Promise((r) => setTimeout(r, 80));
    expect(fit).not.toHaveBeenCalled();
    useCamera.setState({ api: null });
  });
});
