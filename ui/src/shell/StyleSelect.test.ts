import { describe, expect, it } from 'vitest';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { readFileSync } from 'node:fs';
import { StyleSelect, sameAs } from './StyleSelect';
import { styleModified } from '../modules/architecture/JoineryDialog';
import { bayMatches, type BaySpec } from '../modules/architecture/BayFields';

const groups = [{ items: [{ id: 'modern', label: 'Modern' }, { id: 'georgian', label: 'Georgian' }] }];
const render = (value: string | null, modified = false) => renderToStaticMarkup(createElement(StyleSelect, { groups, value, modified, field: 'style', onApply: () => {} }));

describe('a configurator\'s style select (report #111)', () => {
  it('offers "Apply a style…" only while no style was applied', () => {
    const none = render(null);
    expect(none).toContain('data-style-state="none"');
    expect(none).toContain('Apply a style…');
    const applied = render('georgian');
    expect(applied).not.toContain('Apply a style…');
    expect(applied).toContain('data-style-state="applied"');
    expect(applied).toMatch(/<option value="georgian" selected="">Georgian<\/option>/);
  });

  it('names the style "(modified)" once a field differs, the style itself still there to apply again', () => {
    const html = render('georgian', true);
    expect(html).toContain('data-style-state="modified"');
    expect(html).toMatch(/selected=""[^>]*>Georgian \(modified\)<\/option>/);
    expect(html).toMatch(/<option value="georgian">Georgian<\/option>/);
  });

  it('offers the placeholder again for a style no longer in its lists (a project style deleted)', () => {
    expect(render('Our oriel')).toContain('data-style-state="none"');
  });
});

describe('a spec against its style', () => {
  it('compares the style\'s fields only, null as missing, numbers within a micrometre', () => {
    expect(sameAs({ a: 1, b: { c: null } }, { a: 1 + 1e-9, b: {}, extra: 3 })).toBe(true);
    expect(sameAs({ a: [1, 2] }, { a: [1, 2, 3] })).toBe(false);
    expect(sameAs({ a: { on: true } }, { a: null })).toBe(false);
    expect(sameAs(null, undefined)).toBe(true);
  });

  const window = {
    kind: 'window', name: 'W', mark: 'W', width: 1.2, height: 1.5, sillHeight: 0.9,
    frame: { depth: 0.07, width: 0.06, mullion: 0.08, transom: 0.08, offset: 0 }, sash: { width: 0.07, depth: 0.08, leafThickness: 0.04, glassThickness: 0.024 },
    columns: [1, 1], rows: [1], panels: [], board: { interior: 0.2, exterior: 0.05 }, threshold: 0,
    colors: { frame: '#fff', glass: '#9cc8e8', leaf: '#c8a27a' }, isExternal: true,
  } as const;

  it('a door or window type is modified once a field its style sets changes', () => {
    const georgian = { ...window, style: 'georgian', shape: null, muntins: { pattern: 'grid', across: 3, up: 3, width: 0.022, margin: 0.1 },
      details: { surround: { on: true, width: 0.12, projection: 0.03, sill: true }, keystone: { on: true, width: 0.14, height: 0.24, projection: 0.03, color: '#e6dfd0' }, head: null, apron: null,
        sillOutside: { on: true, projection: 0.05, thickness: 0.04, overhang: 0.03, slope: 5 } } };
    expect(styleModified(georgian as never)).toBe(false);
    expect(styleModified({ ...georgian, muntins: { ...georgian.muntins, across: 4 } } as never)).toBe(true);
    // A field the style does not set changes nothing.
    expect(styleModified({ ...georgian, width: 1.4 } as never)).toBe(false);
    expect(styleModified({ ...window } as never)).toBe(false);
  });

  it('a bay window is modified once its size, grid or bay differ from its bay style\'s', () => {
    const style: BaySpec = { width: 2.4, height: 1.5, sillHeight: 0.6, columns: [1, 2, 1], rows: [1], panels: [{ col: 0, row: 0, operation: 'fixed' }],
      bay: { kind: 'bay', projection: 0.6, angle: 45, sides: 'glass', seat: null, base: null, head: null, sill: null } };
    const spec: BaySpec = { ...style, bay: { ...style.bay!, style: 'bay-45' } };
    expect(bayMatches(style, spec)).toBe(true);
    expect(bayMatches(style, { ...spec, bay: { ...spec.bay!, projection: 0.7 } })).toBe(false);
    expect(bayMatches(style, { ...spec, width: 2.6 })).toBe(false);
  });
});

describe('previews on paper (report #110)', () => {
  it('every configurator preview takes the paper card, its tokens put back to paper in the dark theme', () => {
    const css = readFileSync(new URL('../theme.css', import.meta.url), 'utf8');
    const rule = css.slice(css.indexOf('.paper-card,'), css.indexOf('}', css.indexOf('.paper-card,')));
    for (const c of ['.joinery-preview svg', '.stair-preview svg', '.elevator-plan', '.type-preview', '.furniture-preview', '.tb-preview', '.buildup-preview-svg', '.corridor-plot'])
      expect(rule, c).toContain(c);
    expect(rule).toContain('--ink: #1e2a3a');
    expect(rule).toContain('color-scheme: light');
  });
});
