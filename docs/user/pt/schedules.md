---
title: Tabelas e gráficos
order: 95
summary: Liste portas, janelas, vergas, ambientes e tudo o que um módulo conta; adicione linhas e colunas suas, digite por cima dos valores do modelo e faça gráficos da tabela.
---

## Obter quantidades

Tudo o que o Lapin modela é medido pela sua geometria: o comprimento, a altura, a área lateral bruta e líquida e o volume de uma parede, a área e o volume de cada camada, as áreas e o volume de uma laje, a área e o perímetro de um ambiente, o número de portas. Há três caminhos:

- **Um elemento.** Selecione-o: a secção **Quantitativos** do painel de propriedades mostra o que ele mede, e depois cada camada ou parte.
- **Todo o modelo.** **Custos → Levantamento de quantidades** cria (ou abre) o levantamento: todos os elementos que o modelo mede, somados linha a linha, com a contagem, o comprimento, a área bruta e líquida, o volume bruto e líquido e o peso. A sua barra escolhe o que é somado junto: **Por categoria e tipo** (o padrão: as paredes de cada tipo de parede, as lajes de cada tipo…), **Por material** (cada camada sob o seu material: o tijolo, o reboco e o isolamento das paredes à parte, o concreto das vergas), por pavimento, ou combinados. **Todos os elementos** escolhe quais categorias ele lista (só paredes e lajes, por exemplo). O levantamento acompanha o modelo como qualquer tabela e aceita linhas e colunas próprias (uma verba, um preço unitário).
- **Qualquer outra lista.** **Custos → Tabelas…** cria qualquer tabela que a edição oferece: ambientes, acabamentos, esquadrias, composições de tipos, madeira, armaduras, fôrmas, aço… A mesma lista está em **+** ao lado de **Tabelas** no navegador do projeto.

![O levantamento agrupado por categoria e material](shots/quantity-takeoff.webp)

Por material, um material medido por **área** (ver [Materiais](materials.md)) tem uma linha para cada espessura em que é aplicado, com uma coluna **Espessura (mm)**: *Lã mineral 150 mm: 84,20 m²*, *Lã mineral 200 mm: 31,00 m²*. Um medido por volume tem só os seus m³, por massa os seus kg, por peças as suas **Peças**. Uma coluna **Nota** indica quando uma camada não tem uma espessura do catálogo do material. O orçamento faz o mesmo: um artigo sobre um material mede-se como o material diz, uma linha por espessura. Um material sem regra mede-se como antes.

**Custos → Exportar XLSX** guarda logo o levantamento como pasta de trabalho (num navegador é descarregado).

## Quantidades numa prancha

Uma tabela vai para uma prancha como uma vista e é desenhada lá com todas as suas linhas e valores, totais e gráficos:

- **Colocar na prancha…** na barra da tabela: **Nova prancha** cria uma prancha A3 com a tabela e abre-a, ou escolha uma prancha que já tem.
- Ou arraste a tabela do navegador do projeto para uma prancha aberta: fica onde a largar. Plantas, cortes e fachadas arrastam-se do mesmo modo.
- Ou **Posicionar vista…** na barra da própria prancha.

Na prancha, arraste a tabela para movê-la; uma tabela mais longa do que a moldura continua em outras páginas (a prancha avisa, e **colocá-la numa nova prancha** continua-a). A tabela acompanha o modelo: acrescente uma parede e os números da prancha mudam. Imprime-se com **Imprimir** e vai para o PDF e o DXF da prancha exatamente como está desenhada.

![O levantamento colocado numa nova prancha](shots/takeoff-sheet.webp)

## Criar uma tabela

Uma tabela é um quadro que o modelo preenche: portas e janelas, vergas, ambientes, armaduras, madeira, fôrmas e mais. Clique em **+** ao lado de **Tabelas** no navegador de projeto e escolha o que ela lista, ou use a ferramenta de um módulo (Madeira → Tabela de madeira, Obras → Tabela de fôrmas…). Dê um clique duplo nela no navegador para abri-la em uma guia. Uma tabela acompanha o modelo: mova uma parede, adicione uma janela, e o quadro muda junto.

As tabelas de **Esquadrias**, de **portas**, de **janelas** e de **vergas** mostram um cartão para cada tipo, com o desenho dele (a vista interna por padrão) e os valores linha por linha. As outras tabelas são um quadro simples, uma linha por item e uma linha de totais no fim.

Uma porta ou janela invertida para a outra mão do seu tipo (**Mão**, ou uma única seta de inversão; invertida nos dois sentidos, ela está apenas girada) é a imagem espelhada do seu tipo e ganha um cartão próprio: a marca termina em **-M** (D-M), o nome em "(mirrored)", e a linha **Opening**, o desenho e as contagens são os da outra mão. Inverta-a de volta e ela volta ao cartão do seu tipo. A linha **Opening** de uma porta nomeia a abertura como a planta a mostra: olhando da maçaneta para as dobradiças, uma folha que abre à esquerda é *swing left*, uma que abre à direita *swing right*; um par de folhas é *double swing*. A esquerda e a direita de uma janela são vistas pelo lado de dentro, o lado para onde abrem as folhas: *De abrir à direita (dobradiças à esquerda)* no configurador, cuja grade é vista pelo lado de fora. O IFC escreve a mesma janela como esquerda. Os desenhos mostram cada porta como está no modelo, vista pelo lado de dentro, o lado para onde ela abre. Uma nota abaixo dos cartões diz isso.

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

Quando só interessa o resultado, clique em **Editor da tabela** por cima da tabela de edição sob o mapa: recolhe-se numa barra estreita no fundo da vista e o mapa ocupa a vista toda. Um clique na barra volta a abri-lo. O Lapin lembra-se da escolha neste computador. A seta no fim das ferramentas rápidas também as oculta nas folhas e nos mapas (ver [A interface](interface.md)).

![Só o mapa: o editor da tabela recolhido na sua barra, as ferramentas rápidas ocultas](shots/schedule-result-only.webp)

## Molduras de parede

A tabela **Molduras de parede** (**Arquitetura → Tabela de molduras de parede**, ou **Custos → Tabelas…** → **Molduras de parede**) soma as molduras e frisos de parede do projeto por perfil, tipo de peça e material: quantas há, as suas peças (uma moldura cortada por vãos são várias), o seu comprimento ao longo das faces e o seu volume; num friso, o volume que tira à parede. Um total para as molduras e outro para os frisos fecham a tabela. A sua barra soma-as primeiro **por perfil**, **por piso** ou **por tipo de parede**. Os retornos contam no volume, não no comprimento.

## Exportar

**CSV** e **XLSX** na barra exportam a tabela como folha de cálculo (um XLSX tem uma folha por tabela). Na aplicação de computador, o Lapin pergunta onde guardar o ficheiro e propõe o nome da tabela; num navegador o ficheiro é transferido. Os tipos de parede e de laje exportados guardam-se da mesma forma.
