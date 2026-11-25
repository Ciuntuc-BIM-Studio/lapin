/**
 * A part of the window that throws while it draws stops alone (forum topic 258: the profile panel threw and the whole window
 * went white). `PartBoundary` goes round a panel, an overlay, a properties section or a dialog: what it holds is replaced by a
 * short message saying the rest still works, with Try again, Report… (the help menu's form, filled in with the error) and,
 * where the part can be put away, Close. `WindowBoundary` goes round the whole interface, the last resort: the window says
 * it stopped and offers to reload it and to report it on the forum, never a blank page. The error joins the recent ones a
 * report carries.
 */

import { Component, type ErrorInfo, type ReactNode } from 'react';
import { askReport, keepRecentError, openForumTopic, type ReportStart } from './support';

/** The first line of a component stack: where in the interface it threw. */
const whereIn = (info: ErrorInfo): string => (info.componentStack ?? '').split('\n').map((l) => l.trim()).find(Boolean) ?? '';

const messageOf = (e: unknown): string => (e instanceof Error ? e.message : String(e));

/** What a report of a part that stopped starts with. */
export function failureReport(part: string, message: string): ReportStart {
  return { kind: 'bug', title: `${part}: stopped working`.slice(0, 200), description: `${part} stopped working: ${message}\n\nWhat I did just before:\n` };
}

interface PartProps {
  /** The part's name, in English (the report's title; the window translates what it shows). */
  part: string;
  /** Puts the part away (closes its panel or dialog); without it, only Try again. */
  onClose?: () => void;
  /** A change of it (another element selected) draws the part afresh. */
  resetKey?: unknown;
  /** A one-line message, for a part as small as a properties section. */
  compact?: boolean;
  /** The message as a card over the view (a part drawn over it, as the profile panel is). */
  overlay?: boolean;
  children: ReactNode;
}

export class PartBoundary extends Component<PartProps, { error: string | null }> {
  state: { error: string | null } = { error: null };

  static getDerivedStateFromError(e: unknown) { return { error: messageOf(e) || 'unknown error' }; }

  componentDidCatch(e: unknown, info: ErrorInfo) {
    keepRecentError(`render: ${this.props.part}: ${messageOf(e)} ${whereIn(info)}`.trim());
  }

  componentDidUpdate(prev: PartProps) {
    if (this.state.error != null && !Object.is(prev.resetKey, this.props.resetKey)) this.setState({ error: null });
  }

  render() {
    const { error } = this.state;
    if (error == null) return this.props.children;
    const { part, onClose, compact, overlay } = this.props;
    return (
      <div className={`part-failed${compact ? ' compact' : ''}${overlay ? ' overlay' : ''}`} role="alert" data-testid="part-failed" data-part={part}>
        <p className="small"><b>This part of Lapin stopped working; the rest of the window still works.</b></p>
        {!compact && <p className="small muted mono" data-no-i18n>{error}</p>}
        <div className="row">
          <button type="button" className="btn compact" onClick={() => this.setState({ error: null })}>Try again</button>
          <button type="button" className="btn compact" data-testid="part-failed-report" onClick={() => askReport(failureReport(part, error))}>Report…</button>
          {onClose && <button type="button" className="btn compact" onClick={() => { this.setState({ error: null }); onClose(); }}>Close</button>}
        </div>
      </div>
    );
  }
}

/** Round the whole interface: what no part caught still leaves a window that says so and can be reloaded. */
export class WindowBoundary extends Component<{ children: ReactNode }, { error: string | null }> {
  state: { error: string | null } = { error: null };

  static getDerivedStateFromError(e: unknown) { return { error: messageOf(e) || 'unknown error' }; }

  componentDidCatch(e: unknown, info: ErrorInfo) {
    keepRecentError(`render: window: ${messageOf(e)} ${whereIn(info)}`.trim());
  }

  render() {
    const { error } = this.state;
    if (error == null) return this.props.children;
    const report = failureReport("Lapin's window", error);
    return (
      <div className="window-failed" role="alert" data-testid="window-failed">
        <h2>Lapin's window stopped working.</h2>
        <p>Reload the window to go on. Please report what you did just before, so it can be fixed.</p>
        <p className="small muted mono" data-no-i18n>{error}</p>
        <div className="row">
          <button type="button" className="btn primary" onClick={() => window.location.reload()}>Reload the window</button>
          <button type="button" className="btn" onClick={() => this.setState({ error: null })}>Try again</button>
          <button type="button" className="btn" onClick={() => openForumTopic(report.description)}>Report on the forum…</button>
        </div>
      </div>
    );
  }
}
