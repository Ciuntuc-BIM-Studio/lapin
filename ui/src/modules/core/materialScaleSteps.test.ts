import { describe, expect, it } from 'vitest';
import {
  addStep, duplicateStep, firstStepAt, moveStep, newStep, parseScaleDenominator, removeStep, sampleScale, setStepField, setStepGroup, setStepRange,
  simplifySteps, stepCovers, stepFields, stepProblem, stepsOf, stepsPatch, stepsProblem, updateArgs, type ScaleStep,
} from './materialGraphicsModel';
import { parseCutFrom, smallScaleCutPatch } from './viewStyles';

const A: ScaleStep = { to: 50, hatchColor: '#ff0000' };
const B: ScaleStep = { from: 100, to: 199, hatch: null, fill: '#b3b3b3' };
const C: ScaleStep = { from: 200, hatch: 'SOLID' };

describe('scale steps', () => {
  it('builds the engine payload of the example', () => {
    expect(updateArgs(['m1'], 'section', stepsPatch([A, B, C]))).toEqual({ id: 'm1', section: { byScale: [A, B, C] } });
    expect(stepsPatch([])).toEqual({ reset: ['byScale'] });
  });
  it('reads the raw steps', () => {
    expect(stepsOf({ byScale: [A] } as never)).toEqual([A]);
    expect(stepsOf(undefined)).toEqual([]);
  });
  it('validates like the engine', () => {
    expect(stepProblem(A)).toBeNull();
    expect(stepProblem({ hatchScale: 1 })).toMatch(/needs a scale/);
    expect(stepProblem({ from: 0, hatchScale: 1 })).toMatch(/whole number/);
    expect(stepProblem({ from: 100001, hatchScale: 1 })).toMatch(/whole number/);
    expect(stepProblem({ from: 1.5, hatchScale: 1 })).toMatch(/whole number/);
    expect(stepProblem({ from: 200, to: 100, hatchScale: 1 })).toMatch(/larger/);
    expect(stepProblem({ from: 100 })).toMatch(/at least one field/);
    expect(stepProblem({ from: 100, nope: 1 } as never)).toMatch(/cannot set/);
    expect(stepProblem({ from: 100, to: 100, hatch: null })).toBeNull();
    expect(stepsProblem([A, { from: 5 }])).toEqual({ index: 1, text: 'A step sets at least one field' });
    expect(stepsProblem(Array.from({ length: 21 }, () => A))?.text).toBe('At most 20 steps');
    expect(stepsProblem(Array.from({ length: 20 }, () => A))).toBeNull();
  });
  it('parses scale denominators', () => {
    expect(parseScaleDenominator('100')).toBe(100);
    expect(parseScaleDenominator('1:200')).toBe(200);
    expect(parseScaleDenominator('0')).toBeNull();
    expect(parseScaleDenominator('1.5')).toBeNull();
    expect(parseScaleDenominator('x')).toBeNull();
    expect(parseScaleDenominator('100000')).toBe(100000);
    expect(parseScaleDenominator('100001')).toBeNull();
    expect(parseCutFrom('1:200')).toBe(200);
    expect(parseCutFrom('')).toBeNull();
  });
  it('knows which step applies: the first that covers the scale', () => {
    expect(stepCovers(B, 100)).toBe(true);
    expect(stepCovers(B, 199)).toBe(true);
    expect(stepCovers(B, 200)).toBe(false);
    expect(firstStepAt([A, B, C], 20)).toBe(0);
    expect(firstStepAt([A, B, C], 50)).toBe(0);
    expect(firstStepAt([A, B, C], 100)).toBe(1);
    expect(firstStepAt([A, B, C], 500)).toBe(2);
    expect(firstStepAt([A, B, C], 75)).toBe(-1);
    expect(firstStepAt([{ from: 10, hatch: null }, { from: 10, hatch: 'SOLID' }], 20)).toBe(0);
    expect(sampleScale(A)).toBe(50);
    expect(sampleScale(B)).toBe(100);
  });
  it('adds, removes, reorders and duplicates', () => {
    const s = [A, B, C];
    expect(moveStep(s, 0, 1)).toEqual([B, A, C]);
    expect(moveStep(s, 2, -1)).toEqual([A, C, B]);
    expect(moveStep(s, 0, -1)).toEqual(s);
    expect(moveStep(s, 2, 1)).toEqual(s);
    expect(removeStep(s, 1)).toEqual([A, C]);
    expect(duplicateStep(s, 1)).toEqual([A, B, B, C]);
    expect(duplicateStep(s, 1)[1]).not.toBe(duplicateStep(s, 1)[2]);
    expect(addStep([A], C)).toEqual([A, C]);
    expect(s).toEqual([A, B, C]);
  });
  it('edits ranges and fields', () => {
    expect(setStepRange([B], 0, 'to', undefined)).toEqual([{ from: 100, hatch: null, fill: '#b3b3b3' }]);
    expect(setStepRange([B], 0, 'from', 150)[0].from).toBe(150);
    expect(setStepField([A], 0, 'hatch', null)[0]).toEqual({ to: 50, hatchColor: '#ff0000', hatch: null });
    expect(setStepField([A], 0, 'hatchColor', undefined)[0]).toEqual({ to: 50 });
    expect(stepFields(setStepField([A], 0, 'hatchColor', undefined)[0])).toEqual([]);
    expect(setStepGroup([A], 0, 'cutLine', 'weight', 0.5)[0].cutLine).toEqual({ weight: 0.5 });
    expect(setStepGroup([{ ...A, cutLine: { weight: 0.5 } }], 0, 'cutLine', 'weight', undefined)[0].cutLine).toBeUndefined();
    expect(setStepGroup([A], 0, 'backgroundHatch', 'hatchScale', 2)[0].backgroundHatch).toBeUndefined();
  });
  it('offers a valid new step and the simplify preset', () => {
    expect(stepsProblem([newStep(undefined)])).toBeNull();
    expect(stepsProblem(simplifySteps())).toBeNull();
    expect(simplifySteps()).toEqual([B, C]);
  });
  it('builds the small-scale cut patch', () => {
    expect(smallScaleCutPatch({ mode: 'solid', from: 200, color: '#222222' })).toEqual({ smallScaleCut: { mode: 'solid', from: 200, color: '#222222' } });
  });
});
