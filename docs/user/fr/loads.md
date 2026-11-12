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

Un matériau sans masse volumique est laissé de côté, et le Récapitulatif dit lequel.

## La charge d'exploitation d'une pièce

Sélectionnez une pièce : ses propriétés ont une section **Charge d'exploitation**. Sa **Catégorie** est **D'après la fonction** (un séjour, une chambre ou une cuisine est A, un bureau B, un local de stockage E1, un garage F) sauf si vous en choisissez une, ou **None**. En dessous, le **qk** que donne la catégorie.

![La charge d'exploitation d'une pièce dans ses propriétés](shots/loads-room.webp)

## Le tableau des charges d'exploitation

L'onglet **Charges d'exploitation** liste chaque catégorie avec son usage et son **qk** en kN/m², comme le recommande l'EN 1991-1-1. Saisissez dans le champ la valeur de votre annexe nationale ; **Modifié** revient à la valeur par défaut.

![L'onglet Charges d'exploitation](shots/loads-imposed.webp)

## Récapitulatif

L'onglet **Récapitulatif** montre, pour chaque cas, les charges que vous avez dessinées et le total en kN, au total et par niveau, puis les charges dérivées une à une, avec leur origine. Il signale les pièces dont la catégorie n'a pas de cas et note ce qui a été laissé de côté.

![L'onglet Récapitulatif](shots/loads-summary.webp)

## Combinaisons

L'onglet **Combinaisons** liste les combinaisons des cas, d'après l'EN 1990 avec ses valeurs recommandées, indicatives : à l'état limite ultime 1,35 sur les actions permanentes et 1,5 sur la variable dominante, les autres avec leur ψ0 ; à l'état limite de service les combinaisons caractéristique, fréquente et quasi permanente. Un cas qui ne contient rien n'y participe pas.

![L'onglet Combinaisons](shots/loads-combinations.webp)

## Dans le fichier IFC

**Exporter IFC, Fragments ou BOS…** a **Structural Analysis View (modèle analytique et charges)** : le fichier porte alors le modèle analytique des murs porteurs, planchers, poteaux et poutres, avec les cas de charge, les charges et les combinaisons, comme un IFC Structural Analysis View. Voir [Import et export](import-export.md).
