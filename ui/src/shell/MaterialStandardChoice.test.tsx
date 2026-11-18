import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { setPrefs } from '../app/prefs';
import { MaterialStandardChoice } from './MaterialStandardChoice';

describe('the material standard choice of a new project', () => {
  it('shows Lapin chosen until the user chooses another, and lists the built-in standards', () => {
    setPrefs({ newProjectMaterialStandard: null });
    const html = renderToStaticMarkup(<MaterialStandardChoice />);
    expect(html).toMatch(/<option value="lapin"[^>]*selected/);
    for (const id of ['iso-128-50', 'din-1356', 'ro-sr-iso-128-50', 'bs-8888']) expect(html).toContain(`value="${id}"`);
  });
});
