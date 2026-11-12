/** A manifest for the tests: a furniture pack, changed where a test says. */

import type { Manifest } from './manifest';

export const furniturePack = (over: Partial<Manifest> = {}): Manifest => ({
  schema: 1,
  id: 'ro.ciuntucbimstudio.mobilier-living',
  name: 'Mobilier living',
  summary: 'Six pieces of casework for a living room.',
  version: '1.0.0',
  author: { name: 'Ciuntuc BIM Studio', github: 'Ciuntuc-BIM-Studio' },
  license: 'CC-BY-4.0',
  lapin: '>=0.1.860',
  kind: 'furniture',
  tags: ['living', 'casework'],
  languages: ['ro', 'en'],
  contents: { file: 'mobilier-living.lapinlib', items: ['Bibliotecă stejar', 'Comodă TV'] },
  package: { url: 'https://github.com/Ciuntuc-BIM-Studio/lapin-extensions/releases/download/x/mobilier-living.lapinlib', sha256: 'a'.repeat(64), size: 1200 },
  ...over,
});

