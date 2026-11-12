---
title: Extensii
order: 126
summary: Adaugi mobilier, șabloane de proiect, profile, hașuri, cartușe și liste de prețuri făcute de alții din catalogul Lapin Extensions, și le scoți din nou.
---

## Catalogul

**Lapin Extensions** e un catalog gratuit de lucruri făcute pentru Lapin de utilizatorii lui și de noi: pachete de mobilier, șabloane de proiect, biblioteci de profile, hașuri, cartușe și liste de prețuri. Fiecare intră în biblioteca pe care Lapin o are deja pentru el, așa că îl folosești ca și cum l-ai fi făcut tu.

Deschide **Fișier → Extensii…**. **Răsfoiește** arată catalogul: cauți după nume, etichetă sau ce conține o extensie, ori alegi o categorie.

![Extensii: Răsfoiește, detaliile unui pachet de mobilier](shots/extensions-browse.webp)

Clic pe o extensie ca să-i vezi detaliile înainte s-o instalezi:

- **Autor** și **Licență**. Fiecare extensie are o licență, aleasă de autorul ei. Una *permisivă* (MIT, CC-BY, CC0…) te lasă să folosești ce conține în orice proiect; la alta, *citește-i condițiile* întâi.
- **Merge cu**: versiunile Lapin pentru care a fost făcută. Una care nu se potrivește cu Lapin-ul tău spune asta în loc să ofere **Instalează**.
- **Mărime**, fișierul și **SHA-256**-ul lui: amprenta fișierului verificat de catalog.
- **Ce conține**: elementele ei, când autorul le enumeră.
- **Pagina** și **Subiectul de pe forum**: unde îi poți scrie autorului.

## Instalare, actualizare, dezinstalare

Clic pe **Instalează**. Lapin descarcă fișierul, verifică dacă mărimea și SHA-256-ul lui sunt exact cele din catalog (un fișier schimbat pe drum e refuzat și nu se instalează nimic), apoi îl ia în biblioteca lui:

| Fel | Unde ajunge |
|---|---|
| Mobilier | **Interior → Mobilierul meu…** |
| Șabloane de proiect | **Fișier → Nou din șablon…** |
| Profile | bibliotecile de profile de pe acest calculator |
| Hașuri | Hașurile mele, oferite de **Gestionare → Materiale** |
| Cartușe | cartușele de pe acest calculator |
| Liste de prețuri | **Execuție → Liste de prețuri…**, marcate orientative: prețurile sunt ale autorului, verifică-le cu ale tale |

Dacă biblioteca ta are deja ceva cu același nume, Lapin întreabă înainte să-l înlocuiască. Nimic din ce aduce o extensie nu se rulează: extensiile sunt conținut.

**Instalate** arată ce are acest calculator. Fiecare rând deschide locul unde a ajuns (**Mobilierul meu…**, **Șabloane de proiect…**), oferă **Actualizează** când catalogul are o versiune mai nouă, și **Dezinstalează**.

![Instalate: pachetul de mobilier, cu butonul care deschide Mobilierul meu](shots/extensions-installed.webp)

![Tipurile de mobilier ale pachetului în Mobilierul meu](shots/extensions-my-furniture.webp)

**Dezinstalează** scoate din bibliotecile tale ce a adăugat extensia, și doar atât. Ce ai pus deja în proiecte rămâne în ele: un tip de mobilier inserat într-un proiect, un proiect pornit dintr-un șablon.

## Scripturi

Catalogul are și **scripturi Python**. În aplicația desktop poți instala unul de acum; rulează când Lapin va avea consola Python, și doar când îl rulezi tu. Înainte să-l instalezi, detaliile lui spun ce cere (să citească sau să schimbe proiectul deschis, să ajungă la fișiere sau la internet). Un script poate face pe calculatorul tău ce poți face tu, așa că instalează scripturi de la autori în care ai încredere. În browser se instalează doar pachete de conținut.

![Detaliile unui script: ce cere și că nu se poate rula încă](shots/extensions-script.webp)

## Alt catalog

**Catalog…**, jos în dialog, setează adresa catalogului: o adresă https sau calea unui `index.json` de pe acest calculator ori dintr-un folder partajat (catalogul propriu al unui birou). Lasă-l gol pentru catalogul Lapin. Săgeata de lângă el citește catalogul din nou.

## Publică-ți propriile extensii

Oricine poate publica o extensie: o exporți din Lapin (Mobilierul meu → Exportă, Salvează ca șablon…, o bibliotecă de profile, un cartuș), atașezi fișierul la un release al unui depozit GitHub al tău și adaugi manifestul ei în catalog printr-un pull request pe [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). README-ul lui spune cum, și ce verifică testele automate.
