# Forumul Lapin

Cerut de utilizator (10.11.2026): „un forum de discuții pentru utilizatori și fani, poate chiar legat de app”. Decizia 461.

## Decizia (10.11.2026)

- **Discourse**, găzduit pe serverul Hetzner, la **`forum.lapinbim.com`**, pe domeniul produsului (lapinbim.com, cumpărat pe Cloudflare la 10.11.2026), cu o categorie `lapin`.
- **Scopul: fanii și comunitatea.** Suportul personal rămâne la „Raportează o problemă” și Mesaje. **Skool** rămâne pentru cursuri și celelalte materiale ale echipei, alături de forum, nu înlocuit.
- **Nu în aplicație, dar intri direct din ea.** Forumul nu e construit în Lapin; aplicația are legături către el:
  - Ajutor → **Forum**;
  - Ajutor → **Întreabă pe forum…**: un subiect nou în categoria Lapin, cu versiunea aplicației și „desktop” sau „web” sub text, nimic altceva;
  - **Întreabă pe forum** în Ask Me, când nu are răspuns: întrebarea devine titlul subiectului;
  - **Forum** pe ecranul de start.
- **Limba: engleză**, fără să impunem utilizatorului să scrie în engleză. Traducerea nu e treaba forumului (decis 10.11.2026): cine vrea copiază textul și îl traduce în altă parte. Interfața forumului urmează limba browserului.
- **Moderarea:** deocamdată doar utilizatorul. Rolurile de moderator se dau mai târziu, din Discourse.
- **Conturile:** cele ale forumului. Lapin rămâne fără cont și nu știe nimic despre ele (fără SSO).

## De ce Discourse

Standardul forumurilor open-source, cu:

- căutare și SEO: întrebările la care s-a răspuns se găsesc din Google, ceea ce Skool, închis, nu oferă;
- anti-spam și trust levels, potrivite unui singur moderator;
- un API și embed.

E GPL-2.0, dar rulează ca serviciu separat, iar aplicația doar face linkuri spre el, așa că MIT-ul lui Lapin nu e atins. Variantele cântărite:

- **Flarum:** MIT, dar cu moderare mai slabă;
- **GitHub Discussions:** utilizatorii sunt arhitecți, iar pentru feedback s-a decis deja „nimic pe GitHub”;
- **doar Skool:** închis, neindexat, fără API;
- **un forum construit de noi:** luni de muncă pentru ce Discourse face deja.

## Ce e făcut în aplicație

- **Motorul:** `Support.ForumUrl`, `Support.ForumTopic`, `Support.LinkUrl` (`forum` și `forumTopic`, alături de celelalte legături) în `engine/src/Host/Lapin.Host/Support.cs`. `ISupportDesk.Open` primește parametrii comenzii, pentru titlul și textul subiectului. Se deschide numai adresa forumului: titlul (maximum 200 de caractere) și textul (maximum 4000) sunt escapate în query. Ca orice `support.open`, nu e permis unui oaspete al unei sesiuni partajate.
- **Interfața:** `LINKS.forum`, `forumTopicUrl`, `forumQuestion`, `openForumTopic` în `ui/src/shell/support.ts`, aceeași adresă ca în motor (testele din `SupportTests` și `support.test.ts` o fixează în ambele). Meniul ?, Ask Me și ecranul de start; textele noi sunt traduse în toate limbile.
- **Ghidul:** pagina Ajutor și feedback, în toate limbile, iar scena `help` verifică cele două intrări din meniu.

## Ce rămâne de făcut (pe server, de utilizator)

Pașii sunt în `deploy/forum/README.md`:

1. RAM-ul serverului (Discourse cere 2 GB) și un furnizor SMTP, cu SPF, DKIM și DMARC pe `lapinbim.com`.
2. Instalarea `discourse_docker` pe `127.0.0.1:3464`, în spatele tunelului Cloudflare (o regulă de ingress și ruta DNS).
3. Categoria `lapin`, termenii și confidențialitatea (DSA, GDPR), aprobarea primelor postări.
4. Abia după ce forumul e online: o versiune nouă a aplicației și ghidul republicat. Până atunci, legăturile duc la o adresă care nu răspunde.

## Mai târziu, dacă forumul prinde viață

- **„Discuții despre această unealtă”:** etichete Discourse cu id-ul uneltei (`architecture.wall`) și legături din ghid și din unelte.
- **Postare din aplicație**, cu captura de ecran și o bucată de model, printr-o cheie de utilizator Discourse (User API Key), fără parolă în Lapin.
- **Un model 3D viu în postări:** vizualizatorul web într-un iframe.
- **Un raport făcut public** cu acordul celui care l-a trimis.
