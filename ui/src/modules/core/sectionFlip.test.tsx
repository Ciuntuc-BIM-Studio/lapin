/** Forum topic 247: the Section tool's third click (the side it looks to, with its arrows) and Flip direction on a section. */

import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import type { NodeDto } from '../../engine/protocol';
import { SectionFields, canFlipSection, flipSectionCommand, sectionByThreeClicks, sectionGrips, sectionLook, sectionPreview } from './sections';

const section = (props: Record<string, unknown>) => ({ id: 's', type: 'LapinView', name: 'Section 1', parentId: null, props: { kind: 'section', start: [0, 0], end: [10, 0], ...props } }) as unknown as NodeDto;
const a = { x: 0, y: 0 }, b = { x: 10, y: 0 };

describe('the Section tool: start, end, then the side it looks to (forum topic 247)', () => {
  it('a third click on the left keeps the line as drawn; on the right it swaps the ends, so the section looks there', () => {
    expect(sectionByThreeClicks(a, b, { x: 5, y: 3 })).toEqual({ start: a, end: b });
    expect(sectionByThreeClicks(a, b, { x: 5, y: -3 })).toEqual({ start: b, end: a });
    // On the line, or no third point: the left, as before.
    expect(sectionByThreeClicks(a, b, { x: 5, y: 0 })).toEqual({ start: a, end: b });
    expect(sectionByThreeClicks(a, b)).toEqual({ start: a, end: b });
  });

  it('previews the line to the pointer, then arrows at both ends towards the pointer\'s side', () => {
    expect(sectionPreview([a], { x: 4, y: 1 })).toEqual([a, { x: 4, y: 1 }]);
    const ys = (pts: { y: number }[] | null) => (pts ?? []).map((p) => p.y);
    const below = sectionPreview([a, b], { x: 5, y: -3 })!;
    expect(Math.min(...ys(below))).toBeLessThan(0);
    expect(Math.max(...ys(below))).toBeCloseTo(0, 9);
    const above = sectionPreview([a, b], { x: 5, y: 3 })!;
    expect(Math.max(...ys(above))).toBeGreaterThan(0);
    expect(Math.min(...ys(above))).toBeCloseTo(0, 9);
    // Both ends are in it.
    expect(above.some((p) => p.x === 0 && p.y === 0) && above.some((p) => p.x === 10 && p.y === 0)).toBe(true);
  });
});

describe('Flip direction on a section (forum topic 247)', () => {
  it('a section looks left of its line; one across an element flipped looks right', () => {
    expect(sectionLook(section({}))).toEqual({ x: -0, y: 1 });
    expect(sectionLook(section({ across: 'w', flip: true }))).toEqual({ x: 0, y: -1 });
  });

  it('offers a flip arrow and a Flip direction button, both one view.flipSection', () => {
    const s = section({});
    expect(canFlipSection(s)).toBe(true);
    const flip = sectionGrips(s).find((g) => g.kind === 'flip')!;
    expect(flip.at).toEqual({ x: 5, y: 0 });
    // The arrow points where it will look once flipped: down.
    expect(flip.dir!.y).toBeLessThan(0);
    expect(flip.click!()).toEqual({ command: 'view.flipSection', args: { id: 's' } });
    expect(flipSectionCommand(s)).toEqual({ command: 'view.flipSection', args: { id: 's' } });
    expect(renderToStaticMarkup(<dl><SectionFields view={s} /></dl>)).toContain('data-action="section-flip"');
  });

  it('a section following an element has neither', () => {
    const along = section({ along: 'road' });
    expect(canFlipSection(along)).toBe(false);
    expect(sectionGrips(along).some((g) => g.kind === 'flip')).toBe(false);
    expect(renderToStaticMarkup(<dl><SectionFields view={along} /></dl>)).not.toContain('section-flip');
  });
});
