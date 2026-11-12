/**
 * The Lapin Extensions store keys Lapin trusts (docs/plan-extensions.md §9.7, stage 1): Ed25519 public keys, 32 raw bytes in
 * base64, each with its id (the first 16 hex digits of the SHA-256 of those bytes). The catalogue's index and each of its
 * packages are signed by one of them (lapin-extensions: tools/publish.mjs); the updater's key is another and signs nothing here.
 *
 * Empty: signing is not live yet, and Lapin's own catalogue is read unsigned as in stage 0. Once a key is listed, the default
 * catalogue must be signed by a listed key, or it is not read. The key is made by `node scripts/store-key.mjs`, which prints
 * the line to add here. To rotate: add the new key, publish with it, and remove the old one a few releases later; a key that
 * leaked is removed at once.
 */

import type { StoreKey } from './signature';

export const STORE_KEYS: StoreKey[] = [];
