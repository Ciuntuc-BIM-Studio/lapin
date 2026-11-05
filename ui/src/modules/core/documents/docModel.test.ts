import { describe, expect, it } from 'vitest';
import { groupInputs, isChanged, paginate, pensToSvg, pixelsAt, runsText, runText, toComplete, type DocInput } from './docModel';

describe('a document\'s runs', () => {
  it('show their text, the page fields as numbers, a missing field as its key', () => {
    expect(runText({ text: 'Proiect' })).toBe('Proiect');
    expect(runText({ text: '', field: 'page' }, 3, 7)).toBe('3');
    expect(runText({ text: '', field: 'pages' }, 3, 7)).toBe('7');
    expect(runText({ text: 'project.client', missing: true })).toBe('⟨project.client⟩');
    expect(runsText([{ text: 'pagina ' }, { text: '', field: 'page' }, { text: ' din ' }, { text: '', field: 'pages' }], 2, 5)).toBe('pagina 2 din 5');
  });
});

describe('the inputs', () => {
  const inputs: DocInput[] = [
    { key: 'a', label: 'A', multiline: true, group: 'Situația existentă' },
    { key: 'b', label: 'B', multiline: true, group: 'Descrierea construcției' },
    { key: 'c', label: 'C', multiline: false },
    { key: 'd', label: 'D', multiline: true, group: 'Situația existentă' },
  ];

  it('go by group, the groups in the order they first appear, those without one together', () => {
    expect(groupInputs(inputs).map((g) => [g.group, g.inputs.map((i) => i.key)])).toEqual([
      ['Situația existentă', ['a', 'd']], ['Descrierea construcției', ['b']], ['', ['c']],
    ]);
  });

  it('are changed when their text is not the template\'s', () => {
    expect(isChanged({ key: 'a', label: 'A', multiline: true, default: 'x', value: 'x' })).toBe(false);
    expect(isChanged({ key: 'a', label: 'A', multiline: true, default: 'x', value: 'y' })).toBe(true);
    expect(isChanged({ key: 'a', label: 'A', multiline: true, default: null, value: '' })).toBe(false);
  });
});

describe('what is left to complete', () => {
  it('names each field and says where it is written', () => {
    const r = toComplete(['input.a', 'project.client', 'project.name', 'storey.area', 'table:rooms', 'view:x1'],
      [{ key: 'a', label: 'Situația existentă', multiline: true }], [{ key: 'client', label: 'Client' }]);
    expect(r).toEqual([
      { key: 'input.a', label: 'Situația existentă', where: 'inputs' },
      { key: 'project.client', label: 'Client', where: 'info' },
      { key: 'project.name', label: 'Project name', where: 'info' },
      { key: 'storey.area', label: 'storey.area', where: 'model' },
      { key: 'table:rooms', label: 'rooms', where: 'model', kind: 'table' },
      { key: 'view:x1', label: 'x1', where: 'model', kind: 'view' },
    ]);
  });
});

describe('the pages', () => {
  const b = (kind: string) => ({ kind });

  it('take the blocks as they fit, a page break starting a new one', () => {
    expect(paginate([b('paragraph'), b('paragraph'), b('paragraph')], [40, 40, 40], 100)).toEqual([[0, 1], [2]]);
    expect(paginate([b('paragraph'), b('pageBreak'), b('paragraph')], [10, 0, 10], 100)).toEqual([[0], [2]]);
  });

  it('never start with a page break nor leave an empty page, and give a block taller than a page its own', () => {
    expect(paginate([b('pageBreak'), b('paragraph'), b('pageBreak'), b('pageBreak'), b('table')], [0, 10, 0, 0, 500], 100)).toEqual([[1], [4]]);
    expect(paginate([b('paragraph'), b('table'), b('paragraph')], [10, 500, 10], 100)).toEqual([[0], [1], [2]]);
  });

  it('keep a heading with what it heads', () => {
    expect(paginate([b('paragraph'), b('heading'), b('paragraph')], [80, 10, 30], 100)).toEqual([[0], [1, 2]]);
  });
});

describe('a picture\'s pens as SVG', () => {
  const svg = pensToSvg({
    w: 100, h: 50,
    pens: [
      { layer: 'cut', stroke: '#000000', width: 0.5, fill: '#cccccc', loops: [[0, 0, 10, 0, 10, 10]] },
      { layer: 'line', stroke: '#ff0000', width: 0.18, fill: null, dash: [2, 1], segments: [0, 0, 100, 50] },
      { layer: 'text', stroke: '#000000', width: 0, fill: null, texts: [{ x: 5, y: 6, text: 'Cameră <1> & „hol”', height: 2.5, angle: 90, anchor: 'start', baseline: 'bottom', bold: true, font: 'sans' }] },
      { layer: 'image', stroke: null, width: 0, fill: null, images: [{ id: 'img', corners: [0, 0, 10, 0, 10, 5, 0, 5], opacity: 0.5 }] },
    ],
  }, (id) => (id === 'img' ? 'data:image/png;base64,AAA' : null));

  it('is the paper\'s size in mm, ten units a mm, on white', () => {
    expect(svg).toMatch(/^<svg xmlns="http:\/\/www.w3.org\/2000\/svg" width="100mm" height="50mm" viewBox="0 0 1000 500">/);
    expect(svg).toContain('<rect x="0" y="0" width="1000" height="500" fill="#ffffff"/>');
  });

  it('draws loops filled, lines with their paper widths and dashes', () => {
    expect(svg).toContain('<path d="M0 0L100 0L100 100Z" fill="#cccccc" fill-rule="evenodd" stroke="#000000" stroke-width="5"');
    expect(svg).toContain('d="M0 0L1000 500" fill="none" fill-rule="evenodd" stroke="#ff0000" stroke-width="1.8"');
    expect(svg).toContain('stroke-dasharray="20 10"');
  });

  it('writes texts escaped, at their height, bold and turned', () => {
    expect(svg).toContain('font-size="25"');
    expect(svg).toContain('font-weight="700"');
    expect(svg).toContain('transform="rotate(-90 50 60)"');
    expect(svg).toContain('>Cameră &lt;1&gt; &amp; „hol”</text>');
  });

  it('lays a pen\'s picture on its corners', () => {
    expect(svg).toContain('<image href="data:image/png;base64,AAA" x="0" y="0" width="1" height="1" preserveAspectRatio="none" opacity="0.5" transform="matrix(100 0 0 50 0 0)"/>');
  });

  it('is rasterised at its paper size', () => {
    expect(pixelsAt(254, 200)).toBe(2000);
    expect(pixelsAt(0, 200)).toBe(1);
  });
});
