---
status: ready-for-agent
---

# Temas

## Problem Statement

O app só alterna entre o Fluent claro e o escuro (ou segue o sistema), com cores de sintaxe e de diff fixas. Quem passa o dia em editores com temas como GitHub, Dracula, Gruvbox ou Andromeda sente o app deslocado, e algumas cores fixas já se mostraram ilegíveis em fundo escuro.

## Solution

Em Configurações → Tema, uma lista única com Igual ao sistema, Claro, Escuro, GitHub Claro, GitHub Escuro, Dracula, Gruvbox Light e Andromeda, cada um com uma amostra de cores ao lado do nome. O tema escolhido aplica na hora e muda o app inteiro: fundos, painéis, seleção, botões, cor de destaque, editor XML (fundo, texto e sintaxe), cores do diff da Comparação e sublinhado das Validation Issues. A escolha fica salva na sessão.

## User Stories

1. Como usuário, quero escolher o tema numa lista única em Configurações, para não combinar opções sem sentido (ex.: Dracula + claro).
2. Como usuário, quero continuar tendo Igual ao sistema, Claro e Escuro, para manter o visual atual se preferir.
3. Como usuário, quero os temas GitHub Claro, GitHub Escuro, Dracula, Gruvbox Light e Andromeda.
4. Como usuário, quero ver uma amostra de cores (fundo, destaque e três cores de sintaxe) ao lado do nome de cada tema, para reconhecê-lo antes de escolher.
5. Como usuário, quero que o tema aplique na hora, sem reiniciar.
6. Como usuário, quero que o tema escolhido fique salvo e volte ao abrir o app.
7. Como usuário, quero que o tema mude o app inteiro: janelas, painéis, árvores, listas, seleção, botões, barras e diálogos.
8. Como usuário, quero que um tema nomeado use a sua própria cor de destaque; com Igual ao sistema, Claro e Escuro, a do sistema.
9. Como usuário, quero que o editor XML use o fundo, o texto e as cores de sintaxe do tema (tag, atributo, valor, comentário, declaração, entidade, CDATA).
10. Como usuário, quero que os links no editor usem a cor de link do tema.
11. Como usuário, quero que o diff da Comparação use o verde, o vermelho e o amarelo do tema, no fundo das linhas e nos nomes da árvore.
12. Como usuário, quero que o sublinhado de erro e de aviso das Validation Issues use o vermelho e o amarelo do tema.
13. Como usuário, quero que a barra de título do macOS acompanhe o modo (claro/escuro) do tema.
14. Como usuário, quero que a janela de Comparação, o Sobre, as Configurações e os diálogos também troquem de tema na hora.
15. Como usuário, quero que todo texto seja legível em todos os temas.
16. Como usuário que já tinha escolhido Sistema, Claro ou Escuro, quero continuar com a mesma escolha depois da atualização.

## Implementation Decisions

- **Catálogo de temas:** cada tema é um dado puro — nome, modo (claro/escuro), se usa a cor de destaque do sistema, e as cores de todos os papéis: fundos (janela, painel, editor, seleção, hover), texto (principal, secundário), borda, destaque, link, sintaxe do XML (tag, atributo, valor, comentário, declaração, entidade, CDATA), diff (entrou, saiu, mudou, alinhamento) e sublinhado (erro, aviso). O catálogo não depende do Avalonia.
- **Cores oficiais:** GitHub (paleta Primer, claro e escuro), Dracula (draculatheme.com), Gruvbox Light (paleta do morhetz), Andromeda (EliverLara). A sintaxe do XML segue o que cada tema usa para HTML/XML no VS Code.
- **Aplicação:** o tema define a variante do Fluent (claro/escuro, que também ajusta a barra de título) e sobrescreve a paleta do Fluent e os recursos do app com as cores do catálogo; Igual ao sistema, Claro e Escuro não sobrescrevem a paleta do Fluent nem a cor de destaque; só definem as cores próprias do app (fundo e texto do editor, sintaxe, diff, avisos e texto sobre o destaque). Editor, diff e sublinhado passam a ler as cores do tema ativo em vez de cores fixas. Trocar o tema reaplica tudo e redesenha os editores abertos.
- **Configurações:** a lista de tema ganha a amostra de cores por item. A escolha fica salva na sessão; os valores salvos antigos (Sistema/Claro/Escuro) continuam válidos.

## Testing Decisions

- Seam novo e único: o catálogo de temas, num projeto de teste do App. Testa comportamento observável dos dados, não a interface.
- Completude: todo tema define todos os papéis de cor.
- Contraste (WCAG): texto principal sobre fundos (inclusive linhas do diff) ≥ 4,5:1; texto secundário, sintaxe (também sobre as linhas do diff), link, diff e sublinhado ≥ 3:1 sobre o fundo em que aparecem; texto sobre a cor de destaque ≥ 4,5:1 nos temas nomeados.
- Modo: cada tema declara claro ou escuro coerente com o seu fundo.
- Aplicar o tema na interface não tem teste automatizado; conferência por capturas headless de cada tema.

## Out of Scope

- Temas personalizados carregados de arquivos.
- Gruvbox Dark e outros temas além dos listados.
- Trecho de XML de exemplo na tela de Configurações.
- Mudança de fonte por tema.

## Further Notes

- Depois desta spec entraram One Light, One Dark, Nord, Catppuccin Latte, Catppuccin Mocha e Tokyo Night (Night, Storm, Moon, Day), com as mesmas regras. Onde a cor oficial não passa no contraste (comentários escuros, verde e amarelo em fundo claro, o texto do Tokyo Night Day), ela é escurecida ou clareada só o necessário e o ajuste fica anotado no catálogo.
- Cores do diff (Configurações → Cores do diff): "Do tema" ou um esquema do app (GitHub, VS Code, Daltônico azul/laranja, Tritanopia, Clássico, Alto contraste, Monokai, Solarized, Claude), independente do tema. O texto usa a cor do esquema; o fundo da linha é o editor escurecido (tema escuro) ou clareado (tema claro) e tingido com ela, o mais forte (até 18%) que mantém texto e sintaxe do tema legíveis, e os testes de contraste rodam em cada tema × esquema.
- "Tema" é vocabulário de interface, não do domínio; não entra no CONTEXT.md.
- Índigo (neutros slate com destaque índigo) é o primeiro tema nomeado que segue o sistema: traz paleta clara e escura, e é o padrão de quem abre o app pela primeira vez. Sessões já salvas mantêm o tema escolhido.
