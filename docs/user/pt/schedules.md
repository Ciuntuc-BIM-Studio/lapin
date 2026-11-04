---
title: Tabelas e gráficos
order: 95
summary: Liste portas, janelas, vergas, ambientes e tudo o que um módulo conta; adicione linhas e colunas suas, digite por cima dos valores do modelo e faça gráficos da tabela.
---

## Criar uma tabela

Uma tabela é um quadro que o modelo preenche: portas e janelas, vergas, ambientes, armaduras, madeira, fôrmas e mais. Clique em **+** ao lado de **Tabelas** no navegador de projeto e escolha o que ela lista, ou use a ferramenta de um módulo (Madeira → Tabela de madeira, Obras → Tabela de fôrmas…). Dê um clique duplo nela no navegador para abri-la em uma guia. Uma tabela acompanha o modelo: mova uma parede, adicione uma janela, e o quadro muda junto.

As tabelas de **Esquadrias**, de **portas**, de **janelas** e de **vergas** mostram um cartão para cada tipo, com o desenho dele (a vista interna por padrão) e os valores linha por linha. As outras tabelas são um quadro simples, uma linha por item e uma linha de totais no fim.

![Uma tabela de esquadrias com as linhas de resistência ao fogo e de fabricante](shots/joinery-schedule.webp)

## Resistência ao fogo, fabricantes e outros parâmetros

Os parâmetros de uma porta ou de uma janela entram sozinhos na tabela de esquadrias:

- **Resistência ao fogo.** A linha **Resistência ao fogo** lê um parâmetro chamado Fire rating, Fire Rated, FireRating ou Fire resistance. Coloque-o no tipo, assim todas as portas do tipo o têm. Em **Arquitetura → Portas e janelas**, **Parâmetros · nas tabelas** → **+ Resistência ao fogo** adiciona o FireRating do tipo ao Pset_DoorCommon (Pset_WindowCommon para uma janela), que é onde o IFC o guarda. Um parâmetro que você mesmo adiciona a uma porta, nas propriedades dela, também aparece.
- **Qualquer outro parâmetro.** **+ Fabricante**, **+ Código do produto**, **+ Site**, ou um parâmetro seu, vira uma linha própria em todas as tabelas de esquadrias. O valor próprio de uma porta aparece ao lado do valor do tipo dela, quando as portas de um tipo diferem.
- **Digitado na tabela.** Um valor digitado na linha de um parâmetro (em itálico) vai para o tipo, e todas as portas do tipo acompanham. Esvazie-o para tirar o parâmetro do tipo.

![Os parâmetros do tipo no configurador de portas e janelas](shots/joinery-params.webp)

**Tipos não usados.** Um tipo que nenhuma porta ou janela do modelo usa fica fora da tabela. **Tipos não usados** na barra de ferramentas da tabela os mostra de novo, com quantos são.

## Linhas e colunas suas

Toda tabela pode ser preenchida à mão, como uma planilha:

- **Linha** adiciona uma linha sua. Em uma tabela de esquadrias ela tem um rótulo, e você digita o valor dela para cada tipo (Fornecedor, Data de entrega…). Em qualquer outra tabela ela é uma linha inteira, digitada célula por célula (um terraço não modelado, uma provisão); ela fica acima da linha de totais.
- **Pilar** adiciona uma coluna sua. Em uma tabela de esquadrias ela é um cartão sem tipo por trás, com todos os valores digitados (um portão de garagem encomendado à parte). Em qualquer outra tabela ela é uma coluna depois das do modelo (Acabamento de piso, Observações…).
- **Qualquer célula** do quadro sob o desenho pode ser digitada por cima. Um valor digitado por cima do do modelo fica marcado; **↺** devolve o valor do modelo. O que você digita fica com a sua linha: as observações de um ambiente acompanham o ambiente quando a lista é ordenada ou o ambiente é renomeado. Em uma tabela de **Ambientes**, um número, nome, função, departamento, acabamento de piso ou altura digitado na linha de um ambiente vai para o próprio ambiente, como se fosse digitado nas propriedades dele; o carimbo na planta e todas as outras tabelas acompanham.
- **×** ao lado de uma linha ou coluna sua a remove, com o que foi digitado nela.
- As setas no cabeçalho de um quadro **ordenam** as linhas por aquela coluna: uma vez em ordem crescente, de novo em ordem decrescente, uma terceira vez na ordem do modelo. A linha de totais continua por último.

Tudo isso sai impresso: em uma prancha, no PDF e no DXF, e nas exportações CSV e XLSX.

## Gráficos

**Gráficos** na barra de ferramentas da tabela abre os gráficos ao lado do quadro. **Novo gráfico** cria um a partir do quadro:

- **Pilares**, **Barras**, **Pizza** ou **Linha**.
- **Categorias de**: a coluna que dá nome a cada barra ou fatia (Função, Nome, Marca, Pavimento…).
- **Como**: **Soma**, **Média**, **Mínimo** ou **Máximo** de uma coluna (Área, Total, Volume…), ou **Número de linhas**.
- **No máximo**: mantém só as maiores categorias e soma o resto como Outros.
- **Título**: um título próprio, ou o que ele cria (Soma de Área por Função).

O motor desenha cada gráfico depois dos quadros, assim ele aparece na tabela, em uma prancha que coloca a tabela e no PDF, exatamente igual. Ele acompanha o modelo e o que você digitou: mude a área de um ambiente ou adicione uma linha sua, e o gráfico muda junto. **✎** altera um gráfico, **×** o remove.

![Uma tabela de ambientes com uma linha própria e um gráfico de pizza das áreas por função](shots/schedule-chart.webp)

## Composições dos tipos

A tabela **Composições dos tipos** (**+** → **Composições dos tipos**) lista cada tipo de parede e de laje: a sua marca, a espessura total, quantos elementos o usam e depois as suas camadas em ordem, com materiais e espessuras.

**Desenhos das composições**, na sua barra, desenha cada tipo após a tabela, cortado pelas suas camadas à escala da tabela (1:50 se não escolher outra): uma parede como a corta uma planta, uma laje como a corta um corte, a camada 1 em cima. Cada camada é preenchida e hachurada como o seu material nos cortes, as faces do núcleo são desenhadas mais grossas, a espessura total é cotada e cada camada é nomeada ao lado como a tabela a nomeia. Os desenhos imprimem-se com a tabela, numa folha e no PDF. Uma nova tabela de composições desenha-os; desmarque **Desenhos das composições** para ficar só com a tabela.

Os desenhos **Em cada ficha** (vista interior, vista exterior, planta, corte), nas **Configurações** de uma tabela, pertencem às fichas das tabelas de caixilharia; uma tabela simples não tem fichas, por isso não são oferecidos aí.

![A tabela das composições dos tipos com a composição de cada tipo](shots/type-assemblies.webp)

## Exportar

**CSV** e **XLSX** na barra exportam a tabela como folha de cálculo (um XLSX tem uma folha por tabela). Na aplicação de computador, o Lapin pergunta onde guardar o ficheiro e propõe o nome da tabela; num navegador o ficheiro é transferido. Os tipos de parede e de laje exportados guardam-se da mesma forma.
