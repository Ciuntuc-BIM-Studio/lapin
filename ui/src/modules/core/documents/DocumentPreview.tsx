/**
 * A filled document on its pages (docs/plan-documents.md, Interfața): A4 sheets one under the other, at the page's margins, its
 * header and footer on each with the page fields numbered, the blocks laid on the pages as they fit (measured first, unseen),
 * a page break starting a page; headings, paragraphs, lists, tables (a plain one without its rules, numbers to the right, a
 * total row bold), the project's pictures from their pens, the table of contents from the headings with their pages, and the
 * fields still empty marked where they stand. The document's own words are never translated (data-no-i18n).
 */

import { Fragment, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { DrawingLayer } from '../../../viewport/DrawingLayer';
import { paginate, runsText, runText, type DocBlock, type DocImage, type DocPreview, type DocRun } from './docModel';

/** A point in paper mm. */
const PT = 25.4 / 72;
const BODY = 10.5, CAPTION = 9, NOTE = 9, HEADER = 9, TABLE = 9.5;
const headingPt = (level: number) => (level <= 1 ? 16 : level === 2 ? 13 : 11.5);

function Runs({ runs, page = 1, pages = 1 }: { runs: DocRun[]; page?: number; pages?: number }) {
  return (
    <>
      {runs.map((r, i) => {
        if (r.missing) return <mark key={i} className="doc-missing" data-missing={r.text} title={r.text}>⟨{r.text}⟩</mark>;
        const t = runText(r, page, pages);
        if (!r.bold && !r.italic) return <Fragment key={i}>{t}</Fragment>;
        return <span key={i} style={{ fontWeight: r.bold ? 700 : undefined, fontStyle: r.italic ? 'italic' : undefined }}>{t}</span>;
      })}
    </>
  );
}

/** A picture from its pens, at its paper size (narrowed to the text's width), k pixels a mm. */
function Picture({ img, k, width }: { img: DocImage; k: number; width: number }) {
  const s = Math.min(1, width / Math.max(img.w, 1));
  const kk = k * s;
  const toScreen = useMemo(() => (p: { x: number; y: number }) => ({ x: p.x * kk, y: p.y * kk }), [kk]);
  return (
    <figure className="doc-figure">
      <svg width={img.w * kk} height={img.h * kk} viewBox={`0 0 ${img.w * kk} ${img.h * kk}`} className="doc-picture">
        <rect x={0} y={0} width={img.w * kk} height={img.h * kk} fill="#ffffff" />
        <DrawingLayer drawing={{ pens: img.pens, scale: 1000 }} toScreen={toScreen} pxPerMetre={kk} minText={1} />
      </svg>
      {img.caption && <figcaption style={{ fontSize: CAPTION * PT * k }}>{img.caption}</figcaption>}
    </figure>
  );
}

/** One filled block as the pages show it (the editor's atoms too), k pixels a paper mm, `width` the text's in mm. */
export function Block({ b, k, width, page, pages, toc }: { b: DocBlock; k: number; width: number; page: number; pages: number; toc: { level: number; text: string; page: number }[] }) {
  const fs = (pt: number) => pt * PT * k;
  switch (b.kind) {
    case 'heading': {
      const Tag = (`h${Math.min(Math.max(b.level, 1), 4)}`) as 'h1';
      return <Tag className="doc-heading" style={{ fontSize: fs(headingPt(b.level)) }}><Runs runs={b.runs} page={page} pages={pages} /></Tag>;
    }
    case 'paragraph': {
      const pt = b.style === 'caption' ? CAPTION : b.style === 'note' ? NOTE : BODY;
      return <p className={`doc-p doc-${b.style}`} style={{ fontSize: fs(pt) }}><Runs runs={b.runs} page={page} pages={pages} /></p>;
    }
    case 'list': {
      const Tag = b.ordered ? 'ol' : 'ul';
      return <Tag className="doc-list" style={{ fontSize: fs(BODY) }}>{b.items.map((it, i) => <li key={i}><Runs runs={it} page={page} pages={pages} /></li>)}</Tag>;
    }
    case 'table': {
      const numeric = new Set(b.numeric);
      const sum = b.widths?.reduce((a, x) => a + x, 0) ?? 0;
      const last = b.rows.length - 1;
      return (
        <div className="doc-table-wrap" style={{ fontSize: fs(TABLE) }}>
          {b.title && <div className="doc-table-title" style={{ fontSize: fs(BODY) }}>{b.title}</div>}
          <table className={`doc-table${b.plain ? ' plain' : ''}`}>
            {b.widths && sum > 0 && <colgroup>{b.widths.map((w, i) => <col key={i} style={{ width: `${(100 * w) / sum}%` }} />)}</colgroup>}
            {b.header.length > 0 && <thead><tr>{b.header.map((h, i) => <th key={i} className={numeric.has(i) ? 'num' : undefined}>{h}</th>)}</tr></thead>}
            <tbody>
              {b.rows.map((row, r) => (
                <tr key={r} className={b.total && r === last ? 'total' : undefined}>
                  {row.map((cell, c) => <td key={c} className={numeric.has(c) ? 'num' : undefined}><Runs runs={cell} page={page} pages={pages} /></td>)}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
    }
    case 'image': return <Picture img={b} k={k} width={width} />;
    case 'toc':
      return (
        <div className="doc-toc" style={{ fontSize: fs(BODY) }}>
          {b.title && <div className="doc-heading" style={{ fontSize: fs(headingPt(2)), fontWeight: 700 }}>{b.title}</div>}
          {toc.map((e, i) => (
            <div key={i} className="doc-toc-row" style={{ paddingLeft: (e.level - 2) * 4 * k }}>
              <span>{e.text}</span><span className="doc-toc-dots" /><span>{e.page}</span>
            </div>
          ))}
        </div>
      );
    default: return null;
  }
}

/** The preview: `scrollTo` a missing field's key brings its first place into view, marked a moment. */
export function DocumentPreview({ doc, k }: { doc: DocPreview; k: number }) {
  const { page } = doc;
  const [top, right, bottom, left] = page.margins;
  const width = page.w - left - right;
  const room = (page.h - top - bottom) * k;
  const measure = useRef<HTMLDivElement>(null);
  // The pages kept with the document they were laid for: a new document (an input that changes its number of blocks) is
  // drawn on one page until it is measured, never on the last one's page lists.
  const [pages, setPages] = useState<{ doc: DocPreview; list: number[][] } | null>(null);

  // The blocks measured at the text's width, unseen, then laid on pages; again whenever the document or the size changes.
  useLayoutEffect(() => {
    const el = measure.current;
    if (!el) return;
    const kids = [...el.children] as HTMLElement[];
    // Each block's wrapper holds its margins (display: flow-root), so its height is all it takes.
    const heights = doc.blocks.map((_, i) => kids[i]?.getBoundingClientRect().height ?? 0);
    setPages({ doc, list: paginate(doc.blocks, heights, room) });
  }, [doc, k, room]);

  // The headings for the table of contents, each with the page it lands on (the document's title, a level 1, is not listed).
  const laid = pages?.doc === doc ? pages.list : [doc.blocks.map((_, i) => i)];
  const pageOf = new Map<number, number>();
  laid.forEach((list, p) => list.forEach((i) => pageOf.set(i, p + 1)));
  const toc = doc.blocks.flatMap((b, i) => (b.kind === 'heading' && b.level >= 2 && b.level <= 3 ? [{ level: b.level, text: runsText(b.runs), page: pageOf.get(i) ?? 1 }] : []));
  const n = laid.length;

  const sheetStyle = { width: page.w * k, minHeight: page.h * k, padding: `${top * k}px ${right * k}px ${bottom * k}px ${left * k}px` };
  return (
    <div className="doc-pages" data-no-i18n="">
      <div ref={measure} className="doc-sheet doc-measure" aria-hidden style={{ ...sheetStyle, minHeight: 0 }}>
        {doc.blocks.map((b, i) => <div key={i} className="doc-block"><Block b={b} k={k} width={width} page={1} pages={1} toc={toc} /></div>)}
      </div>
      {laid.map((list, p) => (
        <div key={p} className="doc-sheet" style={sheetStyle} data-page={p + 1}>
          {page.header.length > 0 && <div className="doc-header" style={{ top: (top * k) / 2 - HEADER * PT * k, left: left * k, right: right * k, fontSize: HEADER * PT * k }}><Runs runs={page.header} page={p + 1} pages={n} /></div>}
          {list.map((i) => <div key={i} className="doc-block" data-doc-block={i}><Block b={doc.blocks[i]} k={k} width={width} page={p + 1} pages={n} toc={toc} /></div>)}
          {page.footer.length > 0 && <div className="doc-footer" style={{ bottom: (bottom * k) / 2 - HEADER * PT * k, left: left * k, right: right * k, fontSize: HEADER * PT * k }}><Runs runs={page.footer} page={p + 1} pages={n} /></div>}
        </div>
      ))}
    </div>
  );
}
