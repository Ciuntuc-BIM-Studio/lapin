/**
 * A part of the interface fetched the first time it is shown (a dialog, a
 * pane view), so the first load carries only what every session uses
 * (docs/plan-browser-engine.md, Lazy parts). While it comes, a dialog's frame
 * says "Loading…"; when it cannot come (offline, the server unreachable), the
 * frame says so and offers to try again. On the desktop the chunk is read
 * from the app itself and shows at once.
 */

import { Component, lazy, Suspense, useState, type ComponentType, type ReactNode } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../app/store';

/** The message a part of the interface that could not be fetched shows. */
const PART_FAILED = 'This part of Lapin could not be downloaded. Check the connection and try again.';

function Frame({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="dialog-backdrop" role="dialog" aria-label={title}>
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">{title}</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">{children}</div>
      </div>
    </div>
  );
}

class Catch extends Component<{ onRetry: () => void; inline: boolean; title: string; children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() {
    if (!this.state.failed) return this.props.children;
    const body = (
      <>
        <p className="error-text" role="alert">{PART_FAILED}</p>
        <div className="row"><button type="button" className="btn" onClick={() => { this.setState({ failed: false }); this.props.onRetry(); }}>Try again</button></div>
      </>
    );
    return this.props.inline ? <div className="lazy-part">{body}</div> : <Frame title={this.props.title}>{body}</Frame>;
  }
}

/**
 * A component fetched when first drawn. `load` is a dynamic import picking the
 * component out of its module; `inline` draws the waiting and failed states in
 * place (a pane view) instead of as a dialog's frame.
 */
export function lazyPart<P extends object = object>(load: () => Promise<ComponentType<P>>, opts: { title?: string; inline?: boolean } = {}): ComponentType<P> {
  const title = opts.title ?? 'Lapin';
  const inline = opts.inline ?? false;
  // A fetch that failed is asked for afresh next time (React keeps a lazy component's failure for good).
  const make = () => lazy(async () => {
    try { return { default: await load() }; } catch (e) { Lazy = make(); throw e; }
  }) as unknown as ComponentType<P>;
  let Lazy = make();
  function LazyPart(props: P) {
    const [, retry] = useState(0);
    const waiting = inline ? <div className="lazy-part muted">Loading…</div> : <Frame title={title}><p className="muted">Loading…</p></Frame>;
    return (
      <Catch inline={inline} title={title} onRetry={() => retry((n) => n + 1)}>
        <Suspense fallback={waiting}><Lazy {...props} /></Suspense>
      </Catch>
    );
  }
  return LazyPart;
}
