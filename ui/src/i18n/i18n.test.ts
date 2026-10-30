import { describe, expect, it } from 'vitest';
import { DA, DA_PATTERNS } from './da';
import { DE, DE_PATTERNS } from './de';
import { ES, ES_PATTERNS } from './es';
import { FR, FR_PATTERNS } from './fr';
import { LANGUAGES, translate, type Lang, type Language } from './index';
import { RO, RO_PATTERNS } from './ro';

describe('the Romanian interface', () => {
  it('puts known phrases into Romanian, keeping their spaces, and leaves the others', () => {
    expect(translate('Wall', 'ro')).toBe('Perete');
    expect(translate(' Floor plans ', 'ro')).toBe(' Planuri ');
    expect(translate('12 elements', 'ro')).toBe('12 elemente');
    expect(translate('My own wall name', 'ro')).toBeNull();
    expect(translate('', 'ro')).toBeNull();
  });
});

describe('the Spanish interface', () => {
  it('puts known phrases into Spanish, keeping their spaces, and leaves the others', () => {
    expect(translate('Wall', 'es')).toBe('Muro');
    expect(translate(' Floor plans ', 'es')).toBe(' Plantas ');
    expect(translate('12 elements', 'es')).toBe('12 elementos');
    expect(translate('1 element', 'es')).toBe('1 elemento');
    expect(translate('Save as…', 'es')).toBe('Guardar como…');
    expect(translate('My own wall name', 'es')).toBeNull();
    expect(translate('', 'es')).toBeNull();
  });

  it('has every phrase Romanian has, and no other', () => {
    expect(Object.keys(RO).filter((k) => !(k in ES))).toEqual([]);
    expect(Object.keys(ES).filter((k) => !(k in RO))).toEqual([]);
    expect(Object.entries(ES).filter(([, v]) => !v.trim())).toEqual([]);
  });

  it('ends its phrases as the English does (an ellipsis, a colon, a full stop)', () => {
    const ends = (s: string) => s.trim().match(/[…:.?]$/)?.[0] ?? '';
    expect(Object.entries(ES).filter(([k, v]) => ends(k) && ends(k) !== ends(v)).map(([k]) => k)).toEqual([]);
  });
});

describe('the languages', () => {
  it('are offered each named in itself, English first', () => {
    expect(LANGUAGES.map((l) => l.name)).toEqual(['English', 'Română', 'Español', 'Deutsch', 'Français', 'Dansk']);
  });

  it('English is the interface as written', () => {
    expect(translate('Wall', 'en')).toBeNull();
  });

  // One phrase for each pattern, in order: Spanish matches the same ones as Romanian.
  const samples = [
    'Wall — Brick 250 · Floor',
    'Fold the project browser (B)',
    'Keep the properties open (P)',
    'Quick tool 3',
    'Object snap (F3)',
    'Ortho (F8; hold Shift)',
    'Polar tracking every 45° (F10)',
    'Reassign to Draft',
    'Also used by Wall',
    "In Ana's session, to look",
    "In Ana's session",
    'Wall 1 is in this plan.',
    'Wall 1 is in 3D.',
    'Undo „Move”: Someone changed it after you; it is left as it is.',
    'Redo „Move”: Ana changed it after you; it is left as it is.',
    'Segment 2 (ratio)',
    'aligned · 4 segments',
    '12 elements',
    '2 of 3 storeys',
    'cut at 1.20 m',
    'Underlay: Ground floor',
    "4 survey points imported, in the project's coordinates (Manage → Location shows its system and survey point).",
    '3 to check',
    'Plan · Ground floor',
    '2 line edit(s) lost their edge · clean',
    'view range: 1 problem(s)',
    'Walls · 5 selected',
    'Camera at 1.60 m: drag',
    'Key plan · Ground floor',
    'Terrain created from 120 points.',
    '3 element(s) moved to First floor.',
    '3 element(s) selected, now on First floor.',
    'Type parameters · Brick 250',
    '4 bays along × 2 up · IFC: IfcCurtainWall',
    '5 value(s) added.',
    '6 room(s) updated.',
    'IFC properties · 12',
    'shot.gif could not be read as a picture: attach a PNG or JPEG screenshot (or send the report without one).',
    '6 room(s) updated; 2 no longer enclosed (they keep their last outline).',
    'No room has a value for this field yet. Give rooms a Department in their properties.',
    'Listed 3 of 12 · IfcDoor',
    'Computed 2 group(s) over 5 element(s)',
    'Read element D1',
    'Read schedule Doors',
    'Proposed 3 change(s)',
    'Proposed 3 change(s), 1 refused',
    'Tool failed: aggregate',
    '12 row(s)',
    '3 value(s) applied: one step to undo.',
    'Grouped by storey, type',
    '2 of 3 ticked',
  ];
  const first = (patterns: [RegExp, unknown][], s: string) => patterns.findIndex(([re]) => re.test(s));

  it('have the same patterns, each matching the same phrases', () => {
    expect(ES_PATTERNS.map(([re]) => re.source)).toEqual(RO_PATTERNS.map(([re]) => re.source));
    for (const s of samples) {
      expect(first(ES_PATTERNS, s), s).toBe(first(RO_PATTERNS, s));
      expect(first(ES_PATTERNS, s), s).toBeGreaterThanOrEqual(0);
      expect(translate(s, 'es'), s).not.toBe(s);
    }
  });

  // German, French and Danish: the same phrases and patterns as Romanian and Spanish.
  const others: [Lang, string, Language['dict'], Language['patterns']][] = [['de', 'German', DE, DE_PATTERNS], ['fr', 'French', FR, FR_PATTERNS], ['da', 'Danish', DA, DA_PATTERNS]];
  const ends = (s: string) => s.trim().match(/[…:.?]$/)?.[0] ?? '';
  for (const [lang, name, dict, patterns] of others) {
    it(`${name} has every phrase Romanian has, and no other, each ending as the English does`, () => {
      expect(Object.keys(RO).filter((k) => !(k in dict))).toEqual([]);
      expect(Object.keys(dict).filter((k) => !(k in RO))).toEqual([]);
      expect(Object.entries(dict).filter(([, v]) => !v.trim())).toEqual([]);
      expect(Object.entries(dict).filter(([k, v]) => ends(k) && ends(k) !== ends(v)).map(([k]) => k)).toEqual([]);
    });

    it(`${name} has the same patterns, each matching the same phrases and keeping their numbers`, () => {
      expect(patterns.map(([re]) => re.source)).toEqual(RO_PATTERNS.map(([re]) => re.source));
      for (const s of samples) {
        expect(first(patterns, s), s).toBe(first(RO_PATTERNS, s));
        // A pattern's phrase may read the same (French „Plan · …”), but it is never left untranslated.
        const out = translate(s, lang);
        expect(out, s).not.toBeNull();
        for (const num of s.match(/\d+(?:\.\d+)?/g) ?? []) expect(out, `${s} → ${out}`).toContain(num);
      }
      expect(translate('My own wall name', lang)).toBeNull();
      expect(translate(' Floor plans ', lang)).toMatch(/^ \S.*\S $/);
    });
  }

  it('put a pattern into Spanish with its numbers and names', () => {
    expect(translate('Fold the project browser (B)', 'es')).toBe('Plegar el navegador de proyectos (B)');
    expect(translate('Wall — Brick 250 · Floor', 'es')).toBe('Muro — Brick 250 · Losa');
    expect(translate('2 of 3 storeys', 'es')).toBe('2 de 3 plantas');
  });

  it('translates the undo buttons\' tooltips with what they undo and their keys', () => {
    expect(translate('Undo (Ctrl+Z)', 'ro')).toBe('Anulează (Ctrl+Z)');
    expect(translate('Undo Move (Ctrl+Z)', 'ro')).toBe('Anulează Mută (Ctrl+Z)');
    expect(translate('Redo My wall (Ctrl+Y, Ctrl+Shift+Z)', 'ro')).toBe('Refă My wall (Ctrl+Y, Ctrl+Shift+Z)');
    expect(translate('Redo', 'ro')).toBe('Refă');
    expect(translate('Undo Move (Ctrl+Z)', 'es')).toBe('Deshacer Desplazar (Ctrl+Z)');
    expect(translate('Redo (Ctrl+Y)', 'es')).toBe('Rehacer (Ctrl+Y)');
  });
});

describe('labels the guide found in English', () => {
  const langs: Lang[] = ['ro', 'es', 'de', 'fr', 'da'];
  const labels = [
    'Import DXF', 'Import DXF…', 'Link IFC…', 'IFC properties', 'Placed', "by the map's coordinates", 'As a link', 'Units', 'Units:',
    'Roof window', 'Dormer', 'Side', 'Hand', 'Reassign', 'Underlay', 'Execution', 'detach', 'Planning', 'Engineering', 'Assist',
    'Buildable volume', 'Sun study', 'Send the question', 'Ask Lapin', 'Your question…', 'Ask', 'Custom', 'Ends', 'Top and bottom',
    'Radiator', 'Wash basin', 'Layer', 'Architectural', 'Structural', 'hip', 'gable', 'shed', 'mansard', 'barrel', 'flat',
    'Width: how wide one copy of the picture lies on the model', 'Height: how high one copy of the picture lies on the model',
  ];
  for (const lang of langs) {
    it(`are translated into ${lang}`, () => {
      expect(labels.filter((k) => translate(k, lang) == null)).toEqual([]);
    });
  }

  it('keep a phrase split by a name, and an exact phrase before a pattern', () => {
    expect(translate('Import DXF ·', 'de')).toBe('DXF importieren ·');
    expect(translate("Underlay: another level's plan beneath this one, faded, to snap to", 'es')).toMatch(/^Calco: la planta/);
  });
});

describe('a word with two meanings', () => {
  it('takes its context first (data-i18n-ctx), else its common meaning', () => {
    expect(translate('Join', 'es')).toBe('Unir');
    expect(translate('Join', 'es', 'session')).toBe('Unirse');
    expect(translate('Join', 'de', 'session')).toBe('Beitreten');
    expect(translate('Join', 'fr', 'session')).toBe('Rejoindre');
    expect(translate('Join', 'da', 'session')).toBe('Deltag');
    expect(translate('Join', 'ro', 'session')).toBe('Intră');
    expect(translate('Layers', 'de', 'element')).toBe('Schichten');
    expect(translate('Layers', 'fr', 'element')).toBe('Couches');
    expect(translate('Layers', 'ro', 'element')).toBe('Straturi');
    expect(translate('Layers', 'fr')).toBe('Calques');
    expect(translate('Layer', 'fr', 'cad')).toBe('Calque');
    expect(translate('gable', 'de', 'edge')).toBe('Giebel');
    expect(translate('gable', 'de')).toBe('Satteldach');
    // A context with no phrase of its own falls back to the common one.
    expect(translate('Wall', 'es', 'session')).toBe('Muro');
  });

  it('Spanish tells the stair\'s Built as from its Construction tab', () => {
    expect(translate('Built as', 'es')).not.toBe(translate('Construction', 'es'));
  });
});
