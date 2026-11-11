import { describe, expect, it } from 'vitest';
import { drawingTitle, filterRows, fitBox, formatNumber, fromBase64, groupDrawings, phrase, sortRows, storeysShown, unscript, zoomAbout, type PageRow } from './data';
import { VIEWER_PHRASES } from './strings';
import { DA } from '../i18n/da';
import { DE } from '../i18n/de';
import { ES } from '../i18n/es';
import { FR } from '../i18n/fr';
import { IT } from '../i18n/it';
import { LO } from '../i18n/lo';
import { PL } from '../i18n/pl';
import { PT } from '../i18n/pt';
import { RO } from '../i18n/ro';
import { ZH } from '../i18n/zh';

describe('the page Lapin exports for the browser', () => {
  it('reads a script block back as the engine was given it', () => {
    expect(unscript('a <\\/script> b <\\/SCRIPT> <\\!-- c')).toBe('a </script> b </SCRIPT> <!-- c');
    expect(unscript('<svg><text>x</text></svg>')).toBe('<svg><text>x</text></svg>');
  });

  it('decodes the 3D model from base64, spaces aside', () => {
    expect([...fromBase64('Z2xU\nRg==')]).toEqual([0x67, 0x6c, 0x54, 0x46]);
  });

  it('speaks the exporting user\'s language, English where it has no phrase', () => {
    expect(phrase({ Drawings: 'Desene' }, 'Drawings')).toBe('Desene');
    expect(phrase({}, 'Drawings')).toBe('Drawings');
    expect(phrase(undefined, 'Fit')).toBe('Fit');
  });

  it('writes money with two decimals and quantities with up to three, in the page\'s language', () => {
    expect(formatNumber(1234.5, 'money', 'en')).toBe('1,234.50');
    expect(formatNumber(1234.5, 'money', 'ro')).toBe('1.234,50');
    expect(formatNumber(7.25, 'number', 'en')).toBe('7.25');
    expect(formatNumber(null, 'number', 'en')).toBe('');
    expect(formatNumber('m3', 'text', 'en')).toBe('m3');
  });

  const rows: PageRow[] = [
    { cells: ['Walls', null, 23241.2], elements: ['a', 'b'], heading: true },
    { cells: ['LAP-MAS', 'Masonry', 23241.2], elements: ['a', 'b'] },
    { cells: ['Doors', null, 6377.4], elements: ['c', 'd'], heading: true },
    { cells: ['LAP-WIN', 'Window', 5342.4], elements: ['d'] },
    { cells: ['LAP-DOOR', 'Door', 1035], elements: ['c'] },
  ];

  it('sorts a table\'s lines by a column, leaving the chapters out, and keeps them as written unsorted', () => {
    expect(sortRows(rows, null, false)).toBe(rows);
    expect(sortRows(rows, 2, false).map((r) => r.cells[0])).toEqual(['LAP-DOOR', 'LAP-WIN', 'LAP-MAS']);
    expect(sortRows(rows, 0, true).map((r) => r.cells[0])).toEqual(['LAP-WIN', 'LAP-MAS', 'LAP-DOOR']);
  });

  it('filters lines by a word, each kept under its chapter', () => {
    expect(filterRows(rows, 'door').map((r) => r.cells[0])).toEqual(['Doors', 'LAP-DOOR']);
    expect(filterRows(rows, 'walls').map((r) => r.cells[0])).toEqual(['Walls']);
    expect(filterRows(rows, ' ')).toBe(rows);
  });

  const storeys = [{ id: 'f', name: 'Foundations', elevation: -1 }, { id: 'g', name: 'Ground floor', elevation: 0 }, { id: 'u', name: 'First floor', elevation: 3 }];

  it('shows a storey alone, or with those below it, or all of them', () => {
    expect([...storeysShown(storeys, null, true)]).toEqual(['f', 'g', 'u']);
    expect([...storeysShown(storeys, 'g', false)]).toEqual(['g']);
    expect([...storeysShown(storeys, 'g', true)]).toEqual(['f', 'g']);
    expect([...storeysShown(storeys, 'gone', false)]).toEqual(['f', 'g', 'u']);
  });

  it('lists the drawings as the project browser groups them', () => {
    const d = (id: string, kind: string, number?: string) => ({ id, name: id, kind, number, w: 1, h: 1 });
    const groups = groupDrawings([d('S1', 'sheet', 'A-101'), d('A-A', 'section'), d('Ground', 'plan'), d('X', 'odd')]);
    expect(groups.map((g) => g.label)).toEqual(['Floor plans', 'Sections', 'Sheets', 'Views']);
    expect(drawingTitle(d('S1', 'sheet', 'A-101'))).toBe('A-101 · S1');
    expect(drawingTitle(d('Ground', 'plan'))).toBe('Ground');
  });

  it('zooms about the cursor and fits a drawing into its box', () => {
    const z = zoomAbout({ scale: 1, x: 0, y: 0 }, { x: 100, y: 50 }, 2);
    expect(z).toEqual({ scale: 2, x: -100, y: -50 });
    const f = fitBox(200, 100, 448, 248, 24);
    expect(f.scale).toBe(2);
    expect(f).toEqual({ scale: 2, x: 24, y: 24 });
  });

  // The export's own words in the app: its menu item, its dialog, what opening a page says, the engine's refusals.
  const APP_PHRASES = [
    'Lapin for the browser (.html)…', 'Lapin for the browser',
    'One .html file that opens by double-click in any browser, with no internet: the 3D model, the drawings, the estimate and every element\'s properties. With the project inside, File → Open in Lapin takes it back.',
    'The project has no drawing yet.', 'The 3D model, as the 3D view shows it', 'The estimate, worked out from the model now',
    'The project itself, so Lapin can open the page again', 'Without it the page only shows the model: Lapin cannot open it', 'About',
    'Opened the project inside the page. Save it as a .lapin to keep working on it.', 'Writing the page',
    'This web page was not written by Lapin: only a page Lapin exported (File → Lapin for the browser) opens as a project.',
    'This Lapin page was exported without its project: it shows the model in a browser but cannot be opened. Export it again with the project included.',
  ];

  it('has every one of its phrases in every language', () => {
    for (const [lang, dict] of Object.entries({ ro: RO, es: ES, de: DE, fr: FR, da: DA, it: IT, pl: PL, pt: PT, lo: LO, zh: ZH }))
      expect([lang, [...VIEWER_PHRASES, ...APP_PHRASES].filter((p) => !dict[p])]).toEqual([lang, []]);
  });
});
