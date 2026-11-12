/**
 * The catalogue's repository on GitHub, as the store uses it: a submission becomes a branch with the extension's manifest
 * (and its screenshots) and a pull request into main; its review state is read back from the pull request and its checks.
 * The token (STORE_GITHUB_TOKEN: a fine-grained token on Ciuntuc-BIM-Studio/lapin-extensions only, Contents and Pull
 * requests read and write) comes from the environment and is never logged. `fetch` is given (the tests give a fake).
 */

const API = 'https://api.github.com';

export function githubClient({ token, repo, fetch = globalThis.fetch, api = API }) {
  async function call(method, path, body) {
    const res = await fetch(`${api}/repos/${repo}${path}`, {
      method,
      headers: { authorization: `Bearer ${token}`, accept: 'application/vnd.github+json', 'x-github-api-version': '2022-11-28', 'user-agent': 'lapin-store', ...(body ? { 'content-type': 'application/json' } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
    const text = await res.text();
    const json = text ? JSON.parse(text) : null;
    if (!res.ok) {
      const err = new Error(`GitHub ${method} ${path}: ${res.status} ${json?.message ?? ''}`.trim());
      err.status = res.status;
      throw err;
    }
    return json;
  }
  /** A file's blob sha on a branch (to replace it), or null when it is not there. */
  const fileSha = (path, ref) => call('GET', `/contents/${path}?ref=${encodeURIComponent(ref)}`).then((r) => r.sha, (e) => { if (e.status === 404) return null; throw e; });

  return {
    /**
     * A pull request proposing `files` ({ path: Buffer }) on a new branch from main; { number, url, branch }.
     * A file already on main is replaced (a new version of an extension).
     */
    async openPullRequest({ branch, files, title, body, message }) {
      const main = await call('GET', '/git/ref/heads/main');
      await call('POST', '/git/refs', { ref: `refs/heads/${branch}`, sha: main.object.sha });
      for (const [path, bytes] of Object.entries(files)) {
        const sha = await fileSha(path, branch);
        await call('PUT', `/contents/${path}`, { message, branch, content: Buffer.from(bytes).toString('base64'), ...(sha ? { sha } : {}) });
      }
      const pr = await call('POST', '/pulls', { title, head: branch, base: 'main', body, maintainer_can_modify: true });
      return { number: pr.number, url: pr.html_url, branch };
    },
    /** A pull request's state: open, merged or closed, and its checks (passing, failing, running or none). */
    async pullRequestState(number) {
      const pr = await call('GET', `/pulls/${number}`);
      let checks = 'none';
      try {
        const runs = (await call('GET', `/commits/${pr.head.sha}/check-runs`)).check_runs ?? [];
        if (runs.some((r) => r.status !== 'completed')) checks = 'running';
        else if (runs.some((r) => ['failure', 'timed_out', 'cancelled', 'action_required'].includes(r.conclusion))) checks = 'failing';
        else if (runs.length) checks = 'passing';
      } catch { /* the checks are not readable: none said */ }
      return { state: pr.merged_at ? 'merged' : pr.state, checks, url: pr.html_url };
    },
    /** A pull request closed (a submission withdrawn), with a comment saying why. */
    async closePullRequest(number, comment) {
      if (comment) await call('POST', `/issues/${number}/comments`, { body: comment });
      await call('PATCH', `/pulls/${number}`, { state: 'closed' });
    },
  };
}
