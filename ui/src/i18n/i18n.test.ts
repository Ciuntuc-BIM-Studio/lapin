import { describe, expect, it } from 'vitest';
import { DA, DA_PATTERNS } from './da';
import { DE, DE_PATTERNS } from './de';
import { ES, ES_PATTERNS } from './es';
import { FR, FR_PATTERNS } from './fr';
import { IT, IT_PATTERNS } from './it';
import { PL, PL_PATTERNS } from './pl';
import { PT, PT_PATTERNS } from './pt';
import { LANGUAGES, translate, type Lang, type Language } from './index';
import { LO, LO_PATTERNS } from './lo';
import { RO, RO_PATTERNS } from './ro';
import { loadAllLanguages } from './index';

// The languages are loaded on demand in the app; the tests compare them all.
await loadAllLanguages();

describe('the Romanian interface', () => {
  it('puts known phrases into Romanian, keeping their spaces, and leaves the others', () => {
    expect(translate('Wall', 'ro')).toBe('Perete');
    expect(translate(' Floor plans ', 'ro')).toBe(' Planuri ');
    expect(translate('12 elements', 'ro')).toBe('12 elemente');
    expect(translate('My own wall name', 'ro')).toBeNull();
    expect(translate('', 'ro')).toBeNull();
  });
});

describe('the Lao interface', () => {
  it('puts known phrases into Lao, keeping their spaces, and leaves the others', () => {
    expect(translate('Wall', 'lo')).toBe('ຝາ');
    expect(translate(' Floor plans ', 'lo')).toBe(' ແຜນຜັງພື້ນ ');
    expect(translate('12 elements', 'lo')).toBe('12 ອົງປະກອບ');
    expect(translate('My own wall name', 'lo')).toBeNull();
    expect(translate('', 'lo')).toBeNull();
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
    expect(LANGUAGES.map((l) => l.name)).toEqual(['English', 'Română', 'Español', 'Deutsch', 'Français', 'Dansk', 'Italiano', 'Polski', 'Português', 'ພາສາລາວ', '简体中文']);
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
    'cut at 1200 mm',
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
    '3 element(s) copied to 2 storey(s).',
    '3 element(s) copied to 2 storey(s); 1 skipped: already there.',
    'hidden in 3 views',
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
    "The app's version and its last 3 error message(s) go with the report.",
    'Thank you: report #12 is on its way.',
    'Thank you: report #12 is on its way. Replies will appear in Help → Messages.',
    'Push/pull 0.5 m',
    'Fillet r 0.2 m',
    'Polar array × 6',
    'Rotate 90°',
    'Side 3',
    'Select the Studio solids first, then Shell.',
    '2 Studio solid(s): push/pull, fillet, shell and the rest work on them now.',
    'Import chair.obj',
    'Imported chair.obj: 3 references, 1200 triangles. Select one and use Studio → Make Studio solid to edit it.',
    'Grouped 12 element(s).',
    'POINTS · GRID 50 mm',
    'Nothing new from house.frag: its 8 elements are already in this project (the file was exported from it). Import it into another project, or a new one.',
    'Door 3',
    'The playhouse door is lowered to 950 mm to fit under the slope.',
    'Remarks: 3 added, 1 updated (Manage → Model remarks…).',
    '13 tasks · 51 working days · finish 03.02.2027',
    'Imported casa.xml: 4 tasks, 2 links.',
    '12 selected',
    '9 (with the tasks under it)',
    '+2 added',
    '−1 taken out',
    '3 of 40 elements without a task',
    'all 40 elements have a task',
    '1 element(s) have no such part and stay out of „Plaster”.',
    '„Masonry” takes 12 element(s).',
    '2 element(s) without a task, 3 with layers without one: selected.',
    '3 element(s) marked done on 05.03.2027, in 2 task(s).',
    '3 element(s) not started, in 2 task(s).',
    'Baseline saved: 6 tasks.',
    'Progress from site.xlsx: 2 of 6 task(s) changed, 1 row(s) not found.',
    '41.7 % done',
    '2 tasks late · finish +2 d',
    'on the baseline · finish 0 d',
    '12 line(s)',
    '3 type(s) unpriced, 7 element(s)',
    'Price list imported: 15 article(s), 2 row(s) skipped.',
    '3 guide(s) erased.',
    'Guide line at 750 mm.',
    'Distance: 1250 mm.',
    'Apply to the selection (3)',
    '8 joists · 112 whole blocks · 0 cut',
    'Across edge 2 (4.00 m)',
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

  // German, French, Danish, Italian, Polish, Portuguese and Lao: the same phrases and patterns as Romanian and Spanish.
  const others: [Lang, string, Language['dict'], Language['patterns']][] = [['de', 'German', DE, DE_PATTERNS], ['fr', 'French', FR, FR_PATTERNS], ['da', 'Danish', DA, DA_PATTERNS], ['it', 'Italian', IT, IT_PATTERNS], ['pl', 'Polish', PL, PL_PATTERNS], ['pt', 'Portuguese', PT, PT_PATTERNS], ['lo', 'Lao', LO, LO_PATTERNS]];
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

  it('says a report is on its way, and where its replies will appear when one was asked for', () => {
    expect(translate('Thank you: report #12 is on its way.', 'ro')).toBe('Mulțumim: raportul #12 e pe drum.');
    expect(translate('Thank you: report #12 is on its way. Replies will appear in Help → Messages.', 'de')).toBe('Danke: Meldung #12 ist unterwegs. Antworten erscheinen unter Hilfe → Nachrichten.');
    expect(translate("The app's version and its last 3 error message(s) go with the report.", 'es')).toBe('La versión de la aplicación y sus últimos 3 mensajes de error acompañan al informe.');
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
  const langs: Lang[] = ['ro', 'es', 'de', 'fr', 'da', 'it', 'pl', 'pt', 'lo'];
  const labels = [
    'Import DXF', 'Import DXF…', 'Link IFC…', 'IFC properties', 'Placed', "by the map's coordinates", 'As a link', 'Units', 'Units:',
    'Roof window', 'Dormer', 'Side', 'Hand', 'Reassign', 'Underlay', 'Execution', 'detach', 'Planning', 'Engineering', 'Assist',
    'Buildable volume', 'Sun study', 'Send the question', 'Ask Lapin', 'Your question…', 'Ask', 'Custom', 'Ends', 'Top and bottom',
    'Radiator', 'Wash basin', 'Layer', 'Architectural', 'Structural', 'hip', 'gable', 'shed', 'mansard', 'barrel', 'flat',
    'Width: how wide one copy of the picture lies on the model', 'Height: how high one copy of the picture lies on the model',
    'Gantt', 'Critical path', 'Work calendar', 'Import MS Project…',
    'Add to task…', 'Elements without a task', 'Rule', 'Whole elements', 'Core (structure)', 'Finishes (layers but the core)', 'Add the selection', 'Rule from the selection',
    'Generate schedule…', 'Sequence', 'Durations', 'Productivity norms', 'Import norms…', "Use Lapin's indicative values", 'Elements linked', 'Durations from norms',
    'Kept as edited', 'Concrete frame', 'Load-bearing masonry', 'Steel hall', 'Site set-up', 'Struck after (working days)',
    'Simulate', '4D simulation', 'Stages sheet…', 'Gantt on sheet', 'Export video…', 'State at date', 'Under way', 'Being demolished', 'Formwork and scaffolding',
    'Cost', 'Price lists…', 'Map articles…', 'Estimate…', 'Import…', 'Use the indicative values', 'Bill of quantities (F3)', 'Object estimate (F2)', 'Place on sheet', 'Total with VAT',
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

describe('a class name from the engine', () => {
  it('takes the class context, while the plain word keeps its panel meaning', async () => {
    await loadAllLanguages();
    // IfcBuildingElementProxy is a Studio "Solid"; a bay's sides are "Solid" as opposed to glazed (Pline).
    expect(translate('Solid', 'ro', 'class')).toBe('Solid');
    expect(translate('Solid', 'ro')).toBe('Pline');
    expect(translate('Bar', 'ro', 'class')).toBe('Bară de armătură');
    expect(translate('Bar', 'ro')).toBe('Mâna curentă');
    expect(translate('Solid', 'de', 'class')).toBe('Volumenkörper');
    expect(translate('Wall', 'ro', 'class')).toBe('Perete');
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
    // A task's Part is a part of its elements, its Kinds their classes (elsewhere a piece, and kinds of view).
    expect(translate('Part', 'ro', 'task')).toBe('Parte');
    expect(translate('Part', 'ro')).toBe('Piesă');
    expect(translate('Kinds', 'es', 'task')).toBe('Clases');
    expect(translate('Kinds', 'es')).toBe('Clases de vista');
    // A date range's To (until) is not a move's To (towards).
    expect(translate('To', 'ro', 'period')).toBe('Până la');
    expect(translate('To', 'ro')).toBe('Către');
    // The estimate's row number (No.) is not a count of pieces; its measure is not the Measure tool.
    expect(translate('No.', 'ro', 'row')).toBe('Nr.');
    expect(translate('Measure', 'ro', 'cost')).toBe('Măsura');
    expect(translate('Measure', 'ro')).toBe('Măsoară');
    // A context with no phrase of its own falls back to the common one.
    expect(translate('Wall', 'es', 'session')).toBe('Muro');
  });

  it('Spanish tells the stair\'s Built as from its Construction tab', () => {
    expect(translate('Built as', 'es')).not.toBe(translate('Construction', 'es'));
  });
});

describe('a view\'s level of development (decision 404)', () => {
  it('names each level and what it adds in every language, the style\'s level too', async () => {
    const { LOD_HELP, LODS } = await import('../modules/core/viewStyles');
    for (const lang of ['ro', 'es', 'de', 'fr', 'da', 'it', 'pl', 'pt', 'lo', 'zh'] as Lang[]) {
      expect(translate(LOD_HELP, lang)).not.toBeNull();
      for (const [, label] of LODS) expect(translate(label, lang)).not.toBeNull();
    }
    expect(translate('as its style: LOD 350 · connections', 'ro')).toBe('ca stilul: LOD 350 · îmbinări');
  });
});

describe('the Script panel’s messages (decision 489, stage 3)', () => {
  const samples = [
    'Done in 0.12 s: one undo step.',
    'Python was not found on this computer. Install Python 3.11 or newer from python.org, or choose it in Preferences → Developer extras.',
    'A script is editing the model: wait for it to finish, or stop it.',
    '„walls.py” came with the project „house.lapin”.',
  ];
  it('are translated in Romanian, German and Chinese', () => {
    for (const lang of ['ro', 'de', 'zh'] as Lang[]) {
      for (const s of samples) expect(translate(s, lang), `${lang}: ${s}`).not.toBeNull();
    }
    expect(translate('Done in 0.12 s: one undo step.', 'ro')).toContain('0.12');
  });
});
