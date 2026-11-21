/**
 * A structure type's material (forum topic 190): what a column's or beam's body is made of, a footing's concrete. The type keeps
 * it by id (`material`, a footing's `concrete`); the structure types dialog shows and sends it by name.
 */

import type { NodeDto } from '../../engine/protocol';

export const CONCRETE = 'Reinforced concrete';

/** The project's name for a library's material, as the engine gives a new type of a library profile (ProfileLibraries.MaterialName). */
export function libraryMaterialName(material: string | null | undefined): string {
  switch (material) {
    case 'aluminium': return 'Aluminium';
    case 'timber': return 'Glued laminated timber';
    case 'concrete': return CONCRETE;
    case 'pvc': return 'PVC';
    case 'stainless': return 'Stainless steel';
    default: return 'Steel';
  }
}

/** The material a new type takes until one is chosen: a library profile's own (steel for a steel profile), concrete otherwise. */
export function defaultTypeMaterial(library: boolean, profileMaterial?: string | null): string {
  return library ? libraryMaterialName(profileMaterial) : CONCRETE;
}

/** The type's material by name ('' when it has none). */
export function typeMaterialName(type: NodeDto | null | undefined, nodes: ReadonlyMap<string, NodeDto>): string {
  if (!type) return '';
  const id = type.type === 'IfcFootingType' ? type.props.concrete : type.props.material;
  return typeof id === 'string' ? nodes.get(id)?.name ?? '' : '';
}
