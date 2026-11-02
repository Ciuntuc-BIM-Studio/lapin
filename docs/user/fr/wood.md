---
title: Bois
order: 54
summary: Murs à ossature bois depuis le configurateur : montants sur trame, lisses, entretoises, et chaque porte et fenêtre encadrée selon ses règles.
---

## Murs à ossature bois

Un type de mur peut être construit en ossature bois : **Wood → Timber frame** ouvre le configurateur, avec les types de murs du projet à gauche. Cochez **Timber frame** et le noyau du type est fait de pièces au lieu d’une seule couche : les lisses, les montants sur leur trame, les rangs d’entretoises et, autour de chaque porte et fenêtre, les montants principaux, les montants d’appui, le linteau et l’appui. Les autres couches restent telles quelles, et l’isolant remplit les vides entre les pièces.

![Le configurateur d’ossature bois](shots/wood-frame.webp)

- **Préréglages** : **nordique** (45 × 145 à 600 mm, C24) ou **roumain** (50 × 150 à 625 mm, C24, un rang d’entretoises à 1,25 m). Chaque champ peut ensuite être changé.
- **Montants, entraxe, classe, lisses, montants d’extrémité, rangs d’entretoises**. **Trame depuis** fixe où commence la trame : le début de chaque mur ou l’origine du projet, pour que les murs d’une façade la partagent.
- **Porteur** : une cloison ne porte rien et reçoit au-dessus de ses baies une pièce à plat au lieu d’un linteau.

## Portes et fenêtres

Les portes et fenêtres restent celles de l’architecte : déplacez-en une ou changez son type, son encadrement suit. L’ossature se mesure depuis la **baie brute** : la porte ou la fenêtre plus les jeux de pose (sur les côtés, en haut, sous la fenêtre ; la porte descend jusqu’au sol).

Chaque baie prend la règle de sa **tranche de largeur** (le tableau sous **Openings** : jusqu’à 900 mm, jusqu’à 1800 mm, au-delà, par défaut) : ses montants principaux et d’appui par côté, son linteau (aucun, massif, LVL ou lamellé-collé, sa section et ses plis) et ses plis d’appui. Aucun montant ne se trouve dans une baie brute ; les courts au-dessus du linteau et sous l’appui gardent la trame, pour que les joints des panneaux tombent sur des montants. Deux baies plus proches que le **trumeau minimal** partagent un linteau et leurs montants principaux extérieurs. Une baie trop près de l’extrémité du mur est signalée dans les propriétés du mur, jamais déplacée.

Sélectionnez une porte ou une fenêtre : sa section **Wood** montre la baie brute, la règle prise et ce qu’elle reçoit. Cochez **Règle propre** pour donner à cette baie seule d’autres montants, un autre linteau ou un autre appui.

## La liste de bois

**Wood → Timber schedule** fait la liste de débit de tout le bois du projet, les pièces des murs et des charpentes : pièce, section, classe, longueur, nombre, longueur totale et volume, avec les totaux par section. En IFC chaque pièce est un IfcMember (STUD, PLATE, le linteau et l’appui par leur nom) sous son mur, et l’isolant un IfcCovering INSULATION.
