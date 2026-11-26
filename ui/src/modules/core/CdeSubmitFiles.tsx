/**
 * Publishing → Submit files… (docs/plan-cde-references.md §8, stage R7): a coordinator puts another tool's deliverables (a consultant's
 * PDFs, DXFs and IFCs, picked from disk) into the publish queue with a revision, a status and a container, and a note. The files keep
 * their names; the server takes main's commit for the tag (Atrium docs/atrium-server.md, "Files from disk"), runs the W5 checks and IDS on
 * the files, and the gate and the approval are those of Lapin's own issues. Fetched when first opened.
 */

import { useRef, useState } from 'react';
import { FilePlus2, Send, X } from 'lucide-react';
import { notify } from '../../app/store';
import { submitFiles, type Submission } from '../../engine/atrium';
import { defaultSuitability, formProblems, sha256Hex, SUBMIT_ACCEPT, submissionBody, suitabilities, type SubmitForm } from '../../engine/submitFiles';

const kb = (n: number) => (n >= 1024 * 1024 ? `${(n / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`);

export function SubmitFilesForm({ server, project, onDone, onClose }: { server: string; project: string; onDone: (s: Submission) => void; onClose: () => void }) {
  const [files, setFiles] = useState<File[]>([]);
  const [revision, setRevision] = useState('P01');
  const [purpose, setPurpose] = useState<'shared' | 'published'>('shared');
  const [suitability, setSuitability] = useState('S2');
  const [issue, setIssue] = useState('');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState<string | null>(null);
  const input = useRef<HTMLInputElement>(null);
  const form: SubmitForm = { files: files.map((f) => ({ name: f.name, size: f.size })), revision, purpose, suitability, issue, note };
  const problems = formProblems(form);
  const add = (list: FileList | null) => {
    if (!list) return;
    // A name chosen again replaces the one there (ignoring case).
    const picked = [...list];
    setFiles((old) => [...old.filter((f) => !picked.some((p) => p.name.toLowerCase() === f.name.toLowerCase())), ...picked]);
  };
  const send = async () => {
    if (busy || problems.length) return;
    try {
      const blobs = new Map<string, Uint8Array>();
      const hashes: { name: string; sha256: string; size: number }[] = [];
      for (const f of files) {
        setBusy('Reading the files…');
        const bytes = new Uint8Array(await f.arrayBuffer());
        const sha256 = await sha256Hex(bytes);
        blobs.set(sha256, bytes);
        hashes.push({ name: f.name, sha256, size: bytes.length });
      }
      setBusy('Sending the files…');
      const s = await submitFiles(server, project, submissionBody(form, hashes), blobs);
      notify('Submitted for approval: the checks run on the server now.');
      onDone(s);
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(null); }
  };
  return (
    <section className="cde-submit-files" data-cde="submit-files">
      <p className="eyebrow">Submit files <button type="button" className="icon" title="Close" onClick={onClose}><X size={12} /></button></p>
      <p className="muted small">Another program's drawings and models (PDF, DXF, IFC) issued through this queue as Lapin's are: they keep their names, the checks and the gate run on them, and an approver issues them. The issue names the project's latest commit on the server.</p>
      <div className="cde-actions">
        <button type="button" className="btn" disabled={!!busy} onClick={() => input.current?.click()}><FilePlus2 size={13} />Choose files…</button>
        <input ref={input} type="file" multiple accept={SUBMIT_ACCEPT} style={{ display: 'none' }} data-cde="submit-files-input" onChange={(e) => { add(e.target.files); e.target.value = ''; }} />
      </div>
      {files.length > 0 && <ul className="cde-issued-files">{files.map((f) => (
        <li key={f.name} className="small row"><span data-no-i18n>{f.name}</span> <span className="muted" data-no-i18n>{kb(f.size)}</span>
          <button type="button" className="icon mini" title="Leave it out" disabled={!!busy} onClick={() => setFiles(files.filter((x) => x !== f))}><X size={11} /></button></li>))}</ul>}
      <div className="cde-form">
        <label className="row small"><span>Revision</span> <input value={revision} size={5} onChange={(e) => setRevision(e.target.value)} data-cde="submit-revision" /></label>
        <label className="row small"><span>Container</span>
          <select value={purpose} onChange={(e) => { const p = e.target.value as 'shared' | 'published'; setPurpose(p); setSuitability(defaultSuitability(p)); }} data-cde="submit-purpose">
            <option value="shared">Shared</option><option value="published">Published</option>
          </select></label>
        <label className="row small"><span>Status</span>
          <select value={suitability} onChange={(e) => setSuitability(e.target.value)} data-cde="submit-suitability">
            {suitabilities(purpose).map((s) => <option key={s} value={s}>{s}</option>)}
          </select></label>
        <label className="row small"><span>Issue number</span> <input value={issue} size={5} placeholder="next" onChange={(e) => setIssue(e.target.value)} /></label>
        <label className="row small"><span>Note</span> <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="From the structural engineer" data-cde="submit-note" /></label>
      </div>
      {files.length > 0 && problems.map((p) => <p key={`${p.file}:${p.text}`} className="cde-warn small">{p.file && <><span data-no-i18n>{p.file}</span>: </>}<span>{p.text}</span></p>)}
      {busy && <p className="muted small">{busy}</p>}
      <div className="cde-actions">
        <button type="button" className="btn primary" disabled={!!busy || problems.length > 0} data-cde="submit-files-send" onClick={() => void send()}><Send size={13} />Submit for approval</button>
      </div>
    </section>
  );
}
