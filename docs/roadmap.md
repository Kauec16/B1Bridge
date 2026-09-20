# Roadmap do B1Bridge

[Voltar ao README](../README.md)

Este plano considera a base atual do novo repositório. Uma implementação experimental em outra branch ou repositório não equivale a uma entrega incorporada aqui. A ordem abaixo é uma proposta por dependência técnica; poderá ser ajustada quando a primeira operação de negócio for escolhida.

## Fundação disponível

- [x] Solução .NET 10 e quatro projetos com referências entre as camadas.
- [x] Host ASP.NET Core e documento OpenAPI no ambiente de desenvolvimento.
- [x] Remoção dos artefatos e testes de exemplo do template.
- [x] CI com restore, build e etapa de execução de testes.

Ainda não há projetos de teste: a presença da etapa na CI não representa uma suíte implementada. O README e o guia de arquitetura acompanham este plano; funcionalidades só devem mudar de planejadas para disponíveis junto de sua implementação verificada.

## 1. Ciclo de vida da operação

**Entrega:** modelar identidade, estados, tentativas, sucesso, falha e regras de reprocessamento. Criar os primeiros casos de uso e contratos necessários sem exigir SAP ou RabbitMQ para testar as regras.

**Critério de conclusão:** testes exercitam transições válidas e inválidas, histórico de tentativas e elegibilidade para reprocessar. Não criar testes de exemplo sem comportamento relevante.

## 2. Persistência e recuperação

**Entrega:** persistir operações e tentativas em uma base própria, inicialmente com SQLite para desenvolvimento. Definir migrations, unicidade da chave de idempotência e recuperação do trabalho pendente.

**Critério de conclusão:** reiniciar o processo não perde as operações; entregas repetidas e concorrentes não criam registros duplicados; reutilizar a chave com outro conteúdo produz conflito. Testes de integração verificam essas regras no provedor escolhido.

## 3. Contratos, mensageria e outbox

**Entrega:** definir mensagens versionadas de requisição e resultado, integrar RabbitMQ e implementar a passagem consistente entre persistência e publicação. Criar uma biblioteca compartilhada de contratos somente se os consumidores precisarem dela.

**Critério de conclusão:** demonstrar o recebimento durável, o processamento e a publicação do resultado; testar redelivery, reinício entre etapas e indisponibilidade do broker. A confirmação da requisição precisa respeitar o limite de persistência definido na arquitetura.

O executor poderá ser controlado em testes nessa etapa. A entrega ainda não representa comunicação real com SAP.

## 4. Primeiro fluxo SAP e agente

**Entrega:** escolher uma operação SAP concreta e seu contrato, implementar autenticação/sessão do Service Layer, o adaptador tipado e o agente mínimo necessário para uma demonstração ponta a ponta.

**Critério de conclusão:** em ambiente controlado, uma solicitação de um sistema externo atravessa agente e broker, gera um resultado verificável no SAP e retorna correlacionada. Demonstrar sucesso, erro funcional e tratamento de resposta incerta. O agente não recebe credenciais nem acesso direto ao SAP.

O agente e o adaptador podem ser entregues em pull requests separados, aproveitando os contratos definidos na etapa anterior.

## 5. Tratamento operacional de falhas

**Entrega:** consolidar retry com limites, fila de falhas, consulta de operações e reprocessamento autorizado. Separar falhas recuperáveis, erros de negócio e resultados incertos.

**Critério de conclusão:** cada falha deixa histórico consultável; uma nova tentativa preserva a rastreabilidade; repetir a entrega ou reprocessar uma operação não duplica indevidamente um documento no SAP.

## 6. Demonstração e preparação para implantação

**Entrega:** expandir health checks, logs estruturados e correlação; documentar configuração, secrets, recuperação, requisitos de implantação e execução de uma demonstração reproduzível.

**Critério de conclusão:** outra pessoa consegue executar o cenário a partir da documentação, localizar uma falha pelo identificador da operação e entender as limitações restantes. A CI executa os testes reais adicionados nas entregas anteriores.

Segurança, rastreabilidade e testes acompanham todas as etapas; esta entrega consolida sua operação. Interface administrativa, múltiplos provedores de banco e automação de deployment serão avaliados depois que o primeiro fluxo estiver validado.

## Como aproveitar o trabalho anterior

Antes de portar uma implementação, comparar seu conteúdo com a `main` atual, revisar dependências e selecionar apenas a funcionalidade ausente. Commits de templates, estruturas já existentes e documentação incompatível com o código atual não precisam ser reaplicados.

Cada pull request deverá explicar o comportamento adicionado, como foi verificado e quais limitações permanecem. Evitar marcar uma etapa como concluída apenas porque classes ou configurações foram criadas.
