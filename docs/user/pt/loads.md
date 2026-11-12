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
- A **neve** e o **vento**, assim que indica os valores do local (veja [Neve e vento](#neve-e-vento)).

Um material sem densidade fica de fora, e o Resumo diz qual.

## A sobrecarga de um compartimento

Selecione um compartimento: as suas propriedades têm uma secção **Sobrecarga**. A sua **Categoria** é **Da função** (uma sala, um quarto ou uma cozinha é A, um escritório B, um arrumo E1, uma garagem F) a menos que escolha outra, ou **None**. Por baixo está o **qk** que a categoria dá.

![A sobrecarga de um compartimento nas suas propriedades](shots/loads-room.webp)

## A tabela das sobrecargas

O separador **Sobrecargas de utilização** lista cada categoria com o seu uso e o seu **qk** em kN/m², como recomenda a EN 1991-1-1. Escreva no campo o valor do seu anexo nacional; **Alterado** volta ao valor por defeito.

![O separador Sobrecargas de utilização](shots/loads-imposed.webp)

## Neve e vento

O separador **Neve e vento** guarda o clima do local. O Lapin não traz os mapas nacionais: leia os valores do seu local na **CR 1-1-3/2012** (neve, a sua figura 3.1 ou anexo A) e na **CR 1-1-4/2012** (vento, a sua figura 2.1 ou anexo A), ou no anexo nacional por que trabalha, e escreva-os.

![O separador Neve e vento](shots/loads-climate.webp)

- **Neve**: **Carga de neve no solo s0,k (kN/m²)**, **Exposição Ce** (total 0,8, parcial 1,0, abrigada 1,2), **Coeficiente térmico Ct** (1,0, salvo se a cobertura for envidraçada e perder calor) e **Coeficiente de importância γIs** (1,0 para edifícios correntes). **Aplicar a neve**.
- **Vento**: **Pressão de referência do vento qb (kN/m²)**, **Categoria de terreno** (de 0 a IV, do mar ao centro de uma cidade) e **Coeficiente de importância γIw**. **Aplicar o vento**.

Ao aplicá-los criam-se os casos em que as suas cargas entram:

- **S**, a neve sem acumulação, e **S+X**, **S-X**, **S+Y**, **S-Y**, a neve acumulada por um vento que sopra para cada lado: numa cobertura de duas águas, a vertente virada ao vento leva metade. Uma acumulação que aqui não muda nada (nenhuma vertente está virada a esse vento) fica de fora, e o Resumo di-lo.
- **W+X/s**, **W+X/p**, e assim para as quatro direções: o vento que sopra para +X, −X, +Y, −Y, uma vez com a **sucção** (/s) das coberturas e outra com a sua **pressão** (/p).

As disposições da neve e as direções do vento são alternativas: uma combinação toma só uma de cada.

O que o Lapin calcula a partir deles, de cada vez que o pede:

- **Neve** em cada vertente de cobertura, por m² em planta: s = γIs · μ1 · Ce · Ct · s0,k, com μ1 = 0,8 até 30°, depois menos, 0 aos 60° e mais inclinado; numa laje que nada cobre (um terraço, uma cobertura plana) μ1 = 0,8.
- **Vento** nas paredes e coberturas: o contorno do edifício é o invólucro convexo das suas paredes e pilares; as fachadas tomam as zonas A a E, as coberturas planas F a I, uma cobertura de duas águas F a J, pelos coeficientes da EN 1991-1-4, à pressão da altura do edifício para a categoria de terreno. Uma fachada passa o seu vento às vigas, travessas e bordos de laje ao longo dela, a sua faixa mais baixa ao solo; uma cobertura às suas asnas ou madres. As coberturas de quatro águas, a pressão interior e as acumulações de neve nas platibandas ainda não são derivadas.

> Estes valores são **indicativos**: os coeficientes são os recomendados pelas normas. Confirme-os com o seu anexo nacional.

## Resumo

O separador **Resumo** mostra, para cada caso, as cargas que desenhou e o total em kN, no conjunto e em cada nível, e depois as cargas derivadas uma a uma, com a sua origem. Avisa dos compartimentos cuja categoria não tem caso e anota o que ficou de fora.

![O separador Resumo](shots/loads-summary.webp)

## Combinações

O separador **Combinações** lista as combinações dos casos, segundo a EN 1990 com os valores recomendados, indicativas: no estado limite último 1,35 nas ações permanentes e 1,5 na variável principal, as outras com o seu ψ0; no estado limite de serviço as combinações característica, frequente e quase permanente. Um caso que não contém nada não participa.

![O separador Combinações](shots/loads-combinations.webp)

## Analisar

**Executar análise** (no mesmo grupo e na caixa Cargas) analisa o pórtico sob estas cargas: veja [Análise estrutural](analysis.md).

## No ficheiro IFC

**Exportar IFC, Fragments ou BOS…** tem **Structural Analysis View (modelo analítico e cargas)**: o ficheiro leva então o modelo analítico das paredes resistentes, lajes, pilares e vigas, com os casos de carga, as cargas e as combinações, como um IFC Structural Analysis View. Ver [Importação e exportação](import-export.md).
