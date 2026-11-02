---
title: Struttura
order: 50
summary: Pilastri, travi e fondazioni, i loro tipi e come si incontrano.
---

La scheda **Struttura** contiene il telaio e le fondazioni.

![La scheda Struttura](shots/ribbon-structure.webp)

## Telaio

- **Pilastro** (<kbd>CL</kbd>): fai clic dove si trova. Un pilastro va dal suo livello al livello superiore.
- **Trave** (<kbd>BM</kbd>): fai clic sulle sue due estremità. **Trave curva** (<kbd>BC</kbd>) segue un arco.
- **Pilastri sulla griglia** mette un pilastro in ogni incrocio degli assi della griglia. Per disegnare gli assi, usa **Annota → Asse di griglia** (<kbd>GR</kbd>).

## Fondazioni

- **Trave di fondazione** (<kbd>SF</kbd>) corre sotto i muri. I suoi strati esterni sono tagliati a quartabuono agli angoli.
- **Trave di fondazione curva** (<kbd>CF</kbd>) segue un arco.
- **Plinto** (<kbd>PF</kbd>) va sotto un pilastro.

Disegna le fondazioni sul livello **Foundations**, oppure dai loro una pianta propria.

![Pilastri, travi e plinti in 3D](shots/structure-3d.webp)

## Tipi e angoli

**Tipi strutturali** imposta le sezioni di pilastri, travi e fondazioni: sezioni in calcestruzzo e profili in acciaio. Fai clic su un tipo nell’elenco per cambiarne la dimensione o il nome, poi **Salva tipo**: tutti gli elementi di quel tipo si adeguano; **Nuovo tipo** torna alla creazione di un tipo.

Travi e fondazioni si uniscono da sole dove si incontrano, con un angolo a quartabuono. Una trave disegnata da asse di pilastro ad asse di pilastro si ferma alle facce dei pilastri; la sua lunghezza, il suo volume e le sue staffe contano la luce netta tra di essi. **A quartabuono**, **Di testa** e **Scambia** cambiano il modo in cui si incontrano due travi o due fondazioni. **Corrispondenza tipo** (<kbd>MA</kbd>) copia il tipo di un elemento su altri, e **Crea simile** (<kbd>CS</kbd>) avvia il disegno di un altro elemento dello stesso tipo.

## Una vista strutturale

La disciplina di una vista decide cosa mostra. Una vista **Struttura** mostra solo i nuclei portanti, senza le finiture. Scegli la disciplina nelle proprietà della vista. Vedi [Viste](views.md).
