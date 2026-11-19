import { describe, expect, it } from 'vitest';
import { hasMark, inheritedMark } from './MarkField';
import { openStandards, clearStandardsTab, standardsFocus, standardsTab } from './standardsTab';
import { styleToEdit } from './EditStyleButton';
import { useUi } from '../../app/store';
import type { NodeDto } from '../../engine/protocol';

const node = (type: string, props: Record<string, unknown> = {}, name = 'N', storeyId: string | null = 's'): NodeDto =>
  ({ id: type, type, name, props, editable: true, storeyId } as unknown as NodeDto);

describe('marks (forum topic 177)', () => {
  it('shows the type\'s mark, else its name, while the element has none of its own', () => {
    expect(inheritedMark(node('IfcWallType', { mark: 'W1' }, 'Porotherm 240'))).toBe('W1');
    expect(inheritedMark(node('IfcWallType', {}, 'Porotherm 240'))).toBe('Porotherm 240');
    expect(inheritedMark(undefined)).toBe('');
  });

  it('gives built elements a mark, not annotations, rooms or the spatial structure', () => {
    expect(hasMark(node('IfcWall'))).toBe(true);
    expect(hasMark(node('IfcColumn'))).toBe(true);
    for (const t of ['IfcAnnotation', 'IfcBuildingStorey', 'IfcSpace', 'IfcGrid']) expect(hasMark(node(t))).toBe(false);
    expect(hasMark({ ...node('IfcWall'), editable: false })).toBe(false);
    expect(hasMark(node('LapinView'))).toBe(false);
  });
});

describe('Edit style… (forum topic 173)', () => {
  it('opens Drafting standards on the style\'s tab with the style picked out, the standard one when none', () => {
    openStandards('dim', styleToEdit('mine'));
    expect(useUi.getState().dialog).toBe('standards');
    expect(standardsTab()).toBe('dim');
    expect(standardsFocus()).toBe('mine');
    clearStandardsTab();
    expect(standardsTab()).toBeNull();
    expect(standardsFocus()).toBeNull();
    expect(styleToEdit(undefined)).toBe('standard');
    expect(styleToEdit('')).toBe('standard');
    openStandards('project', 'x');
    expect(standardsFocus()).toBeNull();
    clearStandardsTab();
  });
});
