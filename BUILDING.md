# Building and testing

Run the commands below in **Windows PowerShell**, from the repository root. Paths containing spaces must be quoted.

## Requirements

- 64-bit Windows.
- .NET Framework 4.5 or later, with the C# compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and the framework assemblies used by the scripts. Windows 10/11 with .NET Framework 4.x installed is the intended environment.
- No external C# libraries, NuGet packages, or network access are needed for compilation or for the application.
- Building the complete installer additionally requires the three separately obtained translation payload files listed below and sufficient disk space for the output package.

## 1. Compile the interface without payload

```powershell
.\Instalador\compilar.ps1 -SomenteInterface
```

Output: `Instalador\build\Interface.exe`. This is the interface stub, not a complete distributable installer. It can show the UI and folder picker; it cannot install the translation until a payload is appended.

Optional preview, saved to a path you choose:

```powershell
.\Instalador\build\Interface.exe --preview "$PWD\previa.png"
```

## 2. Run the synthetic tests

```powershell
.\Instalador\testar.ps1 -Rapido
```

The tests create small artificial executables and `.pak` files inside a dedicated generated directory under `%TEMP%\PW`. The short fixture root avoids .NET Framework's legacy path-length limit in deep repository checkouts. An alternate short root can be supplied with `-DiretorioTestes`. They verify path validation, SHA-256 checks, initial installation, backup preservation on reinstall, retention of unrelated mods, locked-file rejection, corrupt-payload rejection, and rollback after a simulated replacement failure. They do not install into a real game folder.

On success, the generated fixture directory is removed. On failure, its path is printed and retained for diagnosis. The test program itself is `Instalador\build\InstallerTests.exe`.

If your environment redirects `%TEMP%` to a very long or restricted sandbox path, provide a short writable directory explicitly, for example `-DiretorioTestes "D:\PW-Tests"`. Use a directory you are authorized to write to. The publication was validated with an explicit short fixture root; all 26 synthetic checks passed.

## 3. Build a full installer with external inputs

Arrange the separately obtained payload under a directory such as `D:\PW-Payload`:

```text
D:\PW-Payload\
  ProjectWingman\Binaries\Win64\ProjectWingman-Win64-Shipping.exe
  ProjectWingman\Content\Paks\~mods\ProjectWingman_PTBR_Assets_P.pak
  ProjectWingman\Content\Paks\~mods\ProjectWingman_PTBR_Locres_P.pak
```

Then run:

```powershell
.\Instalador\compilar.ps1 -PastaTraducao "D:\PW-Payload" -Saida "$PWD\ProjectWingman-PTBR-Setup.exe"
```

Use an output filename that does not already exist. Do not point the output at the modified game executable. The wrapper compiles the interface and packager, then passes the interface, payload root, and output path to `BuildPackage.exe`.

The original layout also works: place the external `ProjectWingman` directory beside `Instalador` and run `compilar.ps1` without `-PastaTraducao`. An existing output is not overwritten by the published wrapper.

The packager compresses the three files, records their hashes in `payload.manifest`, and appends a ZIP to the interface executable. It then opens the resulting package to validate its manifest. A full install additionally verifies each extracted file's SHA-256.

## 4. Verify a complete package against the supplied inputs

```powershell
.\Instalador\testar.ps1 -Pacote "$PWD\ProjectWingman-PTBR-Setup.exe" -PastaTraducao "D:\PW-Payload"
```

This runs the synthetic tests and extracts the actual payload into an isolated generated fixture. Installed files are compared with both the package manifest and the supplied source payload. It requires additional free disk space. It does not launch the game or test the translation in gameplay.

## Relationship to the distributed binary

The C# runtime source, application manifest, and visual assets in this repository were copied from the local v0.91 project. The PowerShell wrapper changes are limited to accepting external payload/package paths, ensuring build directories exist, using a short temporary-directory fixture root for .NET Framework path-length compatibility, and refusing to overwrite an existing output in the packaging wrapper.

The published source does not produce a complete installer without the separate payload inputs. The manifest in `audit/` documents the exact inputs found in the local distributed installer; compare your input hashes with it if you need to rebuild the same payload. PE timestamps and ZIP metadata can produce different whole-installer hashes even when the runtime source and payload match.
