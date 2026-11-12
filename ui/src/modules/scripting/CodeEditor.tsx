/**
 * The Script panel's editor: CodeMirror 6 (MIT) with Python's highlighting and indentation, the lines of the last
 * error marked, Ctrl/Cmd+Enter to run, and completion after `lapin.` from the command catalogue (logic.ts). Imported
 * only by the panel, which is itself fetched when first opened, so CodeMirror is never in the app's first load.
 */

import { useEffect, useRef } from 'react';
import { EditorState, StateEffect, StateField, type Extension } from '@codemirror/state';
import { Decoration, EditorView, keymap, lineNumbers, highlightActiveLine, drawSelection, type DecorationSet } from '@codemirror/view';
import { defaultKeymap, history, historyKeymap, indentWithTab } from '@codemirror/commands';
import { bracketMatching, defaultHighlightStyle, indentOnInput, syntaxHighlighting } from '@codemirror/language';
import { autocompletion, closeBrackets, closeBracketsKeymap, completionKeymap, type CompletionContext, type CompletionResult } from '@codemirror/autocomplete';
import { python } from '@codemirror/lang-python';
import { complete, type Completion } from './logic';

const markError = StateEffect.define<number | null>();
const errorLine = StateField.define<DecorationSet>({
  create: () => Decoration.none,
  update(deco, tr) {
    deco = deco.map(tr.changes);
    for (const e of tr.effects) {
      if (!e.is(markError)) continue;
      if (e.value == null || e.value < 1 || e.value > tr.state.doc.lines) return Decoration.none;
      return Decoration.set([Decoration.line({ class: 'cm-error-line' }).range(tr.state.doc.line(e.value).from)]);
    }
    return deco;
  },
  provide: (f) => EditorView.decorations.from(f),
});

/** Completion from the catalogue's namespaces (logic.ts `complete`), read when asked so a catalogue read later is used. */
function lapinCompletions(spaces: () => Map<string, Completion[]>) {
  return (ctx: CompletionContext): CompletionResult | null => {
    const line = ctx.state.doc.lineAt(ctx.pos);
    const before = line.text.slice(0, ctx.pos - line.from);
    const r = complete(spaces(), before);
    if (!r || (!r.options.length && !ctx.explicit)) return null;
    return { from: line.from + r.from, options: r.options.map((o) => ({ label: o.label, type: o.type, detail: o.detail, info: o.info, apply: o.apply })), validFor: /^\w*$/ };
  };
}

export function CodeEditor({ value, onChange, onRun, line, spaces, label }: {
  value: string; onChange: (v: string) => void; onRun: () => void; line: number | null; spaces: () => Map<string, Completion[]>; label: string;
}) {
  const host = useRef<HTMLDivElement>(null);
  const view = useRef<EditorView | null>(null);
  // The latest callbacks, without making the editor again.
  const calls = useRef({ onChange, onRun });
  calls.current = { onChange, onRun };

  useEffect(() => {
    const extensions: Extension[] = [
      lineNumbers(), highlightActiveLine(), drawSelection(), history(), indentOnInput(), bracketMatching(), closeBrackets(),
      syntaxHighlighting(defaultHighlightStyle, { fallback: true }), python(),
      autocompletion({ override: [lapinCompletions(spaces)], activateOnTyping: true }),
      keymap.of([
        { key: 'Mod-Enter', preventDefault: true, run: () => { calls.current.onRun(); return true; } },
        ...closeBracketsKeymap, ...completionKeymap, ...defaultKeymap, ...historyKeymap, indentWithTab,
      ]),
      EditorView.updateListener.of((u) => { if (u.docChanged) calls.current.onChange(u.state.doc.toString()); }),
      EditorView.contentAttributes.of({ 'aria-label': label, 'data-field': 'script-editor', spellcheck: 'false' }),
      errorLine,
    ];
    const v = new EditorView({ state: EditorState.create({ doc: value, extensions }), parent: host.current! });
    view.current = v;
    return () => { v.destroy(); view.current = null; };
    // Made once; the value follows below.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // A script opened (or read again from its file): the editor shows it.
  useEffect(() => {
    const v = view.current;
    if (v && v.state.doc.toString() !== value) v.dispatch({ changes: { from: 0, to: v.state.doc.length, insert: value } });
  }, [value]);

  useEffect(() => { view.current?.dispatch({ effects: markError.of(line) }); }, [line, value]);

  return <div className="script-editor" ref={host} data-no-i18n />;
}
