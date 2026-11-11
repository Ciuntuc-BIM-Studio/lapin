declare module 'virtual:lapin-modules' {
  import type { LapinModule } from './platform/module';
  export const product: import('./platform/brand').ProductInfo;
  const modules: LapinModule[];
  export default modules;
}

declare module 'virtual:lapin-product' {
  export const product: import('./platform/brand').ProductInfo;
  export default product;
}

/** The browser viewer's code (ui/src/standalone/viewer.ts built on its own, vite.config.ts), inlined into the page Lapin exports for the browser. */
declare module 'virtual:lapin-viewer' {
  const code: string;
  export default code;
}
