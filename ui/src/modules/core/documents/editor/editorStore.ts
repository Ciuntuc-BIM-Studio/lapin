/**
 * What the document editor's node views read (they are rendered in portals of their own, so they read a store rather than
 * props): the document edited, what it can read (`document.fields`), the page's scale, and the blocks filled by
 * `document.block`, each kept by its block and the repeats it stands in, asked once while the editor is open.
 */

import { useEffect } from 'react';
import type { Editor } from '@tiptap/core';
import { create } from 'zustand';
import { query } from '../../../../engine/session';
import type { DocBlock } from '../docModel';
import { scopeKey, type Block, type DocScopeArg } from './convert';
import type { DocFieldsInfo } from './fields';

/** One block filled (`document.block`): its blocks, what it misses, the items' values and how many items each repeat keeps. */
export interface FilledPart { blocks: DocBlock[]; missing: string[]; scope: Record<string, string>; items: number[] }

/** An input the document declares, as the editor edits it. */
export interface InputDef { key: string; label: string; multiline: boolean; default?: string | null; group?: string | null }

interface EditorState {
  docId: string | null;
  info: DocFieldsInfo | null;
  /** Pixels a paper mm, and the text's width in mm. */
  k: number;
  width: number;
  inputDefs: InputDef[];
  filled: Record<string, FilledPart | 'pending'>;
  /** Bumped by every change of the edited document, so the chips find their repeats again. */
  tick: number;
  editor: Editor | null;
  /** The item each repeat shows its fields and blocks with (from 0), by its chain without indexes. */
  picks: Record<string, number>;
  /** The atom selected, else the innermost repeat or condition the cursor is in: its position, type and attributes. */
  sel: SelectedPart | null;
}

export interface SelectedPart { pos: number; type: 'docAtom' | 'docRepeat' | 'docIf'; attrs: Record<string, unknown>; alt: boolean }

export const useDocEditor = create<EditorState>(() => ({ docId: null, info: null, k: 2.6, width: 165, inputDefs: [], filled: {}, tick: 0, editor: null, sel: null, picks: {} }));

/** The key a block filled inside its repeats is kept by. */
export const partKey = (block: Block | null, chain: DocScopeArg[]) => `${block ? JSON.stringify(block) : '-'}|${scopeKey(chain)}`;

/** The block filled in its repeats (none: only the repeats' values), asked of the engine the first time; nothing when skipped. */
export function useFilled(block: Block | null, chain: DocScopeArg[], skip = false): FilledPart | null {
  const key = partKey(block, chain);
  const docId = useDocEditor((s) => s.docId);
  const got = useDocEditor((s) => s.filled[key]);
  useEffect(() => {
    if (!docId || got || skip) return;
    useDocEditor.setState((s) => ({ filled: { ...s.filled, [key]: 'pending' } }));
    void query<FilledPart>('document.block', { id: docId, ...(block ? { block } : {}), ...(chain.length ? { scope: chain } : {}) }).then((r) => {
      useDocEditor.setState((s) => ({ filled: { ...s.filled, [key]: r ?? { blocks: [], missing: [], scope: {}, items: [] } } }));
    });
    // The key holds the block and its chain.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [docId, key, got, skip]);
  return !skip && got && got !== 'pending' ? got : null;
}
