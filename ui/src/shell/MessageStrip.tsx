/**
 * The messages console (decision 538): a strip docked at the bottom of a pane (the graph editor, the Script panel).
 * Collapsed it is one line: the latest message and the count of errors; open it lists the rows (kind, time, source, text;
 * a row with a target focuses it when clicked), with the filters Errors / Warnings / All and their counts, Copy (plain text)
 * and Clear, and its height dragged. Its state (open, height, filter) is remembered per place. Messages only: nothing is typed.
 */

import { useEffect, useMemo, useRef } from 'react';
import { AlertTriangle, ChevronDown, ChevronUp, Copy, Eraser, Info, XCircle } from 'lucide-react';
import { clock, countsOf, filterMessages, messagesText, MAX_STRIP, MIN_STRIP, type Message, type MessageFilter, type MessageLogApi } from '../platform/messages';
import { notify } from '../app/store';

const ICON = { error: XCircle, warning: AlertTriangle, info: Info } as const;
const KIND_LABEL = { error: 'Error', warning: 'Warning', info: 'Info' } as const;

function Row({ m, onOpen }: { m: Message; onOpen?: (m: Message) => void }) {
  const Icon = ICON[m.kind];
  const go = m.target !== undefined && onOpen ? () => onOpen(m) : undefined;
  return (
    <li className={`msg-row k-${m.kind}${go ? ' go' : ''}`} data-kind={m.kind} {...(go ? { role: 'button', tabIndex: 0, title: 'Go to it', onClick: go, onKeyDown: (e: React.KeyboardEvent) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); go(); } } } : {})}>
      <Icon size={13} className="msg-icon" aria-label={KIND_LABEL[m.kind]} />
      <span className="msg-time" data-no-i18n>{clock(m.time)}</span>
      {m.source && <b className="msg-source" {...(m.sourcePlain ? { 'data-no-i18n': '' } : {})}>{m.source}</b>}
      <span className="msg-text" {...(m.textPlain ? { 'data-no-i18n': '' } : {})}>{m.text}</span>
      {m.runs > 1 && <span className="msg-runs" data-no-i18n>×{m.runs}</span>}
    </li>
  );
}

export function MessageStrip({ log, onOpen }: { log: MessageLogApi; onOpen?: (m: Message) => void }) {
  const { open, height, filter, list, announce } = log.use();
  const counts = useMemo(() => countsOf(list), [list]);
  const shown = useMemo(() => filterMessages(list, filter), [list, filter]);
  const body = useRef<HTMLUListElement>(null);
  const last = list[list.length - 1];
  // Newest at the bottom, in sight, unless the user scrolled up to read.
  useEffect(() => {
    const el = body.current;
    if (el && el.scrollHeight - el.scrollTop - el.clientHeight < 60) el.scrollTop = el.scrollHeight;
  }, [shown, open]);
  const drag = (e: React.PointerEvent) => {
    e.preventDefault();
    const y0 = e.clientY, h0 = height;
    const move = (p: PointerEvent) => log.setStrip({ height: Math.max(MIN_STRIP, Math.min(MAX_STRIP, Math.round(h0 + (y0 - p.clientY)))) });
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  };
  const copy = async () => {
    try { await navigator.clipboard.writeText(messagesText(shown)); } catch { notify('Copy failed.'); }
  };
  const filters: [MessageFilter, string, number][] = [['errors', 'Errors', counts.errors], ['warnings', 'Warnings', counts.warnings], ['all', 'All', counts.all]];
  return (
    <section className={`msg-strip${open ? ' open' : ''}`} style={open ? { height } : undefined} data-testid="messages" data-open={open ? 'on' : 'off'} aria-label="Messages">
      {open && <div className="msg-resize" onPointerDown={drag} title="Drag to resize" />}
      <div className="msg-head">
        <button type="button" className="msg-toggle" aria-expanded={open} title={open ? 'Hide the messages' : 'Show the messages'} data-action="messages-toggle" onClick={() => log.setStrip({ open: !open })}>
          {open ? <ChevronDown size={14} /> : <ChevronUp size={14} />}<span className="msg-title">Messages</span>
        </button>
        {open ? (
          <>
            <div className="msg-filters" role="group" aria-label="Messages">
              {filters.map(([id, label, n]) => (
                <button type="button" key={id} className={`msg-filter${filter === id ? ' on' : ''}`} aria-pressed={filter === id} data-filter={id} onClick={() => log.setStrip({ filter: id })}>
                  <span>{label}</span> <span className="msg-count" data-no-i18n>{n}</span>
                </button>
              ))}
            </div>
            <span className="grow" />
            <button type="button" className="icon" title="Copy the messages as text" aria-label="Copy" data-action="messages-copy" disabled={shown.length === 0} onClick={() => void copy()}><Copy size={13} /></button>
            <button type="button" className="icon" title="Clear the messages" aria-label="Clear" data-action="messages-clear" disabled={list.length === 0} onClick={log.clear}><Eraser size={13} /></button>
          </>
        ) : (
          <button type="button" className="msg-latest" onClick={() => log.setStrip({ open: true })} tabIndex={-1} aria-hidden>
            {last && <><span className={`msg-dot k-${last.kind}`} />{last.source && <b {...(last.sourcePlain ? { 'data-no-i18n': '' } : {})}>{last.source}</b>}<span {...(last.textPlain ? { 'data-no-i18n': '' } : {})}>{last.text}</span></>}
            <span className="grow" />
            {counts.errors > 0 && <span className="msg-badge" data-no-i18n>{counts.errors}</span>}
          </button>
        )}
      </div>
      {/* Only the new errors are told to screen readers (the rows themselves are a log that is not read out as it fills). */}
      <div className="sr-only" role="status" aria-live="polite" data-no-i18n>{announce}</div>
      {open && (
        <ul className="msg-list" ref={body} role="log" aria-live="off" aria-label="Messages" data-field="messages-list">
          {shown.length === 0 ? <li className="msg-empty muted small">No messages.</li> : shown.map((m) => <Row key={m.id} m={m} onOpen={onOpen} />)}
        </ul>
      )}
    </section>
  );
}
