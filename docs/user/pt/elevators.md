---
title: Elevadores
order: 43
summary: Elevadores a partir de um configurador de doze tipos dimensionados segundo EN 81-20 e EN 81-70, com caixa, poço, altura livre superior, casa de máquinas, portas de cabine e de patamar, as aberturas nas lajes que atravessam, plantas em todos os pisos, um mapa e IFC.
---

## Posicionar um elevador

**Arquitetura → Elevador** (<kbd>LF</kbd>) coloca um elevador com um clique na planta. O clique é o centro da caixa, e as portas ficam voltadas para baixo na planta (gire-o com 0°, 90°, 180° ou 270° na barra, ou depois com a sua alça). O elevador vai do piso da planta até o piso mais alto do projeto e para em todos os pisos intermediários. O seu configurador abre na hora.

Enquanto a ferramenta está ativa, a barra da linha de comando e o painel de propriedades mostram **o estilo** que ela coloca: um dos doze tipos do catálogo, ou um dos estilos do próprio projeto.

## O configurador

![O configurador de elevador, com o estilo acessível a cadeira de rodas](shots/elevator-configurator.webp)

Os estilos ficam à esquerda. Um estilo preenche todos os campos, e depois você pode mudar qualquer campo à mão. O catálogo tem estes tipos:

| Estilo | Pessoas | Carga nominal | Cabine (mm) | Acionamento |
|---|---|---|---|---|
| Residencial pequeno | 4 | 320 kg | 900 × 1000 | sem casa de máquinas |
| Passageiros padrão | 6 | 450 kg | 1000 × 1250 | sem casa de máquinas |
| Passageiros médio | 8 | 630 kg | 1100 × 1400 | de tração, casa de máquinas no topo |
| Passageiros grande | 10 | 800 kg | 1350 × 1400 | de tração |
| Passageiros, tráfego intenso | 13 | 1000 kg | 1600 × 1400 | de tração, 1,6 m/s |
| Acessível a cadeira de rodas | 8 | 630 kg | 1100 × 1400, porta 900 | EN 81-70 tipo 2 |
| Monta-camas hospitalar | 17 | 1275 kg | 1200 × 2300 | de tração, cabine passante |
| Monta-cargas | 26 | 2000 kg | 1500 × 2700 | de tração, 0,5 m/s |
| Hidráulico (baixa altura) | 8 | 630 kg | 1100 × 1400 | macaco, sala da central hidráulica ao lado da caixa |
| Sem casa de máquinas (MRL) | 13 | 1000 kg | 1100 × 2100 | máquina na altura livre superior |
| Panorâmico em vidro | 8 | 630 kg | 1100 × 1400 | cabine de vidro numa caixa de vidro |
| Elevador doméstico | 3 | 250 kg | 800 × 950 | 0,15 m/s, poço pouco fundo |

À direita, três guias:

- **Cabine e carga**: o acionamento (de tração, sem casa de máquinas, hidráulico, doméstico), a carga nominal e as pessoas, a velocidade, o tipo acessível (EN 81-70 tipo 1, 2 ou 3), a largura, a profundidade e a altura da cabine, paredes da cabine em vidro, um elevador de bombeiros, um elevador de evacuação. **Carga e pessoas a partir da cabine** define a menor carga nominal que a EN 81-20 dá à área da cabine, e as pessoas que ela leva.
- **Portas e paradas**: um acesso ou dois (uma cabine passante), a largura e a altura da porta, abertura central ou lateral e as suas folhas (duas ou três folhas deslizam uma atrás da outra), a parada mais alta e os pisos em que para. Um piso que ele passa sem parar mantém a parede da caixa fechada e não tem porta de patamar.
- **Caixa**: o lado do contrapeso (ou do macaco de um elevador hidráulico), o poço sob a parada mais baixa e a altura livre acima da mais alta (ambos seguem o acionamento e a velocidade até você digitar os seus), a casa de máquinas (no topo para os de tração, ao lado da caixa embaixo para um hidráulico, nenhuma nos outros), as paredes da caixa (um tipo de parede do projeto, ou concreto com a espessura que você indicar), uma caixa de vidro, a rotação.

![O corte pela cabine, do poço à casa de máquinas](shots/elevator-section.webp)

A pré-visualização mostra a planta na parada mais baixa e, nas outras guias, um corte pela cabine com todas as paradas (as servidas em vermelho, as que ele passa tracejadas). Embaixo, o que diz a EN 81: uma cabine maior do que a sua carga permite, mais pessoas do que a carga ou a área comportam, uma cabine ou porta acessível pequena demais, um elevador hidráulico com curso acima de cerca de 18 m. Mais abaixo, as medidas livres e exteriores da caixa, a área da cabine comparada com a que a carga permite, as paradas, o curso e as lajes que a caixa abre.

**Aplicar** altera o elevador num só passo de desfazer. **Salvar como estilo…** guarda-o como estilo do projeto; **Atualizar o estilo** salva-o de novo e todos os elevadores desse estilo o seguem. Um estilo do projeto pode ser renomeado ou apagado (os seus elevadores ficam como estão).

## Nos desenhos

![Um elevador em planta: a caixa cortada, a cabine com a sua cruz, o contrapeso e as portas de patamar](shots/elevator-plan.webp)

A caixa abre todas as lajes que atravessa, com uma abertura própria em cada laje. Quando você move, gira ou copia o elevador, ou muda os seus pisos, as aberturas acompanham. Uma laje desenhada depois do elevador é aberta com **Arquitetura → Aberturas da caixa**.

Toda planta de um piso que o elevador alcança o desenha: as paredes da caixa cortadas, a cabine com a sua cruz diagonal, o contrapeso com a sua diagonal (o macaco de um hidráulico como um círculo) e, nos pisos em que para, as folhas da porta de patamar e da porta da cabine, tracejadas onde deslizam quando abertas. Cortes e fachadas desenham a caixa inteira, o poço, o topo, a casa de máquinas, a cabine na parada mais baixa e as portas de patamar. Em 3D a cabine fica na parada mais baixa com as portas fechadas.

![Um elevador em 3D, com a casa de máquinas sobre a casa](shots/elevator-3d.webp)

## Propriedades, mapa e IFC

![As propriedades de um elevador](shots/elevator-properties.webp)

Selecione um elevador para mudar a sua parada mais baixa e a mais alta, marcar os pisos em que para, escolher o tipo de parede da caixa, ler os avisos da EN 81 e abrir **Configurador…**. Os seus campos (acionamento, carga, cabine, portas, poço, altura livre…) ficam abaixo; os que não se aplicam ficam ocultos (sem casa de máquinas para um elevador sem casa de máquinas, sem lado do contrapeso para um hidráulico). Arraste a alça do meio para movê-lo e a alça diante das portas para girá-lo. **Mover**, **Copiar**, **Girar** e **Espelhar** funcionam como para qualquer elemento; um elevador espelhado leva o contrapeso para o outro lado.

**Arquitetura → Mapa de elevadores** lista cada elevador com o seu tipo, acionamento, carga nominal, pessoas, cabine, portas, as paradas que serve, os pisos entre os quais corre, o curso, a velocidade e a caixa.

Cada elevador tem um tipo formado pelo seu gênero, carga e cabine (Passageiros médio 630 kg, 1100 × 1400): elevadores iguais o compartilham, então uma composição de preço posta no tipo (**Execução → Atribuir composições…**) orça todos eles, e o planejamento os encontra pela sua classe (IfcTransportElement).

Em IFC um elevador é um **IfcTransportElement** com o tipo predefinido **ELEVATOR**, de um **IfcTransportElementType**, com os conjuntos do próprio IFC **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) e **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). O Lapin lê o seu próprio arquivo de volta como um elevador nativo, com as suas paradas e as aberturas das lajes.
