---
title: Lucrul împreună
order: 125
summary: Partajezi proiectul deschis și lucrați în el în timp real, din Lapin sau din browser.
---

<!-- video sharing -->

Mai mulți oameni pot lucra în același proiect în același timp, cum intră jucătorii pe serverul unui joc. Unul dintre voi partajează proiectul din Lapin, de pe calculatorul lui: el e **gazda**. Ceilalți intră ca **oaspeți**, din Lapin de pe calculatorul lor sau din browser, cu link-ul trimis de gazdă.

Proiectul rămâne pe calculatorul gazdei, iar Lapin-ul gazdei hotărăște fiecare modificare. Oaspeții văd modelul cum se schimbă, desenează cu aceleași unelte și văd unde sunt ceilalți.

## Partajezi proiectul

**Fișier → Partajează sesiunea…**, îți dai numele, alegi cum intră oaspeții, apoi apeși **Partajează**:

- **editorii** modelează ca tine;
- **vizitatorii** se uită și se plimbă prin model, fără să schimbe nimic.

Lapin îți arată un **link** și un **cod**. Trimiți link-ul celor care intră din browser și codul celor care intră din Lapin. Oricine le are poate intra până apeși **Oprește partajarea**. De fiecare dată când partajezi, link-ul și codul sunt noi.

![Partajarea: link-ul, codul și cine e în sesiune](shots/share-dialog.webp)

În același dialog poți face un oaspete editor sau vizitator, sau îl poți **ruga să iasă**. Închizi dialogul și sesiunea continuă. Iconița cu oameni de lângă participanți, în bara de sus, îl deschide din nou.

Cât proiectul e partajat și are fișier, Lapin îl salvează la fiecare 5 minute. Doar gazda salvează; oaspeții pot exporta pentru ei fișiere IFC și 3D.

## Intri într-o sesiune

- **Din browser**: deschizi link-ul, îți dai numele și apeși **Intră**. Nu instalezi nimic.
- **Din Lapin**: pe ecranul de start sau în **Fișier** alegi **Intră într-o sesiune…**, lipești link-ul sau codul și îți dai numele.

![Intrarea din Lapin](shots/join-dialog.webp)

O bară jos îți spune în sesiunea cui ești. **Ieși** te duce înapoi în Lapin-ul tău. Dacă gazda oprește partajarea sau închide Lapin, Lapin îți spune, iar **Înapoi în Lapin** te duce înapoi.

## Cine unde e

Participanții sunt în bara de sus, fiecare cu culoarea lui. Dai clic pe unul ca să mergi în vederea la care se uită.

![Participanții](shots/participants.webp)

În plan vezi cursoarele celorlalți, cu numele lor, și ce au selectat, conturat în culoarea lor.

![Cursorul și selecția unui oaspete în planul gazdei](shots/sharing-plan.webp)

## Anularea e a ta

<kbd>Ctrl Z</kbd> anulează ultima ta modificare, nu pe a altcuiva. Dacă cineva a modificat-o după tine, Lapin o lasă cum e și îți spune cine. Atunci anularea ta nu merge mai înapoi de modificarea aceea.

## Sigur prin construcție

- Tot ce trece între gazdă și oaspeți trece prin releul Lapin, **criptat cap la cap** cu cheia invitației. Releul doar transmite și nu poate citi nimic.
- Un oaspete nu poate deschide, salva sau citi fișiere de pe calculatorul gazdei și nici nu poate deschide acolo alt proiect.
- Lapin nu deschide nimic spre rețea până nu partajezi și se oprește când oprești.
