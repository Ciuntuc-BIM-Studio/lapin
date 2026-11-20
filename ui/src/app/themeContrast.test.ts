import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

/** WCAG 2 contrast ratios of a theme's key token pairs, read from theme.css. */
const css = readFileSync(new URL('../theme.css', import.meta.url), 'utf8');

function tokens(theme: string): Record<string, string> {
  const m = css.match(new RegExp(`\\[data-theme="${theme}"\\] \\{([^}]*)\\}`));
  if (!m) throw new Error(`no tokens for ${theme}`);
  return Object.fromEntries([...m[1].matchAll(/--([a-z-]+):\s*(#[0-9a-f]{6})\s*;/gi)].map((x) => [x[1], x[2]]));
}
const lum = (hex: string): number => {
  const c = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
};
export const ratio = (a: string, b: string): number => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };

describe('theme contrast', () => {
  it('contrast: text, muted text, accent and borders at AAA on every surface', () => {
    const t = tokens('contrast');
    for (const surface of [t.bg, t.panel, t.raised, t.wash]) {
      expect(ratio(t.ink, surface)).toBeGreaterThanOrEqual(7);
      expect(ratio(t['ink-muted'], surface)).toBeGreaterThanOrEqual(7);
      expect(ratio(t.accent, surface)).toBeGreaterThanOrEqual(7);
      expect(ratio(t.ok, surface)).toBeGreaterThanOrEqual(7);
      expect(ratio(t.err, surface)).toBeGreaterThanOrEqual(7);
    }
    for (const surface of [t.bg, t.panel, t.raised]) {
      expect(ratio(t.rule, surface)).toBeGreaterThanOrEqual(7);
      expect(ratio(t['rule-strong'], surface)).toBeGreaterThanOrEqual(7);
    }
    expect(ratio(t['accent-ink'], t.accent)).toBeGreaterThanOrEqual(7);
    expect(ratio(t.ink, t['accent-wash'])).toBeGreaterThanOrEqual(7);
    expect(ratio(t.accent, t['accent-wash'])).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t.accent, t.ink)).toBeGreaterThan(1.5); // the accent is told apart from black
    expect(ratio('#595959', t.wash)).toBeGreaterThanOrEqual(4.5); // the disabled colour in the stylesheet
  });
  it('classic: text and accent at AA on its surfaces, grey chrome and white panels', () => {
    const t = tokens('classic');
    expect(t.bg).toBe('#ffffff');
    for (const surface of [t.bg, t.panel, t.raised, t.wash]) {
      expect(ratio(t.ink, surface)).toBeGreaterThanOrEqual(7);
      expect(ratio(t['ink-muted'], surface)).toBeGreaterThanOrEqual(4.5);
      expect(ratio(t.accent, surface)).toBeGreaterThanOrEqual(4.5);
      expect(ratio(t.ok, surface)).toBeGreaterThanOrEqual(4.5);
      expect(ratio(t.err, surface)).toBeGreaterThanOrEqual(4.5);
    }
    for (const surface of [t.bg, t.panel]) expect(ratio(t['rule-strong'], surface)).toBeGreaterThanOrEqual(3);
    expect(ratio(t['accent-ink'], t.accent)).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t.ink, t['accent-wash'])).toBeGreaterThanOrEqual(7);
  });
  it('the ratio helper matches known values', () => {
    expect(ratio('#000000', '#ffffff')).toBeCloseTo(21, 1);
    expect(ratio('#777777', '#ffffff')).toBeCloseTo(4.48, 1);
  });
});
