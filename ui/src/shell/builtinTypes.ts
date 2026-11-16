/**
 * Built-in standard wall and slab types offered in Catalog → From the libraries (forum #116).
 * Uses the standard lapin-type-pack v1 format so `types.importFrom` brings each type and its materials in one undo step.
 */

const mat = (ref: string, name: string, category: string, color: string) => ({
  ref,
  class: 'IfcMaterial',
  name,
  attrs: { category, color },
});

const layerSet = (ref: string, name: string, layers: { material: string; thickness: number; core?: boolean; extendDown?: boolean }[]) => ({
  ref,
  class: 'IfcMaterialLayerSet',
  name,
  attrs: {
    layers: layers.map((l) => ({
      material: l.material,
      thickness: l.thickness,
      ...(l.core ? { core: true } : {}),
      ...(l.extendDown ? { extendDown: true } : {}),
    })),
  },
});

export const BUILTIN_TYPE_PACK = JSON.stringify({
  format: 'lapin-type-pack',
  version: 1,
  name: 'Lapin Standard',
  types: [
    { ref: 'w_brick_380', class: 'IfcWallType', name: 'Brick 250 + ETICS 100 (380 mm)', attrs: { mark: 'WT1' }, material: 'ls_w_brick_380' },
    { ref: 'w_brick_280', class: 'IfcWallType', name: 'Brick 250 + Plaster (280 mm)', attrs: { mark: 'WT2' }, material: 'ls_w_brick_280' },
    { ref: 'w_aac_400', class: 'IfcWallType', name: 'Thermal block 300 + EPS 80 (405 mm)', attrs: { mark: 'WT3' }, material: 'ls_w_aac_400' },
    { ref: 'w_gypsum_125', class: 'IfcWallType', name: 'Plasterboard partition (125 mm)', attrs: { mark: 'WT4' }, material: 'ls_w_gypsum_125' },
    { ref: 'w_concrete_200', class: 'IfcWallType', name: 'Reinforced concrete wall (200 mm)', attrs: { mark: 'WT5' }, material: 'ls_w_concrete_200' },
    { ref: 's_floor_220', class: 'IfcSlabType', name: 'Concrete slab 150 + Screed + Finish (220 mm)', attrs: { mark: 'ST1' }, material: 'ls_s_floor_220' },
    { ref: 's_ground_320', class: 'IfcSlabType', name: 'Ground slab 150 + XPS 100 + Screed (320 mm)', attrs: { mark: 'ST2' }, material: 'ls_s_ground_320' },
    { ref: 's_roof_380', class: 'IfcSlabType', name: 'Flat roof slab 200 + XPS 160 (380 mm)', attrs: { mark: 'ST3' }, material: 'ls_s_roof_380' },
  ],
  resources: [
    layerSet('ls_w_brick_380', 'Brick 250 + ETICS 100 (380 mm)', [
      { material: 'm_render', thickness: 0.015 },
      { material: 'm_eps', thickness: 0.1 },
      { material: 'm_brick', thickness: 0.25, core: true, extendDown: true },
      { material: 'm_plaster', thickness: 0.015 },
    ]),
    layerSet('ls_w_brick_280', 'Brick 250 + Plaster (280 mm)', [
      { material: 'm_plaster', thickness: 0.015 },
      { material: 'm_brick', thickness: 0.25, core: true, extendDown: true },
      { material: 'm_plaster', thickness: 0.015 },
    ]),
    layerSet('ls_w_aac_400', 'Thermal block 300 + EPS 80 (405 mm)', [
      { material: 'm_render', thickness: 0.01 },
      { material: 'm_eps', thickness: 0.08 },
      { material: 'm_aac', thickness: 0.3, core: true, extendDown: true },
      { material: 'm_plaster', thickness: 0.015 },
    ]),
    layerSet('ls_w_gypsum_125', 'Plasterboard partition (125 mm)', [
      { material: 'm_gypsum', thickness: 0.025 },
      { material: 'm_wool', thickness: 0.075, core: true },
      { material: 'm_gypsum', thickness: 0.025 },
    ]),
    layerSet('ls_w_concrete_200', 'Reinforced concrete wall (200 mm)', [
      { material: 'm_concrete', thickness: 0.2, core: true, extendDown: true },
    ]),
    layerSet('ls_s_floor_220', 'Concrete slab 150 + Screed + Finish (220 mm)', [
      { material: 'm_floor_finish', thickness: 0.02 },
      { material: 'm_screed', thickness: 0.05 },
      { material: 'm_concrete', thickness: 0.15, core: true },
    ]),
    layerSet('ls_s_ground_320', 'Ground slab 150 + XPS 100 + Screed (320 mm)', [
      { material: 'm_floor_finish', thickness: 0.02 },
      { material: 'm_screed', thickness: 0.05 },
      { material: 'm_xps', thickness: 0.1 },
      { material: 'm_concrete', thickness: 0.15, core: true },
    ]),
    layerSet('ls_s_roof_380', 'Flat roof slab 200 + XPS 160 (380 mm)', [
      { material: 'm_waterproofing', thickness: 0.02 },
      { material: 'm_xps', thickness: 0.16 },
      { material: 'm_concrete', thickness: 0.2, core: true },
    ]),
    mat('m_render', 'Render', 'Finish', '#e8e2d6'),
    mat('m_plaster', 'Plaster', 'Finish', '#ece8df'),
    mat('m_brick', 'Brick', 'Masonry', '#b45a44'),
    mat('m_aac', 'Thermal block', 'Masonry', '#d8d5cc'),
    mat('m_eps', 'EPS insulation', 'Insulation', '#f2efe4'),
    mat('m_xps', 'XPS insulation', 'Insulation', '#dce8e0'),
    mat('m_wool', 'Mineral wool', 'Insulation', '#d9c67a'),
    mat('m_gypsum', 'Plasterboard', 'Finish', '#f1efea'),
    mat('m_concrete', 'Reinforced concrete', 'Concrete', '#9ea3a8'),
    mat('m_screed', 'Screed', 'Finish', '#b9b8b2'),
    mat('m_floor_finish', 'Floor finish', 'Finish', '#c9b393'),
    mat('m_waterproofing', 'Waterproofing membrane', 'Finish', '#3f4448'),
  ],
});
