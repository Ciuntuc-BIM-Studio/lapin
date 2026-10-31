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

export interface PlanItemDto { kind: 'cut' | 'outline' | 'symbol' | string; closed: boolean; points: number[]; text?: string | null; minDetail?: number | null; maxDetail?: number | null; pattern?: string | null }

export interface RenderPart {
  name: string;
  color: string;
  material?: string | null;
  positions: Float32Array;
  normals: Float32Array;
  indices: Int32Array;
  edges: Float32Array;
  /** The layers modes of a 3D view that leave this part out (a finish layer: both; insulation: core); none: every mode shows it (layers3d.ts). */
  hiddenBy?: ('noFinishes' | 'core')[] | null;
  /** Coarser copies for the screen, finest first (docs/plan-display-precision.md): only a part of many triangles has them. */
  lods?: RenderLod[] | null;
}

/** A coarser copy of a part: flat shaded, with its edges; `error`, the most it strays from the part (m). */
export interface RenderLod { error: number; positions: Float32Array; normals: Float32Array; indices: Int32Array; edges: Float32Array }

/** A part's caps where a section cuts it in 3D (view.caps): `part` is its index in the element's parts. */
/** Drawn as on a sheet (style "drawing"): `fill`, the cut's fill, and `hatch`, its hatch as x y z segment pairs. */
export interface CapPart { part: number; color: string; material?: string | null; positions: Float32Array; normals: Float32Array; indices: Int32Array; edges: Float32Array; fill?: string | null; hatch?: Float32Array | null }
export interface CapItem { nodeId: string; parts: CapPart[] }

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
  /** Lines in space with no body (CAD linework with heights): x y z pairs, a segment each six numbers, in `color`. */
  lines?: Float32Array | null;
  /** Drawn as an instance of a shared mesh (docs/plan-instancing.md): its parts are the mesh's, placed by the matrix. */
  instance?: RenderInstance | null;
}

/** A shared mesh's placement: 12 numbers, the images of X, Y and Z and of the origin (p ↦ O + X·x + Y·y + Z·z). */
export interface RenderInstance { mesh: string; matrix: Float32Array }

/** A mesh many elements are drawn with, in its own coordinates; `box`: min x y z, max x y z. */
export interface SharedMesh { id: string; parts: RenderPart[]; box: Float32Array }

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
  /** Shared meshes new or changed (all of them on a reset), and those gone. */
  meshes?: SharedMesh[] | null;
  meshesRemoved?: string[] | null;
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
