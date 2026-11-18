import { describe, expect, it } from 'vitest';
import { alignAttr, alignOf, styleOptions } from './textProps';

const styles = [{ id: 'standard', name: 'Standard', height: 2.5 }, { id: 'text-1a2b3c4d', name: 'Title', height: 5 }];

describe("a drafted text's properties (forum topic 148)", () => {
  it('lists the text styles by name and chooses the text\'s by its id', () => {
    const o = styleOptions(styles, 'text-1a2b3c4d');
    expect(o.value).toBe('text-1a2b3c4d');
    expect(o.options.map((x) => x.label)).toEqual(['Standard (2.5 mm)', 'Title (5 mm)']);
  });

  it('finds a style typed by its name before, and keeps one the standards no longer have', () => {
    expect(styleOptions(styles, ' title ').value).toBe('text-1a2b3c4d');
    const gone = styleOptions(styles, 'Old');
    expect(gone.value).toBe('Old');
    expect(gone.options.at(-1)).toEqual({ id: 'Old', label: 'Old' });
    expect(styleOptions(styles, undefined).value).toBe('');
  });

  it('keeps left as no alignment, centre and right as themselves', () => {
    expect([alignAttr('left'), alignAttr('center'), alignAttr('right'), alignAttr('')]).toEqual([null, 'center', 'right', null]);
    expect([alignOf(undefined), alignOf('center'), alignOf('justify')]).toEqual(['left', 'center', 'left']);
  });
});
