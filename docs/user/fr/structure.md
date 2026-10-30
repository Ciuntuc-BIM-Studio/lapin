---
title: Structure
order: 50
summary: Poteaux, poutres et semelles, leurs types, et la façon dont ils se rencontrent.
---

L’onglet **Structure** regroupe l’ossature et les fondations.

![L’onglet Structure](shots/ribbon-structure.webp)

## Ossature

- **Poteau** (<kbd>CL</kbd>) : cliquez là où il se dresse. Un poteau va de son niveau au niveau supérieur.
- **Poutre** (<kbd>BM</kbd>) : cliquez ses deux extrémités. **Poutre courbe** (<kbd>BC</kbd>) suit un arc.
- **Poteaux sur quadrillage** place un poteau à chaque croisement des axes du quadrillage. Pour dessiner les axes, utilisez **Annoter → Axe de quadrillage** (<kbd>GR</kbd>).

## Fondations

- **Semelle filante** (<kbd>SF</kbd>) passe sous les murs. Ses couches extérieures sont coupées en onglet aux angles.
- **Semelle filante courbe** (<kbd>CF</kbd>) suit un arc.
- **Semelle isolée** (<kbd>PF</kbd>) se place sous un poteau.

Dessinez les fondations sur le niveau **Foundations**, ou donnez-leur un plan à elles.

![Poteaux, poutres et semelles isolées en 3D](shots/structure-3d.webp)

## Types et angles

**Types structurels** définit les sections des poteaux, des poutres et des semelles : sections en béton et profilés en acier.

**Onglet**, **Bout à bout** et **Permuter** changent la façon dont deux poutres ou deux semelles se rencontrent. **Appliquer le type** (<kbd>MA</kbd>) copie le type d’un élément vers d’autres, et **Créer un similaire** (<kbd>CS</kbd>) commence le dessin d’un autre élément du même type.

## Une vue structurelle

La discipline d’une vue décide de ce qu’elle montre. Une vue **Structure** ne montre que les noyaux porteurs, sans les finitions. Vous choisissez la discipline dans les propriétés de la vue. Voir [Vues](views.md).
