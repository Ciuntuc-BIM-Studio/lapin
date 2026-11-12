/**
 * The maintainers' commands, run in the container (deploy/store/README.md):
 *   docker exec lapin-store node cli.mjs list [all]       the submissions (all: withdrawn ones too)
 *   docker exec lapin-store node cli.mjs stats [<id>]     downloads per extension (per version and month for one)
 *   docker exec lapin-store node cli.mjs owner <id> <forum-user-id> <username>
 *                                                         who may publish an extension's versions here (one listed
 *                                                         through GitHub, claimed by its author)
 *   docker exec lapin-store node cli.mjs forget <username>
 *                                                         a person's data erased (GDPR): their unsent and declined
 *                                                         submissions deleted, their name taken off the others and off
 *                                                         the extensions they own (what is published stays, in the
 *                                                         catalogue's public history)
 */

import { pathToFileURL } from 'node:url';
import { storeData, withLock } from './data.mjs';
import { rm } from 'node:fs/promises';

export async function run(args, { dataDir = process.env.DATA_DIR ?? '/data', log = console.log } = {}) {
  const data = storeData(dataDir);
  const [cmd, ...rest] = args;
  if (cmd === 'list' || !cmd) {
    const all = (await data.submissions()).filter((s) => rest[0] === 'all' || s.status !== 'withdrawn');
    if (!all.length) log('No submission.');
    for (const s of all) log(`${s.sid}  ${s.createdAt.slice(0, 10)}  ${s.status.padEnd(9)}  ${s.fields.id} ${s.fields.version}  ${s.owner.username}${s.pr ? `  #${s.pr.number}` : ''}`);
    return 0;
  }
  if (cmd === 'stats') {
    const stats = await data.stats();
    if (rest[0]) { for (const [v, months] of Object.entries(stats[rest[0]] ?? {})) log(`${v}: ${Object.entries(months).map(([m, n]) => `${m} ${n}`).join(', ')}`); return 0; }
    for (const [id, vs] of Object.entries(stats)) log(`${id}: ${Object.values(vs).flatMap((m) => Object.values(m)).reduce((a, b) => a + b, 0)}`);
    return 0;
  }
  if (cmd === 'owner' && rest.length === 3) {
    await withLock(dataDir, () => data.setOwner(rest[0], { forumId: rest[1], username: rest[2] }));
    log(`${rest[0]} is published here by ${rest[2]}.`);
    return 0;
  }
  if (cmd === 'forget' && rest[0]) {
    const who = rest[0].toLowerCase();
    let n = 0;
    await withLock(dataDir, async () => {
      for (const s of await data.submissions()) {
        if (s.owner.username.toLowerCase() !== who) continue;
        n++;
        if (s.status === 'merged' || s.status === 'open') await data.saveSubmission({ ...s, owner: { forumId: `forgotten-${s.sid}`, username: 'forgotten', name: '' }, forgottenAt: new Date().toISOString() });
        else {
          await rm(data.screenshotDir(s.sid), { recursive: true, force: true });
          if (s.package) await rm(data.packagePath(s.fields.id, s.fields.version, s.package.file), { force: true });
          await data.removeSubmission(s.sid);
        }
      }
      const owners = await data.owners();
      for (const [id, o] of Object.entries(owners)) if (o.username.toLowerCase() === who) { delete owners[id]; n++; }
      await data.saveOwners(owners);
    });
    log(`${rest[0]}: ${n} record(s) erased or made anonymous.`);
    return 0;
  }
  log('Usage: cli.mjs list [all] | stats [<id>] | owner <id> <forum-user-id> <username> | forget <username>');
  return 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) process.exit(await run(process.argv.slice(2)));
