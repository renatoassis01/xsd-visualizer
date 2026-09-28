# XSD Visualizer

🇧🇷 [Versão em português](README.md)

A cross-platform desktop app (Windows, macOS, Linux) for exploring sets of XSD and WSDL files, generating valid sample XML and SOAP envelopes, validating existing XML and envelopes, and comparing two versions of the same set of schemas. It was created to keep up with the Brazilian tax authority's (SEFAZ) electronic invoice schemas (NF-e, NFGas, NFCom…), but it is generic: it works with any XSD 1.0.

📖 **[User guide](docs/manual/en.md)**: how to use each screen, with screenshots. · [Manual do usuário (português)](docs/manual/pt-BR.md)

![Main window](docs/images/en/main.png)

## What it does

- **Opens Schema Sets:** pick or drop a folder of XSDs (or a single `.xsd`/`.wsdl`, which opens its folder). Includes and imports are resolved; remote imports use embedded W3C schemas first, then a local cache, and only then a download.
- **Shows the structure** of each Global Element in a searchable tree: cardinality, type, `sequence`/`choice`/`all`, facets, documentation, recursion, substitution groups, `xsi:type` and wildcards.
- **Generates valid, deterministic Samples:**
  - **Minimal:** only what the Schema Set requires.
  - **Maximal:** every optional item; for each set of alternatives, the branch chosen in the tree.
  - **Coverage Set:** the smallest set of Samples in which every choice branch, optional item and enumeration value appears at least once.
  - **Generate all:** writes everything to `<output>/<Schema Set>/<Global Element>/<name>.{max,min,cov-NN}.xml`.
- **Validates Documents:** a dropped XML file is bound (Binding) to the right Global Element and validated in an editor with syntax highlighting, live revalidation as you type, and underlined Validation Issues.
- **Services (WSDL):** the folder's `.wsdl` files appear as Services → Operations. For each Operation you choose the Endpoint (SOAP 1.1/1.2) and the **Payload Binding** (which Global Element goes in the Body and whether it is gzip+base64 compressed), and the same buttons generate complete SOAP **Envelopes**. A captured envelope is validated in three layers: SOAP, the WSDL Body and the Payload. The app does not call the services.
- **Compares Schema Sets:** pick a Before and an After to see which Global Elements were added, removed or changed, the tree of changes (type, cardinality, facets, enumerations, documentation) and a side-by-side diff of the Samples. The summary can be exported as Markdown.
- **Themes and language:** Same as system, Light, Dark, GitHub Light/Dark, Dracula, Gruvbox Light and Andromeda; interface in Portuguese or English.
- **Session:** reopens the Schema Sets from last time, keeps recents, Payload Bindings and preferences, and reloads automatically when an XSD or WSDL changes on disk.

The vocabulary (Schema Set, Global Element, Sample, Coverage Set, Document, Binding, Validation Issue, Service, Operation, Payload, Payload Binding, Envelope, Comparison, Element Pair, Change) is defined in [`CONTEXT.md`](CONTEXT.md) and used the same way in the code and in the interface.

Design documents (glossary, ADRs, specs) are written in Portuguese.

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 (C#) |
| UI | [Avalonia UI](https://avaloniaui.net/) 12 with the Fluent theme |
| XML editor | [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) (`[ObservableProperty]`, `[RelayCommand]`) |
| XSD and validation | .NET `System.Xml.Schema` (XSD 1.0) |
| Text diff | [DiffPlex](https://github.com/mmanela/diffplex) |
| Tests | xUnit |
| UI strings | `Strings.resx` (Portuguese) and `Strings.en.resx` (English), with a `Strings` class generated at build time |

No native dependencies beyond Avalonia's; the Core only uses the BCL and DiffPlex.

## Repository layout

```
xsd-visualizer/
├── CONTEXT.md                      domain glossary
├── docs/
│   ├── manual/                     user guide (pt-BR and en)
│   ├── images/                     screenshots for the guide
│   ├── adr/                        architecture decision records
│   └── spec/                       specs (user stories, decisions)
├── src/
│   ├── XsdVisualizer.Core/         domain: loading, tree, generation, validation, WSDL, comparison (no UI)
│   │   └── W3C/                    embedded xmldsig, xml.xsd and SOAP 1.1/1.2 envelope schemas
│   └── XsdVisualizer.App/          Avalonia app (MVVM)
│       ├── ViewModels/
│       ├── Views/
│       ├── Themes/                 theme catalog and how it is applied to the UI
│       ├── Services/               session, folder watching, dialogs
│       └── Resources/              UI strings (pt-BR and en)
├── tests/
│   ├── XsdVisualizer.Core.Tests/   Core tests through the public facade
│   ├── XsdVisualizer.App.Tests/    theme catalog tests
│   └── Fixtures/                   real SEFAZ packages (XSDs and WSDLs)
└── publish.sh                      self-contained executables per platform
```

## Architecture

Two layers with a one-way dependency: **App → Core**. All testable behavior lives in the Core; the App is a thin shell of view models and views on top of it.

```
 ┌─────────────────────────── XsdVisualizer.App ────────────────────────────┐
 │  Views (XAML) ⇄ ViewModels (MVVM) → Services (session, watcher) · Themes │
 └───────────────────────────────┬──────────────────────────────────────────┘
                                 │ uses only the public facade
 ┌───────────────────────────────▼────────── XsdVisualizer.Core ────────────┐
 │  SchemaSetLoader ──► SchemaSet ──► GlobalElement ──► SchemaNode (tree)    │
 │        │                │               │                                │
 │  SchemaResolver         │               ├─► SampleGenerator ─► Sample    │
 │  (W3C → cache → net)    │               │     ├ ValueGenerator           │
 │                         │               │     ├ XsdRegexGenerator        │
 │  WsdlReader ────────────┴─► Service     │     └ CoverageChoices          │
 │                     └► Operation ─► Envelopes (Sample + SampleEnvelope)  │
 │                                         └─► Validate → ValidationIssue   │
 │  DocumentBinding   SampleExporter   Comparison ─► StructuralDiff         │
 │                                                 └► SampleDiff (DiffPlex) │
 └──────────────────────────────────────────────────────────────────────────┘
```

### Core (`XsdVisualizer.Core`)

The public facade is small:

| Type | Role |
|---|---|
| `SchemaSetLoader` | `Open(folder, .xsd or .wsdl)` → `SchemaSet`. Takes an `ISchemaDownloader` and a cache folder (injected in tests). |
| `SchemaSet` | An opened folder: `GlobalElements`, `Services` and `LoadIssues`. |
| `GlobalElement` | `Tree`, `GenerateMaximal(pins)`, `GenerateMinimal(through)`, `GenerateCoverageSet()`, `Validate(xml)`. |
| `SchemaNode` | A tree node (element, attribute, compositor, wildcard), with children built on demand. |
| `Sample` | Generated XML + kind, number, what it covers and its Validation Issues. An Envelope also carries a `SampleEnvelope` (message, Payload Binding and readable Payload). |
| `DocumentBinding` | Finds the candidate Global Elements (or Operations, for SOAP envelopes) for an XML root. |
| `SampleExporter` | "Generate all" for a Schema Set, including the Envelopes of Operations that have a Payload Binding. |
| `ValidationIssue` | Message + file, line, column and severity (and whether it points into an Envelope's decompressed Payload). |
| `Service` / `Endpoint` / `Operation` / `OperationMessage` | What was read from the WSDLs. `OperationMessage.GenerateEnvelopes(...)` and `ValidateEnvelope(...)`. |
| `PayloadBinding` | The Payload's Global Element for a Request/Response + whether it is compressed. |
| `Comparison` | `Compare(before, after)` → Element Pairs with a `ChangeKind`; each pair provides the tree of Changes, the side-by-side Sample diff and the Markdown summary (`ToMarkdown`). |

How the internals work:

- **Loading** (`SchemaSetLoader`): each *root file* in the folder (an XSD no other file includes) is compiled into its own `XmlSchemaSet`. Real folders mix versions that redefine the same names, and compiling them together fails (ADR-0003). That is why a Global Element is identified by name **and** declaring file. Malformed files, compilation errors and XSD 1.1 constructs become load-time Validation Issues (ADR-0002).
- **Tree** (`SchemaTreeBuilder`): reads the compiled schema and lazily builds `SchemaNode`s. Substitution groups and abstract/derived types appear as a *Choice* node of alternatives. Recursion is marked, not expanded.
- **Generation** (`SampleGenerator`): walks the tree writing with `XmlWriter`.
  - Repetitions: the Minimal uses `minOccurs`; the Maximal uses `max(minOccurs, min(maxOccurs, 2))`.
  - Recursion: expanded once; below that, only the minimum.
  - Optional wildcards are omitted; a required `lax`/`skip` one gets an `<exemplo/>` placeholder.
  - Branch and enumeration choices come from a pluggable strategy (`IGenerationChoices`): pinned branches (`PinnedChoices`) or greedy coverage (`CoverageChoices`, ADR-0001).
- **Values** (`ValueGenerator` + `XsdRegexGenerator`): deterministic candidates from enumerations, `pattern` (a dedicated string generator for XSD regex), lengths, digits and ranges, checked by `XmlSchemaDatatype.ParseValue`. IDs and `key`/`unique` values are distinct. Finally the whole Sample is validated; if something cannot be satisfied, it is marked invalid, never dropped.
- **WSDL** (`WsdlReader`, `Envelopes`): each WSDL 1.1 becomes Services/Endpoints/Operations; the `wsdl:types` schemas are compiled separately (ADR-0004). Envelopes are built around the Payload generated from the bound Global Element. Validating an Envelope combines the SOAP, WSDL and Payload XSDs in a single pass; a compressed Payload is decompressed and validated on its own.
- **Comparison** (`Comparison`, `StructuralDiff`, `SampleDiff`): pairs are matched by namespace + name + file and, failing that, by namespace + name. Both trees are walked in parallel by name path, producing Changes (Added, Removed, Modified, Documentation-only). The side-by-side diff generates both Samples with the same rules, going through the branch of the selected Change, and compares them line by line with DiffPlex.

### App (`XsdVisualizer.App`)

- **`MainViewModel`** orchestrates open Schema Sets, generation, export, tabs and saving; it delegates the tree and search to **`SchemaTreeViewModel`** and Payload Bindings to **`PayloadBindingStore`**. Heavy work runs on `Task.Run`.
- **`SchemaNodeViewModel`** wraps `SchemaNode`; choices show a branch selector that feeds the Maximal's *pins*. **`OperationViewModel`** handles the Endpoint, Request/Response and Payload Binding.
- **`EditorTabViewModel`**: a tab is either a Document (with Binding and "save") or a set of Samples (with a selector and a "covers" panel). The text lives in an AvaloniaEdit `TextDocument`; edits trigger revalidation after a 400 ms delay.
- **`ComparisonViewModel`**: pairs, the tree of Changes with filters and navigation, and the side-by-side diff (Maximal/Minimal), regenerated when the selected Change is in another branch.
- **Themes:** `ThemeCatalog` is plain data (no Avalonia) with each theme's colors; `ThemeApplier` switches the Fluent variant and palette and the app's resources on the fly.
- **Views:** `MainWindow`, `XmlEditorView` (editor, issues and underlines), `ComparisonWindow`, `CompareDialog`, `SettingsWindow`, `AboutWindow` and `ConfirmDialog`.
- **Services:** `SessionStore` (JSON at `LocalApplicationData/XsdVisualizer/session.json`, or at the path in `XSDVISUALIZER_SESSION`) and `SchemaFolderWatcher` (debounced `FileSystemWatcher`).
- Paths passed on the command line open as if they had been dropped on the window.

## Recorded decisions

- [ADR-0001](docs/adr/0001-coverage-set-em-vez-de-produto-cartesiano.md): Coverage Set instead of a cartesian product.
- [ADR-0002](docs/adr/0002-apenas-xsd-1-0.md): XSD 1.0 only.
- [ADR-0003](docs/adr/0003-compilacao-por-arquivo-raiz.md): compile per root file.
- [ADR-0004](docs/adr/0004-apenas-wsdl-1-1-document-literal.md): WSDL 1.1 document/literal only.
- Specs: [v1](docs/spec/0001-xsd-visualizer-v1.md), [WSDL](docs/spec/0002-wsdl.md), [Comparison](docs/spec/0003-comparacao.md), [Themes](docs/spec/0004-temas.md) and [Refactoring](docs/spec/0005-refatoracao.md).

## Building, running and testing

### Prerequisite

[.NET SDK 10](https://dotnet.microsoft.com/download). Check with `dotnet --list-sdks` (it should list `10.0.x`).

If the SDK was installed with the `dotnet-install.sh` script (into `~/.dotnet`, without sudo), add it to your `PATH`:

```bash
export PATH="$HOME/.dotnet:$PATH"   # add to ~/.zshrc to make it permanent
```

All commands below run from the repository root (where `XsdVisualizer.slnx` is).

### Build

```bash
dotnet build                        # whole solution, Debug
dotnet build -c Release             # Release
dotnet build src/XsdVisualizer.App  # just the app (builds the Core too)
```

The first build restores the NuGet packages. The `Strings` class is generated from the `.resx` files at build time and is not checked in.

### Run

```bash
dotnet run --project src/XsdVisualizer.App

# opening Schema Sets and Documents right away (folders, .xsd, .wsdl or .xml), as if they had been dropped
dotnet run --project src/XsdVisualizer.App -- tests/Fixtures/PL_NFGas_NT2026.002_RTC_1.01 path/to/document.xml

# with a separate session, leaving yours untouched
XSDVISUALIZER_SESSION=/tmp/test-session.json dotnet run --project src/XsdVisualizer.App
```

### Tests

```bash
dotnet test                                                   # everything
dotnet test --filter "Category!=Integration"                  # fast tests only, without the real packages
dotnet test --filter "Category=Integration"                   # only the integration tests with real packages
dotnet test --filter "FullyQualifiedName~CoverageSetTests"    # one test class
```

- **Core** (`tests/XsdVisualizer.Core.Tests`), one file per area: opening, tree, generation, Coverage Set, validation, Binding, remote schemas, export, WSDL (loading, envelopes, validation), comparison (structural and Sample diff) and real packages.
- **App** (`tests/XsdVisualizer.App.Tests`): the theme catalog (every theme defines every color; WCAG contrast for text, syntax, diff and underlines; light/dark mode consistent with the background).
- **Output snapshot:** `OutputSnapshotTests` stores a hash of every XML generated from the fixtures in `Snapshots/outputs.sha256`, so a refactoring cannot silently change any output. If a change is intentional, regenerate with `XSDVIS_UPDATE_SNAPSHOTS=1 dotnet test --filter OutputSnapshotTests` and review the diff.

How the tests are designed:

- They only exercise the public facade, with small synthetic XSDs (one per case) written to temporary folders.
- **Oracle:** .NET's own validator. Every generated Sample and Envelope must validate.
- **Integration:** Coverage Set and Minimal for every Global Element in NF-e (PL_010_V1.30) and NFGas; NF-e 4.00 and NFCom WSDLs; comparison PL_009_V4 → PL_010_V1.30.
- **Network:** remote schemas are tested with a fake `ISchemaDownloader`, without network access.
- The UI (views) has no automated tests.

### Publishing executables

```bash
./publish.sh                 # win-x64, osx-arm64, osx-x64 and linux-x64
./publish.sh osx-arm64       # a single platform
```

Output goes to `dist/<rid>/` (git-ignored): a single self-contained executable of about 100 MB that runs without .NET installed. On macOS, run `./dist/osx-arm64/XsdVisualizer`; on Windows, `dist\win-x64\XsdVisualizer.exe`.
