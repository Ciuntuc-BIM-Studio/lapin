/**
 * What a Lapin module brings to the interface: its ribbon tabs, the quick
 * tools of the hotbar, panels and property sections. A product
 * (products/<id>.json) lists its modules, and a build bundles only those
 * (`virtual:lapin-modules`), so a client gets exactly the modules it has.
 * The engine side of a module (C#) is loaded the same way, by id.
 */

import type { ComponentType } from 'react';
import type * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import type { Icon } from '../icons';

export interface Vec2 { x: number; y: number }

/** How a tool takes its input in a view: the gesture, then one engine command. */
export type Gesture =
  | { kind: 'twoPoints'; /** Chained: the end of one is the start of the next (walls). */ repeat?: boolean }
  | { kind: 'point' }
  | { kind: 'rectangle' }
  /** Two points, then a third (a dimension's line; with `arc`, a point the arc passes through). */
  | {
    kind: 'threePoints'; arc?: boolean;
    /** Where the next point may go, given the points placed (a detour's points kept on the leg picked): the aimed point moved onto what is allowed. */
    constrain?: (placed: Vec2[], aimed: Vec2) => Vec2;
    /** What the tool would make, drawn as the cursor moves (a detour's bypass); null: the gesture's own preview. */
    preview?: (placed: Vec2[], aimed: Vec2) => Vec2[] | null;
  }
  | { kind: 'polygon' }
  /** `constrain`: where the next point may go, given the points placed (a pipe of copper turns only by 45°): the aimed point moved onto what is allowed. */
  | { kind: 'polyline'; constrain?: (placed: Vec2[], aimed: Vec2) => Vec2 }
  | {
    kind: 'pick'; /** IFC classes it can be used on. */ types: string[];
    /** How near (screen px) the nearest of them is taken, so the cursor can stand beside it (a door's side of a thin wall); else under the cursor only. */
    reach?: number;
    /** What the click would put there, as lines on the plan (metres), redrawn as the cursor moves. */
    ghost?: (entity: NodeDto, at: Vec2) => Vec2[][];
    /**
     * What the click would put on the element (a door or window: its size and type), so the modules' `placementCheck`s
     * are asked as the cursor moves; null: nothing to check here.
     */
    placing?: (entity: NodeDto, at: Vec2) => { kind: 'door' | 'window'; width: number; typeId: string | null } | null;
  }
  /**
   * An element picked, then a point (a bar cut: one end of the lap on the bar, then the other): the first click's point and the
   * element, then the second point; a length typed after the first click comes as `typed`. Enter or Escape after the first
   * click completes with that click alone. In 3D the pick alone completes.
   */
  | { kind: 'pickThenPoint'; /** IFC classes it can be used on. */ types: string[]; /** What the HUD says the tool asks for. */ hint?: string }
  /**
   * A face of an element (a floor's soffit, a beam's side): in 3D the face under the cursor lights up; in plan, an edge or the outline picked.
   * `query`: the engine query that names the face under the cursor (default core.faceAt, the named faces; Studio's studio.faceAt, any flat face of the body).
   */
  | { kind: 'face'; /** IFC classes it can be used on; '*': any element. */ types: string[]; query?: string };

/** Where a face pick landed: the point (model x, y, z; in plan x, y only) and, in 3D, the normal of the triangle hit. */
export interface FaceHit { point: number[]; normal?: number[] }

export interface ToolInput {
  points: Vec2[]; entity?: NodeDto; face?: FaceHit;
  /** The length typed in the live input (m) when the last point came from it, so a tool may measure along its own path rather than the screen. */
  typed?: number;
  /** In the 3D view, the height (m) of each point as clicked (a snap on a ridge, a corner above the level); a tool that measures in space reads it, the others work on the level. */
  heights?: number[];
}

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
  /** What its button says when hovered, after its name (a script's tool: its header's or lapin.ui.tool's tooltip, shown as written). */
  tooltip?: string;
  /** A tool drawn in a view: its gesture and what it does at the end. */
  gesture?: Gesture;
  complete?: (input: ToolInput, ctx: ToolContext) => Promise<void> | void;
  /** A button: what it does when pressed (a tool with a gesture is switched on instead). */
  run?: () => void;
  /** What Space does while the tool is on and nothing is typed (a door's other hand). */
  space?: () => void;
  /** What the tool lets one choose while it is on (the line a wall is drawn on), shown in the command line's bar. */
  options?: ComponentType;
  /** What it puts down, shown in Properties while it is on (the type a wall is drawn with), before the first click. */
  properties?: ComponentType;
  /** The types it puts down: their class, and its current one read and set, so a quick slot holds the tool with a type (decision 260). */
  typed?: TypedTool;
  /** Points it snaps to in a plan besides what the plan shows, given the plan's storey (a pad footing: the columns of the storey above, decision 507). */
  snapPoints?: (storeyId: string | null) => Vec2[];
}

/** A tool's types, for a quick slot holding the tool with one of them and for the catalog (decision 263). */
export interface TypedTool {
  cls: string;
  get: () => string | null;
  set: (id: string | null) => void;
  /** The dialog its types are edited in. */
  dialog?: string;
  /** A type drawn small for the catalog: its layers, its elevation, its profile. */
  thumb?: ComponentType<{ type: NodeDto }>;
  /** A type in a few words (its layers and thickness, a door's size). */
  about?: (type: NodeDto) => string | null;
}

export interface RibbonGroupDef {
  label: string; tools: ToolDef[]; small?: ToolDef[];
  /** Another module's group on the same tab this one goes after (the gates after the Site tab's Landscape); else in module order. */
  after?: string;
  /** Shown only while this holds (the scripting module's Developer group: Preferences → Developer extras on). The ribbon draws again when a preference changes. */
  shown?: () => boolean;
}

export interface RibbonTabDef {
  id: string;
  label: string;
  groups: RibbonGroupDef[];
  /** Shared groups another module offers (core's "modify"), shown after this tab's own. */
  include?: string[];
  /** Lower comes first (Architecture before Annotate). */
  order: number;
  /** The context its label is translated in, when the same English word means something else elsewhere (Planning: the schedule, not urbanism). */
  i18nCtx?: string;
}

/** A kind of view a module adds to the panes (the planning module's Gantt): its tab's label and what draws it. */
export interface ModuleViewDef { label: string; component: ComponentType }

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
  /** `anchor`: a rebar's anchor standing the cover outside the bar, dragged onto the formwork's face (decision 226). */
  kind?: 'end' | 'vertex' | 'edgeMid' | 'centre' | 'radius' | 'move' | 'rotate' | 'flip' | 'anchor';
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
  /** Where a drag puts it, kept to its own angles (a tag's knee, decision 331); `free` while Shift is held. */
  constrain?: (to: Vec2, free: boolean) => Vec2;
  /** For grips that are not one command: do it. */
  move?: (to: Vec2) => Promise<unknown> | void;
  /** Ctrl/⌘-click: insert a vertex here (an edge's middle). */
  insert?: () => GripStep | null;
  /** Ctrl/⌘-drag (with `insert`): insert a vertex where the grip is dropped; a Ctrl/⌘-click still does `insert`. */
  insertAt?: (to: Vec2) => GripStep | null;
  /** Ctrl/⌘-click: take this vertex out. */
  remove?: () => GripStep | null;
  /**
   * What `remove` takes out when it is a thing of its own (a roof window on its roof: "Remove roof window"): offered on a
   * right-click on the grip, and Delete over the grip does it instead of deleting the selection (decision 280).
   */
  removeLabel?: string;
}

/** A mark the engine drew: an annotation's points as drawn now, and what it tells of itself in `data`. */
/** A drawing's mark: an annotation, or (pick) an element's own pick lines from the drawing, never drawn. data.of: the element a tag marks. */
/** behind: drafting sent behind the model (decision 339), picked only where the model has nothing under the point. */
export interface MarkDto { id: string; segments: number[]; snaps?: number[]; data?: Record<string, unknown>; pick?: boolean; behind?: boolean }

/**
 * What a 2D view lends an entity's grips: the mark the engine drew it with (its own id), every mark of its (the owner's id or
 * `${owner}:…`: a type's stirrups on a beam, a bar seen in another view), and the view they are drawn in.
 */
export interface GripView { mark?: MarkDto; marks?: MarkDto[]; view?: string }

/** A line drawn over a selected element: its axis (walls, beams), its outline (slabs, roofs), its sketch (Studio). */
export interface RefLine {
  points: Vec2[]; closed: boolean; kind: 'axis' | 'outline' | 'sketch'; view?: string; /** A grid axis's tag: drawn only when that axis is selected. */ tag?: string;
  /** A wall's axis: an arrowhead the way it was drawn and its faces' letters, R on its right, L on its left (report #79; viewport/faceMarks.ts). */
  faces?: boolean;
}

/** A section of the properties panel for a selected entity of some IFC classes. */
export interface PropertySectionDef {
  id: string;
  label: string;
  /** The IFC classes it is for; '*': any (with `shows` to say for which). */
  classes: string[];
  /** Whether it shows for this entity at all (a section for any class that only some entities need). */
  shows?: (node: NodeDto) => boolean;
  component: ComponentType<{ node: NodeDto }>;
  /** True when the section shows this node's own fields itself, so the panel leaves out the class's generic ones. */
  replacesFields?: (node: NodeDto) => boolean;
  /** The keys of the class's fields this node shows, when one class holds entities of different kinds (a pipe segment is a run or a stack); null: all of them. */
  fieldKeys?: (node: NodeDto) => string[] | null;
}

/** One action of the File menu (shell/fileActions.ts), by its stable id ("file.save"): what a module may replace. */
export interface FileAction {
  id: string;
  label: string;
  run: () => unknown;
  /** The keyboard command whose keys the menu shows beside it (app.save). */
  keys?: string;
  /** Shown only while this holds (Share session: not in the browser). */
  shown?: () => boolean;
  /** The item's data-menu attribute, what the user guide's scenes find it by. */
  menu?: string;
}

/**
 * What a module replaces of the others' (docs/overrides.md), by id: each gets what it replaces (the next module's
 * replacement wraps this one's, in load order) and returns what stands in its place. A tool keeps its id; a dialog or
 * a view may stand where nothing was (its original is null then). Nothing in the editions of this repository uses it.
 */
export interface Replaces {
  /** By ToolDef id (architecture.wall): wherever the tool is, its tabs, shared groups and quick tools. */
  tools?: Record<string, (original: ToolDef) => ToolDef>;
  /** By the store's `dialog` key (a module's, or the shell's: share, palette…). */
  dialogs?: Record<string, (original: ComponentType | null) => ComponentType>;
  /** By a pane's module view kind (gantt). */
  views?: Record<string, (original: ModuleViewDef | null) => ModuleViewDef>;
  /** By the File menu's action id (file.save); the keyboard's New, Open, Save and Save as run the same. */
  fileActions?: Record<string, (original: FileAction) => FileAction>;
}

/** What a door or window being placed asks of the modules: where along its host's axis (m from its start), how wide, of which type. */
export interface PlacementRequest { kind: 'door' | 'window'; host: NodeDto; along: number; width: number; typeId: string | null }
/** The answer: whether it fits, the reason when it does not (a sentence, shown by the cursor), and the place along the host's axis where it does. */
export interface PlacementVerdict { ok: boolean; message?: string; snapAlong?: number }

/**
 * What a module's plan overlay is given: the plan's own transform (the view's pan, zoom and true-north turn), so it can
 * draw in model coordinates inside the plan's SVG and read where the pointer is in the model.
 */
export interface PlanOverlayProps {
  /** A model point (metres) as the plan shows it, in SVG pixels. */
  toScreen: (p: Vec2) => { x: number; y: number };
  /** An SVG point (pixels, from the plan's top left) as a model point. */
  toWorld: (x: number, y: number) => Vec2;
  /** Pixels per metre. */
  scale: number;
  /** The plan turned on screen (radians; true north up), 0 otherwise. */
  rotation: number;
  /** The plan: its view's id when it has one (else null) and its storey's. */
  view: { id: string | null; storeyId: string | null };
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
  /** Kinds of view the module adds, by the pane's `view` (the Gantt): shown in a pane, a tab, a window of their own. */
  views?: Record<string, ModuleViewDef>;
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
  /** What a type's configurator sets, shown and edited under Type parameters on its elements, by the type's class. */
  typeFields?: Record<string, ComponentType<{ type: NodeDto; node: NodeDto }>>;
  /** What the views draw highlighted with an entity selected (a task: its elements); null when nothing for it. */
  expandSelection?: (id: string, node: NodeDto | undefined) => readonly string[] | null;
  /** Shown over the active pane's model view (not over a module's own view): a card the selection is dragged from. */
  overlays?: ComponentType[];
  /** Drawn over every 3D view's canvas, projected by the camera it is given (the structural analysis' diagrams). Nothing in it takes the pointer. */
  overlays3d?: ComponentType<{ camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }>[];
  /**
   * Drawn inside every plan view's SVG, over its elements and grips, in the plan's own coordinates (`PlanOverlayProps`).
   * Nothing in it takes the pointer unless it asks: an element that should be clicked carries `data-overlay-hit` (and
   * `pointer-events: all`), and a press on it does not start a selection, a grip or a tool in the plan underneath.
   */
  planOverlays?: ComponentType<PlanOverlayProps>[];
  /**
   * Where a grip of an element this module does not own would land (a grid axis of a generated block, kept to 10 mm):
   * asked while the grip is dragged, after the owner's own `constrain`, for every module in turn; the first to answer
   * with a point wins. Not asked while Shift is held (`free`), nor for a grip dragged along with others.
   */
  gripSnap?: (element: NodeDto, grip: Grip, to: Vec2, free: boolean) => Vec2 | undefined;
  /**
   * Whether a door or window fits where the cursor is, for hosts this module drives: asked (throttled) as the cursor moves
   * over a host while the Door or Window tool is on; null leaves the host to the next module and the engine. A refusal
   * (`ok: false`) draws the preview as an error with its `message` by the cursor, and `snapAlong` (m along the host's axis)
   * is where the click goes when the cursor is within a metre of it. The engine still has the last word at the click.
   */
  placementCheck?: (req: PlacementRequest) => Promise<PlacementVerdict | null> | PlacementVerdict | null;
  /** What it replaces of the other modules' and the shell's (an edition of its own; docs/overrides.md). */
  replaces?: Replaces;
}
