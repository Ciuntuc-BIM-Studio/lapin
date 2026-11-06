---
title: Orçamento
order: 98
summary: O orçamento a partir do modelo: as suas tabelas de preços importadas de CSV ou Excel, composições nos tipos e regras nos materiais, quantidades do levantamento vezes preços unitários discriminados em material, mão de obra, equipamentos e transporte, despesas indiretas, lucro e IVA, em RON ou EUR; os formulários romenos F3, F2 e C6 a C9 em pranchas, no Excel e em PDF; orçamentos IFC.
---

## A aba Execução

![A aba Execução](shots/ribbon-cost.webp)

Os grupos de custos estão na aba **Execução**, depois dos do cronograma (a aba se chamava **Quantitativos**, depois **Custos**). Eles mantêm os quantitativos e acrescentam o orçamento:

- **Levantamento de quantidades**, **Tabelas…** e **Exportar XLSX**: as quantidades do modelo (veja [Tabelas e gráficos](schedules.md)).
- **Tabelas de preços…**: os seus preços, importados de um arquivo CSV ou Excel (abaixo).
- **Atribuir composições…**: qual composição dá preço a cada tipo e a cada material (abaixo).
- **Orçamento…**: abre o orçamento sob a vista (ele é criado da primeira vez).
- **Formulário na prancha**, **Orçamento XLSX** e **Orçamento PDF**: o orçamento e os seus formulários em uma prancha, em uma pasta de trabalho ou em um PDF (abaixo).
- **Custo no tempo**: o orçamento colocado no cronograma da obra, a curva S e a tabela mensal (abaixo).

O orçamento é calculado a partir do modelo sempre que este muda. Uma parede que você desenha agora entra no orçamento na hora, com as suas quantidades. Só são guardados os preços, as composições dos tipos, as regras e as suas próprias linhas.

## Tabelas de preços

![Importar uma tabela de preços: o sentido de cada coluna deduzido do seu cabeçalho](shots/price-import.webp)

Os preços são seus: o Lapin não traz nenhum catálogo de preços nem de normas. **Execução → Tabelas de preços… → Importar…** lê um arquivo CSV ou XLSX, por exemplo uma exportação do seu programa de orçamentos ou a tabela de um fornecedor. O Lapin mostra as primeiras linhas do arquivo sob os seus cabeçalhos. Sobre cada coluna está o que o Lapin deduziu que ela contém, a partir do cabeçalho, em inglês ou em romeno:

- **Código** e **Descrição** (*Cod*, *Simbol*, *Denumire*), **Unidade** (*U.M.*: m³ ou mc, m² ou mp, m, pcs ou buc, kg, t, 100 m²).
- O preço unitário discriminado em **Material**, **Mão de obra**, **Equipamentos** e **Transporte** (*Material*, *Manoperă*, *Utilaj*, *Transport*), ou um único **Preço unitário (não discriminado)**, que então conta como material.
- Opcional: **Horas-homem por unidade** (*Normă de timp*) e **Ofício** (*Meserie*), para a lista de mão de obra (C6).
- Opcional: os insumos da composição, linhas que dizem a composição a que pertencem (**Composição a que pertence**), o seu **Tipo de insumo** (M, Man, U, T) e o seu **Consumo por unidade**. Uma composição dada só pelos seus insumos é orçada por eles, e as listas de insumos (C7 a C9) listam-nos um a um.

Altere qualquer coluna deduzida errado, escolha a linha em que estão os cabeçalhos e se os preços estão em RON ou em EUR, e clique em **Importar**. A tabela entra no projeto. Com **Manter também na biblioteca deste computador** marcado, ela também fica neste computador: em **Neste computador**, **Adicionar ao projeto** a leva para o seu próximo projeto.

**Modelo** salva um CSV com as colunas e algumas linhas com os valores indicativos do Lapin, marcados como *indicativo*: preencha-o com os seus preços e importe-o. **Usar os valores indicativos** coloca esses valores no projeto como uma tabela, para você experimentar o orçamento antes de ter os seus. Substitua-os antes de enviar um orçamento a alguém.

## Composições nos tipos, regras nos materiais

![Atribuir composições: cada tipo e material com a sua composição](shots/map-articles.webp)

Uma linha do orçamento vem de uma **composição**: um código da sua tabela de preços. Há duas maneiras de dar uma composição aos elementos:

- **Em um tipo.** Cada elemento do tipo é orçado por ela. A composição diz o que mede: o elemento inteiro ou uma parte dele (o **núcleo** de um tipo em camadas, ou os seus **acabamentos**), e por qual medida (pela unidade do preço, ou volume líquido ou bruto, área líquida ou bruta, comprimento, contagem, peso). Também pode acrescentar um fator e a perda (%).
- **Em um material (uma regra).** Cada camada desse material, em qualquer tipo, é orçada por ela: uma regra para o reboco de cal dá preço ao reboco de todas as suas paredes.

A composição do tipo vem primeiro. Uma camada que as composições do seu tipo já medem não é orçada de novo por uma regra. Uma composição sobre o elemento inteiro orça tudo, por isso as regras ficam de fora. Para uma parede em camadas, dê ao tipo uma composição sobre o seu **núcleo** (a alvenaria por volume) e regras para os seus acabamentos (o reboco por área). A parede é então orçada camada a camada: a alvenaria em **Paredes**, o reboco em **Acabamentos e isolamentos**.

**Execução → Atribuir composições…** lista os tipos do projeto usados pelos elementos e os materiais das suas camadas. Cada linha começa com a composição que tem, ou a que você escolheu para um tipo ou material com o mesmo nome em um projeto anterior, ou a que o Lapin sugere: um código escrito no seu nome, ou a composição que partilha mais palavras com ele (*zidărie*, *BCA*, *tencuială*, *C25/30*). Para um tipo em camadas, ele propõe o núcleo. Digite ou escolha um código, escolha a parte e a medida, marque as linhas que quer e clique em **Aplicar**: um só passo para desfazer. As suas escolhas são lembradas neste computador para os seus próximos projetos.

Os tipos de fôrma e de andaime do canteiro de obras (a aba **Implantação**) já tinham um código de composição (decisão 152): é uma composição no tipo, medida pela unidade do preço.

As fases contam: um elemento que já existia antes da obra (criado em uma fase *existente*) não é orçado. Um demolido é orçado só pela composição do seu tipo cuja parte é **A sua demolição (só quando demolido)**, no capítulo **Demolição**.

## O orçamento

![O orçamento: capítulos, linhas, as taxas e os totais](shots/estimate.webp)

**Execução → Orçamento…** abre o orçamento sob a vista, como o Gantt. As suas linhas são agrupadas em **capítulos** por tipo de serviço: movimento de terra, fundações, estrutura, paredes, cobertura, esquadrias, acabamentos e isolamentos, instalações, canteiro de obras, outros. Uma composição pode indicar um capítulo próprio. **Por pavimento e tipo de serviço** coloca o pavimento antes de cada capítulo.

Cada linha mostra o código, a descrição e a unidade da tabela de preços, a **quantidade** do modelo (a mesma do levantamento), o preço unitário discriminado em **Material**, **Mão de obra**, **Equipamentos** e **Transporte**, o preço unitário e o **valor**. Clique em uma linha para selecionar os seus elementos nas vistas. Selecione elementos em uma vista, e as suas linhas ficam marcadas no orçamento.

Sob as linhas estão os totais, como os orçamentos romenos os dispõem:

1. **Custos diretos**: a soma das linhas.
2. **Contribuições sobre a mão de obra (CAM)**: 2,25 % da mão de obra, por padrão.
3. **Despesas indiretas** (*cheltuieli indirecte*): 10 % dos custos diretos com as contribuições, por padrão.
4. **Lucro**: 5 % dos custos diretos e das despesas indiretas, por padrão.
5. **Total sem IVA**, o **IVA** (21 % por padrão) e o **Total com IVA**.

Altere qualquer taxa na barra de ferramentas. Cada valor é arredondado a duas casas decimais, como um orçamento o imprime.

O orçamento está em **RON** ou em **EUR**. **1 EUR =** é o câmbio do projeto: uma tabela de preços na outra moeda é convertida por ele, e a caixa ao lado mostra também cada valor na outra moeda.

A barra de ferramentas também avisa sobre o que não tem preço: **N tipo(s) sem preço** (clique para abrir Atribuir composições) e os códigos que nenhuma tabela de preços tem. **Uma linha sua** acrescenta um serviço que o modelo não tem (o tapume da obra, uma provisão): digite o seu código, descrição, unidade, quantidade e preços. Uma linha com código e sem preços os toma da tabela de preços.

## Os formulários romenos

![A lista de quantidades de serviços (F3) do mesmo orçamento](shots/estimate-f3.webp)

A lista acima do orçamento mostra o próprio orçamento ou um dos seus formulários. São agrupamentos do mesmo orçamento, por isso somam os mesmos totais. Têm cabeçalhos em romeno, como são entregues na Romênia:

- **Lista de quantidades de serviços (F3)**: *Lista cu cantitățile de lucrări pe categorii de lucrări*, capítulo a capítulo, os preços unitários e os valores discriminados em material, mão de obra, equipamentos e transporte, e os totais por baixo.
- **Orçamento por objeto (F2)**: *Deviz pe obiect*, o valor sem IVA de cada capítulo, o seu IVA e o seu valor com IVA.
- **Mão de obra (C6)**: as horas e o valor por ofício (do ofício e das horas-homem da tabela de preços, ou dos seus insumos de mão de obra).
- **Materiais (C7)**, **Equipamentos (C8)** e **Transporte (C9)**: os insumos com as suas quantidades, preços unitários e valores. Uma composição sem insumos conta como um insumo próprio.

Os formulários seguem a prática comum segundo a metodologia da Ordem 863/2008. Confira-os com o que o seu cliente ou o programa de financiamento pede: fale conosco se precisar de outro layout.

**Colocar na prancha** coloca a tabela mostrada em uma prancha nova, pronta para imprimir com as outras. **XLSX** salva o orçamento e cada formulário em uma pasta de trabalho, uma planilha cada. **PDF** salva-os como PDF, A4 paisagem, pronto para imprimir.

## Custo no tempo

![Custo no tempo: a curva S sobre as barras dos meses, a tabela mensal embaixo](shots/cost-time.webp)

Com um cronograma da obra no projeto (a aba **Execução**), o orçamento é colocado nele sem nenhum vínculo próprio. O valor de cada linha é dividido entre os seus elementos, e a parte de cada elemento vai para as tarefas que constroem a parte que a linha mede: a composição da alvenaria para a tarefa que pega o núcleo das paredes, a do reboco para a que pega os acabamentos. Uma composição no elemento inteiro, construída em duas tarefas, é dividida igualmente entre elas. Uma linha sua vai para a tarefa escolhida ao lado da descrição (**Em nenhuma tarefa** por padrão). O custo de uma tarefa é distribuído igualmente pelos seus dias úteis: nada é gasto em um fim de semana, em um feriado ou, para uma tarefa úmida, na pausa de inverno.

**Execução → Custo no tempo** abre, sob a vista:

- a **curva S**: o custo acumulado planejado ao longo do cronograma, o da **linha de base** (tracejado) quando há uma salva, e o **trabalho feito** (grosso) até a data de status: o custo de cada tarefa vezes o seu % concluído, distribuído desde o seu início real;
- as barras dos meses, na escala da direita: planejado e feito;
- acima, o **orçamento na conclusão** (os custos diretos do orçamento), o **custo até a data** e o que está **não programado**, se houver: linhas cujos elementos nenhuma tarefa constrói (um clique abre o orçamento);
- embaixo, a **tabela mensal**: o custo de cada capítulo por mês, o não programado e o seu total.

Os valores são custos diretos, como as linhas do orçamento. O Gantt mostra o custo de cada tarefa na coluna **Custo** (veja [Planejamento](planning.md)).

![A curva S em uma prancha](shots/cost-curve-sheet.webp)

**Colocar na prancha** coloca a curva em uma prancha nova, desenhada em vetores com a sua legenda. **Tabela mensal na prancha** coloca a tabela mensal, que é também o formulário **Custo por mês** na lista do orçamento: os capítulos mês a mês, depois os custos diretos, os totais sem IVA e com IVA (repartidos em proporção), os custos acumulados e, com progresso, o trabalho feito. Cada linha soma o total do orçamento. **XLSX** salva a curva dia a dia, a tabela mensal e os custos das tarefas em uma pasta de trabalho; **PDF** salva a curva e a tabela mensal.

**Gerenciar → Versões → Comparar** diz, ao lado dos elementos e do cronograma, o que mudou no orçamento: o total com IVA, os custos diretos e a diferença, cada capítulo cujo custo mudou, e as linhas adicionadas, removidas ou alteradas (quantidade e valor).

As medições mensais (*situații de lucrări*), o valor agregado e várias linhas de base são da edição premium.

## IFC

**Arquivo → Exportar IFC…** escreve o orçamento como um **IfcCostSchedule**: os seus capítulos e linhas como **IfcCostItem**, a quantidade de cada linha e os seus preços unitários como quatro **IfcCostValue** (Material, Mão de obra, Equipamentos, Transporte), os elementos que cada linha mede atribuídos a ela (**IfcRelAssignsToControl**), a moeda como a **IfcMonetaryUnit** do projeto. Importado, um orçamento mantém as linhas como o arquivo as tem, com os mesmos totais. Marque **Linhas do modelo** para acrescentar as linhas que o seu modelo dá.

Com um cronograma da obra, cada linha é atribuída também às tarefas em que é feita (**IfcRelAssignsToControl** para as suas **IfcTask**). Importadas, as linhas mantêm as suas tarefas, então a curva S volta igual.
