/**
 * Sign-in through the forum (DiscourseConnect, forum.lapinbim.com as the identity provider): the store sends the visitor to
 * <forum>/session/sso_provider with a payload (a nonce and where to come back) signed by HMAC-SHA256 with the shared secret;
 * the forum sends them back with the user's identity, signed the same way. The secret is set on both sides (the forum's
 * `discourse_connect_provider_secrets`: `store.lapinbim.com|<secret>`; the store's STORE_SSO_SECRET), never in the repository.
 * https://meta.discourse.org/t/use-discourse-as-an-identity-provider-sso-discourseconnect/32974
 */

import { createHmac, randomBytes, timingSafeEqual } from 'node:crypto';

const hmac = (secret, text) => createHmac('sha256', secret).update(text).digest('hex');

/** The address that sends a visitor to the forum to sign in, and the nonce to expect back. */
export function loginRequest({ forum, secret, returnUrl, nonce = randomBytes(16).toString('hex') }) {
  const sso = Buffer.from(new URLSearchParams({ nonce, return_sso_url: returnUrl }).toString(), 'utf8').toString('base64');
  const url = `${forum.replace(/\/$/, '')}/session/sso_provider?${new URLSearchParams({ sso, sig: hmac(secret, sso) })}`;
  return { url, nonce };
}

/**
 * The forum's answer read: the user, when its signature holds; null otherwise. The nonce is the caller's to check (once).
 * The e-mail address comes with it and is dropped here: the store does not keep it.
 */
export function readLogin({ secret, sso, sig }) {
  if (typeof sso !== 'string' || typeof sig !== 'string' || !/^[0-9a-f]{64}$/.test(sig)) return null;
  const want = Buffer.from(hmac(secret, sso), 'hex');
  if (!timingSafeEqual(want, Buffer.from(sig, 'hex'))) return null;
  const p = new URLSearchParams(Buffer.from(sso, 'base64').toString('utf8'));
  const id = p.get('external_id'), username = p.get('username');
  if (!id || !username || !/^[A-Za-z0-9_.-]{1,60}$/.test(username)) return null;
  const groups = (p.get('groups') ?? '').split(',').map((g) => g.trim()).filter(Boolean);
  return {
    nonce: p.get('nonce') ?? '',
    user: { forumId: id, username, name: (p.get('name') || username).slice(0, 80), admin: p.get('admin') === 'true', moderator: p.get('moderator') === 'true', groups },
  };
}

/** An answer as the forum would make it (the tests). */
export function forumAnswer({ secret, nonce, user }) {
  const sso = Buffer.from(new URLSearchParams({ nonce, external_id: user.forumId, username: user.username, name: user.name ?? '', email: user.email ?? 'x@example.org', admin: String(!!user.admin), moderator: 'false', groups: (user.groups ?? []).join(',') }).toString(), 'utf8').toString('base64');
  return { sso, sig: hmac(secret, sso) };
}
