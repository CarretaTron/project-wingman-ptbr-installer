param(
    [switch]$SomenteInterface,
    [string]$PastaTraducao,
    [string]$Saida
)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$buildDirectory = Join-Path $PSScriptRoot 'build'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$references = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll')
$stub = Join-Path $buildDirectory 'Interface.exe'
$uiArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', ('/out:' + $stub), ('/win32icon:' + (Join-Path $PSScriptRoot 'assets\launcher.ico')), ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
$uiArgs += $references
$uiArgs += @('/resource:' + (Join-Path $PSScriptRoot 'assets\banner.jpg') + ',banner.jpg')
$uiArgs += @('/resource:' + (Join-Path $PSScriptRoot 'assets\logo.png') + ',logo.png')
$uiArgs += @('/resource:' + (Join-Path $PSScriptRoot 'assets\launcher.ico') + ',launcher.ico')
$uiArgs += @((Join-Path $PSScriptRoot 'InstallerCore.cs'), (Join-Path $PSScriptRoot 'InstallerUI.cs'), (Join-Path $PSScriptRoot 'FolderPicker.cs'))
& $compiler $uiArgs
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar a interface.' }
if ($SomenteInterface) { Write-Output $stub; exit 0 }
$builder = Join-Path $buildDirectory 'BuildPackage.exe'
& $compiler /nologo /target:exe /platform:x64 /optimize+ ('/out:' + $builder) $references (Join-Path $PSScriptRoot 'InstallerCore.cs') (Join-Path $PSScriptRoot 'BuildPackage.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o empacotador.' }
if ([string]::IsNullOrWhiteSpace($PastaTraducao)) { $PastaTraducao = $projectDirectory }
$payloadDirectory = (Resolve-Path -LiteralPath $PastaTraducao).Path
if ([string]::IsNullOrWhiteSpace($Saida)) { $Saida = Join-Path $projectDirectory 'Project Wingman - Traducao PT-BR v0.91 - Instalador.exe' }
$output = [System.IO.Path]::GetFullPath($Saida)
if (Test-Path -LiteralPath $output) { throw 'O arquivo de saída já existe. Escolha outro caminho com -Saida.' }
& $builder $stub $payloadDirectory $output
if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o pacote.' }
Write-Output $output
