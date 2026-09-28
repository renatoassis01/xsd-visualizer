# XSD Visualizer

Aplicativo desktop multiplataforma (Windows, macOS, Linux) para explorar conjuntos de XSD, gerar XMLs de exemplo válidos a partir deles e validar XMLs existentes. Nasceu para acompanhar os novos schemas da SEFAZ (NF-e, NFGas, NFAg…), mas é genérico: funciona com qualquer XSD 1.0.

## O que ele faz

- **Abre Schema Sets**: selecione ou arraste uma pasta de XSDs (ou um `.xsd` solto, que abre a pasta dele). Includes/imports são resolvidos; imports remotos usam schemas W3C embutidos, depois um cache local, depois download.
- **Mostra a estrutura** de cada Global Element numa árvore pesquisável: cardinalidade, tipo, `sequence`/`choice`/`all`, facets, documentação, recursão, substitution groups, `xsi:type` e wildcards.
- **Gera Samples válidos e determinísticos**:
  - **Maximal**: todos os opcionais; em cada choice, o ramo fixado na árvore.
  - **Minimal**: só o que o Schema Set exige.
  - **Coverage Set**: o menor conjunto de Samples em que cada ramo de choice, opcional e valor de enumeração aparece ao menos uma vez, com um comentário no topo dizendo o que cada um cobre.
  - **Gerar todos**: grava tudo em `<saída>/<Schema Set>/<Global Element>/<nome>.{max,min,cov-NN}.xml`.
- **Valida Documents**: arraste um XML; ele é vinculado (Binding) ao Global Element certo e validado num editor com destaque de sintaxe, revalidação enquanto você digita e Validation Issues sublinhadas.
- **Sessão**: reabre os Schema Sets da última vez, mantém recentes e recarrega sozinho quando um XSD muda em disco.

O vocabulário (Schema Set, Global Element, Sample, Coverage Set, Document, Binding, Validation Issue) está definido em [`CONTEXT.md`](CONTEXT.md) e é usado igual no código e na interface.

## Tecnologias

| Camada | Tecnologia |
|---|---|
| Runtime | .NET 10 (C#) |
| Interface | [Avalonia UI](https://avaloniaui.net/) 12 com tema Fluent |
| Editor XML | [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) (destaque de sintaxe XML embutido) |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) (`[ObservableProperty]`, `[RelayCommand]`) |
| XSD e validação | `System.Xml.Schema` do .NET (XSD 1.0) |
| Testes | xUnit |
| Textos da interface | `.resx` com classe fortemente tipada (`Strings`) gerada no build |

Sem dependências nativas além das do Avalonia; o Core só usa a BCL.

## Estrutura do repositório

```
xsd-visualizer/
├── CONTEXT.md                     glossário do domínio
├── docs/
│   ├── adr/                       decisões de arquitetura (ADRs)
│   └── spec/                      especificação da v1 (user stories, decisões)
├── src/
│   ├── XsdVisualizer.Core/        domínio: carga, árvore, geração, validação (sem UI)
│   │   └── W3C/                   xmldsig e xml.xsd embutidos para uso offline
│   └── XsdVisualizer.App/         aplicativo Avalonia (MVVM)
│       ├── ViewModels/
│       ├── Views/
│       ├── Services/              sessão, monitoramento de pastas, diálogos
│       └── Resources/Strings.resx textos da interface (PT)
├── tests/
│   ├── XsdVisualizer.Core.Tests/  testes do Core pela fachada pública
│   └── Fixtures/                  pacotes reais da SEFAZ (NF-e PL_010, NFGas)
└── publish.sh                     executáveis self-contained por plataforma
```

## Arquitetura

Duas camadas com dependência num único sentido: **App → Core**. Todo o comportamento testável mora no Core; o App é uma casca fina de view models e views sobre ele.

```
 ┌────────────────────────── XsdVisualizer.App ───────────────────────────┐
 │  Views (XAML)  ⇄  ViewModels (MVVM)  →  Services (sessão, watcher)     │
 └──────────────────────────────┬─────────────────────────────────────────┘
                                │ usa só a fachada pública
 ┌──────────────────────────────▼───────── XsdVisualizer.Core ────────────┐
 │  SchemaSetLoader ──► SchemaSet ──► GlobalElement ──► SchemaNode (árvore)│
 │        │                               │                               │
 │  SchemaResolver                        ├─► SampleGenerator ─► Sample   │
 │  (W3C → cache → download)              │      ├ ValueGenerator         │
 │                                        │      ├ XsdRegexGenerator      │
 │                                        │      └ CoverageChoices        │
 │  DocumentBinding    SampleExporter     └─► Validate → ValidationIssue  │
 └────────────────────────────────────────────────────────────────────────┘
```

### Core (`XsdVisualizer.Core`)

A fachada pública é pequena:

| Tipo | Papel |
|---|---|
| `SchemaSetLoader` | `Open(pasta ou .xsd)` → `SchemaSet`. Recebe um `ISchemaDownloader` e a pasta de cache (injeção para testes). |
| `SchemaSet` | Pasta aberta: `GlobalElements` e `LoadIssues`. |
| `GlobalElement` | `Tree`, `GenerateMaximal(pins)`, `GenerateMinimal()`, `GenerateCoverageSet()`, `Validate(xml)`. |
| `SchemaNode` | Nó da árvore (elemento, atributo, compositor, wildcard), com filhos construídos sob demanda. |
| `Sample` | XML gerado + tipo, número, o que cobre e suas Validation Issues. |
| `DocumentBinding` | Encontra os Global Elements candidatos para a raiz de um XML. |
| `SampleExporter` | "Gerar todos" de um Schema Set. |
| `ValidationIssue` | Mensagem + arquivo, linha, coluna e severidade. |

Como as peças internas funcionam:

- **Carga** (`SchemaSetLoader`): cada *arquivo raiz* da pasta (um XSD que nenhum outro inclui) é compilado num `XmlSchemaSet` próprio. Pastas reais misturam versões que redefinem os mesmos nomes, e compilar tudo junto quebra (ADR-0003). Por isso um Global Element é identificado por nome **e** arquivo declarante. Arquivos malformados, erros de compilação e construções XSD 1.1 viram Validation Issues de carga (ADR-0002).
- **Árvore** (`SchemaTreeBuilder`): lê o schema compilado (`ContentTypeParticle`, `AttributeUses`) e monta `SchemaNode`s de forma preguiçosa. Substitution groups e tipos abstratos/derivados aparecem como um nó *Choice* de alternativas. Recursão é marcada, não expandida.
- **Geração** (`SampleGenerator`): percorre a árvore escrevendo com `XmlWriter`. As regras:
  - Repetições: o Minimal usa `minOccurs`; o Maximal usa `max(minOccurs, min(maxOccurs, 2))`.
  - Recursão: expandida uma vez; dali para baixo, só o mínimo.
  - Wildcards opcionais são omitidos; um obrigatório `lax`/`skip` ganha um `<exemplo/>`.
  - As escolhas de ramo e de enumeração vêm de uma estratégia plugável (`IGenerationChoices`): ramos fixados (`PinnedChoices`) ou cobertura gulosa (`CoverageChoices`, ADR-0001).
- **Valores** (`ValueGenerator` + `XsdRegexGenerator`):
  - Candidatos determinísticos a partir de enumerações, `pattern` (um gerador próprio de strings para regex XSD, com classes, subtração, `\i`, `\c`, `\p{…}`), tamanhos, dígitos e faixas.
  - Cada candidato é conferido por `XmlSchemaDatatype.ParseValue`, que aplica todos os facets da cadeia de tipos.
  - No fim, o Sample inteiro é validado. Se algo não for satisfazível, ele sai marcado como inválido, nunca descartado.
- **Validação**: `GlobalElement.Validate` usa o `XmlSchemaSet` da unidade que declarou o elemento.

### App (`XsdVisualizer.App`)

- **`MainViewModel`**: orquestra Schema Sets abertos, seleção e pesquisa na árvore, geração, exportação, abas e salvamento. Trabalho pesado (carga, geração, validação) roda em `Task.Run`.
- **`SchemaNodeViewModel`**: embrulha `SchemaNode`; a seleção vem de `IsSelected` nos nós (o `SelectedItem` do `TreeView` perde nós ainda não renderizados). Choices mostram um seletor de ramo que alimenta os *pins* do Maximal.
- **`EditorTabViewModel`**: uma aba é um Document (com Binding e "salvar") ou um conjunto de Samples (com seletor de Sample e painel "cobre"). O texto vive num `TextDocument` do AvaloniaEdit; alterações disparam revalidação com atraso de 400 ms.
- **Views**:
  - `MainWindow`: layout em três colunas, arrastar e soltar, e os diálogos via `IDialogService`.
  - `XmlEditorView`: editor, lista de issues com clique para ir à posição, e `IssueUnderlineRenderer` para o sublinhado ondulado.
  - `AboutWindow` e `ConfirmDialog`.
- **Services**:
  - `SessionStore`: JSON em `LocalApplicationData/XsdVisualizer/session.json`.
  - `SchemaFolderWatcher`: `FileSystemWatcher` com debounce.
- Caminhos passados na linha de comando abrem como se tivessem sido arrastados.

## Decisões registradas

- [ADR-0001](docs/adr/0001-coverage-set-em-vez-de-produto-cartesiano.md): Coverage Set em vez de produto cartesiano.
- [ADR-0002](docs/adr/0002-apenas-xsd-1-0.md): apenas XSD 1.0.
- [ADR-0003](docs/adr/0003-compilacao-por-arquivo-raiz.md): compilação por arquivo raiz.
- [Spec da v1](docs/spec/0001-xsd-visualizer-v1.md): user stories, decisões de implementação e o que ficou fora.

## Build, execução e testes

### Pré-requisito

[.NET SDK 10](https://dotnet.microsoft.com/download). Confira com `dotnet --list-sdks` (deve listar `10.0.x`).

Se o SDK foi instalado pelo script `dotnet-install.sh` (em `~/.dotnet`, sem sudo), coloque-o no `PATH`:

```bash
export PATH="$HOME/.dotnet:$PATH"   # adicione ao ~/.zshrc para ficar permanente
```

Todos os comandos abaixo rodam na raiz do repositório (onde está `XsdVisualizer.slnx`).

### Build

```bash
dotnet build                        # solution inteira, Debug
dotnet build -c Release             # Release
dotnet build src/XsdVisualizer.App  # só o app (compila o Core junto)
```

O primeiro build restaura os pacotes NuGet. A classe `Strings` (textos da interface) é gerada a partir de `Resources/Strings.resx` durante o build, não fica versionada.

### Executar

```bash
dotnet run --project src/XsdVisualizer.App

# já abrindo Schema Sets e Documents (pastas, .xsd ou .xml), como se tivessem sido arrastados
dotnet run --project src/XsdVisualizer.App -- tests/Fixtures/PL_NFGas_NT2026.002_RTC_1.01 caminho/do/documento.xml
```

### Testes

```bash
dotnet test                                                   # tudo (~5 s)
dotnet test --filter "Category!=Integration"                  # só os rápidos, sem os pacotes reais
dotnet test --filter "Category=Integration"                   # só a integração com NF-e e NFGas
dotnet test --filter "FullyQualifiedName~CoverageSetTests"    # uma classe de testes
dotnet test --filter "DisplayName~Recursion"                  # testes cujo nome contém um trecho
```

Os testes ficam em `tests/XsdVisualizer.Core.Tests`, um arquivo por área: abertura de Schema Sets, árvore, geração, Coverage Set, validação, Binding, schemas remotos, exportação e pacotes reais. Um teste que falha na integração lista os Samples inválidos com arquivo, linha e mensagem.

### Publicar executáveis

```bash
./publish.sh                 # win-x64, osx-arm64, osx-x64 e linux-x64
./publish.sh osx-arm64       # só uma plataforma
```

A saída vai para `dist/<rid>/` (ignorado pelo git): um executável único self-contained, com cerca de 100 MB, que roda sem o .NET instalado. No macOS, rode `./dist/osx-arm64/XsdVisualizer`. No Windows, `dist\win-x64\XsdVisualizer.exe`.

## Testes

Os testes exercitam só a fachada pública do Core, com XSDs sintéticos pequenos (um por caso: choice, recursão, substitution group, facets, wildcard, import remoto, XSD 1.1…) escritos em pastas temporárias.

- **Oráculo:** o validador do próprio .NET. Todo Sample gerado precisa validar contra o seu Schema Set.
- **Cobertura:** verificada lendo os XMLs gerados, sem olhar como o gerador decidiu.
- **Integração:** gera o Coverage Set e o Minimal de **todos** os Global Elements dos pacotes reais da NF-e (PL_010_V1.30) e da NFGas e exige que todos validem.
- **Rede:** schemas remotos são testados com um `ISchemaDownloader` falso, sem rede.

A camada de interface não tem testes automatizados na v1.
