/**
 * @lapin/extension-kit's types: the build (vite.mjs) and the platform contract a plug-in's default export follows
 * (ui/src/platform/module.ts). The SDK's own types are in sdk.d.ts (`/// <reference types="@lapin/extension-kit/sdk" />`).
 */

export type {
  LapinModule, ToolDef, ToolInput, ToolContext, Gesture, RibbonTabDef, RibbonGroupDef, ModuleViewDef, PropertySectionDef,
  PlanOverlayProps, DrawingOverlayProps, Overlay3DProps, Grip, Grip3D, RefLine, Vec2, Vec3,
} from '../src/platform/module';
export * from './vite.mjs';

/** A plug-in's default export: the module, or a function given the SDK that returns it. */
export type PluginExport = import('../src/platform/module').LapinModule | ((sdk: import('../src/platform/sdk').LapinSdk) => import('../src/platform/module').LapinModule);
