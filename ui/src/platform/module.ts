/**
 * What a Lapin module brings to the interface: its ribbon tabs, the quick
 * tools of the hotbar, panels and property sections. A product
 * (products/<id>.json) lists its modules, and a build bundles only those
 * (`virtual:lapin-modules`), so a client gets exactly the modules it has.
 * The engine side of a module (C#) is loaded the same way, by id.
 */

import type { ComponentType } from 'react';
import type { NodeDto } from '../engine/protocol';
import type { Icon } from '../icons';

export interface Vec2 { x: number; y: number }

/** How a tool takes its input in a view: the gesture, then one engine command. */
export type Gesture =
  | { kind: 'twoPoints'; /** Chained: the end of one is the start of the next (walls). */ repeat?: boolean }
  | { kind: 'point' }
  | { kind: 'rectangle' }
  /** Two points, then a third (a dimension's line; with `arc`, a point the arc passes through). */
  | { kind: 'threePoints'; arc?: boolean }
  | { kind: 'polygon' }
  /** `constrain`: where the next point may go, given the points placed (a pipe of copper turns only by 45°): the aimed point moved onto what is allowed. */
  | { kind: 'polyline'; constrain?: (placed: Vec2[], aimed: Vec2) => Vec2 }
  | { kind: 'pick'; /** IFC classes it can be used on. */ types: string[] }
  /** A face of an element (a floor's soffit, a beam's side): in 3D the face under the cursor lights up; in plan, an edge or the outline picked. */
  | { kind: 'face'; /** IFC classes it can be used on. */ types: string[] };

/** Where a face pick landed: the point (model x, y, z; in plan x, y only) and, in 3D, the normal of the triangle hit. */
export interface FaceHit { point: number[]; normal?: number[] }

export interface ToolInput { points: Vec2[]; entity?: NodeDto; face?: FaceHit }

/** What a tool's completion can use: the storey it draws on, and the engine. */
export interface ToolContext {
  storeyId: string;
  /** The view the tool works in (annotations go on it). */
  viewId: string;
  execute: (command: string, args?: Record<string, unknown>) => Promise<unknown>;
}

export interface ToolDef {
  id: string;
  label: string;
  /** A lucide icon, or one of Lapin's own (`ui/src/icons`). */
  icon: Icon;
  /** Two-letter shortcut, as in Revit (WA, DR…). */
  shortcut?: string;
  /** A tool drawn in a view: its gesture and what it does at the end. */
  gesture?: Gesture;
  complete?: (input: ToolInput, ctx: ToolContext) => Promise<void> | void;
  /** A button: what it does when pressed (a tool with a gesture is switched on instead). */
  run?: () => void;
}

export interface RibbonGroupDef { label: string; tools: ToolDef[]; small?: ToolDef[] }

export interface RibbonTabDef {
  id: string;
  label: string;
  groups: RibbonGroupDef[];
  /** Shared groups another module offers (core's "modify"), shown after this tab's own. */
  include?: string[];
  /** Lower comes first (Architecture before Annotate). */
  order: number;
}

/** One engine command a grip asks for; several grips dragged together go as one edit (edit.batch). */
export interface GripStep { command: string; args: Record<string, unknown> }

/**
 * A handle shown on a selected element, to drag: a wall's axis ends, a
 * slab's vertices and edge middles, a circle's radius. Grips of several
 * selected elements at one point become one grip that moves them all.
 */
export interface Grip {
  /** Unique within its element. */
  key: string;
  at: Vec2;
  kind?: 'end' | 'vertex' | 'edgeMid' | 'centre' | 'radius' | 'move' | 'rotate' | 'flip';
  /** A flip arrow's direction (model units); drawn a little way out from `at` along it. */
  dir?: Vec2;
  /** A flip arrow: what a click does. */
  click?: () => GripStep | null;
  /** What the grip does, shown over it. */
  title?: string;
  /** The element it belongs to (gripsOf fills it in). */
  owner?: string;
  /** The view it is drawn in, when it belongs to one (a drafted shape's view); else every 2D view. */
  view?: string;
  /** What dragging it to a point does, as one engine command. */
  step?: (to: Vec2) => GripStep | null;
  /** For grips that are not one command: do it. */
  move?: (to: Vec2) => Promise<unknown> | void;
  /** Ctrl/⌘-click: insert a vertex here (an edge's middle). */
  insert?: () => GripStep | null;
  /** Ctrl/⌘-click: take this vertex out. */
  remove?: () => GripStep | null;
}

/** What a 2D view lends an entity's grips: the mark the engine drew it with (an annotation's points as drawn now, in `data`). */
export interface GripView { mark?: { id: string; segments: number[]; snaps?: number[]; data?: Record<string, unknown> } }

/** A line drawn over a selected element: its axis (walls, beams), its outline (slabs, roofs), its sketch (Studio). */
export interface RefLine { points: Vec2[]; closed: boolean; kind: 'axis' | 'outline' | 'sketch'; view?: string; /** A grid axis's tag: drawn only when that axis is selected. */ tag?: string }

/** A section of the properties panel for a selected entity of some IFC classes. */
export interface PropertySectionDef {
  id: string;
  label: string;
  classes: string[];
  component: ComponentType<{ node: NodeDto }>;
  /** True when the section shows this node's own fields itself, so the panel leaves out the class's generic ones. */
  replacesFields?: (node: NodeDto) => boolean;
  /** The keys of the class's fields this node shows, when one class holds entities of different kinds (a pipe segment is a run or a stack); null: all of them. */
  fieldKeys?: (node: NodeDto) => string[] | null;
}

export interface LapinModule {
  id: string;
  name: string;
  /** One line for the about / product pages. */
  summary: string;
  /** Tabs; a tab with the id of another module's tab adds its groups to it (phases into Manage). */
  tabs?: RibbonTabDef[];
  /** Groups other modules' tabs can include by id (core's "modify": move, copy, rotate…). */
  sharedGroups?: Record<string, RibbonGroupDef>;
  /** Tools for the viewport hotbar (1–9), in order. */
  hotbar?: ToolDef[];
  propertySections?: PropertySectionDef[];
  /** Dialogs the module opens (by the store's `dialog`). */
  dialogs?: Record<string, ComponentType>;
  /** Mounted (invisible) while the workspace is open: what keeps a module's state in step with the model. */
  background?: ComponentType;
  /** The grips of a selected entity, by IFC class (with what the view drew of it, when it draws it itself). */
  grips?: Record<string, (node: NodeDto, view?: GripView) => Grip[]>;
  /** The lines drawn over a selected entity (its axis, its outline), by IFC class. */
  references?: Record<string, (node: NodeDto) => RefLine[]>;
  /** Create Similar (CS): the tool that builds this class, set up like the element (its type, its style). */
  similar?: Record<string, (node: NodeDto) => void>;
  /** Match Type (MA): elements of this class given another type. */
  setType?: Record<string, (ids: string[], typeId: string) => Promise<unknown>>;
}
