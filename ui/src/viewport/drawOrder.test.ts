import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { DRAW_ORDER, draftedOf, drawOrderItems } from './drawOrder';

const node = (id: string, type: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type, name: id, props } as unknown as NodeDto);
const nodes = new Map([
  ['line', node('line', 'IfcAnnotation', { viewId: 'plan:1', predefinedType: 'DRAFT' })],
  ['text', node('text', 'IfcAnnotation', { viewId: 'plan:1', predefinedType: 'TEXT' })],
  ['wall', node('wall', 'IfcWall')],
]);

describe('the draw order (decision 339)', () => {
  it('works on what is drafted on a view, never on model elements', () => {
    expect(draftedOf(['wall', 'line', 'text'], nodes)).toEqual(['line', 'text']);
    expect(drawOrderItems(['wall'], nodes)).toEqual([]);
  });

  it('offers the four moves and behind the model, with the usual keys', () => {
    expect(drawOrderItems(['line'], nodes).map((i) => i.label)).toEqual(['Bring to front', 'Bring forward', 'Send backward', 'Send to back', 'Send behind the model']);
    expect(DRAW_ORDER.map((d) => d.shortcut)).toEqual(['Ctrl+Shift+]', 'Ctrl+]', 'Ctrl+[', 'Ctrl+Shift+[', undefined]);
  });
});
