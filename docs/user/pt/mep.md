---
title: Sistemas de instalações
order: 57
summary: Desenhe sistemas de aquecimento, água, esgoto, elétrica e ventilação como esquemas na planta, com fontes, pontos terminais e os trechos entre eles, e exporte-os como uma rede IFC.
---

A guia **Instalações** desenha as instalações do edifício como um engenheiro as esboça: um sistema por instalação, a sua **fonte** (uma caldeira, um hidrômetro, um tubo de queda, um quadro de distribuição, um ventilador), os seus **pontos terminais** (radiadores, lavatórios, bacias sanitárias, tomadas, luminárias, difusores) e os **trechos** entre eles. Cada nó é um elemento IFC real com as suas portas de conexão; cada trecho é um segmento de tubo, duto ou cabo; as conexões se mantêm quando você move as coisas.

![A guia Instalações](shots/ribbon-mep.webp)

## Um sistema em um clique

Em **Novo sistema**, escolha a instalação (**Aquecimento**, **Água**, **Água quente**, **Esgoto**, **Elétrica**, **Ventilação**) e clique na planta. O Lapin coloca o sistema inteiro: a fonte onde você clicou, um primeiro ponto terminal a alguns metros e o trecho entre eles. O sistema abre para edição.

![Um sistema de aquecimento na planta do térreo: a caldeira, dois radiadores sob as janelas e os trechos entre eles](shots/mep-plan.webp)

No modo de edição, tudo o que não é o sistema fica esmaecido, e só os nós e trechos do sistema podem ser escolhidos. Arraste a fonte e o ponto terminal para onde eles vão: o trecho deles acompanha. Adicione pontos terminais com **+ Radiador**, **+ Lavatório**… no painel Propriedades, ou com as ferramentas em **Aquecimento**, **Hidráulica**, **Elétrica** e **Ar**: cada novo ponto terminal entra no sistema por um trecho próprio, a partir da porta livre mais próxima da fonte ou do trecho mais próximo (um tê). **Concluído** (ou <kbd>Esc</kbd>) sai do modo de edição e informa quantas curvas e tês o sistema forma e o que ainda está aberto. **Editar sistema** (<kbd>ES</kbd>) o abre de novo.

## Trechos

**Trecho** (<kbd>RN</kbd>) desenha um trecho ponto a ponto; pressione <kbd>Enter</kbd> para terminar. Uma ponta que cai em uma porta aberta se conecta a ela; uma ponta que cai em um trecho do mesmo sistema faz uma derivação dele.

**Conectar** liga a ponta de um trecho a um nó ou a outro trecho; **Desconectar** a solta. **Conectores livres** seleciona tudo o que ainda não está conectado.

## Remodelar um trecho

Selecione um trecho: as alças dele ficam sobre a linha como ela está desenhada, mesmo depois de você mover os nós que ele conecta.

- **Arraste uma alça** para mover aquele canto livremente.
- **Arraste o meio de um segmento** para deslizá-lo para o lado; os segmentos vizinhos se esticam, assim um percurso em ângulo reto continua em ângulo reto. Deslize o primeiro ou o último segmento e o Lapin acrescenta um desvio ali, para que a ponta continue na sua porta.
- **Ctrl-clique** em uma alça para retirá-la, ou no meio de um segmento para adicionar uma; **Adicionar alça** coloca uma onde você clica.
- **Desvio** (<kbd>DU</kbd>) leva um trecho em volta de algo no caminho: clique no trecho onde o desvio começa, clique onde ele termina no mesmo segmento, depois vá para o lado para onde ele vai e clique (ou digite a distância). O trecho sai, segue ao lado e volta, com quatro cantos novos; ele mantém as conexões.
- **Redesenhar trajetória** (<kbd>RP</kbd>) dá um novo percurso a um trecho: selecione-o (ou comece nele) e desenhe o percurso ponto a ponto, <kbd>Enter</kbd> para terminar. O trecho mantém o sistema, a dimensão e as conexões, e as pontas voltam para as suas portas.

![O trecho de um radiador levado em volta de um obstáculo com Contornar: sai, segue ao lado e volta](shots/mep-detour.webp)


## Nós

Cada nó tem uma **função**: caldeira, radiador, coletor, hidrômetro, lavatório, pia, bacia sanitária, chuveiro, banheira, tubo de queda, peça de inspeção, quadro de distribuição, tomada, luminária, interruptor, ventilador, difusor, grelha de exaustão, válvula, subida/descida, tampão. A função dá ao nó a classe IFC, as portas, a dimensão e o símbolo na planta. Uma válvula, um medidor ou um interruptor solto sobre um trecho o divide e fica no meio. Uma **subida/descida** leva um trecho para outra cota ou para o pavimento de cima.

![As propriedades de um radiador: o sistema, a função e as portas, a dimensão e a potência](shots/mep-radiator-properties.webp)

No painel Propriedades, um nó mostra o sistema, a função e as portas (abertas ou conectadas); um trecho mostra as duas pontas. Os dois usam os padrões do sistema, a menos que você defina um valor neles: dimensão, cota, o plano em que correm (piso, teto, parede), em par ou simples, material, declividade.

## Sistemas

**Sistemas…** lista os sistemas do projeto e os padrões do sistema atual: a dimensão, o plano e as cotas de cada plano, a declividade (esgoto), se os trechos são desenhados em par (aquecimento: ida e retorno) e com que afastamento, o material e o isolamento. Mude um padrão e todos os nós e trechos sem valor próprio o acompanham.

![A caixa de diálogo Sistemas](shots/mep-systems.webp)

## Plantas, listas e IFC

**Planta de instalações** cria uma planta da disciplina de instalações para o nível, arquivada no conjunto Instalações do navegador. A **Lista de equipamentos** e a **Lista de trechos** em **Sistema** são tabelas como as outras: vão para as pranchas e são exportadas com elas.

Em uma exportação IFC, cada sistema é um IfcDistributionSystem, cada nó e trecho tem as suas IfcDistributionPorts, e as conexões são gravadas entre as portas, assim a rede chega a outro programa conectada da fonte até o último ponto terminal.

## Gás e óleo combustível

**Gás** e **Óleo combustível** são serviços próprios em **Novo sistema**. Um sistema de gás diz seu gás (**Gás natural** ou **GLP**) e sua **Classe de pressão** (baixa dentro da casa, média na rua) em **Sistemas…**; seus tubos são por padrão de aço preto nas paredes a 2,1 m, e podem ser de cobre, multicamada ou **PE 100** para o que fica enterrado. O óleo corre em cobre.

A instalação de gás de uma casa começa na **caixa do medidor** na fachada. Clique em **Novo sistema → Gás**: a caixa vai onde você clica e uma **caldeira a gás** dentro, ligadas por um trecho. No caminho, as ferramentas de **Gás** colocam no trecho o **regulador de gás**, o **medidor de gás**, o **registro geral de gás**, os **registros dos aparelhos** e os **conectores flexíveis**, que o dividem como qualquer conexão; o **fogão a gás** e o **aquecedor a gás** se ligam à instalação por um trecho próprio. Onde um tubo de gás ou de óleo atravessa uma parede, ele passa por uma **luva de proteção**, desenhada e contada com o trecho. Uma caldeira a gás desenhada no sistema de aquecimento como sua fonte se liga à instalação de gás pelas suas propriedades (o botão **+** com o nome do sistema de gás).

![A instalação de gás de uma casa: a caixa, o regulador e o medidor do lado de fora, a caldeira e o fogão na cozinha](shots/mep-gas-plan.webp)

Cada aparelho guarda seu **consumo** (m³/h) e seu **tipo de aparelho** pela exaustão (A sem chaminé, B com chaminé aberta, C estanque), para as listas e o dimensionamento.

**Óleo combustível** coloca um **tanque de óleo** (interno, externo ou enterrado) e uma **caldeira a óleo**; o **bocal de abastecimento** e o **terminal de respiro** se ligam ao abastecimento e ao respiro do tanque, e o **filtro de óleo** fica na ida.

### O abastecimento na rua

Em **Rua**, **Rede de gás ao longo de…** (<kbd>GM</kbd>) assenta uma rede de gás ao longo do que você clica — uma rua, um alinhamento, uma linha desenhada, uma parede — na **distância à linha** e na **profundidade (recobrimento)** da barra da linha de comando; sob um terreno a profundidade é medida a partir do solo, senão a partir do nível. **Rede de gás** a desenha ponto a ponto. A rede vai num sistema de gás de média pressão, em tubo de PE, com sua **fita de advertência**.

**Ramal de ligação** (<kbd>GS</kbd>): clique na rede, depois na caixa. O Lapin coloca um **tê de derivação** na rede, assenta o tubo do ramal em esquadro a partir dela na sua profundidade até a caixa, e sobe ali para dentro da caixa.

![O ramal de ligação da rede da rua até a caixa do medidor, selecionado](shots/mep-gas-street.webp)

![A rede e o ramal debaixo da terra, a instalação na fachada](shots/mep-gas-3d.webp)

![As propriedades de um ramal: do tê de derivação à caixa, seu recobrimento e diâmetro](shots/mep-gas-service-properties.webp)

![Os padrões de um sistema de gás: seu gás e sua classe de pressão](shots/mep-gas-systems.webp)

**Quantitativo de gás e óleo** em **Sistema** lista, sistema por sistema, os tubos por material e diâmetro com seu comprimento (e dele as prumadas), as curvas, tês e luvas, a fita de advertência, os registros, medidores e caixas, e os aparelhos com consumo e potência instalados. Em IFC um sistema de gás é um IfcDistributionSystem do tipo GAS e um de óleo FUEL; a caixa é um IfcDistributionChamberElement, o medidor um IfcFlowMeter GASMETER, os registros IfcValve, o fogão um IfcBurner, o tanque um IfcTank.

![O quantitativo de gás e óleo](shots/mep-gas-list.webp)

Dimensionar os tubos pelo consumo dos aparelhos e verificar a perda de carga é engenharia, na edição **Lapin MEP**.

## MEP Pro

A engenharia sobre a rede — a demanda que cada trecho leva e a dimensão que a comporta, as verificações (pontas abertas, curvas fechadas demais, caimentos de esgoto, fontes sobrecarregadas), as regras de assentamento por material (cobre em passos de 45°, PVC virando no máximo 45°), radiadores propostos sob as janelas, percursos sugeridos ao longo das paredes, o perfil de um trecho e a isométrica de um sistema — está na edição **Lapin MEP**, que acrescenta os grupos **Engenharia** e **Assistentes** a esta guia.
