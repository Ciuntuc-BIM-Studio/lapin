# The forum

Lapin's forum for users and fans is Discourse at `https://forum.lapinbim.com`. Lapin's topics go in the category `lapin` (docs/plan-forum.md, decision 461). The app only links to it: Help → **Forum**, Help → **Ask on the forum…**, Ask Me's **Ask on the forum**, and the start screen. A new topic is Discourse's `/new-topic?title=…&body=…&category=lapin` (`Support.ForumTopic` in the engine, `forumTopicUrl` in `ui/src/shell/support.ts`). If the address or the category changes, change both, and their tests.

Discourse is GPL-2.0. It runs as its own service on the server. Lapin neither ships it nor links its code, so Lapin's MIT licence is not affected.

## On the server

It runs on the same Hetzner server as `lapin-web`, `lapin-feedback` and `lapin-relay`, but in its own folder (`/var/discourse`), using Discourse's official launcher. It stays behind the Cloudflare tunnel like the rest, and has no ports of its own on the internet.

1. **Resources.** Discourse wants 2 GB of RAM (4 GB is more comfortable) and about 10 GB of disk. Check `free -h` and `df -h` first. On 10.11.2026 the server had 2 vCPU, 3.7 GB of RAM (2.7 GB available beside the ten containers, which use about 0.3 GB), no swap, and 45 GB of free disk. Before installing, add 4 GB of swap so a rebuild never runs out of memory:

   ```sh
   fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
   echo '/swapfile none swap sw 0 0' >> /etc/fstab
   sysctl vm.swappiness=10 && echo 'vm.swappiness=10' > /etc/sysctl.d/99-swappiness.conf
   ```

   If the forum grows, move to a larger Hetzner plan (8 GB). That needs a reboot, and BubbleBIM's services go down with it.
2. **E-mail.** Discourse cannot run without SMTP: sign-up, sign-in links and notifications all go by e-mail. Pick a transactional provider (Brevo, Mailgun, Postmark, Amazon SES…). Add its SPF, DKIM and DMARC records to `lapinbim.com` (DNS in Cloudflare), then send from `forum@lapinbim.com` or `noreply@lapinbim.com`. Cloudflare Email Routing can forward the replies to an inbox.
3. **Install.** Done on 10.11.2026: `discourse_docker` cloned into `/var/discourse`, with `containers/app.yml` written by hand rather than by `./discourse-setup`. The file uses the postgres, redis, web, web.ratelimited and cloudflare templates, with no SSL templates because Cloudflare serves HTTPS. It is exposed on `127.0.0.1:3464` only, with `DISCOURSE_FORCE_HTTPS`, 2 unicorn workers, 256 MB of Postgres shared buffers, the admin `ionut@ciuntucbimstudio.ro` and Brevo's `smtp-relay.brevo.com:587` sending as `forum@lapinbim.com`. The SMTP login and key go in on the server only, typed into `/var/discourse/set-smtp.sh` (the key is never echoed, and app.yml is mode 600). Then:

   ```sh
   cd /var/discourse && ./launcher rebuild app      # about 10–15 minutes on 2 vCPU
   ```

   **`./launcher` prints the whole `docker run` line, SMTP key included.** Never paste its output anywhere, and never keep it in a readable log. After changing only the SMTP settings, `./launcher destroy app && ./launcher start app` is enough; a rebuild is not needed.

4. **The tunnel.** The server's cloudflared runs from a token (`/etc/cloudflared/token`), so its routes are managed in the Cloudflare dashboard, not in a file on the server. Go to Zero Trust → Networks → Tunnels, open the server's tunnel and choose **Public Hostname → Add a public hostname**. Set subdomain `forum`, domain `lapinbim.com`, service `HTTP`, URL `127.0.0.1:3464`. Cloudflare adds the DNS record itself, and nothing needs restarting on the server. lapinbim.com is in the same Cloudflare account as the tunnel. Neither the tunnel's id nor the server's address goes in this repository.
5. **Backups.** Admin → Backups: daily, kept 7. Copy them off the server along with the other backups.

Upgrades: `cd /var/discourse && git pull && ./launcher rebuild app` (about 10 minutes offline), or Admin → Upgrade for the plugins.

## The settings

- **Category `lapin`** (slug `lapin`), with subcategories as the forum grows (Questions, Show your work, Ideas, Tips). Lapin's links need only the slug.
- **Sign-in:** a Discourse account (e-mail and password, or an e-mail link). Lapin has no accounts, and the app knows nothing about the forum's.
- **Language:** the interface defaults to English (`default_locale: en`), and each visitor can pick theirs (`allow_user_locale`). The forum's interface follows the visitor's browser (`set_locale_from_accept_language_header`). Posts may be in any language. The forum does not translate them: a reader who wants a translation copies the text into a translator.
- **Moderation by one person:** new users' first posts wait for approval (`approve_unless_trust_level: 1`), Akismet or Discourse's own spam check is on, and trust levels are left as they come. Moderators can be added later from Admin → Users.
- **The law (EU DSA, GDPR):**
  - terms of service and a privacy page, from Discourse's templates, adapted to Ciuntuc BIM Studio SRL;
  - a contact address for authorities and users (`ionut@ciuntucbimstudio.ro`, or a `lapinbim.com` address forwarded by Email Routing);
  - flagging is on (the notice-and-action mechanism);
  - users can delete their accounts (`delete_user_self_max_post_count`).

## Claude Code as an admin

The forum's admin user `claude` (id 2, created on 10.11.2026 with the Rails runner; its password is random and was never shown, and it signs in by API only) holds a global API key. The key is kept in the Mac's keychain as `lapin-forum-api` (account `claude`) and read only inside commands:

```sh
curl -H "Api-Key: $(security find-generic-password -s lapin-forum-api -w)" -H "Api-Username: claude" https://forum.lapinbim.com/admin/site_settings.json
```

Its changes are listed under Admin → Logs → Staff actions. To take the access back, revoke the key in Admin → Advanced → API keys, or remove `claude`'s admin role.

## The state on 10.11.2026

- The title is Lapin Forum, the contact `hello@lapinbim.com`, and the company Ciuntuc BIM Studio SRL (law: Romania; courts: Vaslui).
- Categories, in a fixed order, each with an icon: **Announcements** (`announcements`, id 10; only staff starts topics, everyone replies), **Lapin** (`lapin`, id 5) with Questions (6; Solved: an answer can be accepted), Show your work (7), Ideas (8; topic voting) and Tips (9); then General, Site Feedback and Staff. Questions, Show your work and Ideas have topic templates.
- **Structure** (`structure`, id 13; 13.11.2026, topic 51) with Structural analysis (14), Reinforcement detailing (15) and Steel, timber & connections (16). The finer split of analysis is the tag group Structural analysis (1): `loads-combinations`, `analysis`, `design-checks`, open to everyone (the forum keeps two levels of categories). Other fields get their own top-level category the same way when they have topics.
- A new user's first post waits for approval (`approve_post_count: 1`).
- The Terms of Service (topic 13) and the Privacy Policy (topic 14) were written for the forum and approved by the owner. They sit in Staff, unlisted, and are served at `/tos` and `/privacy`. If a processor or a retention period changes, change the privacy policy.
- The colours are Lapin's: the palettes Lapin Paper (25) and Lapin Graphite (26) on the Horizon theme (taken from `ui/src/theme.css`), switched from the sidebar's footer. The welcome banner sits below the header with Lapin's texts. A public sidebar section, Lapin, links to the web app, the guide, the downloads, Skool and the source. Theme components from GitHub are not installed.
- The logos are Lapin's icon (`desktop/src-tauri/icons/icon.png`), with a light copy of it for dark mode.
- There is no machine translation, by decision.
