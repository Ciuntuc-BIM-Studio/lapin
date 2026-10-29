// The user guide's pages: docs/user/*.md → build/user-docs/*.html, in Lapin's
// colours (paper by day, graphite by night), the pages listed on the left in
// their order, the page's sections on the right, a search over every page.
// Each page starts with its front matter:
//   ---
//   title: Walls
//   order: 20
//   summary: One sentence for the list of pages and the search.
//   ---
// A picture is written ![What it shows](shots/<name>.webp); the scenes take it.
// English pages are docs/user/*.md (→ build/user-docs), Romanian ones
// docs/user/ro/*.md (→ build/user-docs/ro, their pictures from shots/ro); a
// page with the same name in the other language is linked from the header.
// A video tutorial is written <!-- video walls -->, its id one of
// docs/user/videos/videos.json (its YouTube id, length, titles); the page shows
// its poster (docs/user/videos/<id>.webp) and loads YouTube's player
// (youtube-nocookie.com) only when it is played. <!-- videos --> is all of them.
import { execSync } from 'node:child_process';
import { copyFileSync, readFileSync, readdirSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { Marked } from 'marked';

const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const slugify = (s) => s.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/<[^>]+>/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

/** A WebP's size in pixels (lossy, lossless or extended), for the page to keep its place before the picture loads. */
function webpSize(file) {
  const b = readFileSync(file);
  const kind = b.toString('latin1', 12, 16);
  if (kind === 'VP8 ') return [b.readUInt16LE(26) & 0x3fff, b.readUInt16LE(28) & 0x3fff];
  if (kind === 'VP8L') { const n = b.readUInt32LE(21); return [(n & 0x3fff) + 1, ((n >> 14) & 0x3fff) + 1]; }
  if (kind === 'VP8X') return [1 + b.readUIntLE(24, 3), 1 + b.readUIntLE(27, 3)];
  return null;
}

function parse(file) {
  const text = readFileSync(file, 'utf8');
  const m = /^---\n([\s\S]*?)\n---\n/.exec(text);
  if (!m) throw new Error(`${file}: no front matter`);
  const meta = Object.fromEntries(m[1].split('\n').map((l) => { const i = l.indexOf(':'); return [l.slice(0, i).trim(), l.slice(i + 1).trim()]; }));
  return { ...meta, order: Number(meta.order ?? 999), body: text.slice(m[0].length) };
}

/** The guide's own words, in each language. */
const WORDS = {
  en: { video: 'Video', play: 'Play', watch: 'Watch on YouTube', command: 'Command', keys: 'Keys', guide: 'Lapin user guide', brand: 'user guide', eyebrow: 'User guide', search: 'Search the guide', theme: 'Light or dark', open: 'Open Lapin', pages: 'Pages', onPage: 'On this page', prev: 'Previous', next: 'Next', none: 'Nothing found. Try Ask Me in the app.', other: 'Română', otherTitle: 'Ghidul în română',
    footer: (date, commit) => `Pictures taken from Lapin itself on ${date}${commit ? ` (build ${commit})` : ''}. Something missing or wrong? Ask in the app: <b>Ask Me</b>, or <b>?</b> → Report an issue.` },
  ro: { video: 'Video în engleză', play: 'Pornește', watch: 'Vezi pe YouTube', command: 'Comandă', keys: 'Taste', guide: 'Ghidul utilizatorului Lapin', brand: 'ghidul utilizatorului', eyebrow: 'Ghidul utilizatorului', search: 'Caută în ghid', theme: 'Luminos sau întunecat', open: 'Deschide Lapin', pages: 'Pagini', onPage: 'Pe această pagină', prev: 'Înapoi', next: 'Înainte', none: 'Nimic găsit. Întreabă Ask Me în aplicație.', other: 'English', otherTitle: 'The guide in English',
    footer: (date, commit) => `Imagini luate din Lapin pe ${date.split('-').reverse().join('.')}${commit ? ` (build ${commit})` : ''}. Lipsește ceva sau e greșit? Întreabă în aplicație: <b>Ask Me</b>, sau <b>?</b> → Raportează o problemă.` },
};

/** The shortcuts' table: one row per command, under its tab. */
function shortcutsTable(rows, W) {
  const cats = [...new Set(rows.map((r) => r.category))];
  const body = cats.map((c) => `<tr><th colspan="2">${esc(c)}</th></tr>` + rows.filter((r) => r.category === c)
    .map((r) => `<tr><td>${esc(r.command)}</td><td>${r.keys.map((k) => `<kbd>${esc(k)}</kbd>`).join(' ')}</td></tr>`).join('')).join('');
  return `<table class="shortcuts"><thead><tr><th>${W.command}</th><th>${W.keys}</th></tr></thead><tbody>${body}</tbody></table>`;
}

/** The video tutorials, as docs/user/videos/videos.json lists them. */
function videosOf(root) {
  const file = join(root, 'docs/user/videos/videos.json');
  return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : [];
}

/** A video as its poster, YouTube's player loaded only when it is played (guide.js). */
function videoFigure(v, W, lang, up, small = false) {
  const title = lang === 'ro' ? v.titleRo ?? v.title : v.title;
  const summary = lang === 'ro' ? v.summaryRo ?? v.summary : v.summary;
  const url = `https://youtu.be/${v.youtube}`;
  return `<figure class="video${small ? ' small' : ''}"><button type="button" class="video-play" data-yt="${esc(v.youtube)}" data-title="${esc(title)}" aria-label="${esc(`${W.play}: ${title} (${v.length})`)}">`
    + `<img src="${up}videos/${esc(v.id)}.webp" alt="" width="1280" height="720" loading="lazy" decoding="async"><span class="video-icon" aria-hidden="true"></span><span class="video-len">${esc(v.length)}</span></button>`
    + `<figcaption><b>${esc(title)}</b> · ${W.video} · ${esc(summary)} <a href="${url}" target="_blank" rel="noopener">${W.watch}</a></figcaption></figure>`;
}

export function render(root, out, lang = 'en') {
  const W = WORDS[lang];
  const sub = lang === 'en' ? '' : lang;
  const src = join(root, 'docs/user', sub);
  const otherSrc = join(root, 'docs/user', lang === 'en' ? 'ro' : '');
  const shotsDir = join(out, 'shots', sub);
  // Where this language's pages point to the shared files and to the other language.
  const up = sub ? '../' : '';
  const shotsHref = `${up}shots/${sub ? `${sub}/` : ''}`;
  const otherHref = lang === 'en' ? 'ro/' : '../';
  if (!existsSync(src)) return { pages: 0, missing: [], unused: [] };
  // The videos' posters, shared by both languages.
  const videos = videosOf(root);
  mkdirSync(join(out, 'videos'), { recursive: true });
  for (const v of videos) {
    const poster = join(root, 'docs/user/videos', `${v.id}.webp`);
    if (existsSync(poster)) copyFileSync(poster, join(out, 'videos', `${v.id}.webp`));
  }
  out = join(out, sub);
  mkdirSync(out, { recursive: true });
  const pages = readdirSync(src).filter((f) => f.endsWith('.md')).map((f) => ({ file: f, slug: f.replace(/\.md$/, ''), ...parse(join(src, f)) }))
    .sort((a, b) => a.order - b.order || a.title.localeCompare(b.title));
  const used = new Set();
  const missing = [];
  let commit = '';
  try { commit = execSync('git rev-parse --short HEAD', { cwd: root }).toString().trim(); } catch { /* not a checkout */ }
  const date = new Date().toISOString().slice(0, 10);
  const search = [];

  for (const page of pages) {
    const toc = [];
    const marked = new Marked({
      gfm: true,
      renderer: {
        heading({ tokens, depth }) {
          const html = this.parser.parseInline(tokens);
          const id = slugify(html);
          if (depth === 2) toc.push({ id, html });
          return `<h${depth} id="${id}"><a class="anchor" href="#${id}" aria-hidden="true">#</a>${html}</h${depth}>\n`;
        },
        image({ href, text }) {
          const name = href.replace(/^shots\//, '');
          const src = shotsHref + name;
          used.add(name);
          const file = join(shotsDir, name);
          if (!existsSync(file)) { missing.push(`${page.file}: ${href}`); return ''; }
          const [w, h] = webpSize(file) ?? [];
          // The pictures are taken at twice the screen's density: shown at half their pixels.
          const size = w ? ` width="${Math.round(w / 2)}" height="${Math.round(h / 2)}"` : '';
          return `<figure><a href="${esc(src)}" target="_blank" rel="noopener"><img src="${esc(src)}" alt="${esc(text)}"${size} loading="lazy" decoding="async"></a><figcaption>${esc(text)}</figcaption></figure>`;
        },
        link({ href, tokens }) {
          const inner = this.parser.parseInline(tokens);
          const local = !/^[a-z]+:/i.test(href) && /\.md(#|$)/.test(href);
          const target = local ? href.replace(/\.md(#|$)/, '.html$1') : href;
          const ext = /^https?:/.test(href) ? ' target="_blank" rel="noopener"' : '';
          return `<a href="${esc(target)}"${ext}>${inner}</a>`;
        },
        blockquote({ tokens }) { return `<aside class="note">${this.parser.parse(tokens)}</aside>\n`; },
      },
    });
    // A paragraph that is only a picture is a figure, not a paragraph around one; <!-- pages --> is every other page, as cards.
    const cards = `<ul class="cards">${pages.filter((p) => p.slug !== 'index').map((p) => `<li><a href="${p.slug}.html"><b>${esc(p.title)}</b><span>${esc(p.summary ?? '')}</span></a></li>`).join('')}</ul>`;
    let article = marked.parse(page.body).replace(/<p>(<figure>[\s\S]*?<\/figure>)<\/p>/g, '$1').replace('<!-- pages -->', cards);
    // <!-- video id -->: one video tutorial; <!-- videos -->: every one.
    article = article.replace(/(?:<p>)?<!-- video ([a-z0-9-]+) -->(?:<\/p>)?/g, (_, id) => {
      const v = videos.find((x) => x.id === id);
      if (!v) { missing.push(`${page.file}: video ${id}`); return ''; }
      if (!existsSync(join(root, 'docs/user/videos', `${id}.webp`))) missing.push(`${page.file}: videos/${id}.webp`);
      return videoFigure(v, W, lang, up);
    });
    if (article.includes('<!-- videos -->')) article = article.replace('<!-- videos -->', `<div class="videos">${videos.map((v) => videoFigure(v, W, lang, up, true)).join('')}</div>`);
    // <!-- shortcuts -->: the default keyboard shortcuts, as the app's registry has them (the interface scene writes them).
    if (article.includes('<!-- shortcuts -->')) {
      const file = join(shotsDir, 'shortcuts.json');
      if (!existsSync(file)) missing.push(`${page.file}: shortcuts.json`);
      else article = article.replace('<!-- shortcuts -->', shortcutsTable(JSON.parse(readFileSync(file, 'utf8')), W));
    }
    const videoText = page.body.includes('<!-- videos -->') ? videos.map((v) => (lang === 'ro' ? `${v.titleRo} ${v.summaryRo}` : `${v.title} ${v.summary}`)).join(' ') : '';
    const plain = (page.body.replace(/<!--[\s\S]*?-->/g, ' ') + ' ' + videoText).replace(/!\[[^\]]*\]\([^)]*\)/g, '').replace(/[#*_`>|[\]()-]/g, ' ').replace(/\s+/g, ' ');
    search.push({ slug: page.slug, title: page.title, summary: page.summary ?? '', headings: toc.map((h) => ({ id: h.id, text: h.html.replace(/<[^>]+>/g, '') })), text: plain });

    const i = pages.indexOf(page);
    const prev = pages[i - 1], next = pages[i + 1];
    const nav = pages.map((p) => `<li><a href="${p.slug}.html"${p === page ? ' aria-current="page"' : ''}>${esc(p.title)}</a></li>`).join('');
    const onPage = toc.length > 1 ? `<nav class="toc" aria-label="${W.onPage}"><p class="eyebrow">${W.onPage}</p><ul>${toc.map((h) => `<li><a href="#${h.id}">${h.html}</a></li>`).join('')}</ul></nav>` : '<div></div>';
    const other = existsSync(join(otherSrc, page.file)) ? `${otherHref}${page.slug}.html` : null;
    writeFileSync(join(out, `${page.slug}.html`), template({ page, article, nav, onPage, prev, next, date, commit, lang, W, up, other }));
  }
  writeFileSync(join(out, 'search.json'), JSON.stringify(search));
  writeFileSync(join(out, up || '.', 'guide.css'), CSS);
  writeFileSync(join(out, up || '.', 'guide.js'), JS);
  const taken = existsSync(shotsDir) ? readdirSync(shotsDir).filter((f) => f.endsWith('.webp')) : [];
  return { pages: pages.length, missing, unused: taken.filter((f) => !used.has(f)) };
}

function template({ page, article, nav, onPage, prev, next, date, commit, lang, W, up, other }) {
  const title = page.slug === 'index' ? W.guide : `${page.title} · ${W.guide}`;
  return `<!doctype html>
<html lang="${lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(title)}</title>
<meta name="description" content="${esc(page.summary ?? '')}">
<link rel="icon" href="/web/favicon.png">
<link rel="stylesheet" href="${up}guide.css">${other ? `\n<link rel="alternate" hreflang="${lang === 'en' ? 'ro' : 'en'}" href="${other}">` : ''}
<script>try { const t = localStorage.getItem('lapin-guide-theme'); if (t) document.documentElement.dataset.theme = t; } catch {}</script>
</head>
<body>
<header class="top">
  <a class="brand" href="index.html">Lapin <span>${W.brand}</span></a>
  <div class="search">
    <input type="search" id="q" placeholder="${W.search}" aria-label="${W.search}" autocomplete="off">
    <ul id="hits" hidden data-none="${esc(W.none)}"></ul>
  </div>
  ${other ? `<a class="lang" href="${other}" hreflang="${lang === 'en' ? 'ro' : 'en'}" title="${W.otherTitle}">${W.other}</a>` : ''}
  <button type="button" class="theme" id="theme" title="${W.theme}" aria-label="${W.theme}">◐</button>
  <a class="open" href="/web/">${W.open}</a>
</header>
<div class="layout">
  <nav class="pages" aria-label="${W.pages}">
    <details open><summary>${W.pages}</summary><ol>${nav}</ol></details>
  </nav>
  <main>
    <article>
      ${page.slug === 'index' ? '' : `<p class="eyebrow">${W.eyebrow}</p><h1>${esc(page.title)}</h1>${page.summary ? `<p class="lead">${esc(page.summary)}</p>` : ''}`}
      ${article}
    </article>
    <nav class="pager">
      ${prev ? `<a class="prev" href="${prev.slug}.html"><span class="eyebrow">${W.prev}</span>${esc(prev.title)}</a>` : '<span></span>'}
      ${next ? `<a class="next" href="${next.slug}.html"><span class="eyebrow">${W.next}</span>${esc(next.title)}</a>` : '<span></span>'}
    </nav>
    <footer>${W.footer(date, commit)}</footer>
  </main>
  ${onPage}
</div>
<script src="${up}guide.js" defer></script>
</body>
</html>
`;
}

const CSS = `:root {
  --bg: #f6f3ec; --panel: #fdfbf7; --wash: #efeae0; --rule: #e2dccf; --rule-strong: #d6cebf;
  --ink: #1e2a3a; --muted: #6b6357; --accent: #b5532d; --accent-wash: #f1e3d9; --shadow: rgba(30, 42, 58, 0.10);
  --font: "Inter", ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; --mono: ui-monospace, "SF Mono", Menlo, monospace;
  color-scheme: light;
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --bg: #161513; --panel: #1c1b19; --wash: #2c2a26; --rule: #34312c; --rule-strong: #4a463f;
    --ink: #e9e4da; --muted: #a39b8c; --accent: #e07a4f; --accent-wash: #3a2c24; --shadow: rgba(0, 0, 0, 0.4);
    color-scheme: dark;
  }
}
:root[data-theme="dark"] {
  --bg: #161513; --panel: #1c1b19; --wash: #2c2a26; --rule: #34312c; --rule-strong: #4a463f;
  --ink: #e9e4da; --muted: #a39b8c; --accent: #e07a4f; --accent-wash: #3a2c24; --shadow: rgba(0, 0, 0, 0.4);
  color-scheme: dark;
}
* { box-sizing: border-box; }
html { scroll-padding-top: 72px; }
body { margin: 0; background: var(--bg); color: var(--ink); font: 16px/1.65 var(--font); -webkit-font-smoothing: antialiased; }
a { color: var(--accent); text-underline-offset: 2px; }
.eyebrow { display: block; font-size: 11px; font-weight: 600; letter-spacing: 0.14em; text-transform: uppercase; color: var(--muted); margin: 0 0 4px; }
kbd { font: 12.5px var(--mono); padding: 1px 6px; border: 1px solid var(--rule-strong); border-bottom-width: 2px; border-radius: 4px; background: var(--panel); white-space: nowrap; }
code { font: 0.9em var(--mono); background: var(--wash); padding: 1px 5px; border-radius: 3px; }

.top { position: sticky; top: 0; z-index: 10; display: flex; align-items: center; gap: 16px; height: 56px; padding: 0 24px; background: var(--panel); border-bottom: 1px solid var(--rule); }
.brand { font-weight: 900; font-size: 20px; letter-spacing: -0.02em; color: var(--ink); text-decoration: none; white-space: nowrap; }
.brand span { font-weight: 500; font-size: 14px; letter-spacing: 0; color: var(--muted); margin-left: 4px; }
.search { position: relative; flex: 1; max-width: 420px; margin-left: auto; }
.search input { width: 100%; height: 34px; padding: 0 12px; border: 1px solid var(--rule-strong); border-radius: 4px; background: var(--bg); color: var(--ink); font: inherit; font-size: 14px; }
.search input:focus { outline: 2px solid var(--accent); outline-offset: -1px; }
#hits { position: absolute; top: 40px; left: 0; right: 0; max-height: 70vh; overflow: auto; margin: 0; padding: 4px 0; list-style: none; background: var(--panel); border: 1px solid var(--rule-strong); border-radius: 4px; box-shadow: 0 10px 30px var(--shadow); }
#hits a { display: block; padding: 8px 12px; color: var(--ink); text-decoration: none; font-size: 14px; line-height: 1.4; }
#hits a:hover, #hits a.on { background: var(--accent-wash); }
#hits small { display: block; color: var(--muted); }
#hits .none { padding: 8px 12px; color: var(--muted); font-size: 14px; }
.theme { width: 34px; height: 34px; border: 1px solid var(--rule-strong); border-radius: 4px; background: none; color: var(--ink); cursor: pointer; font-size: 16px; }
.lang { padding: 6px 10px; border: 1px solid var(--rule-strong); border-radius: 4px; color: var(--ink); font-size: 13px; text-decoration: none; white-space: nowrap; }
.lang:hover { border-color: var(--accent); color: var(--accent); }
.open { padding: 7px 14px; border-radius: 4px; background: var(--accent); color: #fff; font-weight: 600; font-size: 13px; letter-spacing: 0.04em; text-transform: uppercase; text-decoration: none; white-space: nowrap; }

.layout { display: grid; grid-template-columns: 240px minmax(0, 760px) 220px; gap: 48px; justify-content: center; padding: 0 24px; }
.pages { position: sticky; top: 56px; align-self: start; max-height: calc(100vh - 56px); overflow: auto; padding: 28px 0; }
.pages summary { display: none; }
.pages ol { list-style: none; margin: 0; padding: 0; counter-reset: p; }
.pages li a { display: block; padding: 6px 12px; border-left: 2px solid var(--rule); color: var(--ink); text-decoration: none; font-size: 14.5px; }
.pages li a:hover { border-left-color: var(--rule-strong); background: var(--wash); }
.pages li a[aria-current] { border-left-color: var(--accent); color: var(--accent); font-weight: 600; background: var(--accent-wash); }
.toc { position: sticky; top: 56px; align-self: start; padding: 28px 0; font-size: 13.5px; }
.toc ul { list-style: none; margin: 0; padding: 0; }
.toc a { display: block; padding: 4px 0; color: var(--muted); text-decoration: none; line-height: 1.35; }
.toc a:hover, .toc a.on { color: var(--accent); }

main { padding: 32px 0 48px; min-width: 0; }
h1 { font-size: 38px; line-height: 1.15; letter-spacing: -0.02em; margin: 0 0 10px; }
.lead { font-size: 19px; color: var(--muted); margin: 0 0 28px; }
h2 { font-size: 25px; letter-spacing: -0.01em; margin: 48px 0 12px; padding-top: 20px; border-top: 1px solid var(--rule); }
h3 { font-size: 18px; margin: 28px 0 8px; }
h1 + h2, .lead + h2 { border-top: none; padding-top: 0; }
.anchor { float: left; margin-left: -1.1em; width: 1em; color: var(--rule-strong); text-decoration: none; opacity: 0; }
h2:hover .anchor, h3:hover .anchor { opacity: 1; }
article p, article li { max-width: 68ch; }
article ul, article ol { padding-left: 1.4em; }
article li { margin: 4px 0; }
figure { margin: 22px 0 28px; }
figure a { display: block; cursor: zoom-in; }
figure img { display: block; max-width: 100%; height: auto; border: 1px solid var(--rule-strong); border-radius: 6px; box-shadow: 0 6px 24px var(--shadow); background: var(--panel); }
figcaption { margin-top: 8px; font-size: 14px; color: var(--muted); }
table { border-collapse: collapse; margin: 16px 0 24px; font-size: 15px; width: 100%; }
th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid var(--rule); vertical-align: top; }
th { font-size: 12px; letter-spacing: 0.08em; text-transform: uppercase; color: var(--muted); font-weight: 600; }
.note { margin: 20px 0; padding: 12px 18px; border-left: 3px solid var(--accent); background: var(--accent-wash); border-radius: 0 4px 4px 0; }
.note p { margin: 6px 0; }
.cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 12px; padding: 0; list-style: none; margin: 20px 0; }
.cards li { margin: 0; max-width: none; }
.cards a { display: block; height: 100%; padding: 14px 16px; border: 1px solid var(--rule-strong); border-radius: 6px; background: var(--panel); color: var(--ink); text-decoration: none; }
.cards a:hover { border-color: var(--accent); }
.cards b { display: block; margin-bottom: 4px; }
.cards span { font-size: 14px; color: var(--muted); line-height: 1.45; display: block; }
.video { margin: 22px 0 28px; }
.video-play { position: relative; display: block; width: 100%; padding: 0; border: 1px solid var(--rule-strong); border-radius: 6px; overflow: hidden; background: #000; cursor: pointer; box-shadow: 0 6px 24px var(--shadow); aspect-ratio: 16 / 9; }
.video-play img { display: block; width: 100%; height: 100%; object-fit: cover; opacity: 0.92; transition: opacity 0.2s; }
.video-play:hover img, .video-play:focus-visible img { opacity: 1; }
.video-play:focus-visible { outline: 3px solid var(--accent); outline-offset: 2px; }
.video-icon { position: absolute; left: 50%; top: 50%; width: 72px; height: 72px; margin: -36px 0 0 -36px; border-radius: 50%; background: var(--accent); box-shadow: 0 6px 24px rgba(0, 0, 0, 0.35); transition: transform 0.15s; }
.video-icon::after { content: ""; position: absolute; left: 29px; top: 22px; border-style: solid; border-width: 14px 0 14px 22px; border-color: transparent transparent transparent #fff; }
.video-play:hover .video-icon { transform: scale(1.08); }
.video-len { position: absolute; right: 10px; bottom: 10px; padding: 2px 8px; border-radius: 4px; background: rgba(0, 0, 0, 0.72); color: #fff; font: 600 13px var(--font); }
.video iframe { display: block; width: 100%; aspect-ratio: 16 / 9; border: 1px solid var(--rule-strong); border-radius: 6px; background: #000; }
.video figcaption { margin-top: 8px; font-size: 14px; color: var(--muted); }
.video figcaption b { color: var(--ink); }
.videos { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 8px 20px; }
.videos .video { margin: 12px 0; }
.videos .video-icon { width: 56px; height: 56px; margin: -28px 0 0 -28px; }
.videos .video-icon::after { left: 22px; top: 17px; border-width: 11px 0 11px 17px; }
.pager { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-top: 56px; }
.pager a { padding: 14px 16px; border: 1px solid var(--rule-strong); border-radius: 6px; text-decoration: none; color: var(--ink); font-weight: 600; }
.pager a:hover { border-color: var(--accent); }
.pager .next { text-align: right; }
main > footer { margin-top: 40px; padding-top: 16px; border-top: 1px solid var(--rule); font-size: 13.5px; color: var(--muted); }

@media (max-width: 1180px) { .layout { grid-template-columns: 220px minmax(0, 1fr); } .toc { display: none; } }
@media (max-width: 820px) {
  .top { padding: 0 16px; gap: 10px; }
  .brand span, .open { display: none; }
  .layout { display: block; padding: 0 16px; }
  .pages { position: static; max-height: none; padding: 12px 0 0; }
  .pages summary { display: list-item; cursor: pointer; font-weight: 600; padding: 8px 0; }
  main { padding-top: 16px; }
  h1 { font-size: 30px; }
  .anchor { display: none; }
  .pager { grid-template-columns: 1fr; }
}
`;

const JS = `(() => {
  // Light or dark, remembered here.
  const root = document.documentElement;
  document.getElementById('theme').addEventListener('click', () => {
    const dark = root.dataset.theme ? root.dataset.theme === 'dark' : matchMedia('(prefers-color-scheme: dark)').matches;
    root.dataset.theme = dark ? 'light' : 'dark';
    try { localStorage.setItem('lapin-guide-theme', root.dataset.theme); } catch {}
  });
  // On a phone the list of pages starts closed.
  if (matchMedia('(max-width: 820px)').matches) document.querySelector('.pages details')?.removeAttribute('open');

  // The section being read, lit in the outline.
  const links = [...document.querySelectorAll('.toc a')];
  if (links.length && 'IntersectionObserver' in window) {
    const seen = new Map();
    const io = new IntersectionObserver((es) => {
      for (const e of es) seen.set(e.target.id, e.isIntersecting);
      const first = links.find((a) => seen.get(a.hash.slice(1)));
      if (first) links.forEach((a) => a.classList.toggle('on', a === first));
    }, { rootMargin: '-60px 0px -60% 0px' });
    links.forEach((a) => { const h = document.getElementById(a.hash.slice(1)); if (h) io.observe(h); });
  }

  // A video's poster becomes YouTube's player (youtube-nocookie.com) only when it is played.
  document.addEventListener('click', (e) => {
    const b = e.target.closest('.video-play');
    if (!b) return;
    const f = document.createElement('iframe');
    f.src = 'https://www.youtube-nocookie.com/embed/' + encodeURIComponent(b.dataset.yt) + '?autoplay=1&rel=0&modestbranding=1';
    f.title = b.dataset.title || 'Video';
    f.allow = 'autoplay; encrypted-media; picture-in-picture; fullscreen';
    f.allowFullscreen = true;
    f.referrerPolicy = 'strict-origin-when-cross-origin';
    b.replaceWith(f);
  });

  // Search: every page's title, sections and words.
  const q = document.getElementById('q'), hits = document.getElementById('hits');
  let index = null, at = -1;
  const norm = (s) => s.toLowerCase().normalize('NFD').replace(/[\\u0300-\\u036f]/g, '');
  const esc = (s) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  const show = async () => {
    const words = norm(q.value).split(/\\s+/).filter((w) => w.length > 1);
    if (!words.length) { hits.hidden = true; return; }
    index ??= await (await fetch('search.json')).json();
    const results = [];
    for (const p of index) {
      const title = norm(p.title), text = norm(p.text), sum = norm(p.summary);
      if (!words.every((w) => title.includes(w) || text.includes(w) || sum.includes(w))) continue;
      const h = p.headings.find((h) => words.some((w) => norm(h.text).includes(w)));
      const score = words.filter((w) => title.includes(w)).length * 4 + (h ? 2 : 0) + words.filter((w) => sum.includes(w)).length;
      results.push({ score, href: p.slug + '.html' + (h ? '#' + h.id : ''), title: p.title + (h ? ' › ' + h.text : ''), summary: p.summary });
    }
    results.sort((a, b) => b.score - a.score);
    at = -1;
    hits.innerHTML = results.length ? results.slice(0, 8).map((r) => '<li><a href="' + r.href + '">' + esc(r.title) + '<small>' + esc(r.summary) + '</small></a></li>').join('') : '<li class="none">' + esc(hits.dataset.none) + '</li>';
    hits.hidden = false;
  };
  q.addEventListener('input', show);
  q.addEventListener('keydown', (e) => {
    const as = [...hits.querySelectorAll('a')];
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); at = Math.max(0, Math.min(as.length - 1, at + (e.key === 'ArrowDown' ? 1 : -1))); as.forEach((a, i) => a.classList.toggle('on', i === at)); }
    else if (e.key === 'Enter' && as.length) location.href = as[Math.max(0, at)].href;
    else if (e.key === 'Escape') { hits.hidden = true; q.blur(); }
  });
  document.addEventListener('click', (e) => { if (!e.target.closest('.search')) hits.hidden = true; });
  document.addEventListener('keydown', (e) => { if (e.key === '/' && document.activeElement !== q) { e.preventDefault(); q.focus(); } });
})();
`;
