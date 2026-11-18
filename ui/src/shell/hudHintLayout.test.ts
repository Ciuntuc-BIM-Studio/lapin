/** The tool's prompt banner never stands under the view's toolbar (forum topic 152: GR's banner hidden by the underlay, scale and structure chips). */

import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const css = readFileSync(new URL('../theme.css', import.meta.url), 'utf8');
const rule = (selector: string): string => {
  const m = new RegExp(`^${selector.replace(/[.+~]/g, '\\$&')} \\{([^}]*)\\}`, 'm').exec(css);
  if (!m) throw new Error(`no rule for ${selector}`);
  return m[1];
};
const px = (decls: string, prop: string): number => Number(new RegExp(`(?:^|[ ;])${prop}: (\\d+)px`).exec(decls)?.[1] ?? NaN);

describe('the prompt banner and the view toolbar', () => {
  const toolbar = rule('.view-toolbar');
  const hint = rule('.hud-hint');
  it('starts below the first row of the toolbar and is drawn over the view\'s chips', () => {
    // One row of the toolbar: its top plus the height of a chip or an icon button (28 px).
    expect(px(hint, 'top')).toBeGreaterThanOrEqual(px(toolbar, 'top') + 28);
    expect(Number(/z-index: (\d+)/.exec(hint)?.[1])).toBeGreaterThan(Number(/z-index: (\d+)/.exec(toolbar)?.[1]));
  });
  it('stays inside a narrow pane and lets clicks through to what is under it', () => {
    expect(hint).toContain('max-width: calc(100% - 24px)');
    expect(hint).toContain('pointer-events: none');
  });
  it('is followed by what stands under it: the scope bar pushes it down, the crumbs go below it', () => {
    expect(px(rule('.scope-bar + .hud-hint'), 'top')).toBeGreaterThan(px(hint, 'top'));
    expect(px(rule('.hud-hint ~ .drill-crumbs'), 'top')).toBeGreaterThan(px(hint, 'top'));
    expect(px(rule('.scope-bar ~ .hud-hint ~ .drill-crumbs'), 'top')).toBeGreaterThan(px(rule('.hud-hint ~ .drill-crumbs'), 'top'));
  });
});
