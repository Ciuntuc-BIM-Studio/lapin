import { describe, expect, it } from 'vitest';
import { classificationFields, codeIn, defNamed, freeSystemName, parseOptions, refsOf, refsText, suggestDefs } from './classModel';
import { allChoices, conflictsOf, defaultStandardsChoices, summarise } from '../standardsLibraryModel';

const systems = [{ id: 's1', name: 'Uniclass', items: 3, uses: 1 }, { id: 's2', name: 'CCI', items: 1, uses: 0 }];

describe('classifications', () => {
  it('reads the codes of a node and names its systems', () => {
    const refs = refsOf([{ system: 's1', code: 'Ss_25' }, { nope: 1 }, { system: 's2', code: '21' }]);
    expect(refs).toHaveLength(2);
    expect(codeIn(refs, systems, 'cci')).toBe('21');
    expect(refsText(refs, systems)).toBe('Uniclass Ss_25 · CCI 21');
  });
  it('offers the fields a tag reads', () => {
    expect(classificationFields(systems).map(([k]) => k)).toEqual(['classification.code', 'classification.title', 'classification.Uniclass', 'classification.CCI']);
  });
  it('names a new system freely and suggests shared definitions', () => {
    expect(freeSystemName('CCI', systems)).toBe('CCI 2');
    const defs = [{ id: '1', name: 'Fire rating', kind: 'text' }, { id: '2', name: 'Sound fire', kind: 'text' }];
    expect(suggestDefs(defs, 'fire').map((d) => d.id)).toEqual(['1', '2']);
    expect(defNamed(defs, ' fire RATING ')?.id).toBe('1');
    expect(parseOptions('a, b,,c')).toEqual(['a', 'b', 'c']);
  });
});

describe('the standards library', () => {
  const pack = { options: true, entries: [
    { kind: 'layer', id: 'a', name: 'A', exists: false, same: false }, { kind: 'text', id: 'b', name: 'B', exists: true, same: true }, { kind: 'dim', id: 'c', name: 'C', exists: true, same: false },
  ] };
  it('asks about what differs only', () => {
    expect(conflictsOf(pack).map((e) => e.id)).toEqual(['c']);
    expect(defaultStandardsChoices(pack)).toEqual({ 'dim:c': 'update' });
    expect(allChoices(pack, 'skip')).toEqual({ 'dim:c': 'skip' });
    expect(summarise(pack)).toEqual({ added: 1, same: 1, differ: 1 });
  });
});
