---
title: Extensii
order: 126
summary: Adaugi mobilier, șabloane de proiect, tipuri de pereți, uși și ferestre, profile, hașuri, cartușe și liste de prețuri făcute de alții din catalogul Lapin Extensions, și le scoți din nou.
---

## Catalogul

**Lapin Extensions** e un catalog gratuit de lucruri făcute pentru Lapin de utilizatorii lui și de noi: pachete de mobilier, șabloane de proiect, biblioteci de profile, hașuri, cartușe și liste de prețuri. Fiecare intră în biblioteca pe care Lapin o are deja pentru el, așa că îl folosești ca și cum l-ai fi făcut tu.

Deschide **Fișier → Extensii…**. **Răsfoiește** arată catalogul: cauți după nume, etichetă sau ce conține o extensie, ori alegi o categorie.

![Extensii: Răsfoiește, detaliile unui pachet de mobilier](shots/extensions-browse.webp)

Clic pe o extensie ca să-i vezi detaliile înainte s-o instalezi:

- **Autor** și **Licență**. Fiecare extensie are o licență, aleasă de autorul ei. Una *permisivă* (MIT, CC-BY, CC0…) te lasă să folosești ce conține în orice proiect; la alta, *citește-i condițiile* întâi.
- **Necomercială**: o extensie a cărei licență interzice folosirea comercială (CC-BY-NC, PolyForm Noncommercial…) e marcată așa în listă și în detalii. O poți instala și învăța din ea, dar nu o folosi în lucrări plătite.
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
| Tipuri | proiectul deschis când o instalezi, într-un pas de anulat (**Gestionare → Pachete de tipuri…** le exportă pe ale tale) |

Dacă biblioteca ta are deja ceva cu același nume, Lapin întreabă înainte să-l înlocuiască. Nimic din ce aduce o extensie nu se rulează: extensiile sunt conținut.

Catalogul Lapin este și **semnat** de Lapin Extensions: catalogul și fiecare fișier al lui poartă o semnătură pe care Lapin o verifică înainte să citească lista sau să instaleze un fișier, așa că o copie schimbată pe drum este refuzată. O linie deasupra listei spune **Semnat de Lapin Extensions** când este.

**Instalate** arată ce are acest calculator. Fiecare rând deschide locul unde a ajuns (**Mobilierul meu…**, **Șabloane de proiect…**), oferă **Actualizează** când catalogul are o versiune mai nouă, și **Dezinstalează**.

![Instalate: pachetul de mobilier, cu butonul care deschide Mobilierul meu](shots/extensions-installed.webp)

![Tipurile de mobilier ale pachetului în Mobilierul meu](shots/extensions-my-furniture.webp)

**Dezinstalează** scoate din bibliotecile tale ce a adăugat extensia, și doar atât. Ce ai pus deja în proiecte rămâne în ele: un tip de mobilier inserat într-un proiect, un proiect pornit dintr-un șablon.

## Tipuri

O extensie de **tipuri** aduce tipuri de pereți, plăci, uși, ferestre, stâlpi, grinzi sau scări cu tot ce sunt făcute: straturile, materialele, profilele și parametrii lor. Instalată, tipurile ei intră în proiectul deschis acum, într-un pas de anulat: un material cu același nume deja în proiect e folosit, oricare altul e creat; un tip deja acolo exact la fel e refolosit, iar unul cu același nume dar diferit e adăugat lângă el cu (2) după nume. **Instalate → Adaugă-i tipurile în acest proiect** le aduce în alt proiect.

![Detaliile unei extensii de tipuri: licența ei marcată necomercială](shots/extensions-types.webp)

Tipurile tale intră într-un pachet din **Gestionare → Pachete de tipuri…**: bifezi tipurile, dai pachetului un nume, iar **Exportă tipuri…** salvează un fișier `.lapintypes`. **Importă tipuri…** din același dialog citește unul, te lasă să-i alegi tipurile și le adaugă la fel. Un pachet duce orice fel de tip cu tot ce îl alcătuiește: straturi, materiale cu culorile și hașurile lor, profile și parametri (seturi de proprietăți IFC cu numele, felul, valorile și unitățile lor, cu valorile implicite pentru instanțe marcate). Înainte să le adauge, dialogul arată materialele și tipurile din pachet pe care proiectul le are deja cu același nume, definite altfel, cu ce diferă, și alegi pentru fiecare, ca la importul tipurilor de pereți: un material păstrat, actualizat pe loc din pachet sau păstrat alături ca *Nume (2)*; un tip păstrat alături (implicit), actualizat pe loc (elementele lui urmează) sau sărit. După import se arată ce s-a creat, refolosit, actualizat și sărit. O eroare la citirea unui pachet se arată întotdeauna.

![Gestionare → Pachete de tipuri…: un tip de perete ales pentru export](shots/type-packs-export.webp)

## Scripturi

Catalogul are și **scripturi Python**. În aplicația desktop poți instala unul de acum; rulează când Lapin va avea consola Python, și doar când îl rulezi tu. Înainte să-l instalezi, detaliile lui spun ce cere (să citească sau să schimbe proiectul deschis, să ajungă la fișiere sau la internet). Un script poate face pe calculatorul tău ce poți face tu, așa că instalează scripturi de la autori în care ai încredere. În browser se instalează doar pachete de conținut.

![Detaliile unui script: ce cere și că nu se poate rula încă](shots/extensions-script.webp)

## Alt catalog

**Catalog…**, jos în dialog, setează adresa catalogului: o adresă https sau calea unui `index.json` de pe acest calculator ori dintr-un folder partajat (catalogul propriu al unui birou). Lasă-l gol pentru catalogul Lapin. Săgeata de lângă el citește catalogul din nou. Un catalog propriu nu trebuie să fie semnat: Lapin spune atunci deasupra listei că **nu e semnat de Lapin Extensions**, așa că instalează din el doar dacă ai încredere în cine îl ține. Un catalog a cărui semnătură nu se potrivește nu este citit niciodată.

## Publică-ți propriile extensii

Catalogul are propriul site, **[store.lapinbim.com](https://store.lapinbim.com)**: o pagină pentru fiecare extensie, cu imaginile, versiunile, licența și descărcările ei, și o pagină pentru fiecare autor.

Oricine poate publica o extensie. O exporți din Lapin (Mobilierul meu → Exportă, Salvează ca șablon…, o bibliotecă de profile, un cartuș), apoi:

- **Pe magazin**: intră pe store.lapinbim.com cu contul tău de pe [forum](https://forum.lapinbim.com), deschide **Your extensions** (extensiile tale), completează formularul și încarcă fișierul și câteva capturi de ecran. Magazinul verifică fișierul așa cum o va face Lapin și îl trimite la verificare; pagina ta arată verificarea și, după publicare, de câte ori a fost descărcat. O versiune nouă merge la fel.
- **Pe GitHub**: atașează fișierul la un release al unui depozit al tău și adaugă manifestul lui printr-un pull request pe [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). README-ul lui spune cum, și ce verifică testele automate.

Un pachet de tipuri vine din **Gestionare → Pachete de tipuri… → Exportă tipuri…**; o listă de prețuri din **Liste de prețuri…**, cu butonul de export al unei liste de pe acest calculator (un fișier `.lapinprices`, cu prețurile marcate orientative).
