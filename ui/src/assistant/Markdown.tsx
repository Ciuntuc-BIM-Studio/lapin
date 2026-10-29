/**
 * The little Markdown local models answer in, drawn as React elements (no
 * HTML is ever taken from the model): paragraphs, bullet and numbered lists,
 * tables, **bold**, *italic* and `code`.
 */

import { Fragment, type ReactNode } from 'react';

function inline(text: string): ReactNode[] {
  const out: ReactNode[] = [];
  const re = /(\*\*[^*]+\*\*|`[^`]+`|\*[^*\s][^*]*\*)/g;
  let last = 0;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(text.slice(last, m.index));
    const t = m[0];
    out.push(t.startsWith('**') ? <strong key={m.index}>{t.slice(2, -2)}</strong> : t.startsWith('`') ? <code key={m.index}>{t.slice(1, -1)}</code> : <em key={m.index}>{t.slice(1, -1)}</em>);
    last = m.index + t.length;
  }
  if (last < text.length) out.push(text.slice(last));
  return out;
}

const cells = (line: string) => line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map((c) => c.trim());
const isRule = (line: string) => /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(line);

export function Markdown({ text }: { text: string }) {
  const lines = text.split('\n');
  const blocks: ReactNode[] = [];
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    if (/^\s*\|/.test(line)) {
      const rows: string[][] = [];
      let head: string[] | null = null;
      while (i < lines.length && /^\s*\|/.test(lines[i])) {
        if (isRule(lines[i])) { head = rows.pop() ?? null; } else rows.push(cells(lines[i]));
        i++;
      }
      blocks.push(
        <div className="assistant-table-scroll md-table" key={`t${i}`}>
          <table>
            {head && <thead><tr>{head.map((c, j) => <th key={j}>{inline(c)}</th>)}</tr></thead>}
            <tbody>{rows.map((r, k) => <tr key={k}>{r.map((c, j) => <td key={j}>{inline(c)}</td>)}</tr>)}</tbody>
          </table>
        </div>,
      );
      continue;
    }
    if (/^\s*([-*•]|\d+[.)])\s+/.test(line)) {
      const ordered = /^\s*\d/.test(line);
      const items: string[] = [];
      while (i < lines.length && /^\s*([-*•]|\d+[.)])\s+/.test(lines[i])) { items.push(lines[i].replace(/^\s*([-*•]|\d+[.)])\s+/, '')); i++; }
      const List = ordered ? 'ol' : 'ul';
      blocks.push(<List key={`l${i}`}>{items.map((x, k) => <li key={k}>{inline(x)}</li>)}</List>);
      continue;
    }
    if (!line.trim()) { i++; continue; }
    const para: string[] = [];
    while (i < lines.length && lines[i].trim() && !/^\s*\|/.test(lines[i]) && !/^\s*([-*•]|\d+[.)])\s+/.test(lines[i])) { para.push(lines[i].replace(/^#+\s*/, '')); i++; }
    blocks.push(<p key={`p${i}`}>{para.map((p, k) => <Fragment key={k}>{k > 0 && <br />}{inline(p)}</Fragment>)}</p>);
  }
  return <>{blocks}</>;
}
