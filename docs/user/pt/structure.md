---
title: Estrutura
order: 50
summary: Pilares, vigas e fundações, os seus tipos e como eles se encontram.
---

A guia **Estrutura** reúne a estrutura e as fundações.

![A guia Estrutura](shots/ribbon-structure.webp)

## Estrutura

- **Pilar** (<kbd>CL</kbd>): clique onde ele fica. Um pilar vai do seu nível até o nível de cima.
- **Viga** (<kbd>BM</kbd>): clique nas duas pontas. **Viga curva** (<kbd>BC</kbd>) segue um arco.
- **Pilares na grade** coloca um pilar em cada cruzamento dos eixos da malha. Para desenhar os eixos, use **Anotar → Eixo de grade** (<kbd>GR</kbd>).

## Fundações

- **Sapata corrida** (<kbd>SF</kbd>) passa sob as paredes. As suas camadas externas são cortadas em meia-esquadria nos cantos.
- **Sapata corrida curva** (<kbd>CF</kbd>) segue um arco.
- **Sapata isolada** (<kbd>PF</kbd>) fica sob um pilar.

Desenhe as fundações no nível **Fundações**, ou dê a elas uma planta própria.

![Pilares, vigas e sapatas isoladas em 3D](shots/structure-3d.webp)

## Tipos e cantos

**Tipos estruturais** define as seções de pilares, vigas e sapatas: seções de concreto e perfis de aço. Clique num tipo da lista para mudar o seu tamanho ou o seu nome e depois em **Salvar tipo**: todos os elementos desse tipo acompanham; **Novo tipo** volta a criar um.

Vigas e sapatas se unem sozinhas onde se encontram, um canto em meia-esquadria. Uma viga desenhada de eixo de pilar a eixo de pilar para nas faces dos pilares; o seu comprimento, volume e estribos contam o vão livre entre eles. **Meia-esquadria**, **Topo** e **Inverter** mudam como duas vigas ou duas sapatas se encontram. **Corresponder tipo** (<kbd>MA</kbd>) copia o tipo de um elemento para outros, e **Criar similar** (<kbd>CS</kbd>) começa a desenhar outro elemento do mesmo tipo.

## Uma vista estrutural

A disciplina de uma vista decide o que ela mostra. Uma vista de **Estrutura** mostra só os núcleos portantes, sem os acabamentos. Você escolhe a disciplina nas propriedades da vista. Veja [Vistas](views.md).
