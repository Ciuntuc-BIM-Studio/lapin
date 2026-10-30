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
