---
title: Análise estrutural
order: 52
summary: Execute uma análise estática linear do pórtico sob as suas cargas, leia os diagramas de esforços, momentos e deformada em 3D e defina as libertações nas extremidades das barras.
---

**Executar análise** do grupo **Cargas** do separador **Estrutura** (e da caixa Cargas) analisa as barras do modelo sob as cargas e combinações de [Cargas](loads.md) e mostra os esforços nelas.

![O grupo Cargas, com Executar análise](shots/ribbon-analysis.webp)

> Os resultados são **indicativos**. As cargas, as combinações e a análise devem ser verificadas por si antes de se basear nelas.

## O que é analisado

- As **barras** (pilares, vigas, elementos de aço e de madeira) entram com a sua própria rigidez, a partir do material e da secção. Uma barra cujo material não tem módulo de elasticidade é deixada de fora e listada.
- As **paredes resistentes** são apoios: seguram as barras que assentam nelas, mas não são analisadas como paredes.
- As **lajes** não suportam como placas; passam as suas cargas às barras e paredes por baixo pela **regra dos 45°**: as bissetrizes dos cantos dividem a laje nas áreas que cada lado suporta, e cada uma torna-se uma carga trapezoidal ou triangular na barra de baixo.
- As **coberturas com estrutura** passam a carga a cada asna pela sua **largura de influência**, até meio caminho dos vizinhos; a neve é tomada por projeção horizontal.
- O **peso próprio** das barras é tomado ao longo delas, e as cargas que desenhou e as derivadas vão para onde encontram algo por baixo. Uma carga que não encontra nada é deixada de fora e indicada.

## Execução

Escolha **Executar análise**. Abre-se a caixa **Resultados da análise**:

![A caixa Resultados da análise](shots/analysis-results.webp)

- O resumo: as barras, nós e incógnitas do cálculo e quanto demorou.
- Para cada caso, a **soma das suas cargas** face à **soma das reações**, e se **equilibram** (um visto) ou não (uma cruz: não se baseie nesses resultados). As cargas não atribuídas são dadas em kN.
- O que foi **deixado de fora** da análise, e porquê.
- Os valores **máximos e mínimos** de esforços e momentos, o deslocamento e a flecha máximos (com a relação ao vão, L/300) do caso, combinação ou envoltória escolhidos.

**Mostrar os diagramas** fecha a caixa e mostra os resultados na vista 3D.

## Os diagramas em 3D

Um cartão sobre a vista 3D escolhe o que é desenhado:

![O momento fletor da envoltória no estado limite último](shots/analysis-moments.webp)

- **O quê**: um caso de carga, uma combinação ou a **envoltória** das combinações no estado limite último (ELU) ou de serviço (ELS): o valor máximo e mínimo em cada ponto.
- **Grandeza**: **N** esforço axial (tração positiva), **Vy** e **Vz** esforços transversos, **T** torção, **My** e **Mz** momentos fletores (**Mz** é o eixo forte de uma viga horizontal, positivo com tração em baixo) ou a **Deformada**.
- **Escala**: o tamanho do diagrama no ecrã; o modelo em si não muda.
- **Reações**: setas nos apoios com os seus valores em kN.

Cada barra mostra a sua linha de base, a curva, tracejados entre elas e os valores nos extremos; o vermelho é positivo, o azul negativo. Os momentos são desenhados do lado em que a barra está tracionada.

![A forma deformada](shots/analysis-deformed.webp)

## Libertações nas extremidades

Selecione um pilar, uma viga ou um elemento: as suas propriedades têm uma secção **Análise** com as libertações **Início (i)** e **Fim (j)**.

![A secção Análise de uma viga](shots/analysis-bar.webp)

- **Predefinido**: o que a ligação diz (uma chapa, uma cartela ou um contraventamento articula o elemento), um elemento de madeira da cobertura articulado no seu eixo forte, qualquer outra barra encastrada.
- **Encastrado**: a extremidade transmite momentos. **Articulado**: roda livremente em torno dos dois eixos de flexão.
- **Personalizado**: seis caixas, uma por grau de liberdade (deslocamento ao longo da barra, ao longo de cada eixo de corte, torção, rotação em torno de cada eixo de flexão); um grau marcado fica libertado.

Uma alteração é um só passo de anulação.

## Quando o modelo muda

Os resultados são calculados a partir do modelo tal como está e nunca são guardados. Quando muda o modelo, os diagramas saem da vista e o cartão di-lo:

![O cartão após uma alteração](shots/analysis-pick.webp)

**Executar de novo** analisa o novo modelo.

## Um mecanismo

Se a estrutura se pode mover sem resistência (libertações a mais, ou uma parte que nada segura), a análise para e diz onde:

![Um mecanismo](shots/analysis-mechanism.webp)

Nomeia o nó, o grau de liberdade e as barras em causa; **Selecionar os elementos envolvidos** seleciona-os para que os possa corrigir, normalmente encastrando uma extremidade ou acrescentando um apoio.
