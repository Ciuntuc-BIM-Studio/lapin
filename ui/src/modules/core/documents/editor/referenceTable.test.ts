import { describe, expect, it } from 'vitest';
import { NO_REFERENCE_FILTER, readReferenceFilter, REFERENCE_LIST, tableBase, withReferenceFilter } from './referenceTable';

describe("the reference documents' table filter", () => {
  it('reads the filter after the source, and leaves other keys aside', () => {
    expect(readReferenceFilter('references:documents?type=aviz,acord&folder=permits&status=received&x=1')).toEqual({ type: 'aviz,acord', folder: 'permits', status: 'received' });
    expect(readReferenceFilter(REFERENCE_LIST)).toEqual(NO_REFERENCE_FILTER);
    expect(readReferenceFilter('references:documents?folder=input-cu')).toEqual({ ...NO_REFERENCE_FILTER, folder: 'input-cu' });
  });

  it('writes a filter back, the base alone when it keeps everything', () => {
    expect(withReferenceFilter(REFERENCE_LIST, NO_REFERENCE_FILTER)).toBe(REFERENCE_LIST);
    expect(withReferenceFilter(REFERENCE_LIST, { type: 'aviz,acord', folder: '', status: 'all' })).toBe('references:documents?type=aviz,acord&status=all');
    const round = 'references:documents?type=cu&folder=permits-utilities&status=received';
    expect(withReferenceFilter(REFERENCE_LIST, readReferenceFilter(round))).toBe(round);
  });

  it('names a table by its source without the filter', () => {
    expect(tableBase('references:documents?type=aviz')).toBe(REFERENCE_LIST);
    expect(tableBase('schedule:abc')).toBe('schedule:abc');
  });
});
