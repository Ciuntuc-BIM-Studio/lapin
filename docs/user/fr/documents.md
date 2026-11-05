---
title: Documents
order: 105
summary: Les pièces écrites du projet — la notice descriptive, le cahier des charges, le bordereau des plans — faites à partir de modèles, remplies depuis la maquette et exportées en DOCX, ODT et PDF sans aucun autre logiciel.
---

## Ce qu'est un document

Un projet ne se résume pas à ses plans : il a aussi ses pièces écrites, une notice descriptive, un cahier des charges, un bordereau des plans. Dans Lapin, ce sont des **documents** : chacun part d'un modèle et se remplit depuis le projet — ses informations, ses surfaces, ses niveaux et ses pièces, ses types de murs, ses nomenclatures et ses feuilles. Ce que vous seul savez (la structure, la sécurité incendie, l'installation de chantier), vous l'écrivez dans les saisies du document. Un document suit la maquette : changez un type de mur ou ajoutez une pièce, et le document le dira la prochaine fois que vous l'ouvrirez.

Lapin écrit lui-même les fichiers, en DOCX, ODT et PDF, sans Word ni LibreOffice sur l'ordinateur, dans l'application de bureau comme dans le navigateur.

## Informations du projet

**Gérer → Informations du projet…** contient ce que les documents demandent au projet : son nom, son numéro et sa phase, le maître d'ouvrage et son adresse, l'adresse du site, le numéro cadastral et le livre foncier, le concepteur et son adresse, l'architecte en chef, qui a dessiné et qui a vérifié. Écrivez-les une fois : tous les documents les lisent, et les cartouches des feuilles prennent ici qui a dessiné et qui a vérifié quand une feuille ne le dit pas. Chaque champ est enregistré quand vous le quittez.

![Informations du projet : le numéro, la phase, le maître d'ouvrage, le site et le concepteur](shots/project-info.webp)

Les mêmes champs se trouvent dans l'onglet **Informations du projet** de la boîte de dialogue Documents.

## Faire un document

**Gérer → Documents…** (ou le **+** à côté de **Documents** dans le navigateur du projet) ouvre les documents du projet. À gauche, les documents déjà faits et, sous **Nouveau à partir d'un modèle**, les modèles ; cliquez sur l'un d'eux et le nouveau document est créé et rempli aussitôt.

![Documents : les documents du projet et les modèles, le document sur ses pages, ses saisies et ce qui reste à compléter](shots/documents.webp)

Au centre, le document tel qu'il s'imprimera, page après page, avec sa table des matières, ses tableaux et ses chiffres. Un champ que le projet n'a pas encore est marqué là où il se trouve. Le navigateur du projet liste les documents sous **Documents** ; un clic ouvre la boîte de dialogue sur l'un d'eux.

## Saisies

L'onglet **Saisies** contient les textes que seul le concepteur peut écrire, groupés comme les chapitres du document. Dans les modèles roumains, chacun commence par une indication marquée **[De completat]** (à compléter) qui dit ce qu'il faut y mettre. Écrivez par-dessus : l'aperçu suit un instant après que vous avez cessé de taper. **Rétablir la valeur par défaut** remet le texte du modèle.

![Saisies : la structure écrite par-dessus l'indication du modèle](shots/documents-inputs.webp)

## À compléter

**À compléter** liste ce qui manque encore au document : un champ du projet laissé vide, une valeur que la maquette n'a pas. Cliquez sur l'un pour voir où il se trouve dans le document et aller au champ qui le remplit (un champ du projet ouvre l'onglet **Informations du projet**). La liste se vide au fur et à mesure.

![À compléter : les champs qui manquent encore au document](shots/documents-missing.webp)

## Mettre à jour, renommer, copier

- **Mettre à jour depuis le modèle** reprend le texte et la mise en page du modèle, pour un document fait avant que le modèle ne change. Vos saisies sont conservées.
- **Renommer**, **Dupliquer** et **Supprimer** agissent sur le document affiché. Une suppression est une seule étape d'annulation.

## Exporter

- **Exporter DOCX** et **Exporter ODT** écrivent un fichier que vous ouvrez et modifiez dans Word ou LibreOffice, avec ses titres comme styles propres au logiciel, sa table des matières et sa numérotation des pages comme champs, et les images du projet.
- **Exporter PDF** écrit le document mis en page par Lapin, prêt à imprimer ou à envoyer.

Un champ encore vide reste vide dans les fichiers : vérifiez donc **À compléter** avant d'exporter.

## Émettre des documents

**Gérer → Émettre…** emporte les documents avec les feuilles et la maquette (voir [Dossier du projet et émissions](cde.md)). Sous **Documents, chacun en PDF :** chaque document est coché d'emblée ; cochez **Aussi en DOCX** pour une copie modifiable en plus. Chaque fichier est nommé selon la convention ISO 19650 (`RP` pour une notice, `SP` pour un cahier des charges), son empreinte calculée et listé dans le bordereau d'envoi, et le document prend la révision et le statut de l'émission.

![Émettre : les documents avec les feuilles et la maquette](shots/issue-documents.webp)

## Les modèles

Lapin est livré avec trois modèles, en roumain pour l'instant :

- **Memoriu tehnic de arhitectură** (la notice technique d'architecture, selon la loi roumaine 50/1991) : les données générales des Informations du projet ; le nombre de niveaux (par exemple `P+1E`) et la hauteur maximale ; les surfaces — bâtie, développée, utile et du sous-sol — au total et par niveau ; les pièces de chaque niveau avec leurs surfaces ; les murs par type, avec leurs couches ; les planchers et les toitures ; la nomenclature des menuiseries ; les finitions des pièces ; des saisies pour l'existant, la fonction, la structure, les façades, les installations, la sécurité incendie, l'accessibilité, l'efficacité énergétique et l'installation de chantier.
- **Caiet de sarcini: tâmplărie și finisaje** (le cahier des charges menuiseries et finitions) : les matériaux du projet par catégorie, la nomenclature des menuiseries avec leurs quantités, les finitions pièce par pièce, et des saisies pour les généralités, les exigences, l'exécution et la réception, avec des textes par défaut à adapter.
- **Borderou de piese scrise și desenate** (le bordereau des pièces écrites et dessinées) : les documents et les feuilles du projet, avec leur numéro, titre, échelle et révision.

Un éditeur visuel pour vos propres modèles viendra plus tard.
