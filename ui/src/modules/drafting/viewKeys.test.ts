import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { keyGrips } from './sheetAnnotations';
import { TAG_SOURCES, tagContent } from './tag';
import { readAnswers } from '../../shell/AskBox';

const node = (props: Record<string, unknown>): NodeDto => ({ id: 'n1', type: 'IfcAnnotation', name: 'x', parentId: null, px: 0, py: 0, props, editable: true, module: 'drafting', storeyId: null } as NodeDto);

describe('a view\'s north arrow and scale bar (issue #111)', () => {
  it('moves by a grip at the point the engine drew it, one standing by itself included', () => {
    const [g] = keyGrips(node({ predefinedType: 'NORTHARROW', viewId: 'v1' }), { mark: { id: 'n1', segments: [], data: { at: [3, 4], auto: true } } });
    expect(g.at).toEqual({ x: 3, y: 4 });
    expect(g.view).toBe('v1');
    expect(g.step!({ x: 5, y: 6 })).toEqual({ command: 'sheet.updateKey', args: { id: 'n1', at: [5, 6] } });
    // A placed bar without its mark at hand: its own point.
    expect(keyGrips(node({ predefinedType: 'SCALEBAR', viewId: 'v1', at: [1, 2] }))[0].at).toEqual({ x: 1, y: 2 });
    expect(keyGrips(node({ predefinedType: 'TEXT', viewId: 'v1', at: [1, 2] }))).toEqual([]);
  });
});

describe('a text tag (issue #92)', () => {
  it('is a source of the Tag tool, its look from a tag type when one is chosen', () => {
    expect(TAG_SOURCES.map((s) => s.source)).toContain('text');
    const base = { field: 'mark', roomField: 'name', parameter: 'FireRating', buildup: '', keynote: 'code' as const };
    expect(tagContent({ ...base, source: 'text', tagType: '' })).toEqual({ field: 'mark' });
    expect(tagContent({ ...base, source: 'text', tagType: 'door' })).toEqual({ tagType: 'door' });
  });

  it('keeps the lines typed in the box', () => {
    const { out } = readAnswers([{ key: 'v', label: 'Text', value: '', text: true, multiline: true }], { v: ' Line 1\nLine 2 ' });
    expect(out.v).toBe('Line 1\nLine 2');
  });
});
