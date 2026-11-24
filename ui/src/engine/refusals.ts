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
  // W4: publishing.
  tag_exists: 'That issue tag is on the server already: an issue tag never changes. Make a new revision.',
  already_submitted: 'This issue and revision wait for approval already.',
  unknown_commit: 'The server does not have the issue\'s commit: save to the server first.',
  object_missing: 'The server lacks a file of the issue: send it again.',
  gate_failed: 'Open issues or the errors the checks found stop the approval: resolve them, or an owner approves with a note.',
  reason_required: 'Say why it is rejected.',
  note_required: 'Passing the gate needs a note.',
  not_pending: 'Someone decided on this submission meanwhile.',
  own_submission: 'You cannot approve or reject your own submission.',
  override_forbidden: 'Only the project\'s owners pass a failing gate.',
  no_submission: 'The server has no such submission.',
  no_tag: 'The server has no such issue tag.',
  tag_immutable: 'An issue tag never changes.',
  // W5: checks on foreign documents, the register.
  no_register: 'This project has no document register.',
  bad_register: 'Not a register: a CSV or XLSX file whose first row names the columns, with a code column and at least one row with a code.',
  too_large: 'The file is too large for the server.',
  // W6c: IDS files on the server.
  no_ids: 'The project has no IDS file on the server.',
  bad_ids: 'Not an IDS 1.0 file.',
  too_many: 'The project holds 50 IDS files, the most a project may.',
};
export const refusalText = (code: string, message?: string | null) => REFUSALS[code] ?? message ?? code;

