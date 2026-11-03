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

## MEP Pro

A engenharia sobre a rede — a demanda que cada trecho leva e a dimensão que a comporta, as verificações (pontas abertas, curvas fechadas demais, caimentos de esgoto, fontes sobrecarregadas), as regras de assentamento por material (cobre em passos de 45°, PVC virando no máximo 45°), radiadores propostos sob as janelas, percursos sugeridos ao longo das paredes, o perfil de um trecho e a isométrica de um sistema — está na edição **Lapin MEP**, que acrescenta os grupos **Engenharia** e **Assistentes** a esta guia.
