/**
 * Products are module lists completed with what their modules require
 * (scripts/product.mjs): what a package of the product ships, engine and UI.
 */

import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { productIds, resolveProduct } from '../../../scripts/product.mjs';

const repo = resolve(__dirname, '../../..');
const temp: string[] = [];
afterEach(() => { for (const d of temp.splice(0)) rmSync(d, { recursive: true, force: true }); });

/** A repository with engine modules (id → requires), UI-only modules and products. */
function fixture(engine: Record<string, string[]>, ui: string[], products: Record<string, string[]>): string {
  const root = mkdtempSync(join(tmpdir(), 'lapin-products-'));
  temp.push(root);
  for (const [id, requires] of Object.entries(engine)) {
    const dir = join(root, 'engine/src/Modules', id, `Lapin.${id}`);
    mkdirSync(dir, { recursive: true });
    writeFileSync(join(dir, 'module.json'), JSON.stringify({ id, name: id, assembly: `Lapin.${id}.dll`, requires }));
  }
  for (const id of ui) {
    mkdirSync(join(root, 'ui/src/modules', id), { recursive: true });
    writeFileSync(join(root, 'ui/src/modules', id, 'index.tsx'), '');
  }
  mkdirSync(join(root, 'products'), { recursive: true });
  for (const [id, modules] of Object.entries(products)) writeFileSync(join(root, 'products', `${id}.json`), JSON.stringify({ id, name: id, modules }));
  return root;
}

describe('products', () => {
  it('every product of the repository resolves, core first', () => {
    // An edition with a private module (lapin-mep, lapin-urban, lapin-rail) resolves where its repository is beside this one.
    const present = (id: string) => Object.values<string>(JSON.parse(readFileSync(join(repo, 'products', `${id}.json`), 'utf8')).external ?? {}).every((d) => existsSync(resolve(repo, d)));
    for (const id of productIds(repo).filter(present)) {
      const p = resolveProduct(repo, id);
      expect(p.modules[0]).toBe('core');
      expect(p.engine).toContain('core');
    }
    expect(resolveProduct(repo, 'lapin-architecture').modules).toEqual(['core', 'architecture', 'drafting']);
  });

  it('brings what a module requires, transitively, and says so', () => {
    const root = fixture({ terrain: ['core'], civil: ['terrain'], mep: ['core'] }, ['civil', 'drafting'], { civil: ['civil', 'drafting'] });
    const p = resolveProduct(root, 'civil');
    expect(p.modules).toEqual(['core', 'terrain', 'civil', 'drafting']);
    expect(p.required).toEqual(['terrain']);
    expect(p.engine).toEqual(['core', 'terrain', 'civil']);
    expect(p.ui).toEqual(['civil', 'drafting']);
    expect(p.modules).not.toContain('mep');
  });

  it('refuses a module the repository does not have, naming who wanted it', () => {
    const root = fixture({ civil: ['terrain'] }, [], { civil: ['civil'], ghost: ['nothing'] });
    expect(() => resolveProduct(root, 'civil')).toThrow(/no module "terrain" \(required by "civil"\)/);
    expect(() => resolveProduct(root, 'ghost')).toThrow(/no module "nothing"/);
    expect(() => resolveProduct(root, 'absent')).toThrow(/No product "absent"/);
  });
});
