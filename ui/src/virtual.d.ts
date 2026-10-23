declare module 'virtual:lapin-modules' {
  import type { LapinModule } from './platform/module';
  export const product: { id: string; name: string };
  const modules: LapinModule[];
  export default modules;
}
