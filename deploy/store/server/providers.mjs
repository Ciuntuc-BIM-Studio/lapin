/**
 * Paid extensions' licence keys checked with the seller's provider (docs/plan-extensions.md §12, stage 2a): each provider's
 * public licence API, which needs no account of ours. The answer is one of
 *   active       a key of this product, good;
 *   revoked      a key of this product, but refunded, disabled, expired, disputed, or its subscription over;
 *   invalid      not a key of this product (unknown, mistyped, another product's);
 *   unavailable  the provider did not answer usefully (down, too slow, rate-limited): nothing is concluded.
 * The key is sent to the provider and forgotten: nothing here keeps or logs it. No dependencies (Node 22's fetch).
 *
 * Checked against the providers' public docs on 17.11.2026:
 *   Lemon Squeezy  POST https://api.lemonsqueezy.com/v1/licenses/validate  (form: license_key)
 *                  → { valid, error, license_key: { status: inactive|active|expired|disabled }, meta: { product_id, … } };
 *                  errors 400 / 404 / 422 with `error`. Any shop's key validates there, so meta.product_id is compared.
 *   Gumroad        POST https://api.gumroad.com/v2/licenses/verify  (form: product_id, license_key, increment_uses_count)
 *                  → { success, uses, purchase: { product_id, refunded, chargebacked, disputed, dispute_won,
 *                  subscription_ended_at, subscription_cancelled_at, subscription_failed_at, … } }; 404 { success: false,
 *                  message } for an unknown key, or "This license key has been disabled." for a disabled one.
 */

export const PROVIDER_URLS = {
  lemonsqueezy: 'https://api.lemonsqueezy.com/v1/licenses/validate',
  gumroad: 'https://api.gumroad.com/v2/licenses/verify',
};
export const TIMEOUT_MS = 10_000;

/** Whether a text can be a licence key at all (sent to a provider only then): 1–200 printable characters, no spaces. */
export const isKeyShaped = (k) => typeof k === 'string' && /^[\x21-\x7e]{1,200}$/.test(k);

async function post(fetchFn, url, form, timeoutMs) {
  const res = await fetchFn(url, {
    method: 'POST',
    headers: { accept: 'application/json', 'content-type': 'application/x-www-form-urlencoded', 'user-agent': 'LapinExtensionsStore/1' },
    body: new URLSearchParams(form).toString(),
    redirect: 'error',
    signal: AbortSignal.timeout(timeoutMs),
  });
  let body = null;
  try { body = await res.json(); } catch { body = null; }
  return { status: res.status, body };
}

const unavailable = (why) => ({ status: 'unavailable', why });

/** Lemon Squeezy's answer read. */
export function readLemonSqueezy({ status, body }, productId) {
  if (status === 429 || status >= 500 || !body || typeof body !== 'object') return unavailable(`lemonsqueezy answered ${status}`);
  const meta = body.meta ?? null;
  // A key of another product (any shop's keys validate there) is no key of this one, whatever its state.
  if (meta && meta.product_id != null && String(meta.product_id) !== String(productId)) return { status: 'invalid', why: 'another product' };
  const st = body.license_key?.status;
  if (st === 'expired' || st === 'disabled') return { status: 'revoked', why: st };
  if (status === 200 && body.valid === true && meta && (st === 'active' || st === 'inactive')) return { status: 'active' };
  if (status === 200 && body.valid === true && !meta) return unavailable('lemonsqueezy said valid without naming the product');
  return { status: 'invalid', why: typeof body.error === 'string' ? body.error.slice(0, 120) : `lemonsqueezy answered ${status}` };
}

/** Gumroad's answer read (`now` for a subscription's end). */
export function readGumroad({ status, body }, productId, now = Date.now()) {
  if (status === 429 || status >= 500 || !body || typeof body !== 'object') return unavailable(`gumroad answered ${status}`);
  if (body.success !== true) {
    const msg = typeof body.message === 'string' ? body.message : '';
    if (/disabled/i.test(msg)) return { status: 'revoked', why: 'disabled' };
    return { status: 'invalid', why: msg.slice(0, 120) || `gumroad answered ${status}` };
  }
  const p = body.purchase ?? {};
  if (p.product_id != null && String(p.product_id) !== String(productId)) return { status: 'invalid', why: 'another product' };
  const past = (t) => typeof t === 'string' && Date.parse(t) <= now;
  if (p.refunded === true) return { status: 'revoked', why: 'refunded' };
  if (p.chargebacked === true) return { status: 'revoked', why: 'chargebacked' };
  if (p.disputed === true && p.dispute_won !== true) return { status: 'revoked', why: 'disputed' };
  if (past(p.subscription_ended_at)) return { status: 'revoked', why: 'subscription ended' };
  if (p.subscription_failed_at) return { status: 'revoked', why: 'subscription payment failed' };
  return { status: 'active' };
}

/**
 * A licence key checked for a paid extension's `purchase` ({ provider, productId }): { status, why? }. `why` is for the
 * caller's own use, never the key; the log says only the status.
 */
export async function checkLicence(purchase, key, { fetch: fetchFn = globalThis.fetch, timeoutMs = TIMEOUT_MS, now = Date.now() } = {}) {
  if (!isKeyShaped(key)) return { status: 'invalid', why: 'not a key' };
  const url = PROVIDER_URLS[purchase?.provider];
  if (!url) return unavailable(`no such provider: ${purchase?.provider}`);
  try {
    if (purchase.provider === 'lemonsqueezy') return readLemonSqueezy(await post(fetchFn, url, { license_key: key }, timeoutMs), purchase.productId);
    return readGumroad(await post(fetchFn, url, { product_id: purchase.productId, license_key: key, increment_uses_count: 'false' }, timeoutMs), purchase.productId, now);
  } catch (e) {
    return unavailable(e?.name === 'TimeoutError' || e?.name === 'AbortError' ? `${purchase.provider} did not answer in time` : `${purchase.provider} could not be reached`);
  }
}
