/**
 * The kept reports, read and answered in the feedback container
 * (deploy/feedback/reports.sh runs it over SSH):
 *   list [open|closed|all]   one line each: number, date, app, kind, folder, title;
 *                            ✉ <nickname> when the sender can be answered, ● when they wrote and it is unread
 *   show <n>                 the whole report and its thread (the sender's messages are then read)
 *   reply <n> <text>         a message from Lapin, which the sender sees in Help → Messages
 *   close <n> | reopen <n>   move it between open/ and closed/ (screenshot too)
 *   prune [months]           anonymise threads idle for longer than that (default 12): nickname and key removed
 */

import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import {
  emailOf, findReport, moveReport, pruneThreads, reportsIn, repliable, saveReport, unreadByLapin, withLock,
} from './store.mjs';

export const MAX_REPLY = 4000;

/** Run one command on a data folder; `log` gets each line. The exit code (0 ok, 1 refused or unknown). */
export async function run(args, { dataDir = process.env.DATA_DIR ?? '/data', log = console.log } = {}) {
  const [cmd = 'list', arg = cmd === 'list' ? 'open' : undefined, ...rest] = args;

  const need = async () => {
    const r = await findReport(dataDir, arg);
    if (!r) throw new Error(`no report #${arg}`);
    return r;
  };

  try {
    if (cmd === 'list') {
      if (!['open', 'closed', 'all'].includes(arg)) throw new Error('list open, closed or all');
      const dirs = arg === 'all' ? ['open', 'closed'] : [arg];
      const rows = (await Promise.all(dirs.map((d) => reportsIn(dataDir, d)))).flat();
      if (!rows.length) log(`no ${arg} reports`);
      for (const r of rows) {
        const who = repliable(r) ? `\t✉ ${r.thread.nickname ?? '(no name)'}${unreadByLapin(r) ? ' ●' : ''}` : '';
        log(`#${r.number}\t${r.receivedAt.slice(0, 16).replace('T', ' ')}\t${r.app}\t${r.kind}\t${r.dir}${who}\t${r.title}${r.screenshot ? '\t[screenshot]' : ''}`);
      }
      return 0;
    }
    if (cmd === 'show') {
      const r = await need();
      const { thread, dir, ...report } = r;
      log(JSON.stringify({ ...report, email: emailOf(r), contact: undefined, folder: dir }, null, 2));
      if (r.screenshot) log(`screenshot: ${join(dataDir, dir, r.screenshot)}`);
      if (thread) {
        log(repliable(r) ? `--- thread with ${thread.nickname ?? '(no name)'} (reply with: reply ${r.number} "…")` : `--- thread (forgotten ${thread.forgotten?.slice(0, 10) ?? ''}: no replies)`);
        if (!thread.messages.length) log('(no messages yet)');
        thread.messages.forEach((m, i) => log(`${m.at.slice(0, 16).replace('T', ' ')}  ${m.from === 'lapin' ? 'lapin' : 'user '}${repliable(r) && m.from === 'user' && i >= (thread.lapinSeen ?? 0) ? ' ●' : ''}  ${m.text}`));
        // Shown is read.
        if (repliable(r) && (thread.lapinSeen ?? 0) < thread.messages.length) {
          await withLock(dataDir, async () => {
            const now = await findReport(dataDir, r.number);
            if (now && repliable(now)) await saveReport(dataDir, { ...now, thread: { ...now.thread, lapinSeen: now.thread.messages.length } });
          });
        }
      }
      return 0;
    }
    if (cmd === 'reply') {
      const text = rest.join(' ').trim();
      if (!text) throw new Error('usage: reply <n> <text>');
      if (text.length > MAX_REPLY) throw new Error(`a reply is at most ${MAX_REPLY} characters`);
      return await withLock(dataDir, async () => {
        const r = await need();
        if (!repliable(r)) throw new Error(`#${r.number} did not ask for a reply${r.thread?.forgotten ? ' (its sender was forgotten)' : ''}${emailOf(r) ? `; its e-mail: ${emailOf(r)}` : ''}`);
        const at = new Date().toISOString();
        const messages = [...r.thread.messages, { from: 'lapin', text, at }];
        // Answering reads what came before.
        await saveReport(dataDir, { ...r, thread: { ...r.thread, messages, lapinSeen: messages.length, lastActivity: at } });
        log(`#${r.number}: reply sent to ${r.thread.nickname ?? '(no name)'}`);
        return 0;
      });
    }
    if (cmd === 'close' || cmd === 'reopen') {
      const [from, to] = cmd === 'close' ? ['open', 'closed'] : ['closed', 'open'];
      return await withLock(dataDir, async () => {
        const r = await need();
        if (r.dir !== from) { log(`#${r.number} is already ${r.dir}`); return 0; }
        await moveReport(dataDir, r, to);
        log(`#${r.number} ${to === 'closed' ? 'closed' : 'reopened'}`);
        return 0;
      });
    }
    if (cmd === 'prune') {
      const months = arg === undefined ? 12 : Number(arg);
      if (!(months > 0)) throw new Error('usage: prune [months]');
      const n = await pruneThreads(dataDir, months);
      log(`${n} thread(s) idle for more than ${months} month(s) anonymised`);
      return 0;
    }
    log('usage: list [open|closed|all] | show <n> | reply <n> <text> | close <n> | reopen <n> | prune [months]');
    return 1;
  } catch (e) {
    log(e instanceof Error ? e.message : String(e));
    return 1;
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  process.exitCode = await run(process.argv.slice(2));
}
