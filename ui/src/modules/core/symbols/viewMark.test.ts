import { describe, expect, it } from 'vitest';
import { automaticMark, headSlot } from './viewMark';
import type { SlotInfo } from './SlotPicker';

const slot = (id: string, own: string | null = null, project: string | null = null): SlotInfo => ({ id, label: id, owner: 'section', category: 'section', project, own });
const mark = { name: 'Section 1', number: '3', sheet: 'A-101', own: null };

describe("a view's mark (report #167)", () => {
  it("is the section's name with Lapin's own head", () => {
    expect(automaticMark('section', [slot('sectionHead')], mark)).toBe('Section 1');
    expect(headSlot('elevation')).toBe('elevationHead');
  });

  it('is its number on its sheet with a head symbol, its own or the project\'s', () => {
    expect(automaticMark('section', [slot('sectionHead', 'builtin:sym:section-head')], mark)).toBe('3');
    expect(automaticMark('section', [slot('sectionHead', null, 'builtin:sym:section-head')], mark)).toBe('3');
    expect(automaticMark('section', [slot('sectionHead', 'builtin:sym:section-head')], { ...mark, number: '' })).toBeNull();
  });

  it("is a plan's number on its sheet (its title, its callouts)", () => {
    expect(automaticMark('plan', [], mark)).toBe('3');
    expect(automaticMark('plan', [], null)).toBeNull();
  });
});
