/**
 * The CDE server's refusals (Atrium Server's stable codes: docs/atrium-server.md there) as the interface says them, in English here and
 * in the reader's language through the translator; a code it does not know is said as the server words it.
 */

/** The server's refusals the interface says in the reader's language (W2, W3); any other as the server words it. */
export const REFUSALS: Record<string, string> = {
  status_only: 'As the assignee you may change only the status of this issue.',
  close_forbidden: 'Only the issue\'s author or a project owner closes it.',
  comment_required: 'Say how it was resolved: add a comment, then set Resolved.',
  delete_forbidden: 'Only the project\'s owners delete issues: it was brought back.',
  forbidden: 'Your role in this project does not allow this.',
  changed: 'Someone changed it at the same time: sync again.',
  bad_request: 'The server did not accept a value (a type, status, label or assignee the project does not offer).',
  exists: 'There is an account with that e-mail address already.',
  self: 'You cannot disable your own account or take away your own administration.',
  no_account: "Nobody has an account with that e-mail address. The server's administrator adds accounts.",
  weak_password: 'The new password needs at least 8 characters.',
  last_owner: 'The project needs at least one owner: make someone else owner first.',
  not_offered: 'This server does not offer it (an older Atrium Server).',
  unauthorized: 'Sign in to the server again.',
};
export const refusalText = (code: string, message?: string | null) => REFUSALS[code] ?? message ?? code;

