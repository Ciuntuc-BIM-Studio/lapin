---
title: Pisos e telhados
order: 40
summary: Desenhe pisos e telhados pelo contorno, escolha a forma e as bordas de um telhado e corte aberturas.
---

## Pisos

Escolha **Arquitetura → Piso** (<kbd>SB</kbd>) e clique nos cantos do contorno. Feche o contorno no primeiro ponto ou pressione <kbd>Enter</kbd>. Um piso também é feito de camadas, definidas em **Arquitetura → Tipos de piso**. Uma camada de um tipo de piso pode ter espessura 0 (uma impermeabilização, a pintura): ela não constrói nada, é desenhada como uma linha nos cortes e é medida pela área do piso.

Para mudar o contorno, selecione o piso e arraste os cantos. **Abertura no piso** (<kbd>SH</kbd>) corta um furo no piso, para uma escada ou um shaft.

Um piso desenhado fora das paredes, diante de uma porta, é uma varanda. Para os degraus até ela, veja [Escadas](stairs.md).

## Telhados

Escolha **Arquitetura → Telhado** (<kbd>RF</kbd>) e clique no contorno das paredes sob ele. O Lapin constrói o telhado sobre esse contorno, no topo do seu nível.

![Uma casa com telhado de duas águas na vista 3D](shots/house-3d.webp)

Selecione o telhado para mudá-lo no painel de propriedades:

- **Forma.** Quatro águas, duas águas, uma água, mansarda, abobadado ou plano. Um telhado abobadado é uma abóbada curva entre duas empenas; **Abóbada: flecha** define quanto ele sobe no topo, no máximo metade do vão.
- **Inclinação e beiral** para o telhado inteiro.
- **Direção da cumeeira (duas águas, uma água)**: automática, x ou y. A cumeeira de um telhado de uma água é o seu lado alto: em x ele cai para −y, em y para −x; **Uma água: inverter a inclinação** faz ele cair para o outro lado.
- **Cada borda** com o seu tipo (beiral ou empena), inclinação e beiral. Uma mansarda com bordas de empena é um telhado holandês.
- **Estilo.** Os estilos de telhado dão um visual completo numa só escolha: a cobertura, os beirais, as calhas e os condutores.
- **Material da cobertura.** A cobertura é de um material do projeto, por omissão o da cobertura escolhida (Clay tile, Metal sheet, Slate…), criado na cor da cobertura da primeira vez que um telhado precisa dele. Escolha outro nas propriedades ou em **Cobertura e drenagem** do configurador; mudar a cobertura muda-o também, a menos que tenha escolhido um. Alçados e cortes tracejam a cobertura por esse material (telhas, telhas de madeira e ardósia em fiadas horizontais, chapa em juntas verticais, a menos que lhe dê um tracejado **Elevation** próprio), um corte desenha a cobertura cortada com o tracejado de corte do material, e o IFC, as medições e o orçamento listam a cobertura sob o seu material.

![As propriedades de um telhado](shots/roof-properties.webp)

**Configurador de telhado** (o botão ao lado do estilo, ou **Arquitetura → Configurador de telhado** com um telhado selecionado) reúne tudo num só lugar: os estilos à esquerda, depois a forma e as bordas, a cobertura e a drenagem, a estrutura e as aberturas, sobre uma planta do telhado (clique numa borda para torná-la automática, beiral ou empena) e, para a estrutura, um corte através da cumeeira com a tesoura ou o par de caibros no meio. Escolha um estilo para começar e mude qualquer campo à mão; **Aplicar** faz disso uma só etapa de desfazer. **Salvar como estilo…** guarda o telhado como está configurado como um estilo do projeto, listado em **Projeto** acima dos do catálogo (e na lista do painel de propriedades) para outros telhados e para os novos; **Atualizar o estilo** salva as mudanças nele de novo, e todos os telhados do estilo o acompanham. Um estilo do projeto pode ser renomeado ou excluído; seus telhados mantêm a aparência.

![O configurador de telhado](shots/roof-configurator.webp)

**Calhas e condutores.** Em **Cobertura e drenagem** do configurador, a calha recebe o tamanho, quanto ela fica para fora do beiral e quanto abaixo da cobertura, o caimento até as saídas (mm/m) e as tampas das pontas; a tabela das bordas liga ou desliga a calha de cada beiral. Os condutores começam como do próprio telhado, um a cada 10 m; **Torná-los editáveis** os transforma em elementos, e **Arquitetura → Condutor** adiciona um com um clique numa calha. A alça de um condutor só o desliza ao longo da sua calha; suas propriedades definem o diâmetro, a curva no pé e o trajeto, uma cadeia de segmentos, cada um com seu comprimento e seu ângulo em relação à vertical (na direção da parede), o último descendo até o chão. No IFC, uma calha é um IfcPipeSegment GUTTER e um condutor é um segmento rígido chamado Downpipe.

As paredes no nível do telhado param sob ele e sobem até as empenas sozinhas, também sob um telhado curvo. Uma parede em outro nível acompanha o telhado depois de **Arquitetura → Anexar ao telhado** (<kbd>AR</kbd>): selecione as paredes e clique no telhado. A mesma ferramenta aceita qualquer outro corpo sobre as paredes: um sólido do Studio, uma laje, uma viga, um elemento importado de IFC. As paredes sobem até a face inferior dele e são cortadas ao longo dela, seja qual for a forma. **Desanexar do telhado** desfaz isso.

**Janela de telhado** e **Água-furtada** adicionam aberturas a um telhado: clique numa água. Elas pertencem ao telhado. Selecione o telhado, e cada uma mostra suas alças na planta. Arraste a alça do meio para movê-la no telhado. Arraste as alças das bordas ou dos cantos de uma janela de telhado para redimensioná-la na água (a borda oposta fica onde está; você pode digitar o comprimento enquanto arrasta). A largura corre ao longo do beiral e o comprimento sobe pela água, medido sobre ela. Uma água-furtada tem uma alça de cada lado para a sua largura. Uma mudança que não caberia no telhado é recusada. Para removê-la, clique com o botão direito numa alça dela e escolha **Remover janela de telhado** (ou **Remover água-furtada**), pressione **Delete** com o cursor sobre a alça, ou **Ctrl/⌘-clique** na alça do meio. As propriedades do telhado também as listam: digite ali a largura e o comprimento de uma janela, ou clique em **×** para removê-la.

![A alça de uma janela de telhado no telhado selecionado](shots/roof-window-grip.webp)

### Estrutura de madeira

**Estrutura** define como o telhado é estruturado:

- **caibros**: caibros sobre terças, com uma viga de cumeeira.
- **tirante**: pares de caibros com um tirante horizontal em **Tirantes: fração da altura** (dois terços por padrão), sem terças.
- **tesouras**: tesouras pré-fabricadas ao longo da cumeeira principal, no **Espaçamento dos caibros**: banzos superiores nas águas, um banzo inferior sobre os frechais, diagonais em W (uma tesoura de uma água num telhado de uma água). As pontas de um telhado de quatro águas mantêm seus caibros.
- **nenhuma**: um painel, sem madeira.

**Caibros e banzos** aceita as seções antigas em cm ou as nórdicas em mm (45×95 a 45×245); **Diagonais e montantes da tesoura** e **Classe de resistência da madeira** (C14 a C30, madeira laminada colada) vêm junto. Os caibros se apoiam no frechal por um entalhe (**Apoio dos caibros no frechal**) e terminam no beiral em prumo ou em esquadro (**Pontas dos caibros**). Os estilos **Telhado com tesouras suecas** e **Telhado nórdico com tirantes** trazem sua estrutura com eles.

Cada peça é uma parte própria: a tabela **Madeira do telhado** as lista como uma lista de corte (peça, seção, classe, comprimento, quantidade, volume), e o IFC recebe cada uma como um IfcMember do seu tipo, cada tesoura como um IfcElementAssembly.

## Forros

**Acabamentos → Forro** (<kbd>CG</kbd>) desenha um forro sobre um contorno que você clica; **Forro em um ambiente** preenche o ambiente que você clica, seguindo o seu contorno. Um forro fica pendurado na sua **Altura** acima do pavimento (2700 mm, a menos que você mude nas propriedades).

O tipo, em **Acabamentos → Tipos de forro**, define o **Sistema**: **Placas sobre perfis aparentes** (a largura, o comprimento e a espessura da placa, a largura da face da grelha, a altura e a rotação, pendurais até a estrutura no seu espaçamento) ou **Gesso acartonado, contínuo**. A grelha fica centrada no forro, para que as placas cortadas nas bordas saiam iguais.

**Painel luminoso** e **Difusor de ar** colocam um na célula que você clica; clique de novo na célula com a outra ferramenta para trocá-lo. No painel de propriedades, **Distribuir** coloca painéis de luz ou difusores de ar a cada tantas células, e **Limpar** os remove. As tabelas contam as placas inteiras, os painéis de luz e os difusores, e o forro vai para o IFC como um IfcCovering do tipo CEILING.

![As propriedades de um forro: tipo, altura e acessórios](shots/ceiling-properties.webp)

![Forros em grelha com painéis de luz e difusores de ar, em 3D](shots/ceilings-3d.webp)

## Rodapés

**Acabamentos → Rodapé no ambiente** (<kbd>SK</kbd>) coloca um rodapé em volta do ambiente que você clica, ao longo das faces das paredes, interrompido em cada porta (qualquer abertura que desce até o piso); clicado dentro de paredes sem ambiente, ele as segue do mesmo jeito. O rodapé de um ambiente acompanha o ambiente: mova uma parede ou uma porta e o rodapé vai junto. **Rodapé** desenha um ao longo de uma linha que você clica na face de uma parede (Enter termina); a peça fica do lado oposto à parede, e **Trocar de lado** nas propriedades a vira.

O tipo, em **Acabamentos → Tipos de rodapé**, define o **Perfil** — **Liso (reto)**, **Chanfrado (MDF)**, **Moldurado (madeira)**, **Com meia-cana (metal)**, **Borda boleada** ou **Borda arredondada** — desenhado ao lado em escala, a **Altura** (40 a 300 mm), a **Espessura** (5 a 40 mm), o **Material** e a sua cor. **Da biblioteca** traz tipos prontos para o projeto: MDF 80 chanfrado, Carvalho 70 moldurado, Cerâmica 80 reto, Alumínio 60 arredondado, Boleado 50 e Meia-cana 70. Enquanto uma ferramenta de rodapé está ativa, o painel de propriedades mostra o tipo que ela coloca, para escolher antes do clique.

As propriedades de um rodapé têm o tipo, o **Deslocamento da base** acima do piso (em Dimensões) e **Interrompido nas portas** (desligado, ele continua através delas). **Tabela de rodapés** (ou **Rodapés** na lista de novas tabelas do navegador do projeto) os lista por tipo com as peças e o comprimento, sem as folgas das portas. A peça é cortada em meia-esquadria nos cantos; uma planta desenha a sua borda frontal, e um rodapé vai para o IFC como um IfcCovering do tipo SKIRTINGBOARD, de um IfcCoveringType do mesmo tipo.

![Os tipos de rodapé: o perfil desenhado em escala, a biblioteca](shots/skirting-types.webp)

![As propriedades de um rodapé: tipo, deslocamento da base e folgas nas portas](shots/skirting-properties.webp)
