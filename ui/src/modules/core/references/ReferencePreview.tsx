/**
 * A reference document's file shown (docs/plan-cde-references.md §5): a PDF drawn page by page with pdf.js (loaded only when the
 * first one is shown, as the checks do), an image inline, a text file's text, and an IFC offered as a link to the project
 * (Open as link: the same import as File → Link IFC…, docs/links.md). Anything else is saved with Save a copy.
 */

import { useEffect, useRef, useState } from 'react';
import { ChevronLeft, ChevronRight, Link2 } from 'lucide-react';
import { notify } from '../../../app/store';
import { useRefAdapter } from './adapter';
import type { FileKind, RefDoc, RefFile } from './references';

const bytesOf = (data: string) => Uint8Array.from(atob(data), (c) => c.charCodeAt(0));

export function ReferencePreview({ doc, file, kind }: { doc: RefDoc; file: RefFile; kind: FileKind }) {
  if (kind === 'pdf') return <PdfPreview doc={doc} file={file} />;
  if (kind === 'image') return <ImagePreview doc={doc} file={file} />;
  if (kind === 'text') return <TextPreview doc={doc} file={file} />;
  if (kind === 'ifc') return <IfcPreview doc={doc} file={file} />;
  return <p className="muted small">No preview for this kind of file: Save a copy opens it in its own program.</p>;
}

function PdfPreview({ doc, file }: { doc: RefDoc; file: RefFile }) {
  const adapter = useRefAdapter();
  const canvas = useRef<HTMLCanvasElement>(null);
  const pdf = useRef<import('pdfjs-dist').PDFDocumentProxy | null>(null);
  const [page, setPage] = useState(1);
  const [pages, setPages] = useState(0);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    let live = true;
    void (async () => {
      try {
        const r = await adapter.file(doc.id, file.name);
        if (!r) throw new Error('The file could not be read.');
        const { openPdf } = await import('../pdfPage');
        const d = await openPdf(bytesOf(r.data));
        if (!live) { void d.destroy(); return; }
        pdf.current = d;
        setPages(d.numPages);
        setPage(1);
      } catch (e) { if (live) setProblem(e instanceof Error ? e.message : String(e)); }
    })();
    return () => { live = false; void pdf.current?.destroy(); pdf.current = null; };
  }, [doc.id, file.name, adapter]);

  useEffect(() => {
    const c = canvas.current, d = pdf.current;
    if (!c || !d || pages === 0) return;
    let live = true;
    void import('../pdfPage').then(({ drawPage }) => live ? drawPage(d, page, c, 0.9) : null).catch((e) => { if (live) setProblem(e instanceof Error ? e.message : String(e)); });
    return () => { live = false; };
  }, [page, pages]);

  if (problem) return <p className="cde-warn small">{problem}</p>;
  return (
    <div className="refs-preview" data-testid="refs-preview">
      {pages > 1 && (
        <div className="row small">
          <button type="button" className="icon mini" aria-label="Previous page" disabled={page <= 1} onClick={() => setPage(page - 1)}><ChevronLeft size={12} /></button>
          <span>{page} / {pages}</span>
          <button type="button" className="icon mini" aria-label="Next page" disabled={page >= pages} onClick={() => setPage(page + 1)}><ChevronRight size={12} /></button>
        </div>
      )}
      <canvas ref={canvas} />
      {pages === 0 && <p className="muted small">Reading…</p>}
    </div>
  );
}

function ImagePreview({ doc, file }: { doc: RefDoc; file: RefFile }) {
  const adapter = useRefAdapter();
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    let made: string | null = null, live = true;
    void adapter.file(doc.id, file.name).then((r) => {
      if (!r || !live) return;
      made = URL.createObjectURL(new Blob([bytesOf(r.data) as BlobPart], { type: r.mime ?? 'image/png' }));
      setUrl(made);
    });
    return () => { live = false; if (made) URL.revokeObjectURL(made); };
  }, [doc.id, file.name, adapter]);
  return <div className="refs-preview" data-testid="refs-preview">{url ? <img src={url} alt={file.name} /> : <p className="muted small">Reading…</p>}</div>;
}

function TextPreview({ doc, file }: { doc: RefDoc; file: RefFile }) {
  const adapter = useRefAdapter();
  const [text, setText] = useState<string | null>(null);
  useEffect(() => { let live = true; void adapter.text(doc.id, file.name, '1').then((r) => { if (live) setText(r?.pages[0]?.text ?? ''); }); return () => { live = false; }; }, [doc.id, file.name, adapter]);
  return <pre className="refs-text small">{text ?? '…'}</pre>;
}

function IfcPreview({ doc, file }: { doc: RefDoc; file: RefFile }) {
  const adapter = useRefAdapter();
  const open = async () => {
    const r = await adapter.file(doc.id, file.name);
    if (!r) { notify('The file could not be read.'); return; }
    // The same two steps as File → Link IFC…: the file looked at, then the dialog to choose what comes in.
    const { surveyIfc } = await import('../../../engine/ifcImport');
    await surveyIfc({ name: r.name, data: r.data }, 'link');
  };
  if (adapter.kind === 'server') return <p className="muted small refs-preview">A model: save a copy, and open it in Lapin as a link (File → Link IFC…).</p>;
  return (
    <div className="refs-preview">
      <p className="muted small">A model: it opens in the project as a link, read-only, drawn in the views and in 3D.</p>
      <button type="button" className="btn small" data-testid="refs-link" onClick={() => void open()}><Link2 size={12} /> Open as link</button>
    </div>
  );
}
