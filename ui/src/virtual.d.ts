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
