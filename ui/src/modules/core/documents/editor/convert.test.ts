import { describe, expect, it } from 'vitest';
import { blocksToDoc, docToBlocks, inlineToText, inputKey, scopeOf, textToInline, type Block, type PmNode } from './convert';
import { getSchema } from '@tiptap/core';
import { Node as PMNode } from '@tiptap/pm/model';
import { docExtensions } from './extensions';
import { searchFields, type DocFieldsInfo } from './fields';

/** Every kind of block, nested repeats and ifs with their other branches, the keys the editor does not show. */
const ALL: Block[] = [
  { kind: 'heading', level: 1, text: 'Memoriu {{project.name}}' },
  { kind: 'heading', text: 'Without a level' },
  { kind: 'heading', level: 3, text: '3.1. **Funcțiunea**' },
  { kind: 'paragraph', text: 'Beneficiar: **{{project.client}}**, *{{project.address}}*.' },
  { kind: 'paragraph', style: 'caption', text: 'A **bold *and italic* run** then *italic*' },
  { kind: 'paragraph', text: '' },
  { kind: 'list', ordered: false, items: ['Aria construită: {{areas.built}}', 'Aria desfășurată: **{{areas.gross}}**'] },
  { kind: 'list', ordered: true, items: ['one', 'two'] },
  { kind: 'list', items: ['without ordered'] },
  { kind: 'fields', rows: [['Denumirea', '{{project.name}}'], ['Faza', '{{project.phase}}']], skipEmpty: true, title: 'Date' },
  { kind: 'table', source: 'kind:all', title: 'Extras de tâmplărie', columns: ['Mark', 'Width'], total: false },
  { kind: 'image', view: 'v1', scale: 100, caption: 'Plan parter, {{project.name}}' },
  {
    kind: 'repeat', over: 'storeys', blocks: [
      { kind: 'heading', level: 3, text: '{{storey.name}}' },
      { kind: 'if', field: 'storey.area', blocks: [{ kind: 'paragraph', text: 'Aria {{storey.area}}.' }], else: [{ kind: 'paragraph', text: 'Cota {{storey.elevation}}.' }] },
      { kind: 'if', field: 'storey.usable', not: true, blocks: [{ kind: 'paragraph', text: 'Fără arie utilă' }] },
      { kind: 'repeat', over: 'rooms', where: { storeyId: '{{storey.id}}' }, as: 'r', blocks: [{ kind: 'paragraph', text: '{{r.number}} {{r.name}}: {{r.area}}' }] },
    ],
  },
  { kind: 'repeat', over: 'wallTypes', empty: [{ kind: 'paragraph', text: '[De completat] Nu sunt pereți.' }], blocks: [{ kind: 'paragraph', text: '**{{wallType.name}}**' }] },
  { kind: 'repeat', over: 'sheets', blocks: [] },
  { kind: 'input', key: 'structura' },
  { kind: 'toc', title: 'Cuprins' },
  { kind: 'pageBreak' },
];

describe('the markup of a block\'s text', () => {
  it('becomes marks and field nodes', () => {
    expect(textToInline('Beneficiar: **{{project.client}}**, *x*')).toEqual([
      { type: 'text', text: 'Beneficiar: ' },
      { type: 'field', attrs: { key: 'project.client' }, marks: [{ type: 'bold' }] },
      { type: 'text', text: ', ' },
      { type: 'text', text: 'x', marks: [{ type: 'italic' }] },
    ]);
    expect(textToInline('{{ room.name }}')).toEqual([{ type: 'field', attrs: { key: 'room.name' } }]);
    // An unclosed field stays text.
    expect(textToInline('a {{b')).toEqual([{ type: 'text', text: 'a {{b' }]);
  });

  it('is written back the same', () => {
    for (const s of ['plain', '**b**', '*i*', '**a *b* c**', '***both***', '*a***b**', '{{x}}**{{y}}**', 'a **b** c *d* e', '']) expect(inlineToText(textToInline(s))).toBe(s);
  });

  it('closes what an edit leaves open and drops empty text', () => {
    expect(inlineToText([{ type: 'text', text: 'x', marks: [{ type: 'bold' }, { type: 'italic' }] }, { type: 'text', text: '' }, { type: 'text', text: 'y' }])).toBe('***x***y');
    expect(inlineToText([{ type: 'text', text: 'a', marks: [{ type: 'italic' }] }, { type: 'field', attrs: { key: 'k' }, marks: [{ type: 'bold' }] }])).toBe('*a***{{k}}**');
  });
});

describe('a document\'s blocks in the editor', () => {
  it('are read and written back the same, every kind and nesting', () => {
    expect(docToBlocks(blocksToDoc(ALL))).toEqual(ALL);
  });

  it('are the same after a pass through JSON (as TipTap gives them back)', () => {
    const doc = JSON.parse(JSON.stringify(blocksToDoc(ALL))) as PmNode;
    expect(docToBlocks(doc)).toEqual(ALL);
  });

  it('are a document of the editor\'s schema, the same once ProseMirror has read and written it', () => {
    const schema = getSchema(docExtensions());
    const node = PMNode.fromJSON(schema, blocksToDoc(ALL));
    node.check();
    expect(docToBlocks(node.toJSON() as PmNode)).toEqual(ALL);
  });

  it('take the editor\'s nodes: text, containers with their zones, atoms whole', () => {
    const doc = blocksToDoc(ALL);
    const types = doc.content!.map((n) => n.type);
    expect(types.slice(0, 9)).toEqual(['heading', 'heading', 'heading', 'paragraph', 'paragraph', 'paragraph', 'bulletList', 'orderedList', 'bulletList']);
    expect(types.filter((t) => t === 'docAtom')).toHaveLength(6);
    const storeys = doc.content![12];
    expect(storeys.type).toBe('docRepeat');
    expect(storeys.content!.map((z) => z.attrs?.role)).toEqual(['main']);
    const ifNode = storeys.content![0].content![1];
    expect(ifNode.type).toBe('docIf');
    expect(ifNode.content!.map((z) => z.attrs?.role)).toEqual(['main', 'alt']);
    expect(doc.content![13].content!.map((z) => z.attrs?.role)).toEqual(['main', 'alt']);
    // An empty repeat's zone holds an empty line to type in; written back as no blocks.
    expect(doc.content![14].content![0].content).toEqual([{ type: 'paragraph', attrs: { docStyle: null, src: null } }]);
  });

  it('change as the editor changes them', () => {
    const doc = blocksToDoc([{ kind: 'paragraph', style: 'note', text: 'x' }, { kind: 'list', ordered: false, items: ['a'] }]);
    // A paragraph made a heading by the toolbar has a heading's attributes only.
    doc.content![0] = { type: 'heading', attrs: { level: 2, src: null }, content: doc.content![0].content };
    doc.content![1].type = 'orderedList';
    doc.content!.push({ type: 'paragraph', attrs: { docStyle: null, src: null }, content: [{ type: 'text', text: 'new ' }, { type: 'field', attrs: { key: 'areas.built' } }] });
    expect(docToBlocks(doc)).toEqual([
      { kind: 'heading', level: 2, text: 'x' },
      { kind: 'list', ordered: true, items: ['a'] },
      { kind: 'paragraph', text: 'new {{areas.built}}' },
    ]);
    // An else taken away, a document emptied.
    const withElse = blocksToDoc([{ kind: 'if', field: 'a', blocks: [{ kind: 'paragraph', text: 'y' }], else: [{ kind: 'paragraph', text: 'n' }] }]);
    withElse.content![0].content!.pop();
    expect(docToBlocks(withElse)).toEqual([{ kind: 'if', field: 'a', blocks: [{ kind: 'paragraph', text: 'y' }] }]);
    expect(docToBlocks(blocksToDoc([]))).toEqual([]);
  });

  it('give a repeat\'s scope and an input\'s key', () => {
    expect(scopeOf({ over: 'rooms', where: { storeyId: '{{storey.id}}' } })).toEqual({ collection: 'rooms', where: { storeyId: '{{storey.id}}' } });
    expect(scopeOf({ over: 'rooms', as: 'r' })).toEqual({ collection: 'rooms', as: 'r' });
    expect(scopeOf({})).toBeNull();
    expect(inputKey('Situația existentă', [])).toBe('situatiaExistenta');
    expect(inputKey('Situația existentă', ['situatiaExistenta', 'situatiaExistenta2'])).toBe('situatiaExistenta3');
    expect(inputKey('  ', [])).toBe('text');
    expect(inputKey('3 etaje', [])).toBe('t3Etaje');
  });
});

describe('the field search', () => {
  const info: DocFieldsInfo = {
    fields: [
      { key: 'project.name', label: 'Project name', value: 'Casa Popescu' },
      { key: 'project.client', label: 'Client', value: 'Ionescu Maria' },
      { key: 'areas.built', label: 'Built area', value: '86,40 m²' },
      { key: 'areas.basement', label: 'Basement area', value: null },
    ],
    collections: [
      { key: 'storeys', label: 'Storeys', itemPrefix: 'storey', fields: ['id', 'name', 'elevation'], count: 3 },
      { key: 'rooms', label: 'Rooms', itemPrefix: 'room', fields: ['name', 'area', 'storeyId'], count: 5 },
    ],
    tables: [],
  };

  it('groups the project\'s fields by their first part, with their values', () => {
    const g = searchFields(info, [], '');
    expect(g.map((x) => [x.title, x.fields.map((f) => f.key)])).toEqual([['Project', ['project.name', 'project.client']], ['Areas', ['areas.built', 'areas.basement']]]);
    expect(g[0].fields[1].value).toBe('Ionescu Maria');
  });

  it('offers the items\' fields of the repeats around, the innermost first, with the first item\'s values', () => {
    const g = searchFields(info, [{ collection: 'storeys', prefix: 'storey' }, { collection: 'rooms', prefix: 'r' }], 'name', { 'r.name': 'Living', 'storey.name': 'Parter' });
    expect(g.map((x) => x.title)).toEqual(['Rooms', 'Storeys', 'Project']);
    expect(g[0]).toMatchObject({ item: true, fields: [{ key: 'r.name', value: 'Living' }] });
    expect(g[1].fields).toEqual([{ key: 'storey.name', label: 'storey.name', value: 'Parter' }]);
    expect(searchFields(info, [{ collection: 'rooms', prefix: 'room' }], '')[0].fields.map((f) => f.key)).toEqual(['room.index', 'room.name', 'room.area', 'room.storeyId']);
  });

  it('matches labels, keys and values, every word, with or without diacritics', () => {
    expect(searchFields(info, [], 'ionescu').flatMap((x) => x.fields.map((f) => f.key))).toEqual(['project.client']);
    expect(searchFields(info, [], 'POPESCU name').flatMap((x) => x.fields.map((f) => f.key))).toEqual(['project.name']);
    expect(searchFields({ ...info, fields: [{ key: 'x.y', label: 'Suprafață', value: null }] }, [], 'suprafata')).toHaveLength(1);
    expect(searchFields(info, [], 'nothing like it')).toEqual([]);
    expect(searchFields(null, [], '')).toEqual([]);
  });
});
