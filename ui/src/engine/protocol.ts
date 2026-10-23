/**
 * What the Lapin engine sends (engine/src/Platform/Lapin.Platform/Wire.cs).
 * An IFC entity travels as a node whose type is its IFC class; meshes arrive
 * as typed arrays (frame.ts).
 */

export type Json = null | boolean | number | string | Json[] | { [key: string]: Json };
export type Props = Record<string, Json>;

export interface NodeDto {
  id: string;
  /** The IFC class: "IfcWall". */
  type: string;
  name: string;
  /** The spatial parent: an element's storey, a storey's building. */
  parentId: string | null;
  px: number;
  py: number;
  /** The entity's attributes, metres; its predefinedType among them. */
  props: Props;
  /** False for a class no module of this product owns: shown, kept, never edited. */
  editable: boolean;
  module: string | null;
  storeyId: string | null;
}

export interface RelationDto { id: string; class: string; relating: string; related: string[] }

export interface PlanItemDto { kind: 'cut' | 'outline' | 'symbol' | string; closed: boolean; points: number[]; text?: string | null }

export interface RenderPart {
  name: string;
  color: string;
  material?: string | null;
  positions: Float32Array;
  normals: Float32Array;
  indices: Int32Array;
  edges: Float32Array;
}

export interface RenderItem {
  nodeId: string;
  type: string;
  storeyId: string | null;
  editable: boolean;
  color: string;
  positions: Float32Array;
  normals: Float32Array;
  indices: Int32Array;
  edges: Float32Array;
  plan: PlanItemDto[];
  /** Flat x,y pairs the plan snaps to. */
  snaps: number[];
  diagnostics: string[];
  parts?: RenderPart[] | null;
}

export interface DocState {
  canUndo: boolean; canRedo: boolean;
  undoLabel: string | null; redoLabel: string | null;
  dirty: boolean; path: string | null; nodeCount: number; edgeCount: number;
}

export interface Diff {
  version: number;
  reset: boolean;
  nodes: NodeDto[];
  removedNodes: string[];
  render: RenderItem[];
  renderRemoved: string[];
  state: DocState;
  relations: RelationDto[];
  removedRelations: string[];
}

export interface FieldDescriptor {
  key: string; label: string;
  kind: 'number' | 'length' | 'angle' | 'text' | 'bool' | 'choice' | 'ref' | 'color' | 'table';
  min?: number | null; max?: number | null; choices?: string[] | null; readOnly?: boolean;
}

export interface ClassInfo {
  type: string; label: string; module: string; color: string;
  /** "element", "type", "resource", "spatial". */
  category: string;
  placeable: boolean;
  fields: FieldDescriptor[];
}

export interface SessionInfo {
  product: { id: string; name: string; modules: string[] };
  modules: { id: string; name: string; schemaVersion: number }[];
  nodeTypes: ClassInfo[];
  commands: { name: string; label: string }[];
  units: string;
  engineVersion: string;
  catalogs: Record<string, Json>;
}
