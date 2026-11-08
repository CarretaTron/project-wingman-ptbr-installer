# Project Wingman PT-BR — Installer source

Source code of the Windows installer for **Project Wingman PT-BR v0.91**, a Brazilian Portuguese fan translation by **Carreta**. Published for inspection, rebuilding, and executable review.

The application is written in C# using Windows Forms and .NET Framework. It displays a game-folder picker, validates the selected installation, extracts and verifies three bundled files, backs up existing target files, and attempts rollback if file replacement fails.

## Build and review

See **[BUILDING.md](BUILDING.md)** for requirements, compilation commands, preview generation, and tests. The installer interface and the synthetic test suite can be built without the game or translation payload.

```powershell
.\Instalador\compilar.ps1 -SomenteInterface
.\Instalador\testar.ps1 -Rapido
```

The scripts use the Windows .NET Framework C# compiler. There are no NuGet packages, runtime downloads, or package-manager installation steps.

## What is included

| File | Purpose |
| --- | --- |
| `Instalador/InstallerUI.cs` | Window, folder selection, progress, and optional elevation |
| `Instalador/FolderPicker.cs` | Native Explorer-style folder picker via COM |
| `Instalador/InstallerCore.cs` | Package creation/reading, SHA-256 checks, destination validation, backup, and rollback |
| `Instalador/BuildPackage.cs` | Command-line entry point for creating the self-extracting package |
| `Instalador/InstallerTests.cs` | Synthetic integration tests and optional full-payload verification |
| `Instalador/app.manifest` | Application privileges, DPI, and Windows compatibility metadata |
| `Instalador/assets/` | Original visual resources used by the interface |
| `audit/` | Bundled-file manifest and hash of the local distributed installer |

## Scope of this source publication

This repository contains the installer source and its visual resources. It does **not** contain the game's source code, the modified game executable, the translation `.pak` files, or a precompiled installer.

The full installer packages these separate input files:

```text
ProjectWingman/Binaries/Win64/ProjectWingman-Win64-Shipping.exe
ProjectWingman/Content/Paks/~mods/ProjectWingman_PTBR_Assets_P.pak
ProjectWingman/Content/Paks/~mods/ProjectWingman_PTBR_Locres_P.pak
```

**Reviewing this installer source does not audit or establish the safety of the modified game executable or the `.pak` contents.** Those inputs require their own review. The installer copies them as supplied; it does not patch the game executable at installation time.

The runtime C# files and visual assets were copied from the local installer project without modification for this publication. The published PowerShell wrappers additionally accept external payload/package paths, so reviewers can work from a source-only checkout. Their packaging wrapper also refuses to overwrite an existing output file. Test fixtures use a short temporary-directory root to accommodate .NET Framework path-length limits.

## Runtime behavior relevant to reviewers

- No network requests or downloads are implemented by the installer.
- The default application privilege is `asInvoker`. When file access is denied, the UI can relaunch itself through Windows `runas` for the selected destination.
- Steam library locations are checked locally to suggest an existing game installation.
- Payload is a ZIP appended to the executable, containing three allowed paths and `payload.manifest`.
- Every payload file is extracted into a staging directory and checked against its SHA-256 before replacement begins.
- Existing target files are backed up under `Backup_Traducao_PTBR_v0.91` in the game directory. Existing backups are retained.
- Temporary prior files are retained during replacement for rollback. If restoration fails, the recovery directory is kept and its path is reported.
- No service, scheduled task, startup entry, browser component, or telemetry is installed by this code.

## Binary identification

[`audit/SHA256SUMS.txt`](audit/SHA256SUMS.txt) identifies the local installer supplied for this publication. [`audit/payload.manifest`](audit/payload.manifest) was read from that installer's appended archive and records the uncompressed sizes and SHA-256 hashes of its three inputs.

These are integrity records, not digital signatures or a statement of approval by Nexus Mods. Builds are **not promised to be byte-for-byte reproducible**: compiler and ZIP metadata can differ. The repository does not claim that a Nexus-hosted file has the recorded hash unless independently compared with that file.

## Credits and rights

Translation by Carreta. Fan project, not officially affiliated with Project Wingman's developers or publishers. Project Wingman branding, artwork, and game content belong to their respective rights holders. This publication does not grant rights to third-party assets or game files. No additional software license is selected in this publication.

## Português

Este é o código-fonte do **instalador** da tradução PT-BR de Project Wingman. As instruções de compilação estão em [BUILDING.md](BUILDING.md). Os arquivos do jogo e os `.pak` da tradução são entradas externas e não estão no repositório. A análise deste código não substitui a análise desses arquivos.
