// The icon sheet: every Lapin icon from ui/src/icons/icons.json at 22 px (the
// ribbon's size) scaled ×3, with its name, on paper and on graphite, to check
// the shapes read as the sheet symbols they stand for.
//   node scripts/icon-sheet.mjs   → docs/mock/icons.svg

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const icons = JSON.parse(readFileSync(resolve(root, 'ui/src/icons/icons.json'), 'utf8'));
const names = Object.keys(icons);
const kebab = (k) => k.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`);
const node = ([tag, attrs]) => `<${tag} ${Object.entries(attrs).map(([k, v]) => `${kebab(k)}="${v}"`).join(' ')}/>`;

const cols = 8, cell = 92, scale = 66 / 24;
const rows = Math.ceil(names.length / cols);
const themes = [{ bg: '#fdfbf7', ink: '#b5532d', text: '#6b6357' }, { bg: '#1c1b19', ink: '#e07a4f', text: '#a39b8c' }];
const bandH = rows * cell + 20;
let out = `<svg xmlns="http://www.w3.org/2000/svg" width="${cols * cell}" height="${bandH * 2}" font-family="Inter, system-ui, sans-serif" font-size="10">\n`;
themes.forEach((t, ti) => {
  out += `<rect x="0" y="${ti * bandH}" width="${cols * cell}" height="${bandH}" fill="${t.bg}"/>\n`;
  names.forEach((name, i) => {
    const x = (i % cols) * cell + 13, y = ti * bandH + Math.floor(i / cols) * cell + 10;
    out += `<g transform="translate(${x} ${y}) scale(${scale})" fill="none" color="${t.ink}" stroke="${t.ink}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">${icons[name].map(node).join('')}</g>\n`;
    out += `<text x="${x + 33}" y="${y + 80}" text-anchor="middle" fill="${t.text}">${name}</text>\n`;
  });
});
out += '</svg>\n';
writeFileSync(resolve(root, 'docs/mock/icons.svg'), out);
console.log(`${names.length} icons → docs/mock/icons.svg`);
