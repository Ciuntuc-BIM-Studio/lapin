import { beforeEach, describe, expect, it } from 'vitest';
import {
  applyEntries, entriesOf, headerSignature, layerMappingText, mergeOptions, missingRequired, optionDefaults, parseMappingFile, recallLayer, recallMapping, rememberLayers, rememberMapping,
  setColumnKey, setColumnUnit, tableMappingText, type TableTarget,
} from './tableModel';

const store: Record<string, string> = {};
beforeEach(() => {
  for (const k of Object.keys(store)) delete store[k];
  (globalThis as { localStorage?: unknown }).localStorage = { getItem: (k: string) => store[k] ?? null, setItem: (k: string, v: string) => { store[k] = v; }, removeItem: (k: string) => { delete store[k]; } };
});

describe('the header signature', () => {
  it('ignores case, accents, punctuation and the order of the headings', () => {
    expect(headerSignature('analysis.loads', ['Nivel', 'Sarcină (kN)', 'X'])).toBe(headerSignature('analysis.loads', ['x', 'SARCINA kn', 'nivel']));
    expect(headerSignature('a', ['x'])).not.toBe(headerSignature('b', ['x']));
  });
});

describe('the mapping memory', () => {
  const headers = ['Storey', 'Load kN', 'Note'];
  it('gives back what was kept, for the same headings in another order', () => {
    rememberMapping('analysis.loads', headers, [{ column: 0, key: 'storey' }, { column: 1, key: 'value', unit: 'kN' }]);
    expect(recallMapping('analysis.loads', ['Note', 'Load kN', 'Storey'])).toEqual([{ column: 1, key: 'value', unit: 'kN' }, { column: 2, key: 'storey' }]);
  });
  it('has nothing for other headings', () => { expect(recallMapping('analysis.loads', ['A', 'B'])).toBeNull(); });
  it('survives a storage that throws', () => {
    (globalThis as { localStorage?: unknown }).localStorage = { getItem: () => { throw new Error('no'); }, setItem: () => { throw new Error('no'); } };
    expect(() => rememberMapping('t', ['a'], [])).not.toThrow();
    expect(recallMapping('t', ['a'])).toBeNull();
  });
  it('applies entries by heading and reports none when nothing matches', () => {
    expect(applyEntries(['a', 'b'], [{ header: 'B', key: 'x' }])).toEqual([{ column: 1, key: 'x' }]);
    expect(applyEntries(['a'], [{ header: 'z', key: 'x' }])).toBeNull();
    expect(entriesOf(['a', ''], [{ column: 0, key: 'k' }])).toEqual([{ header: 'a', key: 'k' }]);
  });
});

describe('choosing a column', () => {
  it('takes a target column from the column that had it', () => {
    const m = setColumnKey([{ column: 0, key: 'value' }], 2, 'value');
    expect(m).toEqual([{ column: 2, key: 'value' }]);
  });
  it('lets any number of columns be open, and clears with null', () => {
    let m = setColumnKey([], 0, 'open'); m = setColumnKey(m, 1, 'open');
    expect(m).toHaveLength(2);
    expect(setColumnKey(m, 0, null)).toEqual([{ column: 1, key: 'open' }]);
  });
  it('sets and clears a unit', () => {
    const m = setColumnUnit([{ column: 0, key: 'v' }], 0, 'kN');
    expect(m[0].unit).toBe('kN');
    expect(setColumnUnit(m, 0, null)).toEqual([{ column: 0, key: 'v' }]);
  });
  it('lists the required columns left unmapped', () => {
    const t: TableTarget = { id: 't', label: 'T', module: 'm', open: false, columns: [{ key: 'a', label: 'A', kind: 'text', required: true }, { key: 'b', label: 'B', kind: 'text', required: true }] };
    expect(missingRequired(t, [{ column: 0, key: 'a' }]).map((c) => c.key)).toEqual(['b']);
    expect(missingRequired(t, [], (k) => k === 'a').map((c) => c.key)).toEqual(['a', 'b'].filter((k) => k !== 'a'));
  });
});

describe('the .lapinmapping file', () => {
  it('writes and reads a table mapping', () => {
    const text = tableMappingText('analysis.loads', ['Storey', 'Load'], [{ column: 1, key: 'value', unit: 'kN' }]);
    const f = parseMappingFile(text);
    expect(f).toEqual({ app: 'lapin', kind: 'table', version: 1, target: 'analysis.loads', mapping: [{ header: 'Storey', key: null }, { header: 'Load', key: 'value', unit: 'kN' }] });
  });
  it('writes and reads a layer map', () => {
    const layers = [{ layer: 'A-WALL', converter: 'walls', options: { height: 2.8 } }, { layer: 'A-DOOR', converter: null, options: {} }];
    expect(parseMappingFile(layerMappingText(layers))).toEqual({ app: 'lapin', kind: 'layers', version: 1, layers });
  });
  it('refuses what is not one, and a newer one', () => {
    expect(() => parseMappingFile('nope')).toThrow(/not a Lapin mapping/);
    expect(() => parseMappingFile('{"app":"x"}')).toThrow(/not a Lapin mapping/);
    expect(() => parseMappingFile('{"app":"lapin","kind":"table","version":9,"target":"t","mapping":[]}')).toThrow(/newer/);
  });
});

describe('converter options', () => {
  const options = [{ key: 'height', label: 'H', kind: 'length', default: 2.8 }, { key: 'join', label: 'J', kind: 'choice', default: 'a', choices: ['a', 'b'] }, { key: 'type', label: 'T', kind: 'type', typeClass: 'IfcWallType' }];
  it('starts at the defaults', () => { expect(optionDefaults(options)).toEqual({ height: 2.8, join: 'a' }); });
  it('keeps what fits and drops what does not', () => {
    expect(mergeOptions(options, { height: 3, join: 'z', type: 'w1', extra: 1 })).toEqual({ height: 3, join: 'a', type: 'w1' });
  });
  it('remembers layers on this computer', () => {
    rememberLayers([{ layer: 'A-WALL', converter: 'walls', options: { height: 3 } }]);
    expect(recallLayer('A-WALL')).toEqual({ converter: 'walls', options: { height: 3 } });
    expect(recallLayer('X')).toBeNull();
  });
});
