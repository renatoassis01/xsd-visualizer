---
status: ready-for-agent
---

# Distribuição: repositório público, CI e releases

## Problem Statement

O XSD Visualizer só existe na máquina de quem o desenvolve. Para usá-lo, outra pessoa precisa instalar o .NET SDK, clonar e compilar. Não há licença, então ninguém sabe se pode usar ou contribuir, e nada garante que os testes passam em Windows e Linux.

## Solution

O projeto vai para um repositório público no GitHub (`renatoassis01/xsd-visualizer`) com licença MIT. O GitHub Actions roda os testes nos três sistemas a cada push e PR, e cada tag `v*` gera um release com pacotes prontos para Windows, macOS e Linux, em x64 e arm64, que rodam sem o .NET instalado.

## User Stories

1. Como usuário, quero baixar o app pronto na página de Releases do GitHub, sem instalar o .NET nem compilar.
2. Como usuário de Windows, quero um instalador com atalho no menu Iniciar e desinstalador, e também um `.zip` portátil.
3. Como usuário de macOS, quero um `.dmg` com o `.app` (ícone e nome no Dock), para Apple Silicon e Intel.
4. Como usuário de macOS, quero instruções claras para abrir o app na primeira vez, já que ele não é assinado com um Developer ID.
5. Como usuário de Linux, quero um AppImage, um `.deb` e um `.tar.gz`, para x64 e arm64.
6. Como usuário, quero conferir os downloads com `SHA256SUMS.txt`.
7. Como usuário, quero ver a versão no Sobre, igual à do release.
8. Como mantenedor, quero publicar uma versão só criando uma tag `v*`.
9. Como mantenedor, quero que o release só saia se os testes passarem.
10. Como mantenedor, quero notas de release geradas a partir dos commits, com as instruções de instalação no topo.
11. Como contribuidor, quero ver a licença e o status do CI no README.
12. Como contribuidor, quero que todo PR rode build e testes em Linux, macOS e Windows.
13. Como mantenedor, quero que o histórico publicado não exponha meu e-mail pessoal.

## Implementation Decisions

- **Licença:** MIT. As dependências são MIT (Avalonia, AvaloniaEdit, CommunityToolkit) e Apache-2.0 (DiffPlex), compatíveis.
- **Histórico:** antes do primeiro push, os commits passam a usar o e-mail `noreply` do GitHub.
- **Versão:** vem da tag (`v1.2.0` → `1.2.0`) e é passada ao build; o `.csproj` guarda a versão padrão para builds locais. Primeiro release: v1.2.0.
- **Finais de linha:** LF para o código em todos os sistemas; schemas de terceiros (fixtures e W3C) ficam byte a byte.
- **Pacotes:** scripts em `packaging/`, um por sistema, que também rodam à mão.
  - macOS: publish self-contained dentro de `XSD Visualizer.app`, com assinatura ad hoc (exigida no Apple Silicon), em `.dmg` e `.zip`.
  - Windows: publish single-file, instalador Inno Setup (por usuário, sem administrador, em português e inglês) e `.zip`.
  - Linux: publish single-file, AppImage (appimagetool), `.deb` (`/opt/xsd-visualizer`, comando `xsd-visualizer`, atalho e ícone) e `.tar.gz`. Cada arquitetura empacota num runner nativo.
- **CI:** build + testes em ubuntu, macos e windows a cada push na `main` e PR.
- **Release:** tag `v*` → testes → seis pacotes em paralelo → release no GitHub com arquivos, checksums e notas.

## Testing Decisions

- Os testes do código não mudam; o CI passa a rodá-los nos três sistemas.
- O empacotamento é verificado rodando os scripts: macOS local (o `.app` abre), Linux num container, Windows e o release no próprio GitHub Actions.

## Out of Scope

- Homebrew (tap próprio) e winget: entram numa etapa seguinte, a partir dos arquivos do release.
- Assinatura de código (Apple Developer ID e notarização; certificado para Windows).
- Versão web (WebAssembly).
- `.rpm` e Flatpak.
