/** The material graphics configurator's payloads (stage 0b, decision 670). */

import { describe, expect, it } from 'vitest';
import { splitAlpha, withOpacity } from '../../viewport/drawing';
import {
  copyArgs, draftGraphics, fieldPatch, groupPatch, inheritedText, isSet, parseNumber, parseOpacity, parseWeight, previewArgs, sameGraphics, swatchFill, swatchInk, updateArgs,
  type MaterialRow, type ResolvedGraphics,
} from './materialGraphicsModel';

const resolved = (extra: Partial<ResolvedGraphics> = {}): ResolvedGraphics => ({ hatch: null, hatchScale: 1, fill: null, membrane: false, drawJoints: false, source: 'name', ...extra });
const row = (id: string, own: MaterialRow['own'] = {}): MaterialRow => ({ id, name: id, color: '#aa5522', inUse: false, section: resolved(), plan: resolved(), elevation: resolved(), own });

describe('a typed value', () => {
  it('reads a comma as a point, and refuses what is not a number', () => {
    expect(parseNumber(' 0,35 ')).toBe(0.35);
    expect(parseNumber('')).toBeNaN();
    expect(parseNumber('x')).toBeNaN();
  });
  it('keeps a weight within 0.05 to 5 mm and an opacity within 0 to 100', () => {
    expect(parseWeight('0.25')).toBe(0.25);
    expect(parseWeight('0,05')).toBe(0.05);
    expect(parseWeight('0.04')).toBeNull();
    expect(parseWeight('5.1')).toBeNull();
    expect(parseOpacity('40.4')).toBe(40);
    expect(parseOpacity('101')).toBeNull();
    expect(parseOpacity('-1')).toBeNull();
  });
});

describe('a field\'s change', () => {
  it('sends the value, or a reset to take it back to the default (hatch and fill too: null would mean none)', () => {
    expect(fieldPatch('hatchWeight', 0.35)).toEqual({ hatchWeight: 0.35 });
    expect(fieldPatch('hatch', null)).toEqual({ hatch: null });
    expect(fieldPatch('hatch', undefined)).toEqual({ reset: ['hatch'] });
    expect(fieldPatch('halftone', false)).toEqual({ halftone: false });
  });
  it('knows a field the material sets from one it inherits', () => {
    expect(isSet({ hatchColor: '#ff0000' }, 'hatchColor')).toBe(true);
    expect(isSet({ hatch: null }, 'hatch')).toBe(true);
    expect(isSet({}, 'hatchColor')).toBe(false);
    expect(isSet(undefined, 'hatchColor')).toBe(false);
  });
  it('shows the inherited value as the placeholder, nothing for a group', () => {
    expect(inheritedText(resolved({ hatchScale: 2 }), 'hatchScale')).toBe('2');
    expect(inheritedText(resolved({ cutLine: { weight: 0.5 } }), 'cutLine')).toBe('');
    expect(inheritedText(resolved(), 'hatchWeight')).toBe('');
  });
});

describe('a group\'s change (the engine replaces a group whole)', () => {
  it('sends the whole group with the part changed', () => {
    expect(groupPatch({ cutLine: { weight: 0.5 } }, 'cutLine', 'color', '#112233')).toEqual({ cutLine: { weight: 0.5, color: '#112233' } });
    expect(groupPatch(undefined, 'separatorLine', 'lineType', 'dashed')).toEqual({ separatorLine: { lineType: 'dashed' } });
  });
  it('takes a part back to the default and resets the group when nothing is left', () => {
    expect(groupPatch({ cutLine: { weight: 0.5, color: '#112233' } }, 'cutLine', 'color', undefined)).toEqual({ cutLine: { weight: 0.5 } });
    expect(groupPatch({ cutLine: { weight: 0.5 } }, 'cutLine', 'weight', undefined)).toEqual({ reset: ['cutLine'] });
  });
  it('keeps a background hatch only with its pattern', () => {
    expect(groupPatch(undefined, 'backgroundHatch', 'hatch', 'ANSI31')).toEqual({ backgroundHatch: { hatch: 'ANSI31' } });
    expect(groupPatch({ backgroundHatch: { hatch: 'ANSI31', hatchScale: 2 } }, 'backgroundHatch', 'hatchScale', 3)).toEqual({ backgroundHatch: { hatch: 'ANSI31', hatchScale: 3 } });
    expect(groupPatch({ backgroundHatch: { hatch: 'ANSI31', hatchScale: 2 } }, 'backgroundHatch', 'hatch', undefined)).toEqual({ reset: ['backgroundHatch'] });
  });
});

describe('the command\'s arguments', () => {
  it('names one material with id and many with ids (one undo step)', () => {
    expect(updateArgs(['a'], 'plan', { hatchOpacity: 50 })).toEqual({ id: 'a', plan: { hatchOpacity: 50 } });
    expect(updateArgs(['a', 'b'], 'section', { reset: ['hatch'] })).toEqual({ ids: ['a', 'b'], section: { reset: ['hatch'] } });
  });
  it('copies a material\'s own graphics, replacing the targets\', and clears the kinds it has none for', () => {
    const from = row('src', { section: { hatch: 'ANSI31', hatchColor: '#ff0000', cutLine: { weight: 0.7 } }, elevation: {} });
    const args = copyArgs(['x', 'y'], from);
    expect(args).toEqual({ ids: ['x', 'y'], replace: true, section: { hatch: 'ANSI31', hatchColor: '#ff0000', cutLine: { weight: 0.7 } }, plan: null, elevation: null });
    // A copy, not the same object.
    expect((args.section as object)).not.toBe(from.own.section);
  });
});

describe('the preview\'s question', () => {
  it('names the material, kind, scale and layout, with the draft only when there is one', () => {
    expect(previewArgs('m1', 'section', 50, 'strip', {})).toEqual({ id: 'm1', kind: 'section', scale: 50, layout: 'strip', width: 60, height: 15 });
    const q = previewArgs('m1', 'plan', 20, 'band', { hatchOpacity: 30, halftone: undefined });
    expect(q).toMatchObject({ kind: 'plan', scale: 20, layout: 'band', height: 30, graphics: { hatchOpacity: 30 } });
    expect(draftGraphics({ a: undefined })).toBeUndefined();
  });
});

describe('a swatch', () => {
  it('fills with the material\'s colour or its own, at the fill\'s opacity', () => {
    expect(swatchFill(resolved(), '#aa5522')).toBeNull();
    expect(swatchFill(resolved({ fill: 'material' }), '#aa5522')).toBe('#aa5522');
    expect(swatchFill(resolved({ fill: '#00ff00', fillOpacity: 50 }), '#aa5522')).toBe('#00ff0080');
  });
  it('draws its hatch in the hatch colour at its opacity', () => {
    expect(swatchInk(resolved({ hatchColor: '#ff0000', hatchOpacity: 40 }), '#000000')).toEqual({ color: '#ff0000', opacity: 0.4 });
    expect(swatchInk(resolved(), '#000000')).toEqual({ color: '#000000', opacity: 1 });
  });
});

describe('opacity carried in a colour (8-digit hex)', () => {
  it('splits #rrggbbaa into the colour and its opacity, and leaves other colours whole', () => {
    expect(splitAlpha('#ff000080')).toEqual({ color: '#ff0000', opacity: 0.502 });
    expect(splitAlpha('#ff0000ff')).toEqual({ color: '#ff0000', opacity: 1 });
    expect(splitAlpha('#ff0000')).toEqual({ color: '#ff0000', opacity: 1 });
    expect(splitAlpha(null)).toEqual({ color: null, opacity: 1 });
  });
  it('writes a percentage as the two digits the engine writes', () => {
    expect(withOpacity('#ff0000', 50)).toBe('#ff000080');
    expect(withOpacity('#ff000080', 100)).toBe('#ff0000');
    expect(withOpacity('#ff0000', undefined)).toBe('#ff0000');
  });
});

describe('a multi-selection', () => {
  it('knows when the materials share their graphics', () => {
    expect(sameGraphics([row('a', { section: { hatch: 'X' } }), row('b', { section: { hatch: 'X' } })])).toBe(true);
    expect(sameGraphics([row('a', { section: { hatch: 'X' } }), row('b')])).toBe(false);
  });
});
