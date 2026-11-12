---
title: Cargas
order: 51
summary: Casos de carga, cargas de superfície, lineares e pontuais, as sobrecargas dos compartimentos, as cargas derivadas e as combinações da EN 1990, para uma análise estrutural.
---

O grupo **Cargas** do separador **Estrutura** tem as cargas que uma análise estrutural toma: **Casos de carga…**, **Carga de superfície**, **Carga linear** e **Carga pontual**. Os valores estão em kN, kN/m e kN/m².

![O grupo Cargas do separador Estrutura](shots/ribbon-loads.webp)

> Os valores que o Lapin traz (as sobrecargas da EN 1991-1-1 e os coeficientes da EN 1990, os recomendados) são **indicativos**. Ponha os valores do seu anexo nacional e verifique-os antes de usar os resultados.

## Casos de carga

**Casos de carga…** abre a caixa Cargas. **Adicionar casos padrão** cria G1 (peso próprio), G2 (acabamentos e divisórias), Q-A (sobrecarga, categoria A), S (neve) e W (vento).

![O separador Casos da caixa Cargas](shots/loads-cases.webp)

Um caso tem um **Código**, um **Nome**, um **Tipo** (Permanente, Variável ou Acidental), uma **Origem** (Peso próprio / carga permanente, Acabamentos, Sobrecarga, Neve, Vento, Sismo ou Definida pelo utilizador) e, num caso de sobrecarga, uma **Categoria**. Os três campos ψ0, ψ1 e ψ2 são os coeficientes de combinação; deixe-os vazios para tomar os da categoria na tabela. Um valor que não é um número de 0 a 1 fica marcado e não é guardado.

**Adicionar casos em falta** cria um caso de sobrecarga para cada categoria usada pelos compartimentos que não tem um.

## Colocar cargas

- **Carga de superfície**: clique nos cantos e prima <kbd>Enter</kbd>.
- **Carga linear**: clique nas suas duas extremidades.
- **Carga pontual**: clique onde atua.

Enquanto uma ferramenta está ativa, a barra da linha de comandos oferece o **valor** da carga e o **caso de carga** em que entra (por defeito **Sobrecarga (Q-A)**). A carga vai para o nível ativo.

![Uma carga de superfície de 2,5 kN/m² desenhada na sala](shots/loads-plan.webp)

## O que o Lapin calcula por si

Estas não as desenha; o Lapin deriva-as do modelo de cada vez que as pede, e acompanham cada alteração:

- O **peso próprio** de lajes, paredes, pilares e vigas, a partir das densidades dos seus materiais, no caso cuja origem é o peso próprio (G1).
- Os **acabamentos das lajes**, a partir das suas camadas, e as **paredes não resistentes**, como cargas lineares, no caso que lista acabamentos e divisórias (G2).
- Os **compartimentos**: a sobrecarga da categoria de um compartimento sobre a sua área, no caso dessa categoria (Q-A, Q-B…).

Um material sem densidade fica de fora, e o Resumo diz qual.

## A sobrecarga de um compartimento

Selecione um compartimento: as suas propriedades têm uma secção **Sobrecarga**. A sua **Categoria** é **Da função** (uma sala, um quarto ou uma cozinha é A, um escritório B, um arrumo E1, uma garagem F) a menos que escolha outra, ou **None**. Por baixo está o **qk** que a categoria dá.

![A sobrecarga de um compartimento nas suas propriedades](shots/loads-room.webp)

## A tabela das sobrecargas

O separador **Sobrecargas de utilização** lista cada categoria com o seu uso e o seu **qk** em kN/m², como recomenda a EN 1991-1-1. Escreva no campo o valor do seu anexo nacional; **Alterado** volta ao valor por defeito.

![O separador Sobrecargas de utilização](shots/loads-imposed.webp)

## Resumo

O separador **Resumo** mostra, para cada caso, as cargas que desenhou e o total em kN, no conjunto e em cada nível, e depois as cargas derivadas uma a uma, com a sua origem. Avisa dos compartimentos cuja categoria não tem caso e anota o que ficou de fora.

![O separador Resumo](shots/loads-summary.webp)

## Combinações

O separador **Combinações** lista as combinações dos casos, segundo a EN 1990 com os valores recomendados, indicativas: no estado limite último 1,35 nas ações permanentes e 1,5 na variável principal, as outras com o seu ψ0; no estado limite de serviço as combinações característica, frequente e quase permanente. Um caso que não contém nada não participa.

![O separador Combinações](shots/loads-combinations.webp)

## No ficheiro IFC

**Exportar IFC, Fragments ou BOS…** tem **Structural Analysis View (modelo analítico e cargas)**: o ficheiro leva então o modelo analítico das paredes resistentes, lajes, pilares e vigas, com os casos de carga, as cargas e as combinações, como um IFC Structural Analysis View. Ver [Importação e exportação](import-export.md).
