/** Decision 802: a plain section's Limit to its ends, its checkbox in the view's settings and its strip in plan. */

import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import type { NodeDto } from '../../engine/protocol';
import { SectionFields, limitEndsCommand, limitsEnds, offersLimitEnds } from './sections';
import { sectionFarLine } from './ViewDepth';

const section = (props: Record<string, unknown>) => ({ id: 's', type: 'LapinView', name: 'Section 1', parentId: null, props: { kind: 'section', start: [0, 4], end: [10, 4], ...props } }) as unknown as NodeDto;
const box = (html: string) => html.match(/<input[^>]*data-field="section-limit-ends"[^>]*>/)?.[0] ?? null;

describe("a section's Limit to its ends (decision 802)", () => {
  it('shows a checked box for a new section and an unchecked one for a section saved before', () => {
    const on = box(renderToStaticMarkup(<dl><SectionFields view={section({ limitEnds: true })} /></dl>));
    const off = box(renderToStaticMarkup(<dl><SectionFields view={section({})} /></dl>));
    expect(on).toContain('type="checkbox"');
    expect(on).toContain('checked');
    expect(off).not.toBeNull();
    expect(off).not.toContain('checked');
    expect(renderToStaticMarkup(<dl><SectionFields view={section({})} /></dl>)).toContain('Limit to its ends');
  });

  it('is offered only on a plain straight line', () => {
    expect(offersLimitEnds(section({}))).toBe(true);
    expect(offersLimitEnds(section({ pts: [[0, 0], [5, 0], [5, 2], [10, 2]] }))).toBe(false);
    expect(offersLimitEnds(section({ curve: 'arc', pts: [[0, 0], [10, 0], [5, 2]] }))).toBe(false);
    expect(offersLimitEnds(section({ along: 'w' }))).toBe(false);
    expect(offersLimitEnds(section({ across: 'w' }))).toBe(false);
    expect(box(renderToStaticMarkup(<dl><SectionFields view={section({ pts: [[0, 0], [5, 0], [5, 2], [10, 2]] })} /></dl>))).toBeNull();
  });

  it('turns on and off by view.setLine, one undo step', () => {
    expect(limitsEnds(section({ limitEnds: true }))).toBe(true);
    expect(limitsEnds(section({}))).toBe(false);
    expect(limitEndsCommand(section({}), true)).toEqual({ command: 'view.setLine', args: { id: 's', limitEnds: true } });
    expect(limitEndsCommand(section({ limitEnds: true }), false)).toEqual({ command: 'view.setLine', args: { id: 's', limitEnds: false } });
  });

  it('draws the strip it sees in plan: the sides out to its far line', () => {
    const [closed] = sectionFarLine(section({ limitEnds: true, depth: 3 }));
    expect(closed.closed).toBe(true);
    expect(closed.points).toEqual([{ x: 0, y: 4 }, { x: 10, y: 4 }, { x: 10, y: 7 }, { x: 0, y: 7 }]);
    // Without end: the two sides, as far as its far grip (half its length, 2 m at least), open at the far end.
    const [open] = sectionFarLine(section({ limitEnds: true }));
    expect(open.closed).toBe(false);
    expect(open.points).toEqual([{ x: 0, y: 9 }, { x: 0, y: 4 }, { x: 10, y: 4 }, { x: 10, y: 9 }]);
    // Not limited: as before, only the far line once it has a depth.
    expect(sectionFarLine(section({}))).toEqual([]);
    expect(sectionFarLine(section({ depth: 3 }))[0].points).toEqual([{ x: 0, y: 7 }, { x: 10, y: 7 }]);
  });
});
