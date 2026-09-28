## Download

| System | File |
|---|---|
| **Windows** | `XsdVisualizer-@VERSION@-windows-x64-setup.exe` (installer) or `…-windows-x64.zip` (portable) · ARM: `…-windows-arm64-…` |
| **macOS** Apple Silicon | `XsdVisualizer-@VERSION@-macos-arm64.dmg` |
| **macOS** Intel | `XsdVisualizer-@VERSION@-macos-x64.dmg` |
| **Linux** | `XsdVisualizer-@VERSION@-linux-x64.AppImage`, `.deb` or `.tar.gz` · ARM: `…-linux-arm64.…` |

The builds are self-contained: .NET does not need to be installed.

**macOS with Homebrew:** `brew install --cask renatoassis01/tap/xsd-visualizer` (allows the app to open; update with `brew upgrade`).

**macOS from the `.dmg`:** the app is not signed with an Apple Developer ID. The first time, macOS says it cannot verify the app. Drag it to Applications and run `xattr -dr com.apple.quarantine "/Applications/XSD Visualizer.app"` in the Terminal, or open it and go to *System Settings → Privacy & Security → Open Anyway*.

**Windows:** SmartScreen may say "Windows protected your PC" because the installer is not signed. Click *More info → Run anyway*.

**Linux:** `chmod +x XsdVisualizer-*.AppImage` and run it, or install the package with `sudo apt install ./XsdVisualizer-*.deb`.

Checksums are in `SHA256SUMS.txt`.
