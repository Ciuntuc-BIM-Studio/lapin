import { describe, expect, it } from 'vitest';
import { byKind, defaultProjectChoices, importArgs, typeConflicts, type SourceType } from './projectTypes';

const t = (ref: string, name: string, status: SourceType['status'], differs?: string[], label = 'Wall type'): SourceType =>
  ({ ref, class: 'IfcWallType', label, name, count: 0, materials: [], status, exists: status !== 'new', differs });

describe('importing types from a project', () => {
  const types = [t('a', 'WT1', 'new'), t('b', 'WT2', 'same'), t('c', 'WT3', 'name', ['layers']), t('d', 'WT4', 'id', ['mark']), t('e', 'D1', 'new', undefined, 'Door type')];

  it('asks only for the types the project has made otherwise', () => {
    expect(typeConflicts(types).map((x) => x.key)).toEqual(['c', 'd']);
  });

  it('replaces a type of the same GlobalId, keeps one of the same name beside, keeps the project\'s materials', () => {
    const c = defaultProjectChoices({ types, materials: [{ name: 'Brick', differs: ['colour'] }] });
    expect(c.types).toEqual({ c: 'both', d: 'update' });
    expect(c.materials).toEqual({ Brick: 'keep' });
  });

  it('sends the chosen types and their choices only', () => {
    const a = importArgs({ text: '{}' }, new Set(['a', 'd']), { materials: { Brick: 'update' }, types: { c: 'skip', d: 'both' } });
    expect(a).toEqual({ text: '{}', refs: ['a', 'd'], materials: { Brick: 'update' }, types: { d: 'both' } });
  });

  it('lists by kind, then name', () => {
    expect(byKind(types).map((x) => x.name)).toEqual(['D1', 'WT1', 'WT2', 'WT3', 'WT4']);
  });
});
