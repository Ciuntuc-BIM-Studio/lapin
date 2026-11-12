---
title: Scripturi Python
order: 127
summary: Conduci Lapin cu Python din panoul Script, un extra pentru dezvoltatori: o consolă și un editor care rulează Python-ul tău, fiecare Rulare un singur pas de anulare.
---

## Pornește Extra pentru dezvoltatori

Lapin poate fi condus cu Python: fiecare unealtă e o comandă, iar un script trimite aceleași comenzi. Panoul **Script** e un *extra pentru dezvoltatori*, oprit până îl pornești, așa că altfel nu se vede nimic din el.

Deschide **Fișier → Preferințe…** și, la **General**, bifează **Extra pentru dezvoltatori**.

![Preferințe: Extra pentru dezvoltatori](shots/python-preferences.webp)

- **Python**: Python-ul pe care îl rulează panoul. Gol, Lapin caută unul pe acest calculator (`python3`, `py -3` pe Windows, cel de la python.org și cel din Homebrew). Trebuie să fie **Python 3.11 sau mai nou**; Lapin nu instalează Python. Ia-l de pe [python.org](https://www.python.org/downloads/) dacă nu ai, sau scrie calea celui pe care îl vrei (al unui mediu virtual, de exemplu).
- **Editor extern**: editorul pe care îl folosește **Deschide în editorul extern**, ca o comandă (`code` pentru Visual Studio Code). Gol, editorul de text al sistemului.

Python rulează doar în aplicația desktop: în Lapin pentru browser, panoul spune asta.

## Panoul Script

Deschide **Vedere → Script**. Panoul se deschide de-a lungul marginii de jos a ferestrei; trage de marginea lui de sus ca să-l faci mai înalt. Antetul lui spune ce Python a găsit sau de ce n-a găsit niciunul.

![Panoul Script după o rulare: patru pereți, un singur pas de anulare](shots/python-panel.webp)

- **Ale mele**, în stânga: scripturile tale, fișiere `.py` obișnuite, păstrate în folderul lui Lapin de pe acest calculator. Dă clic pe unul ca să-l deschizi; **Script nou** pornește de la un exemplu.
- **Editorul**, la mijloc: Python cu culorile și indentarea lui. Scrii `lapin.` și îți propune ce are Lapin (comenzile tuturor modulelor ediției tale și apelurile lui proprii); `lapin.architecture.` propune comenzile de arhitectură cu argumentele lor. Dă-i scriptului un nume și **salvează-l** în Ale mele; **Deschide în editorul extern** îi deschide fișierul în editorul tău, iar ce salvezi acolo revine în panou.
- **Consola**, în dreapta: ce afișează scripturile, erorile lor cu rândul la care s-au oprit (marcat în editor) și un rând în care scrii Python. **Enter** îl rulează, **Shift+Enter** începe un rând nou, **Sus** și **Jos** aduc înapoi rândurile scrise înainte. Un nume pe care îl dai în consolă rămâne pentru rândul următor și pentru scripturile pe care le rulezi.

În panou, `lapin` e pachetul `lapinbim`, deja încărcat. Același script rulează dintr-un terminal cu `import lapinbim as lapin`, cât timp Lapin e deschis.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Lungimile sunt în metri și unghiurile în grade, oricare ar fi unitatea în care arată proiectul.

## Rulează, Oprește, Repornește, Anulează

- **Rulează** (**Ctrl+Enter** în editor) rulează scriptul. Tot ce face e **un singur pas de anulare**, numit după script: un singur **Anulează** le ia pe toate înapoi.

  ![Un singur Anulează a luat înapoi cei patru pereți ai rulării](shots/python-undo.webp)

- Cât rulează un script, modelul e al scriptului: o unealtă folosită în fereastră e refuzată cu *A script is editing the model*; vederile, selecția și extrasele merg mai departe.
- Un script care eșuează nu păstrează **nimic** din ce a făcut: modelul e cum era înainte de **Rulează**, iar consola arată eroarea și rândul ei.
- **Oprește** oprește scriptul; nu se păstrează nimic din ce a făcut. Unui script care nu se oprește în două secunde i se închide Python-ul.
- **Repornește** pornește Python din nou: numele păstrate de consolă se uită.

## Siguranța

Un script poate face orice poți face tu pe acest calculator: rulează în Python-ul tău, cu drepturile tale. Rulează scripturi scrise de tine sau în care ai încredere.

Un invitat al unei sesiuni partajate nu poate rula scripturi pe calculatorul tău, iar un script se conectează doar la Lapin-ul de pe propriul lui calculator.

## Pentru dezvoltatori

Ce oferă `lapin` (comenzile, interogările, loturile, modelul, evenimentele) e descris în ghidul pentru dezvoltatori, *Lapin from Python* (`docs/python.md` în depozitul lui Lapin); pachetul e pe PyPI ca `lapinbim`.
