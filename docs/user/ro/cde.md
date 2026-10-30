---
title: Folderul proiectului și emiterile
order: 125
summary: Emiți planșele și modelul într-un folder al proiectului așa cum cere ISO 19650, denumite după convenție, cu borderou de transmitere, după verificările pe care le face un birou de proiectare.
---

## Folderul proiectului

**Manage → Project folder** alege folderul în care se emite proiectul: un folder de pe calculatorul tău, un share de rețea sau un folder SharePoint ori OneDrive sincronizat pe disc. Lapin nu cere niciun cont și nu vorbește cu niciun server: scrie fișiere, iar ce sincronizează folderul e treaba biroului tău. Calea se păstrează în proiect, deci călătorește cu fișierul; un folder care nu e pe acest calculator e un avertisment, nu o eroare.

![Folderul proiectului: folderul, containerele, denumirea](shots/project-folder.webp)

În folder Lapin creează, la prima emitere, cele patru containere ISO 19650: **WIP**, **Shared**, **Published** și **Archived**. Codul de stare sugerează containerul: S0 merge în WIP, S1–S7 în Shared, A, B și CR în Published.

## Denumirea

Fișierele se denumesc după ISO 19650-2: **Proiect-Emitent-Volum-Nivel-Tip-Rol-Număr**, apoi starea și revizia, de exemplu `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. În dialogul Project folder stabilești codul proiectului, emitentul (biroul tău), volumul și nivelul și rolul implicite. Restul vine din proiect:

- **nivelul** unei planșe e etajul pe care îl desenează planurile ei (`00` la ±0,00, `01` deasupra, `B1` dedesubt, `ZZ` când desenează mai multe sau niciunul);
- **rolul** e al disciplinei ei (arhitectură A, structură S, MEP M, civil C);
- **numărul** e din cifrele numărului planșei (`A-101` devine `0101`);
- modelul e `M3`, borderoul `IE`.

## Verificări

**Manage → Check** rulează, pe tot proiectul, regulile pe care un birou de proiectare le verifică din ochi înainte de o emitere. O constatare **blocantă** oprește emiterea; un **avertisment** poate fi trecut cu vederea. Click pe o constatare te duce la planșă, la vedere sau la element.

![Verificare: constatările pe reguli](shots/cde-check.webp)

Blocante: un cartuș fără număr, fără titlu sau fără cine a desenat; o vedere fără scară; o vedere în afara chenarului sau peste cartuș; o planșă schimbată de la ultima emitere, dar rămasă la aceeași revizie; un număr de planșă din care nu iese un număr de fișier. Avertismente: cine a verificat, data și faza lăsate goale; un plan colorat după o schemă de încăperi, sau cu multe hașuri, fără legendă; un plan, o secțiune sau o fațadă pe nicio planșă; un plan de situație fără săgeata nordului; elemente fără etaj, fără tip sau fără material; uși și ferestre fără etichetă; încăperi fără număr; proprietățile IFC obligatorii (IsExternal, LoadBearing, FireRating la uși) lăsate goale.

## Emiterea

**Manage → Issue…** scrie o emitere într-un container (doar în aplicația desktop, pentru că scrie fișiere):

![Emitere: planșele, revizia, starea, verificările](shots/cde-issue.webp)

1. Dă emiterii un nume, **revizia** (`P01`, `P02`… cât e preliminară, `C01`… odată contractuală; următoarea e propusă din ultima emitere), **starea** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) și **containerul**.
2. Bifează planșele care merg, modelul ca IFC, fiecare planșă și ca DXF, sau toate planșele și într-un singur PDF. Numele fiecărui fișier se vede așa cum va fi scris.
3. Verificările rulează pe măsură ce alegi. Cu o constatare blocantă emiterea nu poate pleca; doar cu avertismente, bifează **Emite oricum**.
4. **Emite** scrie fiecare planșă ca PDF, IFC-ul și restul în container, denumite după convenție, calculează hash-ul (SHA-256) fiecărui fișier, scrie **borderoul de transmitere** (un PDF și un CSV: proiectul, emiterea, data, revizia, starea, fișierele) și un **manifest** (JSON, cu toate hash-urile) lângă ele, și înregistrează emiterea în proiect. Fiecare planșă tipărită ia revizia și starea emiterii în cartuș, iar tabelul ei de revizii listează emiterile în care a plecat.
5. Proiectul se salvează apoi ca **versiune** cu numele emiterii (vezi [Fișiere, versiuni și recuperare](files.md)), ca o emitere să poată fi redeschisă, restaurată sau comparată cu proiectul de acum.

Emiterile sunt listate în josul dialogului Project folder, fiecare cu fișierele ei și **Deschide folderul**.

## Observații (BCF)

**Manage → Observații pe model…** păstrează observațiile făcute pe model — ale tale și ale unui verificator — ca subiecte BCF (formatul BIM Collaboration Format pe care îl schimbă BIMcollab, Solibri, Navisworks și Revit), în proiect, niciodată în IFC. O observație are un titlu, o descriere, un tip, o stare (Open, In progress, Resolved, Closed), o prioritate, un responsabil, un termen și etichete; comentariile ei; și punctele ei de vedere — camera 3D, elementele selectate, cutia de secțiune ca planuri de tăiere și o captură.

![Observații: lista cu filtrele ei, o observație cu captura și comentariile ei](shots/remarks.webp)

- **Observație nouă** face una din vederea 3D din față: camera așa cum e, elementele selectate, cutia de secțiune și o imagine luată din vedere. Completezi câmpurile și **Creează**.
- Lista se filtrează după un text, o stare, o prioritate, responsabil și **Doar deschise**. Alegi o observație ca să-i vezi și să-i schimbi câmpurile, să-i citești comentariile și să răspunzi în casetă (**Comentează**; bifezi **Cu vederea 3D** ca răspunsul să ia vederea așa cum e).
- **Arată în 3D** deschide punctul de vedere al observației: vederea 3D merge la camera ei, elementele ei sunt selectate (cele pe care proiectul le are, după GlobalId-ul IFC), iar planurile ei de tăiere devin cutia de secțiune. Un comentariu cu vederea lui are **Arată** lângă el.
- **Reia din vederea 3D** înlocuiește punctul de vedere al observației cu vederea așa cum e acum.
- **Importă BCF…** citește un fișier `.bcfzip` (BCF 2.1) sau `.bcf` (BCF 3.0): un subiect pe care proiectul îl are deja (același GUID) se actualizează — câmpurile fișierului câștigă, iar comentariile făcute aici pe care fișierul nu le are rămân — celelalte se adaugă. **Exportă toate**, **Exportă deschise** sau **Exportă selectată** scriu un fișier BCF, 2.1 dacă nu alegi 3.0, de dat verificatorului sau de pus în containerul Shared al folderului proiectului. Subiectele își păstrează GUID-urile, deci un drum dus-întors printr-un alt program se întoarce la aceleași observații.

![O observație deschisă la punctul ei de vedere în vederea 3D](shots/remark-viewpoint.webp)

Numele tău pe ce scrii se pune în caseta **Numele tău** a dialogului (se păstrează pe acest calculator).
## Observații pe planșe

Un verificator deschide un PDF emis în Acrobat, Bluebeam, Preview sau Drawboard și îl marchează: note lipite, text, evidențieri și tăieri, trasee cu creionul, dreptunghiuri, cercuri, săgeți, nori, ștampile, măsurători, răspunsuri. **Manage → Observații pe planșe…** citește aceste marcaje înapoi în proiect, ca observații pe planșă:

![Observații: lista pe planșe, un PDF citit](shots/cde-remarks.webp)

- **Citește observațiile din PDF…** alege fișierul. Planșa căreia îi aparține vine din emitere (manifestul de lângă fișier sau emiterile înregistrate în proiect), altfel din convenția de denumire a fișierului; când nimic nu se potrivește, alegi tu planșa. Paginile unui set PDF merg pe planșele lui, în ordine. Ce s-a găsit e arătat — pagini, adnotări, cum s-a potrivit planșa, eventualele avertismente — iar **Import** aduce observațiile. Citirea aceluiași fișier din nou actualizează observațiile în loc să le dubleze; o observație pe care ai rezolvat-o rămâne rezolvată.
- **Scanează folderul proiectului** (aplicația desktop) listează PDF-urile emise din Shared și Published, care dintre ele s-au schimbat după emitere și dacă observațiile lor au fost citite de atunci.
- Lista e grupată pe planșe și filtrată după stare: **deschise**, **rezolvate**, **închise**. Un click deschide planșa cu observația în față. Fiecare observație poate fi rezolvată, închisă, redeschisă sau ștearsă; starea „Completed” sau „Accepted” pusă de verificator sosește ca rezolvată.

![O planșă cu observațiile ei deasupra](shots/cde-remarks-sheet.webp)

Pe planșă, observațiile sunt desenate peste hârtie la locul lor: conturul în culoarea de revizuire, un marcaj cu inițialele autorului, textul într-un tooltip; cea din față are cardul ei cu textul, cine și când, răspunsurile și starea. Butonul din bara planșei le ascunde sau le arată, iar browserul proiectului numără observațiile deschise lângă planșă. Sunt o suprapunere doar pentru ecran: planșa se tipărește și se emite fără observații.

Lapin citește PDF-ul singur, fără alt program: fișiere clasice și fișiere salvate cu fluxuri de referințe încrucișate, salvări incrementale (ce adaugă programul verificatorului), text în orice codificare. Un fișier pe care nu-l poate citi întreg dă ce se poate, cu un avertisment. Lapin nu scrie niciodată în PDF.
