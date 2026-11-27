import { describe, expect, it } from 'vitest';
import type { NodeDto, RelationDto } from '../engine/protocol';
import { elementMaterials, scriptValues } from './scriptBody';

const node = (id: string, type: string, name: string, props: Record<string, unknown> = {}): NodeDto =>
  ({ id, type, name, parentId: null, px: 0, py: 0, props, editable: true } as unknown as NodeDto);
const rel = (id: string, cls: string, relating: string, related: string[]): RelationDto => ({ id, class: cls, relating, related } as RelationDto);

describe("a script's element in Properties (a bridge modeller's pier, forum)", () => {
  const steel = node('m1', 'IfcMaterial', 'Structural steel');
  const concrete = node('m2', 'IfcMaterial', 'Calcestruzzo C35/45');
  const pier = node('p', 'IfcColumn', 'Pier', { scriptOwned: true, partMaterials: ['m2', 'm2', 'Neoprene'], externalAttrs: { span: 32.5, kind: 'pier', axes: [1, 2] } });
  const model = (relations: RelationDto[], extra: NodeDto[] = []) => ({
    nodes: new Map([steel, concrete, pier, ...extra].map((n) => [n.id, n])),
    relations: new Map(relations.map((r) => [r.id, r])),
  });

  it('shows its own material and its parts\', each once, by name', () => {
    const s = model([rel('r1', 'IfcRelAssociatesMaterial', 'm1', ['p'])]);
    expect(elementMaterials(s, pier)).toEqual({ own: 'Structural steel', parts: ['Calcestruzzo C35/45', 'Neoprene'] });
  });

  it('shows nothing for a typed element (its type shows its material) nor for one of no material', () => {
    const type = node('t', 'IfcColumnType', 'C1');
    expect(elementMaterials(model([rel('r2', 'IfcRelDefinesByType', 't', ['p'])], [type]), pier)).toBeNull();
    const plain = node('q', 'IfcColumn', 'Plain');
    expect(elementMaterials(model([], [plain]), plain)).toBeNull();
  });

  it("lists the script's own values as text", () => {
    expect(scriptValues(pier)).toEqual([['span', '32.5'], ['kind', 'pier'], ['axes', '[1,2]']]);
    expect(scriptValues(node('x', 'IfcBeam', 'B'))).toEqual([]);
  });
});
