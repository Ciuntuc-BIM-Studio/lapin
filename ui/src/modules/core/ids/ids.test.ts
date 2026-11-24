import { describe, expect, it } from 'vitest';
import { failing, failureText, specLine, submitGate, totals, type IdsRun, type IdsSpec } from './ids';

const spec = (over: Partial<IdsSpec>): IdsSpec => ({
  index: 0, name: 'S', usage: 'required', status: 'pass', applicable: 2, passed: 2, failed: 0, failures: [], more: 0, elements: [], requirements: [], ...over,
});
const run = (specs: IdsSpec[]): IdsRun => ({
  source: 'model', exportMs: 1, ms: 2, stale: false, failing: specs.filter((s) => s.status === 'fail').length, blocks: true, onSubmit: true, blocking: false,
  files: [{ id: 'f', name: 'Office', file: 'office.ids', title: 'Office', schema: 'IFC4', objects: 3, ms: 1, failing: 0, passing: 0, skipped: 0, specifications: specs }],
});

describe('a specification in words', () => {
  it('counts what passes, says when nothing applies, and reads prohibited and skipped ones their own way', () => {
    expect(specLine(spec({ applicable: 5, passed: 3, failed: 2, status: 'fail' }))).toBe('3 of 5 pass');
    expect(specLine(spec({ applicable: 0, passed: 0, status: 'fail' }))).toBe('Nothing it applies to');
    expect(specLine(spec({ applicable: 0, passed: 0, usage: 'optional' }))).toBe('Nothing it applies to (optional)');
    expect(specLine(spec({ usage: 'prohibited', applicable: 0 }))).toBe('None, as asked');
    expect(specLine(spec({ usage: 'prohibited', applicable: 4, status: 'fail' }))).toBe('4 found, none allowed');
    expect(specLine(spec({ status: 'skipped' }))).toBe('Not for this IFC version');
  });

  it('a failure names the element, what it has and what is needed', () => {
    expect(failureText({ globalId: 'g', class: 'IfcWall', name: 'W1', facet: 0, requirement: 'Attribute Name', actual: 'Name = (none)', expected: 'Name filled in' }))
      .toBe('W1 (IfcWall): Name = (none) — Name filled in');
    expect(failureText({ globalId: 'g', class: 'IfcSlab', facet: 0, requirement: 'r', actual: 'a', expected: 'e' })).toBe('(IfcSlab): a — e');
  });
});

describe('a run', () => {
  const r = run([spec({ index: 0 }), spec({ index: 1, status: 'fail', failed: 1, passed: 1 }), spec({ index: 2, status: 'skipped' })]);
  it('totals its specifications and lists the failing ones with their file', () => {
    expect(totals(r)).toEqual({ pass: 1, fail: 1, skipped: 1 });
    expect(failing(r).map((x) => [x.file.id, x.spec.index])).toEqual([['f', 1]]);
    expect(totals(null)).toEqual({ pass: 0, fail: 0, skipped: 0 });
    expect(failing(null)).toEqual([]);
  });
});

describe('the gate before Submit for approval', () => {
  const failingRun = run([spec({ status: 'fail', failed: 2, passed: 0 }), spec({ index: 1, status: 'fail', failed: 1, passed: 1 })]);
  const passingRun = run([spec({})]);
  const state = { files: 1, onSubmit: true, blocks: true, running: false, failed: false };
  const off = { on: false, note: '' };
  it('lets the submission go with no IDS file, when the project does not run them, or when every specification passes', () => {
    expect(submitGate(null, { ...state, files: 0 }, off).blocked).toBe(false);
    expect(submitGate(null, { ...state, onSubmit: false }, off).blocked).toBe(false);
    expect(submitGate(passingRun, state, off)).toEqual({ blocked: false, why: null });
  });
  it('waits while checking and stops when the check could not run', () => {
    expect(submitGate(null, { ...state, running: true }, off)).toEqual({ blocked: true, why: 'Checking the IDS files…' });
    expect(submitGate(null, state, off).blocked).toBe(true);
    expect(submitGate(passingRun, { ...state, failed: true }, off).blocked).toBe(true);
  });
  it('stops a failing run unless overridden with a note, which goes with the submission', () => {
    expect(submitGate(failingRun, state, off).blocked).toBe(true);
    expect(submitGate(failingRun, state, { on: true, note: '   ' })).toEqual({ blocked: true, why: 'Say why it goes despite the IDS failures.' });
    expect(submitGate(failingRun, state, { on: true, note: ' Agreed with the client ' })).toEqual({ blocked: false, why: null, idsOverride: 'Agreed with the client', idsFailing: 2 });
  });
  it('lets failures through where the project says they do not block, still counting them', () => {
    expect(submitGate(failingRun, { ...state, blocks: false }, off)).toEqual({ blocked: false, why: null, idsFailing: 2 });
  });
});
