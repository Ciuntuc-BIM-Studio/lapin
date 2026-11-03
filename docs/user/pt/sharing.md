---
title: Trabalhando juntos
order: 125
summary: Compartilhe o projeto aberto e trabalhe nele ao vivo com outras pessoas, pelo Lapin ou por um navegador.
---

<!-- video sharing -->

Várias pessoas podem trabalhar no mesmo projeto ao mesmo tempo, como jogadores entram no servidor de um jogo. Um de vocês compartilha o projeto pelo Lapin no próprio computador: essa pessoa é o **anfitrião**. As outras entram como **convidados**, pelo Lapin no próprio computador ou por um navegador, com o link que o anfitrião envia.

O projeto fica no computador do anfitrião, e o Lapin do anfitrião decide cada edição. Os convidados veem o modelo enquanto ele muda, desenham com as mesmas ferramentas e veem onde os outros estão.

## Compartilhar o projeto

**Arquivo → Compartilhar sessão…**, informe o seu nome, escolha como os convidados entram e pressione **Compartilhar**:

- **editores** modelam como você;
- **visualizadores** olham e navegam, e não mudam nada.

O Lapin mostra um **link** e um **código**. Envie o link para quem entra por um navegador e o código para quem entra pelo Lapin. Qualquer pessoa que os tenha pode entrar até você pressionar **Parar de compartilhar**. A cada vez que você compartilha, o link e o código são novos.

![Compartilhando: o link, o código e quem está na sessão](shots/share-dialog.webp)

Na mesma caixa de diálogo você pode tornar um convidado editor ou visualizador, ou **pedir que ele saia**. Feche-a, e a sessão continua. O ícone de pessoas ao lado dos participantes na barra superior a abre de novo.

Enquanto o projeto está compartilhado e tem um arquivo, o Lapin o salva a cada 5 minutos. Só o anfitrião salva; os convidados podem exportar arquivos IFC e 3D para si mesmos.

## Entrar numa sessão

- **Por um navegador**: abra o link, informe o seu nome e pressione **Entrar**. Nada para instalar.
- **Pelo Lapin**: na tela inicial, ou em **Arquivo**, escolha **Entrar em uma sessão…**, cole o link ou o código e informe o seu nome.

![Entrando pelo Lapin](shots/join-dialog.webp)

Uma barra embaixo diz de quem é a sessão em que você está. **Sair** leva você de volta ao seu próprio Lapin. Se o anfitrião parar de compartilhar ou fechar o Lapin, o Lapin avisa, e você volta com **Voltar ao Lapin**.

## Quem está onde

Os participantes ficam na barra superior, cada um com a sua cor. Clique num deles para ir à vista que ele está olhando.

![Os participantes](shots/participants.webp)

Na planta você vê os cursores dos outros com os seus nomes, e o que eles selecionaram, contornado na cor deles.

![O cursor e a seleção de um convidado na planta do anfitrião](shots/sharing-plan.webp)

## O desfazer é só seu

<kbd>Ctrl Z</kbd> desfaz a sua última edição, não a de outra pessoa. Se alguém mudou o elemento depois de você, o Lapin o deixa como está e diz quem mudou. A partir daí, o seu desfazer não volta além dessa edição.

## Seguro por padrão

- Tudo entre o anfitrião e os convidados passa pelo relay do Lapin, **criptografado de ponta a ponta** com a chave do convite. O relay repassa os dados e não consegue lê-los.
- Um convidado não pode abrir, salvar nem ler arquivos no computador do anfitrião, nem abrir outro projeto ali.
- O Lapin não abre nada para a rede até você compartilhar, e para quando você para.
