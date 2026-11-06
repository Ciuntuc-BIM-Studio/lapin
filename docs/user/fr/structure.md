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

**Types structurels** définit les sections des poteaux, des poutres et des semelles : sections en béton et profilés en acier. La section d’un poteau est un **Rectangle**, un **Cercle** ou un **Polygone**, régulier, donné par ses **Côtés** et son **Diamètre** entre les sommets (un poteau octogonal, comme peut en avoir un IFC importé). Cliquez sur un type dans la liste pour modifier sa taille ou son nom, puis **Enregistrer le type** : tous les éléments de ce type suivent ; **Nouveau type** revient à la création d’un type.

Les poutres et les semelles se joignent d’elles-mêmes là où elles se rencontrent, un angle en onglet. Une poutre tracée d’axe de poteau à axe de poteau s’arrête aux faces des poteaux ; sa longueur, son volume et ses cadres comptent la portée libre entre elles. **Onglet**, **Bout à bout** et **Permuter** changent la façon dont deux poutres ou deux semelles se rencontrent. **Appliquer le type** (<kbd>MA</kbd>) copie le type d’un élément vers d’autres, et **Créer un similaire** (<kbd>CS</kbd>) commence le dessin d’un autre élément du même type.

## Semelles filantes à profil

Un type de semelle filante est soit le type standard, un voile avec ses couches sur une semelle, soit n'importe quel profil balayé le long de l'axe de la semelle, comme les profils de semelle de Revit. Dans **Types structurels → Strip**, choisissez **Forme : Profil** et un profil : un profil du catalogue (rectangulaire, à 2 ou 3 redans, à dessus incliné, excentrée avec son voile sur un bord, avec voile) avec ses dimensions, ou un profil du projet (**Gérer → Profils**, où vous pouvez aussi saisir ses sommets ou lire un contour dessiné). Le point d'insertion du profil se place sur l'axe de la semelle, au niveau **Bas** du type.

Les semelles à profil sont droites ou courbes. Deux semelles qui se rencontrent en angle sont coupées en onglet, et une semelle qui s'arrête contre une autre y pénètre et est coupée par son corps, de sorte que chaque redan rejoint le redan de l'autre. Les plans, coupes et élévations coupent le corps balayé. Les quantités donnent sa longueur, l'aire de sa section et son volume, et ses faces verticales sont celles que les Installations de chantier coffrent. En IFC, une semelle droite est un IfcFooting STRIP_FOOTING dont le corps est son profil extrudé (un IfcArbitraryClosedProfileDef), coupé par les plans des angles ; une semelle courbe est écrite par ses faces. Choisissez **Standard** pour revenir au voile sur semelle.

![Semelles filantes à redans sous une maison, en onglet aux angles](shots/footing-profile-3d.webp)

![Un type de semelle filante à profil](shots/footing-profile-type.webp)

## Une vue structurelle

La discipline d’une vue décide de ce qu’elle montre. Une vue **Structure** ne montre que les noyaux porteurs, sans les finitions. Vous choisissez la discipline dans les propriétés de la vue. Voir [Coupes, élévations et 3D](views.md).
