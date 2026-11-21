import { describe, expect, it } from 'vitest';
import { loadAllLanguages, translate } from '../../i18n';
import { levelNotice } from './resources';

await loadAllLanguages();

describe('the levelling notice (decision 772)', () => {
  it('says nothing moved when no resource is short', () => {
    expect(levelNotice({ moved: 0, finishBefore: '2026-11-04', finishAfter: '2026-11-04', delay: 0, tooBig: [] }))
      .toBe('No resource is asked for more than it has: nothing moved.');
  });

  it('tells how many tasks moved and how far the finish went, and names the tasks it could not place', () => {
    const r = { moved: 2, finishBefore: '2026-11-04', finishAfter: '2026-11-10', delay: 4, tooBig: ['t1'], tooBigNames: ['Placă etaj 2'] };
    expect(levelNotice({ ...r, tooBig: [], tooBigNames: [] })).toBe('2 task(s) moved; the schedule finishes on 2026-11-10 (4 working days later).');
    const s = levelNotice(r);
    expect(s).toBe('2 task(s) moved; the schedule finishes on 2026-11-10 (4 working days later). Asking for more than a resource has, not moved: Placă etaj 2.');
    expect(translate(s, 'ro')).toBe('2 sarcini mutate; programul se termină pe 2026-11-10 (cu 4 zile lucrătoare mai târziu). Cer mai mult decât are o resursă, nemutate: Placă etaj 2.');
    for (const lang of ['es', 'de', 'fr', 'da', 'it', 'pl', 'pt', 'lo', 'zh'] as const) expect(translate(s, lang), lang).toContain('Placă etaj 2');
  });

  it('puts the ribbon and the dialog into every language', () => {
    for (const lang of ['ro', 'es', 'de', 'fr', 'da', 'it', 'pl', 'pt', 'lo', 'zh'] as const)
      for (const k of ['Resources…', 'Level resources', 'Histogram', 'Clear levelling', 'Formwork from site works', 'Resource histogram', 'Kept after (formwork)'])
        expect(translate(k, lang), `${k} in ${lang}`).not.toBeNull();
  });
});
