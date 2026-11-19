import { beforeEach, describe, expect, it } from 'vitest';
import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import type { NodeDto, RelationDto } from '../engine/protocol';
import { editTypeItems } from './selectionMenu';
import { openTypeDialog, typeToEdit } from './typeToEdit';

const node = (id: string, type: string): NodeDto => ({ id, type, name: id, parentId: null, px: 0, py: 0, props: {}, editable: true, module: null, storeyId: 's1' });
const rel = (id: string, relating: string, related: string[]): RelationDto => ({ id, class: 'IfcRelDefinesByType', relating, related });

function model() {
  const nodes = new Map([node('dt1', 'IfcDoorType'), node('dt2', 'IfcDoorType'), node('wt', 'IfcWindowType'), node('ct', 'IfcColumnType'),
    node('d1', 'IfcDoor'), node('w1', 'IfcWindow'), node('c1', 'IfcColumn')].map((n) => [n.id, n]));
  const relations = new Map([rel('r1', 'dt2', ['d1']), rel('r2', 'wt', ['w1']), rel('r3', 'ct', ['c1'])].map((r) => [r.id, r]));
  useSession.setState({ nodes, relations });
}

describe('Edit type… opens the types dialog on the element\'s type (forum topic 176, decision 726)', () => {
  beforeEach(() => { useUi.setState({ dialog: null, selected: [] }); model(); });

  it('holds the type asked for while that dialog is open, and forgets it once it closes', () => {
    openTypeDialog('joinery', 'dt2');
    expect(useUi.getState().dialog).toBe('joinery');
    expect(typeToEdit('joinery')).toBe('dt2');
    expect(typeToEdit('wallTypes')).toBeNull();
    useUi.setState({ dialog: null });
    useUi.setState({ dialog: 'joinery' });
    expect(typeToEdit('joinery')).toBeNull();
  });

  it('a door opens Doors and windows on its own type, not the first of the list', () => {
    useUi.setState({ selected: ['d1'] });
    const items = editTypeItems();
    expect(items.map((i) => i.label)).toEqual(['Edit type…']);
    items[0].onSelect();
    expect(useUi.getState().dialog).toBe('joinery');
    expect(typeToEdit('joinery')).toBe('dt2');
  });

  it('a window too', () => {
    useUi.setState({ selected: ['w1'] });
    editTypeItems()[0].onSelect();
    expect(typeToEdit('joinery')).toBe('wt');
  });

  it('a column opens Structure types on its type', () => {
    useUi.setState({ selected: ['c1'] });
    const items = editTypeItems();
    expect(items).toHaveLength(1);
    items[0].onSelect();
    expect(useUi.getState().dialog).toBe('structureTypes');
    expect(typeToEdit('structureTypes')).toBe('ct');
  });
});
