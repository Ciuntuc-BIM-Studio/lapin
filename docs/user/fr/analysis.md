---
title: Analyse structurelle
order: 52
summary: Lancez une analyse statique linéaire du portique sous vos charges, lisez les diagrammes des efforts, des moments et de la déformée en 3D et réglez les relâchements aux extrémités des barres.
---

**Lancer l'analyse** du groupe **Charges** de l'onglet **Structure** (et de la boîte Charges) analyse les barres du modèle sous les charges et combinaisons de [Charges](loads.md) et montre les efforts qui s'y exercent.

![Le groupe Charges, avec Lancer l'analyse](shots/ribbon-analysis.webp)

> Les résultats sont **indicatifs**. Les charges, les combinaisons et l'analyse sont à vérifier par vous avant de vous y fier.

## Ce qui est analysé

- Les **barres** (poteaux, poutres, éléments en acier et en bois) entrent avec leur propre rigidité, d'après leur matériau et leur section. Une barre dont le matériau n'a pas de module d'élasticité est laissée de côté et listée.
- Les **murs porteurs** sont des appuis : ils tiennent les barres qui reposent dessus, mais ne sont pas analysés comme des murs.
- Les **planchers et dalles** ne portent pas comme des plaques ; ils reportent leurs charges sur les barres et murs en dessous par la **règle des 45°** : les bissectrices des angles divisent la dalle en zones que porte chaque côté, et chacune devient une charge trapézoïdale ou triangulaire sur la barre dessous.
- Les **toitures à charpente** reportent la charge sur chaque chevron selon sa **largeur tributaire**, jusqu'à mi-distance de ses voisins ; la neige est prise par projection horizontale.
- Les **tiges et tirants** (une barre ronde pleine qui contrevente, un tirant ou un câble) travaillent axialement : ils sont articulés à leurs extrémités et leur poids va à leurs extrémités, de sorte qu'ils ne fléchissent pas comme des poutres. Le champ **Barre à effort axial** dans les propriétés **Analyse** d'une barre le change : **D'après sa section et son usage**, **Oui (une tige ou un tirant)** ou **Non (une poutre)**.
- Les **poutres de hauteurs différentes** qui rencontrent un poteau gardent chacune leur niveau ; le poteau n'a qu'un nœud, et les poutres y sont liées par un bras rigide.
- Le **vent sur les façades** va aux poutres, lisses et rives de plancher le long d'elles ; la bande à leur pied va directement au sol et est listée à part des sommes des barres.
- Le **poids propre** des barres est pris le long d'elles, et les charges que vous avez dessinées et les charges dérivées vont là où elles trouvent quelque chose dessous. Une charge qui ne trouve rien est laissée de côté et signalée.

## Lancer le calcul

Choisissez **Lancer l'analyse**. La boîte **Résultats de l'analyse** s'ouvre :

![La boîte Résultats de l'analyse](shots/analysis-results.webp)

- Le résumé : les barres, nœuds et inconnues du calcul et sa durée.
- Pour chaque cas, la **somme de ses charges** face à la **somme des réactions**, et s'ils **s'équilibrent** (une coche) ou non (une croix : ne vous fiez pas à ces résultats). Les charges non affectées sont données en kN.
- Ce qui a été **laissé hors** de l'analyse, et pourquoi.
- Les valeurs **maximales et minimales** des efforts et moments, le déplacement et la flèche maximaux (avec leur rapport à la portée, L/300) du cas, de la combinaison ou de l'enveloppe choisis.

**Afficher les diagrammes** ferme la boîte et montre les résultats dans la vue 3D.

## Les diagrammes en 3D

Une carte sur la vue 3D choisit ce qui est dessiné :

![Le moment fléchissant de l'enveloppe à l'état limite ultime](shots/analysis-moments.webp)

- **Quoi** : un cas de charge, une combinaison ou l'**enveloppe** des combinaisons à l'état limite ultime (ELU) ou de service (ELS) : la valeur maximale et minimale en chaque point.
- **Grandeur** : **N** effort normal (traction positive), **Vy** et **Vz** efforts tranchants, **T** torsion, **My** et **Mz** moments fléchissants (**Mz** est l'axe fort d'une poutre horizontale, positif quand la fibre inférieure est tendue) ou la **Déformée**.
- **Échelle** : la taille du diagramme à l'écran ; le modèle lui-même ne change pas.
- **Réactions** : des flèches aux appuis avec leurs valeurs en kN.

Chaque barre montre sa ligne de base, la courbe, des hachures entre elles et les valeurs aux extrêmes ; le rouge est positif, le bleu négatif. Les moments sont tracés du côté où la barre est tendue.

![La déformée](shots/analysis-deformed.webp)

## Relâchements aux extrémités

Sélectionnez un poteau, une poutre ou un élément : ses propriétés ont une section **Analyse** avec les relâchements **Début (i)** et **Fin (j)**.

![La section Analyse d'une poutre](shots/analysis-bar.webp)

- **Par défaut** : ce que dit l'assemblage (une éclisse, un gousset ou un contreventement articule l'élément), un élément de charpente en bois articulé selon son axe fort, toute autre barre encastrée.
- **Encastré** : l'extrémité reprend des moments. **Articulé** : elle tourne librement autour des deux axes de flexion.
- **Personnalisé** : six cases, une par degré de liberté (déplacement le long de la barre, le long de chaque axe de cisaillement, torsion, rotation autour de chaque axe de flexion) ; un degré coché est relâché.

Un changement est une seule étape d'annulation.

## Quand le modèle change

Les résultats sont calculés à partir du modèle tel qu'il est et jamais enregistrés. Quand vous changez le modèle, les diagrammes quittent la vue et la carte le dit :

![La carte après un changement](shots/analysis-pick.webp)

**Relancer** analyse le nouveau modèle.

## Un mécanisme

Si la structure peut bouger sans résistance (trop de relâchements, ou une partie que rien ne retient), l'analyse s'arrête et dit où :

![Un mécanisme](shots/analysis-mechanism.webp)

Elle nomme le nœud, le degré de liberté et les barres concernées ; **Sélectionner les éléments concernés** les sélectionne pour que vous puissiez corriger, en général en encastrant une extrémité ou en ajoutant un appui.
