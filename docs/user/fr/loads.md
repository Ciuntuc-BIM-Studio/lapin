---
title: Charges
order: 51
summary: Cas de charge, charges surfaciques, linéiques et ponctuelles, charges d'exploitation des pièces, charges dérivées et combinaisons de l'EN 1990, pour un calcul de structure.
---

Le groupe **Charges** de l'onglet **Structure** contient les charges que prend un calcul de structure : **Cas de charge…**, **Charge surfacique**, **Charge linéique** et **Charge ponctuelle**. Les valeurs sont en kN, kN/m et kN/m².

![Le groupe Charges de l'onglet Structure](shots/ribbon-loads.webp)

> Les valeurs que Lapin fournit (les charges d'exploitation de l'EN 1991-1-1 et les coefficients de l'EN 1990, ceux recommandés) sont **indicatives**. Saisissez les valeurs de votre annexe nationale et vérifiez-les avant d'utiliser les résultats.

## Cas de charge

**Cas de charge…** ouvre la boîte Charges. **Ajouter les cas standard** crée G1 (poids propre), G2 (finitions et cloisons), Q-A (exploitation, catégorie A), S (neige) et W (vent).

![L'onglet Cas de la boîte Charges](shots/loads-cases.webp)

Un cas a un **Code**, un **Nom**, un **Type** (Permanente, Variable ou Accidentelle), une **Source** (Poids propre / charge permanente, Finitions, Exploitation, Neige, Vent, Séisme ou Définie par l'utilisateur) et, pour un cas d'exploitation, une **Catégorie**. Les trois champs ψ0, ψ1 et ψ2 sont les coefficients de combinaison ; laissez-les vides pour prendre ceux de la catégorie dans le tableau. Une valeur qui n'est pas un nombre de 0 à 1 est signalée et n'est pas conservée.

**Ajouter les cas manquants** crée un cas d'exploitation pour chaque catégorie utilisée par les pièces qui n'en a pas.

## Poser des charges

- **Charge surfacique** : cliquez les coins, puis appuyez sur <kbd>Enter</kbd>.
- **Charge linéique** : cliquez ses deux extrémités.
- **Charge ponctuelle** : cliquez où elle s'applique.

Tant qu'un outil est actif, la barre de la ligne de commande propose la **valeur** de la charge et le **cas de charge** où elle va (par défaut **Charge d'exploitation (Q-A)**). La charge va sur le niveau actif.

![Une charge surfacique de 2,5 kN/m² dessinée dans le séjour](shots/loads-plan.webp)

## Ce que Lapin calcule pour vous

Celles-ci, vous ne les dessinez pas ; Lapin les dérive du modèle à chaque demande, et elles suivent chaque modification :

- Le **poids propre** des planchers, murs, poteaux et poutres, d'après les masses volumiques de leurs matériaux, dans le cas dont la source est le poids propre (G1).
- Les **finitions des planchers**, d'après leurs couches, et les **murs non porteurs**, en charges linéiques, dans le cas qui liste finitions et cloisons (G2).
- Les **pièces** : la charge d'exploitation de la catégorie d'une pièce sur sa surface, dans le cas de cette catégorie (Q-A, Q-B…).
- La **neige** et le **vent**, dès que vous donnez les valeurs du site (voir [Neige et vent](#neige-et-vent)).

Un matériau sans masse volumique est laissé de côté, et le Récapitulatif dit lequel.

## La charge d'exploitation d'une pièce

Sélectionnez une pièce : ses propriétés ont une section **Charge d'exploitation**. Sa **Catégorie** est **D'après la fonction** (un séjour, une chambre ou une cuisine est A, un bureau B, un local de stockage E1, un garage F) sauf si vous en choisissez une, ou **None**. En dessous, le **qk** que donne la catégorie.

![La charge d'exploitation d'une pièce dans ses propriétés](shots/loads-room.webp)

## Le tableau des charges d'exploitation

L'onglet **Charges d'exploitation** liste chaque catégorie avec son usage et son **qk** en kN/m², comme le recommande l'EN 1991-1-1. Saisissez dans le champ la valeur de votre annexe nationale ; **Modifié** revient à la valeur par défaut.

![L'onglet Charges d'exploitation](shots/loads-imposed.webp)

## Neige et vent

L'onglet **Neige et vent** contient le climat du site. Lapin ne livre pas les cartes nationales : relevez les valeurs de votre site dans la **CR 1-1-3/2012** (neige, sa figure 3.1 ou son annexe A) et la **CR 1-1-4/2012** (vent, sa figure 2.1 ou son annexe A), ou dans l'annexe nationale selon laquelle vous travaillez, et saisissez-les.

![L'onglet Neige et vent](shots/loads-climate.webp)

- **Neige** : **Charge de neige au sol s0,k (kN/m²)**, **Coefficient d'exposition Ce** (complète 0,8, partielle 1,0, abritée 1,2), **Coefficient thermique Ct** (1,0 sauf si la toiture est vitrée et perd de la chaleur) et **Coefficient d'importance γIs** (1,0 pour les bâtiments ordinaires). **Appliquer la neige**.
- **Vent** : **Pression de référence du vent qb (kN/m²)**, **Catégorie de terrain** (de 0 à IV, de la mer au centre-ville) et **Coefficient d'importance γIw**. **Appliquer le vent**.

Les appliquer crée les cas dans lesquels vont leurs charges :

- **S**, la neige sans accumulation, et **S+X**, **S-X**, **S+Y**, **S-Y**, la neige accumulée par un vent soufflant vers chaque côté : sur une toiture à deux versants, le versant face au vent en prend la moitié. Une accumulation qui ne change rien ici (aucun versant ne fait face à ce vent) est laissée de côté, et le Récapitulatif le dit.
- **W+X/s**, **W+X/p**, et ainsi de suite pour les quatre directions : le vent soufflant vers +X, −X, +Y, −Y, une fois avec la **succion** (/s) des toitures et une fois avec leur **pression** (/p).

Les dispositions de la neige et les directions du vent sont des alternatives : une combinaison n'en prend qu'une de chaque.

Ce que Lapin en déduit, à chaque demande :

- **Neige** sur chaque versant de toiture, par m² en plan : s = γIs · μ1 · Ce · Ct · s0,k, avec μ1 = 0,8 jusqu'à 30°, puis moins, 0 à 60° et plus raide ; sur un plancher que rien ne couvre (une terrasse, une toiture plate) μ1 = 0,8.
- **Vent** sur les murs et les toitures : le contour du bâtiment est l'enveloppe convexe de ses murs et poteaux ; les façades prennent les zones A à E, les toitures plates F à I, une toiture à deux versants F à J, selon les coefficients de l'EN 1991-1-4, à la pression de la hauteur du bâtiment pour la catégorie de terrain. Une façade passe son vent aux poutres, lisses et rives de plancher le long d'elle, sa bande la plus basse au sol ; une toiture à ses chevrons ou pannes. Les toitures à croupes, la pression intérieure et les accumulations de neige aux acrotères ne sont pas encore dérivées.

> Ces valeurs sont **indicatives** : les coefficients sont ceux que recommandent les normes. Vérifiez-les avec votre annexe nationale.

## Récapitulatif

L'onglet **Récapitulatif** montre, pour chaque cas, les charges que vous avez dessinées et le total en kN, au total et par niveau, puis les charges dérivées une à une, avec leur origine. Il signale les pièces dont la catégorie n'a pas de cas et note ce qui a été laissé de côté.

![L'onglet Récapitulatif](shots/loads-summary.webp)

## Combinaisons

L'onglet **Combinaisons** liste les combinaisons des cas, d'après l'EN 1990 avec ses valeurs recommandées, indicatives : à l'état limite ultime 1,35 sur les actions permanentes et 1,5 sur la variable dominante, les autres avec leur ψ0 ; à l'état limite de service les combinaisons caractéristique, fréquente et quasi permanente. Un cas qui ne contient rien n'y participe pas.

![L'onglet Combinaisons](shots/loads-combinations.webp)

## Analyser

**Lancer l'analyse** (dans le même groupe et dans la boîte Charges) analyse le portique sous ces charges : voir [Analyse structurelle](analysis.md).

## Dans le fichier IFC

**Exporter IFC, Fragments ou BOS…** a **Structural Analysis View (modèle analytique et charges)** : le fichier porte alors le modèle analytique des murs porteurs, planchers, poteaux et poutres, avec les cas de charge, les charges et les combinaisons, comme un IFC Structural Analysis View. Voir [Import et export](import-export.md).
