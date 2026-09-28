---
status: ready-for-agent
---

# Refatoração acumulada

## Problem Statement

As revisões de WSDL, Comparação e temas deixaram pontos que tornam o código mais difícil de mudar: `Sample` com dois papéis, o par (Operation, Request/Response) solto em várias assinaturas, enums quase iguais na Comparação, cores de tema como texto (e pincéis recriados a cada desenho), duplicações pequenas e um `MainViewModel` com responsabilidades demais.

## Solution

Refatoração sem mudar comportamento (com uma exceção explícita no item C), em commits independentes, na ordem E → B → A → C → D → F.

## User Stories

1. Como mantenedor, quero que a conversão de erro de schema em Validation Issue, a descrição de enumeração, o nome do arquivo de Global Element/Service e o rótulo Request/Response tenham um único dono (E).
2. Como mantenedor, quero que "a Request (ou Response) de uma Operation" seja um só tipo — o `OperationMessage`, que conhece a sua Operation — em geração, validação, Payload Binding, exportação, Binding de Envelope e sessão (B).
3. Como mantenedor, quero que `Sample` tenha só o que é comum a todo Sample e uma parte opcional Envelope (mensagem, Payload Binding, Payload legível), com `Element` sempre sendo o Global Element que gerou o conteúdo (A).
4. Como mantenedor, quero um só enum de mudança na Comparação (o status do par é um `ChangeKind`) e um só mapa de cor/rótulo na interface (C).
5. Como analista fiscal, quero ver "só documentação" num par cuja única mudança foi documentação, em vez de "igual" (C — única mudança visível).
6. Como mantenedor, quero cores de tema já interpretadas no catálogo e um conjunto de pincéis montado uma vez por aplicação de tema, usado por editores e renderizadores (D).
7. Como mantenedor, quero o `MainViewModel` dividido em árvore/seleção/pesquisa (`SchemaTreeViewModel`), guarda de Payload Bindings e Endpoints (`PayloadBindingStore`) e o orquestrador (F).
8. Como usuário, quero que tudo continue funcionando exatamente como antes (Samples, Envelopes, validação, Comparação, temas).

## Implementation Decisions

- Um commit por item, cada um com build e testes verdes.
- B antes de A: a parte Envelope do Sample referencia o `OperationMessage`. O `OperationCandidate` deixa de existir. As chaves de sessão viram um tipo derivado do `OperationMessage`.
- A: composição, não herança.
- C: `PairStatus` sai; pares só com documentação passam a ter status Documentation-only.
- D: o catálogo continua sem depender do Avalonia; o tema ativo continua global (é do app inteiro), documentado no código.
- F: fronteiras `SchemaTreeViewModel`, `PayloadBindingStore`, `MainViewModel`.

## Testing Decisions

- Seams existentes: fachada pública do Core e catálogo de temas. Testes só mudam onde a assinatura pública muda.
- Rede de segurança: retrato da saída do Core com fixtures reais (Samples da NFGas, Envelopes da NF-e, Validation Issues de Envelope, resumo da Comparação PL_009 × PL_010), gravado antes da refatoração e comparado byte a byte. Atualizado de propósito só no item C.
- Interface: conferência por capturas headless no fim.

## Out of Scope

- Mensagens do Core no idioma escolhido (comentário "cobre", Validation Issues de carga, resumo em Markdown).
- Qualquer funcionalidade nova.
