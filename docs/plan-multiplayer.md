# Plan: lucrul împreună (client–server, ca în jocuri)

Întrebarea (28.10.2026): „Ar fi interesant de gândit ideea aplicației pe principiul client–server framework — ca în jocuri, cineva să poată face server și să invite multiple players.” Documentul acesta e **analiza și planul**, nu implementarea: pornește de la ce face Lapin azi, compară cu ce fac Revit, ArchiCAD, Figma și ceilalți, și se termină cu o arhitectură recomandată, etape, riscuri și întrebările la care trebuie răspuns înainte de primul commit.

## Regula: local first și fără abonament

Ca la motorul din browser (`docs/plan-browser-engine.md`), întâi regula, pentru că restul se îndoaie după ea:

1. **Desktopul singur rămâne exact ce e.** Fără sesiune comună, aplicația nu deschide niciun port în rețea, nu caută nimic pe LAN, nu vorbește cu niciun server. Totul e pornit explicit, de utilizator, pe un proiect.
2. **Proiectul nu pleacă de pe calculatorul gazdei** decât dacă gazda alege un releu în cloud; și atunci releul vede doar trafic criptat cap la cap.
3. **Fără abonament.** Modelul de afaceri e „clientul își deține unealta”: gazda e aplicația lui (listen server) sau un calculator al lui din birou (server dedicat). Un releu de la Ciuntuc BIM Studio e o comoditate opțională, iar codul releului se poate rula pe serverul clientului.
4. **Aceleași comenzi, același motor.** Un oaspete face ce face utilizatorul local: aceleași comenzi, același undo ca pas, aceleași diff-uri. Nu există o a doua cale de editare.

## Ce există azi (faptele pe care se sprijină planul)

Lapin e deja, intern, un client–server cu un singur jucător. Asta face planul mult mai mic decât pare.

- **Motorul e autoritar.** `Lapin.Host/Program.cs` pornește Kestrel pe `127.0.0.1`, o `Session` pe proces. Interfața nu ține modelul: trimite comenzi (`doc.execute {command, args}`), primește diff-uri. `EngineHub` (`Lapin.Host/EngineHub.cs`) servește **deja mai mulți clienți**: fiecare WebSocket e un `Client`, iar diff-ul fiecărei schimbări e trimis tuturor (`Broadcast`) înainte de răspunsul celui care a cerut-o. Ferestrele detașate de vedere (`ui/src/app/windows.ts`) și puntea MCP (`docs/mcp.md`, `scripts/lapin-mcp.mjs`) sunt azi exact asta: alți clienți ai aceluiași motor, ale căror editări le vede fereastra principală imediat.
- **Ordinea e una.** `EngineHub.Handle` trece fiecare apel printr-un `SemaphoreSlim(1,1)`: sesiunea e single-threaded prin construcție, deci comenzile sunt deja serializate de un singur arbitru — ce face un server de joc autoritar.
- **Protocolul** (`Lapin.Protocol/EngineDispatcher.cs`, `BinaryFrame.cs`; clientul `ui/src/engine/client.ts`): JSON-RPC text spre motor, cadre binare înapoi; evenimentul `diff` aduce nodurile (entitățile ca DTO), relațiile, ce s-a șters și **item-urile de randare** (geometria tesselată, `RenderItem`), plus `DocState` (undo/redo, dirty, calea). `ui/src/engine/session.ts` le aplică în store-ul zustand (`applyDiff`). Transportul e deja abstract (`EngineTransport`: WebSocket sau Web Worker).
- **Tranzacțiile sunt delte de stare, nu comenzi.** `History.Execute` (`Lapin.Model/History.cs`) rulează comanda printr-un `TransactionBuilder` și păstrează `Transaction(Seq, Label, Entities, Relations, At)`, unde fiecare `Change<T>` are `Before` și `After` complete. Undo/redo aplică aceste stări (`Replaying.Revert/Apply`), nu re-rulează comanda. Consecința importantă: **o replică nu are nevoie ca comenzile să fie deterministe**; îi ajunge tranzacția.
- **Comenzile nu sunt deterministe** și nici nu trebuie: `ctx.NewId()` dă `GlobalId.New()` (un GUID aleator), `Sheets.cs` pune data cu `DateTime.Now`, unele module citesc ceasul. Re-rularea aceleiași comenzi pe alt calculator ar da alte id-uri. De aceea modelul „lockstep” din jocurile RTS (toți rulează aceleași comenzi) **nu** se potrivește; modelul „server autoritar care trimite starea” se potrivește.
- **Identitate stabilă.** Fiecare entitate și relație are un GlobalId IFC (`Lapin.Model/GlobalId.cs`), stabil peste salvări, versiuni și export. Asta e cheia pentru prezență (selecția altuia), blocări și comparări.
- **Un singur istoric.** `History` are o singură stivă `_undo`/`_redo`; `Session.Undo()` scoate ultima tranzacție, oricine ar fi făcut-o. Cu doi utilizatori, Ctrl+Z al unuia ar desface munca celuilalt. Asta e prima schimbare reală din motor.
- **Jurnalul de recuperare** (`Lapin.Protocol/Recovery.cs`, `IJournalStore`) scrie fiecare `tx`, `undo`, `redo` pe disc imediat, din evenimentul `Session.HistoryChanged`. E deja jurnalul de operații pe care `docs/format-lapin.md` îl anunța „pentru lucru în echipă mai târziu”.
- **Un cârlig pentru „nu e al tău”.** `CommandContext.EnsureEditable` (`Session.cs`) refuză deja `Add/Replace/Remove` pe entitățile unei legături (`linkId`, `docs/links.md`) și pe clasele unui modul lipsă. Rezervările (blocările) intră exact acolo.
- **Fișierul.** `.lapin` format 3 (`docs/format-lapin.md`): zip cu părți adresate după SHA-256, Arrow pe modul × clasă × etaj; versiunile (`docs/versions.md`) sunt manifeste în același fișier, restaurate într-un pas de undo prin `ModelCompare.Apply` după GlobalId, comparate element cu element. Analiza formatului a prevăzut sincronizarea pe părți („se urcă doar părțile cu hash nou”).
- **Motorul rulează și în browser** (`Lapin.Browser`, AOT, 6,8 MB, 2,4× nativ): o replică de motor poate exista la orice client, și desktop și web.
- **Ce e doar al gazdei:** `project.open/save` cu cale, `project.importIfc` cu cale, exporturile la cale, `project.reloadLink`, `recovery.*`, `support.open`, `open_separately` (Tauri). Azi nu contează (doar procesul local vorbește cu motorul, plus verificarea `Origin` din `EngineHub.AllowedOrigin`). Pentru un client din rețea, fiecare dintre ele e o gaură: un oaspete ar putea citi sau scrie orice fișier de pe calculatorul gazdei.
- **Vederile sunt în model.** O vedere e o entitate `LapinView` (`Views.cs`), planșele `LapinSheet`; starea pur personală (camera, zoom-ul, panourile, selecția, unealta) stă în UI (`ui/src/app/store.ts`). Deci „schimb scara vederii” e o editare comună (corect, ca în Revit), iar „mă uit în altă parte” nu.

## Modelul de joc, tradus în Lapin

| Joc | Lapin |
|---|---|
| Server autoritar | `Session` a gazdei: singura care execută comenzi și scrie istoria |
| Clientul trimite input, nu stare | Clientul trimite `doc.execute {command, args}`, niciodată entități |
| Serverul ordonează și difuzează | Semaforul din `EngineHub` ordonează; `Broadcast` trimite diff-ul (sau tranzacția) tuturor |
| Predicție locală pe client | Previzualizarea uneltei (rubber-band, cote live) e deja în UI; obiectul „adevărat” vine din diff |
| Snapshot la intrare | `doc.snapshot` (sau pachetul `.lapin` + jurnalul de la ultima salvare) |
| Tick rate pentru poziții | Prezența (cursoare, camere) pe un canal separat, efemer, 10–20 Hz |
| Lobby / invitație | Cod de invitație sau link; descoperire pe LAN |

### Trei feluri de gazdă

1. **Listen server** (ca în jocuri: unul dintre jucători e și serverul). Aplicația desktop a utilizatorului deschide, la cerere, un al doilea ascultător în rețea; motorul ei e cel autoritar, fișierul e al ei. Cel mai simplu, zero infrastructură, se potrivește cu „clientul își deține unealta”. Minus: când gazda închide, sesiunea se termină; calculatorul gazdei duce toată geometria.
2. **Server dedicat**: același `Lapin.Host`, fără fereastră (`Lapin.Host --serve proiect.lapin --listen 0.0.0.0:47900`), pe un calculator din birou, un NAS cu Docker sau un VPS al clientului. Toți sunt clienți egali; proiectul stă pe server. E pasul firesc pentru un birou de 3–10 oameni; tot fără abonament (clientul îl rulează).
3. **Releu în cloud** (ca serviciile de matchmaking/relay din jocuri): gazda (listen sau dedicată) și oaspeții ies amândoi spre un releu public (ex. pe Hetzner, lângă `lapin.ciuntucbimstudio.ro`), care doar mută octeți între ei. Rezolvă NAT-ul fără configurare. Releul **nu** ține proiectul și nu rulează motorul; traficul e criptat cap la cap (vezi Securitate). Opțional, autogăzduibil.

Un al patrulea fel, **motorul autoritar în cloud** (Figma, iTwin, BIMcloud), e exclus de regulă: ar fi abonament de facto și ar scoate proiectul de la client.

### Client subțire sau replică

- **Client subțire** (ca fereastra detașată de azi): oaspetele are doar interfața, legată de motorul gazdei. Primește diff-urile cu geometria gata tesselată. Avantaj: zero muncă în motor, funcționează din prima zi, oaspetele nu are nevoie de modulele motorului. Dezavantaj: geometria trece prin rețea (pe LAN nu contează; pe internet un etaj rebuild-uit poate fi mulți MB), iar **orice interogare** (desenele 2D ale vederilor, `view.drawing`, extrasele, exporturile PDF) rulează pe gazdă, prin același semafor: exportul PDF al unuia blochează editarea tuturor.
- **Replică**: oaspetele are și el un motor (desktopul lui `Lapin.Host`, sau motorul wasm în browser). Gazda îi trimite **tranzacțiile** (delte de entități și relații, câțiva KB), iar replica le aplică prin `History.Replay`-ul de azi și își construiește singură geometria, desenele și extrasele. Comenzile tot la gazdă se execută. Avantaj: lățime de bandă mică, interogări locale (deci nu blochează pe nimeni), merge pe internet. Cere: aceeași versiune de aplicație și aceleași module de motor la toți (ca în jocuri: „versiunea serverului diferă”).

Recomandarea: **clientul subțire întâi** (LAN, aproape gratis), **replica după** (internet, browser).

## Ce se schimbă în motor

### Autorul și istoria comună

- `Transaction` primește `Author` (id-ul participantului; local: „me”). Jurnalul de recuperare îl scrie; versiunile pot avea în sfârșit autor (punct „Rămase” din `docs/versions.md`).
- `History` devine un **jurnal global**, numai-adăugare, cu o **stivă de undo pe utilizator** (o listă de `Seq`-uri ale lui).
- **Undo-ul unui utilizator nu mai scoate ultima tranzacție din jurnal**, ci face o **tranzacție nouă, compensatoare**: pentru fiecare schimbare a tranzacției lui, pune `Before` înapoi **doar dacă starea curentă e încă `After`** (compare-and-swap pe entitate). Dacă altcineva a schimbat între timp una dintre entități, undo-ul e refuzat cu mesaj („Ana a modificat peretele după tine”), nu aplicat pe jumătate. Așa face Figma (undo local, istoric comun) și, în esență, ArchiCAD.
- Redo la fel: tranzacție nouă care pune `After`, dacă starea e încă `Before`.
- În sesiunea locală, cu un singur autor, regula dă exact comportamentul de azi (starea curentă e mereu `After`-ul ultimei tranzacții), deci testele de undo existente rămân valabile. Asta e testul de acceptare al pasului.
- `DocState` devine pe client: `CanUndo/UndoLabel` sunt ale stivei lui.

### Ordinea și conflictele

- Ordinea e cea de sosire la gazdă (semaforul). Nu există „merge”: fiecare comandă rulează pe starea curentă, ca local. O comandă care nu mai găsește ce caută (peretele a fost șters între timp) eșuează cu `ModelException`/`CommandException`, iar clientul vede mesajul, ca azi.
- **Revizia entității.** `ModelStore` ține, pe fiecare entitate și relație, `Seq`-ul ultimei tranzacții care a atins-o. Clientul care trimite o editare bazată pe ce vedea (mutarea unui grip, o valoare din panoul de proprietăți) trimite și `expect: {id: rev}`. Dacă revizia s-a schimbat, comanda e refuzată („modificat de Ion acum 2 s”), iar UI-ul se reîmprospătează. Pentru editări de **câmpuri independente** (numele, o proprietate, materialul), regula e mai blândă: ultima scriere câștigă **pe câmp** (ca Figma), pentru că două persoane care schimbă câmpuri diferite ale aceluiași perete nu sunt în conflict.
- Comenzile ating și vecini (îmbinările de pereți rescriu relațiile `IfcRelConnectsPathElements`, un acoperiș schimbă pereții de sub el). Asta e corect să rămână automat: e efectul comenzii, calculat pe starea curentă de arbitrul unic. Blocările de mai jos se aplică intenției (ce a selectat utilizatorul), nu fiecărei relații derivate; de decis exact unde e linia (întrebarea 4).

### Rezervări (blocări soft)

Trei niveluri, de introdus în ordine:

1. **Conștientizare** (fără blocare): ce are altul selectat sau editează (un grip apucat, un dialog de tip deschis) apare colorat cu culoarea lui. Încercarea de a edita același element arată un avertisment, nu un refuz. Pentru echipe mici asta acoperă 90% din coliziuni.
2. **Rezervări explicite**, ca în ArchiCAD Teamwork: „Rezervă” pe elemente, pe un etaj, pe o vedere/planșă, pe tipuri sau materiale. O rezervare a altcuiva face `CommandContext.EnsureEditable` să refuze, cu numele lui și „Cere-i”. Cererea de eliberare e un mesaj de prezență; rezervarea se eliberează la cerere, la deconectare (după un timp de grație) sau de către proprietar.
3. **Împrumut implicit** (ca „element borrowing” din Revit worksharing): prima editare a unui element îl rezervă automat până la „Eliberează tot” sau până la salvare. Mai strict, util pentru echipe mai mari; opțional, pe proiect.

Rezervările nu sunt în model și nu intră în undo: stau în gazdă, lângă sesiune (o tabelă `id → participant`), și se trimit pe canalul de prezență.

### Previzualizare optimistă sau confirmare

- Pe LAN (sub 5 ms dus-întors) nu e nevoie de nimic: comanda pleacă, diff-ul vine înapoi înainte ca ochiul să observe. Previzualizările de azi (rubber-band, valori live, fantoma unui perete) sunt deja locale în UI.
- Pe internet (50–150 ms) un perete apare cu o mică întârziere după clic. Ca în jocuri, se arată imediat o **fantomă** (previzualizarea uneltei, înghețată) până vine diff-ul; dacă vine o eroare, fantoma dispare cu mesajul. Nu se aplică optimist în modelul clientului: comenzile Lapin au efecte derivate (îmbinări, goluri) pe care doar motorul le știe, iar o predicție greșită urmată de corecție e mai derutantă decât 100 ms de fantomă.
- Cu replici, o comandă se poate „simula” local pe replică pentru o previzualizare exactă (inclusiv îmbinările), apoi se aruncă rezultatul și se așteaptă tranzacția gazdei. Variantă pentru etapa replicilor, dacă fantoma nu ajunge.

### Ce poate un client din rețea (capabilități)

`EngineDispatcher.Handle` primește **cine** a cerut. Pentru un participant din rețea:

- permise: `doc.*` (execute, undo/redo ale lui, query, snapshot), `quantities.of`, exporturile **fără cale** (se livrează ca octeți prin `Deliver`, deci la oaspete), `project.saveBytes` („Salvează o copie”, dacă rolul permite), `project.importIfc` / DXF **cu octeți** (fișierul vine de la oaspete);
- refuzate: tot ce primește o cale pe discul gazdei (`project.open/save` cu `path`, exporturile cu `path`, `project.importIfc` cu `path`, `project.reloadLink`), `recovery.*`, `support.*`, `project.new`, `project.open*` (doar gazda schimbă proiectul), comenzile de versiuni care scriu fișierul (doar proprietarul);
- regulile sunt o listă albă în dispatcher, cu test: orice metodă nouă e refuzată din rețea până e adăugată explicit.

### Un singur fir, mulți utilizatori

Semaforul unic e corect pentru scrieri, dar azi și citirile grele trec prin el. Cu clienți subțiri, un export PDF de 20 s al unuia oprește pe toți. Măsuri, în ordine: (a) exporturile și desenele mari rulează pe o **copie a modelului** făcută la începutul lor (store-ul e de entități imutabile, deci copia e ieftină: dicționare noi peste aceleași obiecte), în afara semaforului; (b) replicile mută interogările la oaspeți. Asta trebuie măsurat devreme (risc 2).

## Prezența

Efemeră, pe un canal separat de cel al modelului (`presence.*`, nu intră în istoric, în jurnal sau în fișier), limitată la 10–20 mesaje/s pe participant, fiecare participant cu o culoare:

- **Lista participanților** în bara de sus (avatar cu inițiale, rol, ce vedere are în față); clic = „urmărește-l” (vederea mea o ia pe a lui, ca la Figma), Esc oprește.
- **Cursoarele în plan și în secțiuni**: poziția în coordonatele modelului + id-ul vederii, desenate doar în aceeași vedere. Cu numele lângă.
- **Selecțiile altora**: contur în culoarea lor (GlobalId-uri, deci stabile), în plan, 3D și în browserul de proiect. Grip-ul apucat de altul se vede ca „ocupat”.
- **Camerele în 3D ca avatare**: un mic tronc de piramidă (frustum) cu numele, unde stă și încotro privește fiecare; util la recenzii („vino lângă mine, la scara 2”).
- **Vederile deschise**: în browserul de proiect, lângă fiecare vedere/planșă, buline cu cine o are deschisă.
- **Chat** simplu, pe sesiune, păstrat doar cât ține sesiunea (sau salvat în proiect, întrebarea 8).
- **Adnotări de recenzie** (pin-uri cu comentariu pe un element sau un punct): pe termen lung ca **BCF** (BIM Collaboration Format, standard buildingSMART), care se potrivește cu „IFC-native”; până atunci, ca entități Lapin ne-exportate.

Fereastra detașată de azi împarte deja selecția, unealta și etajul prin evenimente Tauri (`windows.ts`, lista `SHARED`); prezența e aceeași idee, peste rețea, dar **fără** a împărți selecția (fiecare are a lui, a celorlalți doar se vede).

## Intrarea într-o sesiune

- **Gazda**: File → Share session… alege rolul implicit al oaspeților și felul legăturii (doar LAN / internet direct / prin releu). Aplicația arată un **cod de invitație** (6–8 caractere ușor de dictat) și un link `lapin://join/<cod>` (plus un QR, pentru tabletă).
- **Oaspetele**: ecranul de start → Join session…, cod sau link. Pe același LAN, sesiunile deschise apar singure în listă (mDNS/DNS-SD, `_lapin._tcp`), dar intrarea tot cere codul.
- **Strângerea de mână**: versiunea aplicației (`0.1.<commits>`), produsul și modulele (cu versiunile schemei). La client subțire trebuie să aibă modulele UI ale produsului gazdei; la replică, și modulele motorului. Nepotrivire → mesaj clar („Gazda are Lapin 0.1.812 cu modulul civil; actualizează”), nu o sesiune pe jumătate. Edițiile se potrivesc cu regula formatului: o clasă al cărei modul lipsește se vede, nu se editează (`EnsureEditable` face deja asta).
- **Drumul în rețea**, încercat în ordine:
  1. **Direct pe LAN** (adresa din codul de invitație sau din mDNS).
  2. **Direct pe internet**, cu port deschis automat prin **UPnP-IGD / NAT-PMP / PCP** pe routerul gazdei (multe routere de birou le au oprite; atunci se trece mai departe).
  3. **Prin VPN-ul clientului** (Tailscale, ZeroTier, WireGuard): pentru Lapin e tot „direct”; documentat ca drumul recomandat pentru birouri cu IT. Zero cod.
  4. **Releu** (ca TURN): ambele părți deschid o conexiune WebSocket/TLS spre releu, care împerechează după cod și mută cadre. Simplu, merge prin orice firewall care lasă HTTPS. Costul e lățimea de bandă a releului, motiv în plus pentru replici.
  5. **WebRTC data channels** pentru **oaspeții din browser**: o pagină `https://` nu poate deschide `ws://192.168.x.x` (mixed content) și nu are încredere într-un certificat autosemnat de pe LAN. WebRTC rezolvă ambele (DTLS propriu, ICE încearcă direct, apoi STUN, apoi TURN), cu semnalizare prin releu. Pe partea .NET cere o bibliotecă WebRTC (de evaluat licența prin `check:licenses`) sau gazda desktop deschide canalul din fereastra ei (WebView-ul Tauri are WebRTC) și îl leagă de motor.

## Securitate

- **Ascultătorul local rămâne cum e**: `127.0.0.1`, verificarea `Origin`, fără autentificare (interfața proprie, ferestrele detașate, MCP). Rețeaua are **un al doilea ascultător**, pornit doar cât e sesiunea partajată, pe alt port, doar cu TLS și autentificare. Cele două nu se amestecă: un client din rețea nu ajunge niciodată pe căile locale.
- **TLS fără autoritate de certificare**: gazda își generează o cheie la prima partajare; **amprenta certificatului face parte din codul de invitație** (ca ID-urile de dispozitiv din Syncthing). Clientul verifică amprenta, deci nici releul, nici cineva din rețea nu poate sta la mijloc. Prin releu, TLS-ul e cap la cap, în interiorul conexiunii la releu: releul vede doar octeți criptați.
- **Autentificare**: codul de invitație e un secret de unică folosință sau cu termen; la intrare, oaspetele primește un **token de participant** (semnat de gazdă) cu nume și rol, valabil pentru reconectări. Fără conturi la început. Conturile (și deci autorul pe versiuni, invitații pe e-mail) vin doar cu un server dedicat sau un cloud, dacă se vor vreodată.
- **Roluri**: **owner** (gazda: salvează, versiuni, schimbă rolurile, dă afară, închide sesiunea), **editor** (comenzi, rezervări, import din octeții lui, „salvează o copie” dacă owner-ul permite), **viewer** (vede, navighează, prezență, adnotări; nicio comandă). Opțional mai târziu: editor limitat la anumite module sau etaje.
- **Lista albă de metode** (secțiunea Capabilități) e linia de apărare principală: un client din rețea nu poate cere nicio cale de pe discul gazdei.
- **Puntea MCP** rămâne doar locală. Un agent AI al gazdei e, practic, încă un participant cu rolul gazdei, iar editările lui apar ca ale gazdei (sau ca „AI pentru Ionuț”, dacă `Author` o permite: util la urmărire).
- **Plugin-urile private** (Bubbles) nu se transmit niciodată: modulele vin din instalarea fiecăruia, nu de la gazdă.

## Salvarea

- **Doar gazda scrie `.lapin`** (owner-ul; la serverul dedicat, serverul). Oaspeții nu au fișierul; „Salvează o copie” (`project.saveBytes`) e o permisiune.
- **Autosalvare**: jurnalul de recuperare al gazdei scrie deja fiecare tranzacție pe disc imediat, deci nicio editare a nimănui nu se pierde la o cădere a gazdei. Peste el, o salvare automată a fișierului la interval (5–10 min, configurabil) cât sesiunea e partajată, ca să nu depindă totul de gazda care uită să apese Save.
- **Versiunile ca puncte de control**: la pornirea și la oprirea unei sesiuni partajate, opțional o versiune automată („Sesiune 28.10, 14:00–17:30, Ana, Ion”); „Restore” rămâne un pas de undo (al owner-ului) pe care îl văd toți. Compararea versiunilor (`ModelCompare`) arată, cu autorul pe tranzacții, și cine ce a schimbat.

## Deconectare, reconectare, offline

- **Oaspetele pierde legătura**: `EngineClient` se reconectează deja singur. La revenire trimite ultimul `Seq` văzut; gazda trimite tranzacțiile de atunci (replica) sau un `doc.snapshot` (clientul subțire, cum face și azi la pornire). Rezervările lui se păstrează un timp de grație (ex. 10 min).
- **Gazda dispare**: la clientul subțire sesiunea se oprește, cu „Gazda a ieșit; ultimele modificări sunt în jurnalul ei”. La replică, oaspetele are tot modelul și poate „Salva o copie”. **Migrarea gazdei** (un oaspete devine server, ca în unele jocuri) e posibilă cu replici, dar nu în primele etape: ar crea două adevăruri dacă gazda revine.
- **Offline, apoi îmbinare** (lucrez în tren pe o copie, apoi aduc schimbările înapoi): nu în sesiune, ci ca **îmbinare de versiuni**. `ModelCompare` compară deja element cu element după GlobalId; o îmbinare în trei căi (baza comună, a mea, a lor) cu conflictele pe element arătate într-un dialog e o funcție separată, utilă și fără sesiuni (doi colegi cu două copii). E în etapa 6.

## Cum fac alții

| | Model | Ce luăm | Ce nu |
|---|---|---|---|
| **Revit worksharing** | Model central pe un share de rețea (sau Revit Server / BIM 360-ACC în cloud); fiecare are o copie locală, „Synchronize with Central”; worksets și „element borrowing” | Împrumutul implicit al elementelor, worksets ca rezervări pe grupuri; ideea că vederile sunt comune | Sincronizarea manuală, cu minute de așteptare și conflicte la sincronizare; copia locală care se strică; abonamentul pentru cloud |
| **ArchiCAD Teamwork / BIMcloud** | Server (BIMcloud Basic gratuit pe LAN, BIMcloud ca serviciu); „Send & Receive” cu delte mici; **rezervări** explicite, cereri de eliberare, mesaje | Rezervările și cererile, chatul, BIMcloud Basic ca precedent pentru „server pe LAN fără abonament” | Send & Receive manual (la noi totul e live, ca la un joc); licențele de server |
| **Figma** | Server autoritar pe document, clienți cu replică, **ultima scriere câștigă pe proprietate**, undo local în istoric comun; explicit **nu** CRDT pentru că serverul există mereu | Aproape tot modelul: autoritate, LWW pe câmp, undo compensator, prezența (cursoare, urmărire) | Documentul în cloud |
| **Speckle** | Server (open source, autogăzduibil) de **obiecte imutabile adresate după hash**, „commits” pe „streams”, conectori din alte aplicații | Adresarea după conținut (o avem în formatul 3), autogăzduirea, istoricul ca șir de commit-uri | Nu e editare live împreună; e schimb de date între aplicații |
| **Bentley iTwin / iModelHub** | iModel (SQLite) + **changesets** liniare, ordonate de hub; brief-cases locale; blocări și coduri gestionate central | Changesets ordonate = tranzacțiile noastre cu `Seq`; blocările ca serviciu al gazdei | Hub-ul în cloud ca obligatoriu |
| **Jocuri (Unreal, Source)** | Listen sau dedicated server, clientul trimite input, serverul replică starea, predicție + reconciliere | Listen/dedicated/relay, lobby cu cod, verificarea versiunii la intrare, prezența la frecvență mare pe canal separat | Lockstep determinist (comenzile noastre nu sunt deterministe și nici nu trebuie) |

### De ce nu CRDT (Automerge, Yjs)

CRDT-urile permit editare fără server și îmbinare garantată fără conflicte. Pentru un model IFC cu reguli, nu se potrivesc:

- **Invarianții**: o îmbinare CRDT face din două stări valide o stare pe care **nicio comandă n-a produs-o** (un perete șters de unul și îmbinat de altul; un `IfcRelVoidsElement` spre un perete care nu mai e; un `IfcMaterialLayerSetUsage` folosit de două elemente). Modelul Lapin își ține invarianții în comenzi și în `TransactionBuilder.Remove` (ce nu poate exista fără element dispare cu el); un arbitru serial îi păstrează gratis.
- **Efectele derivate** (îmbinări, goluri, pereți sub acoperiș, încăperi care își urmăresc pereții) sunt calculate de motor pe starea curentă; cu CRDT ar trebui recalculate după fiecare îmbinare, la fiecare replică, cu risc de divergență.
- **Nu avem nevoie de ce oferă**: într-o sesiune există mereu o gazdă online (lucru fără server = offline = etapa de îmbinare de versiuni, unde conflictele trebuie oricum arătate unui om, nu rezolvate tăcut).
- **Tehnic**: motorul e C#; Yjs e JS, Automerge e Rust/JS cu legături .NET imature; ar însemna a doua reprezentare a modelului.

CRDT ar avea sens pentru textul liber (chat, note lungi editate de doi deodată); nici acolo nu e necesar la început.

## Arhitectura recomandată

```
           ┌──────────── gazda (desktopul owner-ului, sau Lapin.Host --serve) ────────────┐
 UI local ─┤ ws://127.0.0.1 (ca azi, fără auth)                                           │
 MCP      ─┤        │                                                                     │
           │   EngineHub ── Session (autoritară) ── History: jurnal global + undo/utilizator
           │        │            │                    └─ Recovery (jurnal pe disc, cu Author)
           │        │            └─ Reservations (id → participant), Presence (efemer)
           │   SessionListener: wss://0.0.0.0:47900, TLS cu cheia gazdei, token, rol,
           │        listă albă de metode  ──┬── direct (LAN, UPnP, VPN)
           └────────────────────────────────┼── releu (WebSocket, autogăzduibil) ──┐
                                            │                                     │
                     oaspete desktop (subțire → replică)            oaspete browser (replică wasm, WebRTC)
```

- **Un proces, o sesiune, un arbitru**: `Session` a gazdei, neschimbată în esență.
- **Două canale**: modelul (comenzi → tranzacții/diff-uri, ordonate, în jurnal) și prezența (efemeră, fără ordine strictă).
- **Stratul nou e subțire și stă lângă `EngineHub`**: `Participant` (id, nume, culoare, rol, token), `SessionListener` (al doilea Kestrel/ascultător), capabilitățile în `EngineDispatcher`, `Reservations`, `Presence`. Modulele nu se schimbă: comenzile lor nu știu cine le cheamă (doar `EnsureEditable` știe de rezervări).
- **Schimbările din model**: `Author` pe `Transaction`, undo/redo pe utilizator prin tranzacții compensatoare, revizia pe entitate, `expect` opțional pe comenzi.
- **UI**: Share session…, Join session…, lista participanților, straturile de prezență în plan/3D/browser, rezervări în meniul contextual, chat.

## Etape

Estimările sunt ale unui om, cu testele, în ritmul de până acum.

### Etapa 1: sesiune pe LAN, clienți subțiri, prezența de bază (≈ 2 săptămâni)

- `Author` pe `Transaction` și în jurnal; undo/redo pe utilizator prin tranzacții compensatoare, cu refuz la conflict. Teste: toate testele de undo de azi trec neschimbate; două stive intercalate; undo refuzat când altul a modificat entitatea.
- `SessionListener` pe LAN, TLS cu amprenta în cod, cod de invitație, token, roluri owner/editor/viewer, lista albă din dispatcher (test: fiecare metodă cu cale refuzată din rețea).
- Strângerea de mână (versiune, produs, module).
- UI: Share session…, Join session… (adresă + cod), lista participanților, selecțiile și cursoarele altora în plan și 3D, „urmărește-l”.
- Gazda salvează; autosalvare la interval cât e partajată.
- Acceptare: două Mac-uri pe același Wi-Fi desenează pereți pe același etaj; îmbinările se văd la amândoi; Ctrl+Z al fiecăruia desface doar ce a făcut el.

### Etapa 2: coliziuni și rezervări (≈ 1,5 săptămâni)

- Revizia pe entitate, `expect` din grip-uri și din panoul de proprietăți, LWW pe câmp pentru proprietăți.
- Conștientizarea (ce editează altul, avertisment), apoi rezervările explicite (elemente, etaj, vedere/planșă, tipuri), cererea de eliberare, eliberarea la deconectare după grație. `EnsureEditable` le aplică.
- Chat; vederile deschise de fiecare în browserul de proiect; camerele ca avatare în 3D.

### Etapa 3: internet (≈ 2 săptămâni)

- UPnP/NAT-PMP; pagina din ghidul utilizatorului pentru Tailscale/ZeroTier.
- Releul: un serviciu mic (împerechere după cod, mutare de cadre, fără stare), pe Hetzner lângă `/api/feedback`, cu imagine Docker pentru autogăzduire.
- Reconectare cu recuperare de la `Seq`; fantoma comenzii în așteptare; lățimea de bandă măsurată pe testLapin.lapin și Bloc-P-4.
- Exporturile și desenele mari pe o copie a modelului, în afara semaforului.

### Etapa 4: replici (≈ 3 săptămâni)

- Oaspetele desktop pornește propriul `Lapin.Host` ca replică: primește pachetul la intrare, apoi tranzacțiile; aplică prin `History.Replay`; interogările și desenele rulează local. Gazda nu mai trimite geometrie.
- Testul cheie: după N tranzacții intercalate de 3 participanți, modelul fiecărei replici e identic cu al gazdei (hash-ul manifestului formatului 3, fără ordinea de creare dacă diferă).
- Simulare locală opțională pentru previzualizarea exactă.

### Etapa 5: oaspeți din browser (≈ 1,5 săptămâni după etapa 4)

- Motorul wasm ca replică; WebRTC data channel cu semnalizare prin releu; „Join” direct din `lapin.ciuntucbimstudio.ro/web/` cu link-ul de invitație. Proiectul tot la gazdă.

### Etapa 6: server dedicat și îmbinarea de versiuni (când e cerut)

- `Lapin.Host --serve` fără fereastră, mai multe proiecte, imagine Docker; administrarea minimă (participanți, roluri, salvări).
- Îmbinarea în trei căi a două copii (`ModelCompare` + dialog de conflicte pe element), pentru lucrul offline.
- Adnotările ca BCF (import/export), autorul pe versiuni.

Drumul minim util: etapele 1 + 2 (≈ 3,5 săptămâni) dau un birou care lucrează împreună pe LAN. Cu 3, și de acasă.

## Riscuri

1. **Securitatea dispatcher-ului.** Azi orice client al motorului poate citi și scrie fișiere ale gazdei (căile). Lista albă și ascultătorul separat sunt obligatorii înainte de orice port deschis; un test care enumeră toate metodele și cere fiecăreia o decizie.
2. **Semaforul unic.** Interogările grele (desene, PDF, IFC, extrase) blochează editările tuturor. De măsurat în etapa 1 cu trei clienți pe testLapin.lapin; copia de model e soluția din etapa 3, replicile soluția definitivă.
3. **Lățimea de bandă cu clienți subțiri**: diff-urile poartă geometria tesselată; o mutare de etaj pe un model mare poate trimite zeci de MB. Pe LAN e acceptabil; pe internet nu, de unde replicile.
4. **Undo-ul compensator** schimbă un comportament de bază; dacă refuzurile („altul a modificat”) apar prea des (vecinii atinși de îmbinări), utilizatorii vor simți un undo „stricat”. Regula poate ignora atributele derivate pe care comanda le recalculează oricum; de măsurat cu cazuri reale.
5. **Efectele pe vecini și rezervările**: o comandă pe peretele meu rescrie îmbinarea cu peretele rezervat de Ana. Dacă rezervările blochează și asta, editarea devine imposibilă la granițe; dacă nu, „rezervat” nu mai e absolut. Linia trebuie trasă explicit (întrebarea 4).
6. **Nepotriviri de versiune și ediție** între participanți: verificate la intrare, dar fiecare actualizare a aplicației cere ca toți să actualizeze (actualizatorul există: `ui/src/app/updates.ts`).
7. **Determinismul geometriei la replici**: aceeași tranzacție poate da, pe alt procesor sau în wasm, triunghiuri ușor diferite. Pentru afișare nu contează; pentru extrase și cantități, valorile „oficiale” sunt ale gazdei (sau se acceptă diferențe sub toleranță).
8. **Starea „globală” ascunsă**: tot ce stă azi în motor fără să fie în model (ex. setări ale sesiunii, calea, legăturile citite de pe disc) trebuie trecut prin lista albă sau făcut al gazdei. Legăturile cu cale (`docs/links.md`) se reîncarcă doar la gazdă.
9. **Licențe**: UPnP, mDNS și WebRTC în .NET cer biblioteci; fiecare trece prin `pnpm check:licenses` (permisivă) sau se scrie în casă (mDNS și NAT-PMP sunt mici; WebRTC nu e).

## Întrebări pentru tine

1. **Ce scenariu contează întâi?** Un birou pe LAN (2–5 oameni, aceeași rețea), colaborare de acasă cu un inginer de structuri, sau recenzii cu clientul (viewer)? Ordinea etapelor depinde de asta. (Implicit: biroul pe LAN.)
2. **Listen server, dedicat, sau ambele de la început?** (Implicit: listen server în etapa 1; `--serve` e același cod, devine produs în etapa 6.)
3. **Rezervări: conștientizare, explicite (ArchiCAD) sau împrumut implicit (Revit)?** (Implicit: conștientizare + explicite; împrumutul implicit ca opțiune pe proiect.)
4. **Ce înseamnă „rezervat”** pentru efectele pe vecini (îmbinări, goluri, pereți sub acoperiș)? (Implicit: rezervarea blochează doar comenzile țintite pe element; efectele derivate trec, și apar marcate la proprietar.)
5. **Undo-ul**: e bine „doar al meu, refuzat dacă altul a modificat”? Sau vrei și un „undo global” al owner-ului? (Implicit: doar al meu; owner-ul are versiunile.)
6. **Releul**: îl ținem noi (Hetzner), îl dăm clientului de autogăzduit, sau ambele? Gratuit, inclus în licența unei ediții, sau separat? (Implicit: ambele; codul releului e MIT, instanța noastră gratuită cu limite rezonabile.)
7. **Este lucrul împreună al tuturor edițiilor** sau al unei ediții „echipă” (`docs/decisions` despre ediții)? (Implicit: în toate; e o proprietate a platformei, nu un modul.)
8. **Chatul și adnotările se păstrează în proiect** (și cum: BCF, entități Lapin) sau doar cât ține sesiunea? (Implicit: chatul doar în sesiune; adnotările în proiect, ca BCF mai târziu.)
9. **Conturi**: rămânem la coduri de invitație și tokeni ai gazdei (fără conturi), sau vrei identități stabile (cine a făcut ce peste sesiuni)? (Implicit: fără conturi; numele introdus la intrare, ținut de aplicație.)
10. **Oaspeți din browser**: un client care primește un link și intră din browser e important (recenzii cu beneficiarul), sau desktopul ajunge? (Implicit: important, dar după replici.)
11. **Lucrul offline cu îmbinare** e o nevoie reală acum (șantier fără internet, laptop în deplasare)? (Implicit: nu înainte de etapele 1–3.)
